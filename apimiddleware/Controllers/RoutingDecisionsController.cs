using ApiMiddleware.Data;
using ApiMiddleware.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMiddleware.Controllers
{
    [ApiController]
    [Route("api")]
    public class RoutingDecisionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RoutingDecisionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/routing-decisions
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.RoutingDecisions.ToListAsync();
            return Ok(items);
        }

        // GET: api/routing-decisions/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _context.RoutingDecisions.FindAsync(id);
            if (item == null)
                return NotFound(new { message = "RoutingDecision not found" });

            return Ok(item);
        }

        // PUT: api/routing-decisions/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RoutingDecision updated)
        {
            if (updated == null)
                return BadRequest(new { message = "Body is required" });

            if (id != updated.Id)
                return BadRequest(new { message = "Id in route must match Id in body" });

            var exists = await _context.RoutingDecisions.AnyAsync(x => x.Id == id);
            if (!exists)
                return NotFound(new { message = "RoutingDecision not found" });

            _context.Entry(updated).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/routing-decisions/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.RoutingDecisions.FindAsync(id);
            if (item == null)
                return NotFound(new { message = "RoutingDecision not found" });

            _context.RoutingDecisions.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}