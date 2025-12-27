using System.Text;
using System.Text.Json;
using ApiMiddleware.Models;
using ApiMiddleware.Models.DTOs;
using ApiMiddleware.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;

namespace ApiMiddleware.Middleware;

public class GatewayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayMiddleware> _logger;

    public GatewayMiddleware(RequestDelegate next, ILogger<GatewayMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        RequestCacheService cacheService,
        IsolationForestClient isolationClient,
        RoutingService routingService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip gateway logic for health, audit, swagger, etc.
        if (path.StartsWith("/health") ||
            path.StartsWith("/audit") ||
            path.StartsWith("/swagger") ||
            path.StartsWith("/favicon.ico"))
        {
            await _next(context);
            return;
        }

        string requestId = Guid.NewGuid().ToString("N").Substring(0, 16); // e.g. a1b2c3d4e5f67890
        string clientIp = GetClientIp(context);

        try
        {
            // 1. Cache the original request
            await cacheService.CacheRequestAsync(context.Request, requestId, clientIp);

            // 2. Build ML analysis payload
            var analysisRequest = await BuildAnalysisRequestAsync(context.Request, requestId, clientIp);

            // 3. Call ML server
            var analysisResult = await isolationClient.AnalyzeRequestAsync(analysisRequest);

            // 4. Route the request (real call or honeypot)
            var (responseMessage, routingDecision) = await routingService.RouteRequestAsync(
                context.Request,
                analysisResult,
                requestId);

            // 5. Write response back to client
            if (responseMessage != null)
            {
                context.Response.StatusCode = (int)responseMessage.StatusCode;

                // Copy headers, but skip problematic ones
                foreach (var header in responseMessage.Headers)
                {
                    // Skip headers that can cause encoding issues
                    if (ShouldSkipHeader(header.Key))
                        continue;

                    try
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                    catch
                    {
                        // Ignore headers that can't be set
                    }
                }

                foreach (var header in responseMessage.Content.Headers)
                {
                    // Skip problematic content headers
                    if (ShouldSkipHeader(header.Key))
                        continue;

                    try
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                    catch
                    {
                        // Ignore headers that can't be set
                    }
                }

                // Add gateway trace headers
                context.Response.Headers["X-Gateway-Request-Id"] = routingDecision.RequestId;
                context.Response.Headers["X-Gateway-Routed-To"] = routingDecision.RoutedTo;
                context.Response.Headers["X-Gateway-Anomaly"] = routingDecision.IsAnomaly.ToString().ToLower();
                context.Response.Headers["X-Gateway-Confidence"] = ((float)routingDecision.Confidence).ToString("F4");

                // Stream body directly
                if (responseMessage.Content != null)
                {
                    await responseMessage.Content.CopyToAsync(context.Response.Body);
                }
            }
            else
            {
                // Fallback when upstream failed (should rarely happen)
                context.Response.StatusCode = 502;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Bad Gateway",
                    message = "Upstream service unavailable",
                    requestId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway middleware failed for RequestId={RequestId}", requestId);

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Gateway Internal Error",
                message = ex.Message,
                requestId
            });
        }
    }

    private static bool ShouldSkipHeader(string headerName)
    {
        // These headers are managed by ASP.NET Core and should not be copied
        return headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AnalysisRequest> BuildAnalysisRequestAsync(HttpRequest request, string requestId, string clientIp)
    {
        string? bodyText = null;

        if (request.ContentLength > 0)
        {
            request.EnableBuffering();
            request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            bodyText = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Rewind for downstream
        }

        object? payload = null;
        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            try
            {
                payload = JsonSerializer.Deserialize<object>(bodyText);
            }
            catch
            {
                payload = bodyText; // fallback to raw string
            }
        }

        return new AnalysisRequest
        {
            RequestId = requestId,
            IpAddress = clientIp,
            Endpoint = request.Path.Value ?? "",
            HttpMethod = request.Method,
            QueryString = request.QueryString.Value ?? "",
            Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Payload = payload,
            ContentType = request.ContentType ?? "",
            Timestamp = DateTime.UtcNow
        };
    }

    private static string GetClientIp(HttpContext context)
    {
        // Handle proxies/load balancers
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor.FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            return realIp.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

// Extension method
public static class GatewayMiddlewareExtensions
{
    public static IApplicationBuilder UseGatewayMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GatewayMiddleware>();
    }
}