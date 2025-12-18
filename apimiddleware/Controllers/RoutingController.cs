using System;
using System.Threading.Tasks;
using ApiMiddleware.Models.DTOs;
using ApiMiddleware.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiMiddleware.Controllers;

/// <summary>
/// ONLY FOR TESTING / DEBUGGING
/// Normal traffic never hits this controller — it goes through GatewayMiddleware
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoutingController : ControllerBase
{
    private readonly RoutingService _routingService;

    public RoutingController(RoutingService routingService)
    {
        _routingService = routingService;
    }

    /// <summary>
    /// POST api/Routing/decision
    /// Use this endpoint only for manual testing (Postman, curl, etc.)
    /// Example payload:
    /// {
    ///   "request_id": "optional-guid",
    ///   "is_anomaly": true,
    ///   "confidence": 0.97,
    ///   "model_version": "1.2.0"
    /// }
    /// </summary>
    [HttpPost("decision")]
    public async Task<IActionResult> MakeDecision([FromBody] AnalysisResponse analysisResult)
    {
        if (analysisResult == null)
            return BadRequest(new { error = "Request body cannot be null" });

        // Generate RequestId if missing
        if (string.IsNullOrWhiteSpace(analysisResult.RequestId))
            analysisResult.RequestId = Guid.NewGuid().ToString("N")[..16];

        // We need the original HttpRequest and RequestId for RoutingService
        var (upstreamResponse, decision) = await _routingService.RouteRequestAsync(
            HttpContext.Request,
            analysisResult,
            analysisResult.RequestId);

        // Return only the decision (admin UI already reads from DB)
        var response = new
        {
            decision.RequestId,
            decision.IsAnomaly,
            Confidence = (float)decision.Confidence,
            decision.ModelVersion,
            decision.RoutedTo,
            decision.ResponseStatusCode,
            decision.ResponseTimeMs,
            decision.DecidedAt,
            message = upstreamResponse != null
                ? $"Request routed to {decision.RoutedTo}"
                : "Routing executed in simulation/fallback mode"
        };

        return Ok(response);
    }
}