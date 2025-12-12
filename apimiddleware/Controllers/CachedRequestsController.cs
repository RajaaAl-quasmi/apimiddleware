using System.Linq;
using System.Threading.Tasks;
using ApiMiddleware.Data;
using ApiMiddleware.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMiddleware.Controllers
{
    [ApiController]
    [Route("api/cached-requests")]
    public class CachedRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CachedRequestsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/cached-requests
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.CachedRequests
                .OrderByDescending(x => x.ReceivedAt)
                .ToListAsync();

            return Ok(items);
        }

        // GET: api/cached-requests/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _context.CachedRequests.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // PUT: api/cached-requests/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CachedRequest updated)
        {
            var existing = await _context.CachedRequests.FindAsync(id);
            if (existing == null) return NotFound();

            // تحديث الحقول الأساسية
            existing.RequestId = updated.RequestId;
            existing.ClientIp = updated.ClientIp;
            existing.HttpMethod = updated.HttpMethod;
            existing.RequestPath = updated.RequestPath;
            existing.QueryString = updated.QueryString;
            existing.HeadersJson = updated.HeadersJson;
            existing.RequestBody = updated.RequestBody;
            existing.ContentType = updated.ContentType;
            existing.ReceivedAt = updated.ReceivedAt;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        // DELETE: api/cached-requests/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.CachedRequests.FindAsync(id);
            if (existing == null) return NotFound();

            _context.CachedRequests.Remove(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}