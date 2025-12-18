using System.Threading.Tasks;
using ApiMiddleware.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiMiddleware.Controllers;

/// <summary>
/// Health check endpoint – used by load balancers, monitoring tools, and Admin Dashboard
/// GET /health ? returns full system status
/// </summary>
[ApiController]
[Route("health")]  // ? This matches the documentation and GatewayMiddleware skip list
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// GET /health
    /// Returns detailed health of all components:
    /// - Database
    /// - Isolation Forest ML Server
    /// - Honeypot API
    /// - Real Production System
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _healthCheckService.CheckAllServicesAsync();

        // Return proper HTTP status codes
        return result.Status == "healthy"
            ? Ok(result)           // 200 OK
            : StatusCode(503, result); // 503 Service Unavailable
    }
}