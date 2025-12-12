using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ApiMiddleware.Models;
using ApiMiddleware.Models.DTOs;

namespace ApiMiddleware.Services
{
    public class RoutingService
    {
        private readonly IConfiguration _configuration;

        public RoutingService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// محاكاة لعملية التوجيه (بدون استدعاء حقيقي) - Simulation Mode
        /// </summary>
        public Task<(HttpResponseMessage? response, RoutingDecision decision)> RouteRequestAsync(
            HttpRequest originalRequest,
            AnalysisResponse analysisResult)
        {
            var decision = new RoutingDecision
            {
                RequestId = analysisResult.RequestId,
                IsAnomaly = analysisResult.IsAnomaly,
                Confidence = analysisResult.Confidence,
                MLModelVersion = analysisResult.ModelVersion,
                RoutedTo = analysisResult.IsAnomaly ? "honeypot" : "real_system",
                TargetUrl = "/api/simulated",
                ResponseStatusCode = 200,
                ResponseTimeMs = 0,
                ResponseBodyJson = JsonSerializer.Serialize(new { message = "Routing simulated" }),
                ResponseHeadersJson = "{}",
                RoutingError = false,
                ErrorMessage = null,
                RoutedAt = DateTime.UtcNow
            };

            // Simulation فقط – ما في استدعاء حقيقي لـ APIs خارجية
            return Task.FromResult<(HttpResponseMessage?, RoutingDecision)>((null, decision));
        }
    }
}