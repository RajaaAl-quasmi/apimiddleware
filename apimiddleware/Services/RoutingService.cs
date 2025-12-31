using ApiMiddleware.Data;
using ApiMiddleware.Models;
using ApiMiddleware.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiMiddleware.Services;

public class RoutingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RoutingService> _logger;
    private readonly string _honeypotUrl;
    private readonly string _realSystemUrl;

    public RoutingService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        AppDbContext dbContext,
        ILogger<RoutingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dbContext = dbContext;
        _logger = logger;
        _honeypotUrl = (configuration["ServiceUrls:HoneypotApi"] ?? "http://localhost:5001").TrimEnd('/');
        _realSystemUrl = (configuration["ServiceUrls:RealSystem"] ?? "http://localhost:5000").TrimEnd('/');
    }

    public async Task<(HttpResponseMessage? response, RoutingDecision decision)> RouteRequestAsync(
        HttpRequest originalRequest,
        AnalysisResponse analysisResult,
        string requestId)
    {
        var stopwatch = Stopwatch.StartNew();
        bool isAnomaly = analysisResult.IsAnomaly;
        string target = isAnomaly ? "honeypot" : "real_system";
        string targetUrl = isAnomaly ? _honeypotUrl : _realSystemUrl;

        var decision = new RoutingDecision
        {
            RequestId = requestId,
            IsAnomaly = isAnomaly,
            Confidence = (decimal)analysisResult.Confidence,
            ModelVersion = analysisResult.ModelVersion ?? "unknown",
            RoutedTo = target,
            DecidedAt = DateTime.UtcNow
        };

        var cachedRequest = await _dbContext.CachedRequests
            .FirstOrDefaultAsync(c => c.RequestId == requestId);
        decision.CachedRequestId = cachedRequest?.Id ?? 0;

        HttpResponseMessage? upstreamResponse = null;

        // Fix path construction - preserve the original path
        string forwardPath = originalRequest.Path.Value ?? string.Empty;

        // If honeypot needs special routing, add prefix (optional - adjust based on your honeypot setup)
        if (isAnomaly)
        {
            forwardPath = "/api/" + forwardPath.Replace('/','-');
        }

        // Construct full URL
        var finalUrl = $"{targetUrl}{forwardPath}{originalRequest.QueryString}";

        try
        {
            _logger.LogInformation(
                "Routing RequestId={RequestId} (IsAnomaly={IsAnomaly}) to {Target} at URL: {Url}",
                requestId,
                isAnomaly,
                target,
                finalUrl);

            var upstreamRequest = new HttpRequestMessage(
                new HttpMethod(originalRequest.Method),
                new Uri(finalUrl));

            // Add Accept header for JSON
            upstreamRequest.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Copy headers (skip Host and other problematic headers)
            foreach (var header in originalRequest.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, "Connection", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool added = upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                if (!added && upstreamRequest.Content != null)
                {
                    upstreamRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // Copy request body
            if (HttpMethods.IsPost(originalRequest.Method) ||
                HttpMethods.IsPut(originalRequest.Method) ||
                HttpMethods.IsPatch(originalRequest.Method))
            {
                if (originalRequest.ContentLength > 0)
                {
                    originalRequest.EnableBuffering();
                    originalRequest.Body.Position = 0;

                    var content = new StreamContent(originalRequest.Body);
                    if (originalRequest.ContentType != null)
                    {
                        content.Headers.ContentType = new MediaTypeHeaderValue(originalRequest.ContentType);
                    }
                    upstreamRequest.Content = content;
                }
            }

            // Use client with automatic decompression
            var client = _httpClientFactory.CreateClient("ProxyClient");
            client.Timeout = TimeSpan.FromSeconds(30);

            upstreamResponse = await client.SendAsync(upstreamRequest);
            stopwatch.Stop();

            decision.ResponseStatusCode = (int)upstreamResponse.StatusCode;
            decision.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            // Process response
            if (upstreamResponse.Content != null)
            {
                var contentType = upstreamResponse.Content.Headers.ContentType?.MediaType;

                // Log warning if not JSON
                if (contentType != null && !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Unexpected content type: {ContentType} for RequestId={RequestId} from {Target}",
                        contentType,
                        requestId,
                        target);
                }

                byte[] contentBytes = await upstreamResponse.Content.ReadAsByteArrayAsync();
                string bodyString = Encoding.UTF8.GetString(contentBytes);


                decision.ResponseBodyPreview = bodyString.Length > 1000
                    ? bodyString.Substring(0, 1000) + "..."
                    : bodyString;

                // Collect all headers
                var allHeaders = upstreamResponse.Headers
                    .Concat(upstreamResponse.Content.Headers)
                    .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                decision.ResponseHeadersJson = JsonSerializer.Serialize(allHeaders);

                // Clean conflicting headers to avoid proxy issues
                upstreamResponse.Content.Headers.ContentEncoding.Clear();
                upstreamResponse.Headers.TransferEncoding.Clear();
                upstreamResponse.Headers.TransferEncodingChunked = false;
                upstreamResponse.Content.Headers.ContentLength = null;
                upstreamResponse.Content.Headers.Remove("Content-MD5");

                _logger.LogInformation(
                    "Successfully routed RequestId={RequestId} to {Target}, Status={StatusCode}, Time={TimeMs}ms",
                    requestId,
                    target,
                    decision.ResponseStatusCode,
                    decision.ResponseTimeMs);
            }
            else
            {
                var headersDict = upstreamResponse.Headers
                    .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                decision.ResponseHeadersJson = JsonSerializer.Serialize(headersDict);
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "HTTP request failed for RequestId={RequestId} to {Target} at {Url}. StatusCode={StatusCode}",
                requestId,
                target,
                finalUrl,
                ex.StatusCode);

            decision.ResponseStatusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 502;
            decision.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            var errorResponse = new
            {
                error = "upstream_http_error",
                target,
                targetUrl = finalUrl,
                message = ex.Message,
                statusCode = ex.StatusCode?.ToString() ?? "unknown"
            };

            decision.ResponseBodyPreview = JsonSerializer.Serialize(errorResponse);

            upstreamResponse = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
            {
                Content = new StringContent(decision.ResponseBodyPreview, Encoding.UTF8, "application/json")
            };
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Request timeout for RequestId={RequestId} to {Target} at {Url}",
                requestId,
                target,
                finalUrl);

            decision.ResponseStatusCode = 504;
            decision.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            var errorResponse = new
            {
                error = "upstream_timeout",
                target,
                targetUrl = finalUrl,
                message = "The upstream service did not respond within the timeout period"
            };

            decision.ResponseBodyPreview = JsonSerializer.Serialize(errorResponse);

            upstreamResponse = new HttpResponseMessage(System.Net.HttpStatusCode.GatewayTimeout)
            {
                Content = new StringContent(decision.ResponseBodyPreview, Encoding.UTF8, "application/json")
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Unexpected error routing RequestId={RequestId} to {Target} at {Url}. Exception={ExceptionType}",
                requestId,
                target,
                finalUrl,
                ex.GetType().Name);

            decision.ResponseStatusCode = 502;
            decision.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            var errorResponse = new
            {
                error = "upstream_unavailable",
                target,
                targetUrl = finalUrl,
                message = ex.Message,
                exceptionType = ex.GetType().Name
            };

            decision.ResponseBodyPreview = JsonSerializer.Serialize(errorResponse);

            upstreamResponse = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
            {
                Content = new StringContent(decision.ResponseBodyPreview, Encoding.UTF8, "application/json")
            };
        }

        // Save routing decision
        try
        {
            _dbContext.RoutingDecisions.Add(decision);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "Failed to save routing decision for RequestId={RequestId}", requestId);
            // Don't fail the request if database save fails
        }

        return (upstreamResponse, decision);
    }
}