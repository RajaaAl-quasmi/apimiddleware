using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ApiMiddleware.Data;
using ApiMiddleware.Models.DTOs;
using Microsoft.Extensions.Configuration;

namespace ApiMiddleware.Services
{
    public class HealthCheckService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public HealthCheckService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<HealthCheckResponse> CheckAllServicesAsync()
        {
            var services = new Dictionary<string, ServiceHealth>();

            // Database
            services["database"] = await CheckDatabaseAsync();

            // IsolationForestServer
            services["isolation_forest_server"] =
                await CheckServiceAsync(_configuration["ServiceUrls:IsolationForestServer"]);

            // Honeypot API
            services["honeypot_api"] =
                await CheckServiceAsync(_configuration["ServiceUrls:HoneypotApi"]);

            // Real System
            services["real_system"] =
                await CheckServiceAsync(_configuration["ServiceUrls:RealSystem"]);

            // Overall health
            var allHealthy = services.Values.All(s => s.Status == "healthy");
            var overallHealth =
                allHealthy ? "All systems operational" : "One or more services degraded";

            return new HealthCheckResponse
            {
                Status = allHealthy ? "healthy" : "degraded",
                Timestamp = DateTime.UtcNow,
                Services = services,
                OverallHealth = overallHealth
            };
        }

        private async Task<ServiceHealth> CheckDatabaseAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _context.Database.CanConnectAsync();
                stopwatch.Stop();
                return new ServiceHealth
                {
                    Status = "healthy",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch
            {
                stopwatch.Stop();
                return new ServiceHealth
                {
                    Status = "unhealthy",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<ServiceHealth> CheckServiceAsync(string url)
        {
            var stopwatch = Stopwatch.StartNew();
            var client = _httpClientFactory.CreateClient();

            try
            {
                var response = await client.GetAsync($"{url}/");
                stopwatch.Stop();
                return new ServiceHealth
                {
                    Status = response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                    Url = url,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch
            {
                stopwatch.Stop();
                return new ServiceHealth
                {
                    Status = "unhealthy",
                    Url = url,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
    }
}