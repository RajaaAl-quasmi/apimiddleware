using ApiMiddleware.Data;
using ApiMiddleware.Models;
using ApiMiddleware.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
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

        // Link to cached request
        var cachedRequest = await _dbContext.CachedRequests
            .FirstOrDefaultAsync(c => c.RequestId == requestId);
        decision.CachedRequestId = cachedRequest?.Id ?? 0;

        HttpResponseMessage? upstreamResponse = null;

        try
        {
            var upstreamRequest = new HttpRequestMessage(
                new HttpMethod(originalRequest.Method),
                new Uri(targetUrl + originalRequest.Path + originalRequest.QueryString));

            // Copy headers (skip Host header)
            foreach (var header in originalRequest.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                    continue;

                var added = upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                if (!added && upstreamRequest.Content != null)
                {
                    upstreamRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // Copy body for POST/PUT/PATCH etc.
            if (HttpMethods.IsPost(originalRequest.Method) ||
                HttpMethods.IsPut(originalRequest.Method) ||
                HttpMethods.IsPatch(originalRequest.Method))
            {
                if (originalRequest.ContentLength > 0)
                {
                    originalRequest.EnableBuffering();
                    originalRequest.Body.Position = 0;

                    var content = new StreamContent(originalRequest.Body);
                    content.Headers.ContentType = originalRequest.ContentType is not null
                        ? new System.Net.Http.Headers.MediaTypeHeaderValue(originalRequest.ContentType)
                        : null;

                    upstreamRequest.Content = content;
                }
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            upstreamResponse = await client.SendAsync(upstreamRequest);

            stopwatch.Stop();

            decision.ResponseStatusCode = (int)upstreamResponse.StatusCode;
            decision.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            // Capture response body and headers for audit
            if (upstreamResponse.Content is not null)
            {
                var bodyBytes = await upstreamResponse.Content.ReadAsByteArrayAsync();
                var bodyString = Encoding.UTF8.GetString(bodyBytes);

                decision.ResponseBodyPreview = bodyString.Length > 1000
                    ? bodyString[..1000] + "..."
                    : bodyString;

                decision.ResponseHeadersJson = JsonSerializer.Serialize(
                    upstreamResponse.Headers
                        .Concat(upstreamResponse.Content.Headers)
                        .ToDictionary(h => h.Key, h => h.Value.ToArray()));

                // Re-attach body so gateway can stream it to client
                upstreamResponse.Content = new ByteArrayContent(bodyBytes);
                foreach (var h in upstreamResponse.Content.Headers)
                {
                    upstreamResponse.Content.Headers.Remove(h.Key);
                }
                foreach (var h in upstreamResponse.Content.Headers)
                {
                    upstreamResponse.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Routing failed for RequestId={RequestId} to {Target}", requestId, target);

            decision.ResponseStatusCode = 502;
            decision.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            decision.ResponseBodyPreview = JsonSerializer.Serialize(new
            {
                error = "upstream_unavailable",
                target,
                message = ex.Message
            });
        }

        // Save decision to database
        _dbContext.RoutingDecisions.Add(decision);
        await _dbContext.SaveChangesAsync();

        return (upstreamResponse, decision);
    }
}