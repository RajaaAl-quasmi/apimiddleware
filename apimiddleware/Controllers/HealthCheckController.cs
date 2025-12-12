using Microsoft.AspNetCore.Mvc;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ApiMiddleware.Services;

namespace ApiMiddleware.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthCheckController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;

        public HealthCheckController(HealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        // GET: api/HealthCheck/status
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var result = await _healthCheckService.CheckAllServicesAsync();
            return Ok(result);
        }
    }
}