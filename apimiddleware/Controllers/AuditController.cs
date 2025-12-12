using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ApiMiddleware.Data;
using ApiMiddleware.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMiddleware.Controllers
{
    [ApiController]
    [Route("audit")]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

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
            // الأساس: كل القرارات + الطلب المخزَّن
            var query = _context.RoutingDecisions
                .Include(rd => rd.CachedRequest)
                .AsQueryable();

            // الفلاتر
            if (!string.IsNullOrEmpty(clientIp))
            {
                query = query.Where(rd =>
                    rd.CachedRequest != null &&
                    rd.CachedRequest.ClientIp == clientIp);
            }

            if (isAnomaly.HasValue)
            {
                query = query.Where(rd => rd.IsAnomaly == isAnomaly.Value);
            }

            if (!string.IsNullOrEmpty(routedTo))
            {
                query = query.Where(rd => rd.RoutedTo == routedTo);
            }

            if (minConfidence.HasValue)
            {
                query = query.Where(rd => rd.Confidence >= minConfidence.Value);
            }

            if (maxConfidence.HasValue)
            {
                query = query.Where(rd => rd.Confidence <= maxConfidence.Value);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(rd => rd.RoutedAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(rd => rd.RoutedAt <= dateTo.Value);
            }

            if (statusCode.HasValue)
            {
                query = query.Where(rd => rd.ResponseStatusCode == statusCode.Value);
            }

            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            // أولاً نرجع البيانات كنصوص (بدون JsonSerializer داخل LINQ)
            var rawItems = await query
                .OrderByDescending(rd => rd.RoutedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(rd => new
                {
                    Routing = new
                    {
                        rd.RequestId,
                        rd.IsAnomaly,
                        rd.Confidence,
                        rd.MLModelVersion,
                        rd.RoutedTo,
                        rd.TargetUrl,
                        rd.ResponseStatusCode,
                        rd.ResponseTimeMs,
                        rd.ResponseHeadersJson,
                        rd.ResponseBodyJson,
                        rd.RoutedAt
                    },
                    Request = rd.CachedRequest == null ? null : new
                    {
                        rd.CachedRequest.Id,
                        rd.CachedRequest.ClientIp,
                        Method = rd.CachedRequest.HttpMethod,
                        Path = rd.CachedRequest.RequestPath,
                        rd.CachedRequest.QueryString,
                        rd.CachedRequest.ContentType,
                        rd.CachedRequest.ReceivedAt,
                        rd.CachedRequest.HeadersJson,
                        rd.CachedRequest.RequestBody
                    }
                })
                .ToListAsync();

            // ثانياً: نحول الهيدرز من JSON إلى object في الذاكرة (مش داخل EF)
            var items = rawItems.Select(x => new
            {
                routing = new
                {
                    x.Routing.RequestId,
                    x.Routing.IsAnomaly,
                    x.Routing.Confidence,
                    x.Routing.MLModelVersion,
                    x.Routing.RoutedTo,
                    x.Routing.TargetUrl,
                    x.Routing.ResponseStatusCode,
                    x.Routing.ResponseTimeMs,
                    headers = string.IsNullOrEmpty(x.Routing.ResponseHeadersJson)
                        ? null
                        : JsonSerializer.Deserialize<object>(x.Routing.ResponseHeadersJson),
                    body = x.Routing.ResponseBodyJson,
                    x.Routing.RoutedAt
                },
                request = x.Request == null ? null : new
                {
                    x.Request.Id,
                    x.Request.ClientIp,
                    x.Request.Method,
                    x.Request.Path,
                    x.Request.QueryString,
                    x.Request.ContentType,
                    x.Request.ReceivedAt,
                    headers = string.IsNullOrEmpty(x.Request.HeadersJson)
                        ? null
                        : JsonSerializer.Deserialize<object>(x.Request.HeadersJson),
                    body = x.Request.RequestBody
                }
            }).ToList();

            return Ok(new
            {
                page,
                pageSize,
                totalRecords,
                totalPages,
                items
            });
        }

        // GET: /audit/routing/{id}
        [HttpGet("routing/{id}")]
        public async Task<IActionResult> GetRoutingDecisionDetails(string id)
        {
            try
            {
                // نجلب RoutingDecision + CachedRequest حسب الـ RequestId
                var data = await
                    (from dec in _context.RoutingDecisions
                     join req in _context.CachedRequests
                         on dec.RequestId equals req.RequestId into reqGroup
                     from req in reqGroup.DefaultIfEmpty()
                     where dec.RequestId == id
                     select new { dec, req })
                    .FirstOrDefaultAsync();

                if (data == null)
                    return NotFound();

                var rd = data.dec;   // RoutingDecision
                var cr = data.req;   // CachedRequest (ممكن يكون null)

                var result = new
                {
                    rd.RequestId,
                    rd.IsAnomaly,
                    rd.Confidence,
                    rd.MLModelVersion,
                    rd.RoutedTo,
                    rd.TargetUrl,
                    rd.ResponseStatusCode,
                    rd.ResponseTimeMs,
                    headers = string.IsNullOrEmpty(rd.ResponseHeadersJson)
                        ? null
                        : JsonSerializer.Deserialize<object>(rd.ResponseHeadersJson),
                    body = rd.ResponseBodyJson,
                    rd.RoutedAt,

                    request = cr == null ? null : new
                    {
                        cr.Id,
                        cr.ClientIp,
                        Method = cr.HttpMethod,
                        Path = cr.RequestPath,
                        cr.QueryString,
                        cr.ContentType,
                        cr.ReceivedAt,
                        headers = string.IsNullOrEmpty(cr.HeadersJson)
                            ? null
                            : JsonSerializer.Deserialize<object>(cr.HeadersJson),
                        body = cr.RequestBody
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Failed to fetch request details",
                    message = ex.Message
                });
            }
        }
    }
}