using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ApiMiddleware.Services;
using ApiMiddleware.Models.DTOs;

namespace ApiMiddleware.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoutingController : ControllerBase
    {
        private readonly RoutingService _routingService;

        public RoutingController(RoutingService routingService)
        {
            _routingService = routingService;
        }

        // POST: api/Routing/decision
        [HttpPost("decision")]
        public async Task<IActionResult> MakeDecision([FromBody] AnalysisResponse analysisResult)
        {
            // لو requestId مش جاي من الـ body نولّد واحد جديد
            if (string.IsNullOrEmpty(analysisResult.RequestId))
            {
                analysisResult.RequestId = Guid.NewGuid().ToString();
            }

            // استدعاء خدمة التوجيه
            var result = await _routingService.RouteRequestAsync(HttpContext.Request, analysisResult);
            var decision = result.decision;

            return Ok(decision);
        }
    }
}