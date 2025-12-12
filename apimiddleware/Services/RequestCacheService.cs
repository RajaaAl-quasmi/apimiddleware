using ApiMiddleware.Data;
using ApiMiddleware.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ApiMiddleware.Services
{
    public class RequestCacheService
    {
        private readonly AppDbContext _context;

        public RequestCacheService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CacheRequestAsync(HttpRequest request, string requestId, string clientIp)
        {
            // السماح بقراءة البودي أكثر من مرة
            request.EnableBuffering();
            request.Body.Position = 0;

            string bodyText = string.Empty;

            // قراءة البودي
            using (var reader = new StreamReader(request.Body, leaveOpen: true))
            {
                bodyText = await reader.ReadToEndAsync();
            }

            request.Body.Position = 0;

            // تحويل الهيدرز إلى JSON
            var headersDict = new Dictionary<string, string>();
            foreach (var h in request.Headers)
            {
                headersDict[h.Key] = h.Value.ToString();
            }

            var headersJson = JsonSerializer.Serialize(headersDict);

            // إنشاء سجل CacheRequest
            var cached = new CachedRequest
            {
                RequestId = requestId,
                ClientIp = clientIp,
                HttpMethod = request.Method,
                RequestPath = request.Path.Value ?? string.Empty,
                QueryString = request.QueryString.Value ?? string.Empty,

                // ⬅⬅⬅ هون:
                ContentType = request.ContentType ?? string.Empty,

                HeadersJson = headersJson,
                RequestBody = bodyText,
                ReceivedAt = DateTime.UtcNow
            };
            _context.CachedRequests.Add(cached);
            await _context.SaveChangesAsync();
        }
    }
}