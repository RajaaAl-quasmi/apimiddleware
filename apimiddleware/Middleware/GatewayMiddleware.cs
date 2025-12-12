using System.Text.Json;
using ApiMiddleware.Models.DTOs;
using ApiMiddleware.Services;

namespace ApiMiddleware.Middleware
{
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
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

            // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
            // 1️⃣   تخطي مسارات الصحة والإحصائيات
            // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
            if (path.StartsWith("/api/health") || path.StartsWith("/api/statistics") || path.StartsWith("/api/audit"))
            {
                await _next(context);
                return;
            }

            try
            {
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                // 2️⃣ استخراج IP العميل
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                // 3️⃣ STEP 1: قراءة الطلب + التخزين المؤقت
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                var requestId = Guid.NewGuid().ToString();

                await cacheService.CacheRequestAsync(context.Request, requestId, clientIp);

                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                // 4️⃣ STEP 2: بناء طلب ML
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                var analysisRequest = await BuildAnalysisRequestAsync(context.Request, requestId, clientIp);

                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                // 5️⃣ STEP 3: استدعاء Isolation Forest (ML)
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                var analysisResult = await isolationClient.AnalyzeRequestAsync(analysisRequest);

                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                // 6️⃣ STEP 4: استدعاء خدمة التوجيه (RoutingService)
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                var (response, routingDecision) =
                    await routingService.RouteRequestAsync(context.Request, analysisResult);

                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                // 7️⃣ STEP 5: إعادة الاستجابة للعميل
                // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                if (response != null)
                {
                    context.Response.StatusCode = (int)response.StatusCode;

                    foreach (var header in response.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }

                    context.Response.Headers["X-Gateway-Request-Id"] = routingDecision.RequestId;
                    context.Response.Headers["X-Gateway-Routed-To"] = routingDecision.RoutedTo ?? "unknown";

                    var body = await response.Content.ReadAsStringAsync();
                    await context.Response.WriteAsync(body);
                }
                else
                {
                    // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                    //      📌 Simulation Mode — بدون اتصال خارجي
                    // ▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
                    context.Response.StatusCode = 200;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        routingDecision.RequestId,
                        routingDecision.IsAnomaly,
                        routingDecision.Confidence,
                        routingDecision.MLModelVersion,
                        routingDecision.RoutedTo,
                        routingDecision.TargetUrl,
                        routingDecision.RoutedAt,
                        message = "Routing simulated (no real upstream call yet)"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gateway middleware error");

                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal gateway error",
                    message = ex.Message
                });
            }
        }

        // ---------------------------------------------------------------
        // 🔧 Helper: build ML analysis request
        // ---------------------------------------------------------------
        private async Task<AnalysisRequest> BuildAnalysisRequestAsync(HttpRequest request, string requestId, string clientIp)
        {
            object payload = null;

            if (request.ContentLength > 0)
            {
                request.EnableBuffering();
                request.Body.Position = 0;

                using var reader = new StreamReader(request.Body, leaveOpen: true);
                var text = await reader.ReadToEndAsync();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    try { payload = JsonSerializer.Deserialize<object>(text); }
                    catch { payload = text; }
                }

                request.Body.Position = 0;
            }

            return new AnalysisRequest
            {
                RequestId = requestId,
                IpAddress = clientIp,
                Endpoint = request.Path.Value,
                HttpMethod = request.Method,
                Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                Payload = payload,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    // ---------------------------------------------------------------
    // Extension method: UseGatewayMiddleware()
    // ---------------------------------------------------------------
    public static class GatewayMiddlewareExtensions
    {
        public static IApplicationBuilder UseGatewayMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GatewayMiddleware>();
        }
    }
}