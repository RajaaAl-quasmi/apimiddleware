using System.Text.Json;
using ApiMiddleware.Data;
using ApiMiddleware.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMiddleware.Controllers;

[ApiController]
[Route("audit")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuditController(AppDbContext context)
    {
        _context = context;
    }

    // Existing endpoints...
    // GET: /audit/routing
    [HttpGet("routing")]
    public async Task<IActionResult> GetRoutingDecisions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? clientIp = null,
        [FromQuery] bool? isAnomaly = null,
        [FromQuery] string? routedTo = null,
        [FromQuery] float? minConfidence = null,
        [FromQuery] float? maxConfidence = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int? statusCode = null)
    {
        // ... (your existing implementation remains unchanged)
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.RoutingDecisions
            .Include(rd => rd.CachedRequest)
            .AsQueryable();

        // Apply filters (unchanged)
        if (!string.IsNullOrWhiteSpace(clientIp))
            query = query.Where(rd => rd.CachedRequest != null && rd.CachedRequest.ClientIp == clientIp);
        if (isAnomaly.HasValue)
            query = query.Where(rd => rd.IsAnomaly == isAnomaly.Value);
        if (!string.IsNullOrWhiteSpace(routedTo))
            query = query.Where(rd => rd.RoutedTo == routedTo.Trim());
        if (minConfidence.HasValue)
            query = query.Where(rd => rd.Confidence >= (decimal)minConfidence.Value);
        if (maxConfidence.HasValue)
            query = query.Where(rd => rd.Confidence <= (decimal)maxConfidence.Value);
        if (dateFrom.HasValue)
            query = query.Where(rd => rd.DecidedAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue)
            query = query.Where(rd => rd.DecidedAt <= dateTo.Value.ToUniversalTime());
        if (statusCode.HasValue)
            query = query.Where(rd => rd.ResponseStatusCode == statusCode.Value);

        var totalRecords = await query.CountAsync();

        var items = await query
            .OrderByDescending(rd => rd.DecidedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(rd => new
            {
                routing = new
                {
                    rd.RequestId,
                    rd.IsAnomaly,
                    Confidence = (float)rd.Confidence,
                    rd.ModelVersion,
                    rd.RoutedTo,
                    rd.ResponseStatusCode,
                    rd.ResponseTimeMs,
                    rd.DecidedAt
                },
                request = rd.CachedRequest == null ? null : new
                {
                    rd.CachedRequest.Id,
                    rd.CachedRequest.ClientIp,
                    Method = rd.CachedRequest.HttpMethod,
                    Path = rd.CachedRequest.RequestPath,
                    rd.CachedRequest.QueryString,
                    rd.CachedRequest.ContentType,
                    rd.CachedRequest.ReceivedAt
                }
            })
            .ToListAsync();

        var result = items.Select(x => new
        {
            routing = new
            {
                x.routing.RequestId,
                x.routing.IsAnomaly,
                x.routing.Confidence,
                x.routing.ModelVersion,
                x.routing.RoutedTo,
                x.routing.ResponseStatusCode,
                x.routing.ResponseTimeMs,
                x.routing.DecidedAt,
                headers = TryParseJson(x.routing.ModelVersion),
                bodyPreview = x.routing.ModelVersion
            },
            request = x.request == null ? null : new
            {
                x.request.Id,
                x.request.ClientIp,
                x.request.Method,
                x.request.Path,
                x.request.QueryString,
                x.request.ContentType,
                x.request.ReceivedAt,
                headers = TryParseJson(x.request.ContentType),
                body = x.request.ContentType
            }
        });

        return Ok(new
        {
            page,
            pageSize,
            totalRecords,
            totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
            items = result
        });
    }

    // GET: /audit/routing/{id}
    [HttpGet("routing/{id}")]
    public async Task<IActionResult> GetRoutingDecisionDetails(string id)
    {
        // ... (your existing implementation remains unchanged)
        var decision = await _context.RoutingDecisions
            .Include(rd => rd.CachedRequest)
            .FirstOrDefaultAsync(rd => rd.RequestId == id);

        if (decision == null)
            return NotFound(new { message = "Routing decision not found" });

        var cr = decision.CachedRequest;

        var result = new
        {
            decision.RequestId,
            decision.IsAnomaly,
            Confidence = (float)decision.Confidence,
            decision.ModelVersion,
            decision.RoutedTo,
            decision.ResponseStatusCode,
            decision.ResponseTimeMs,
            decision.DecidedAt,
            responseHeaders = TryParseJson(decision.ResponseHeadersJson),
            responseBodyPreview = decision.ResponseBodyPreview,
            request = cr == null ? null : new
            {
                cr.Id,
                cr.ClientIp,
                Method = cr.HttpMethod,
                Path = cr.RequestPath,
                cr.QueryString,
                cr.ContentType,
                cr.ReceivedAt,
                requestHeaders = TryParseJson(cr.HeadersJson),
                requestBody = cr.RequestBody
            }
        };

        return Ok(result);
    }

    // NEW ENDPOINT: GET /audit/chart
    [HttpGet("chart")]
    public async Task<IActionResult> GetChartData()
    {
        // 1. Routing Breakdown: Honeypot vs Real System
        var routingCounts = await _context.RoutingDecisions
            .GroupBy(rd => rd.RoutedTo)
            .Select(g => new
            {
                RoutedTo = g.Key ?? "Unknown",
                Count = g.Count()
            })
            .ToListAsync();

        var honeypotCount = routingCounts
            .Where(x => !string.IsNullOrEmpty(x.RoutedTo) &&
                        x.RoutedTo.Contains("honeypot", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);

        var totalCount = routingCounts.Sum(x => x.Count);
        var realCount = totalCount - honeypotCount;

        // 2. Last 7 days (including today)
        var today = DateTime.UtcNow.Date;
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-i))
            .Reverse() // oldest first
            .ToList();

        var startOfPeriod = last7Days.First();

        // Query all decisions in the last 7 days
        var decisionsInPeriod = await _context.RoutingDecisions
            .Where(rd => rd.DecidedAt >= startOfPeriod && rd.DecidedAt < today.AddDays(1))
            .Select(rd => new { rd.DecidedAt, rd.IsAnomaly, rd.RoutedTo })
            .ToListAsync();

        // Group in memory by date (safe since data volume is small)
        var anomalyByDay = decisionsInPeriod
            .GroupBy(d => d.DecidedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count(d => d.IsAnomaly));

        var legitimateByDay = decisionsInPeriod
            .Where(d => d.IsAnomaly == false &&
                        !string.IsNullOrEmpty(d.RoutedTo) &&
                        !d.RoutedTo.Contains("honeypot", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d.DecidedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        // Build arrays in correct order (oldest → newest)
        var anomalyTrendData = last7Days.Select(date => anomalyByDay.GetValueOrDefault(date, 0)).ToArray();
        var legitimateTrendData = last7Days.Select(date => legitimateByDay.GetValueOrDefault(date, 0)).ToArray();
        var dayLabels = last7Days.Select(d => d.ToString("ddd")).ToArray(); // Mon, Tue, ...

        var response = new
        {
            routingBreakdown = new
            {
                labels = new[] { "Honeypot", "Real System" },
                data = new[] { honeypotCount, realCount }
            },
            anomalyTrend = new
            {
                labels = dayLabels,
                data = anomalyTrendData
            },
            legitimateTrends = new
            {
                labels = dayLabels,
                data = legitimateTrendData
            }
        };

        return Ok(response);
    }

    // Safe JSON parser – never throws
    private static object? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<object>(json);
        }
        catch
        {
            return json; // fallback: return raw string if invalid JSON
        }
    }
}