using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ApiMiddleware.Data;
using Microsoft.Extensions.Configuration;

namespace ApiMiddleware.Services;

public class HealthCheckService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _mlServerUrl;
    private readonly string _honeypotUrl;
    private readonly string _realSystemUrl;

    public HealthCheckService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;

            _mlServerUrl = (configuration["MLServer:Url"] ?? "http://localhost:8000").TrimEnd('/');
            _honeypotUrl = (configuration["ServiceUrls:HoneypotApi"] ?? "http://localhost:5001").TrimEnd('/');
            _realSystemUrl = (configuration["ServiceUrls:RealSystem"] ?? "http://localhost:5000").TrimEnd('/');
        }

    public async Task<HealthCheckResponse> CheckAllServicesAsync()
    {
        var services = new Dictionary<string, ServiceHealth>();

        // 1. Database
        services["database"] = await CheckDatabaseAsync();

        // 2. Isolation Forest ML Server
        services["isolation_forest_server"] = await CheckExternalServiceAsync(_mlServerUrl, "ML Server");

        // 3. Honeypot API
        services["honeypot_api"] = await CheckExternalServiceAsync(_honeypotUrl, "Honeypot");

        // 4. Real Production System
        services["real_system"] = await CheckExternalServiceAsync(_realSystemUrl, "Real System");

        bool allHealthy = services.Values.All(s => s.Status == "healthy");

        return new HealthCheckResponse
        {
            Status = allHealthy ? "healthy" : "degraded",
            Timestamp = DateTime.UtcNow,
            OverallHealth = allHealthy
                ? "All systems operational"
                : "One or more services are down or slow",
            Services = services
        };
    }

    private async Task<ServiceHealth> CheckDatabaseAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            sw.Stop();
            return new ServiceHealth
            {
                Status = canConnect ? "healthy" : "unhealthy",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Details = canConnect ? "Connected successfully" : "Cannot reach database"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ServiceHealth
            {
                Status = "unhealthy",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Details = $"Error: {ex.GetType().Name}"
            };
        }
    }

    private async Task<ServiceHealth> CheckExternalServiceAsync(string baseUrl, string name)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new ServiceHealth
            {
                Status = "unhealthy",
                ResponseTimeMs = 0,
                Details = "URL not configured"
            };
        }

        var sw = Stopwatch.StartNew();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        try
        {
            // Try common health endpoints 00000
            var urlsToTry = new[]
            {
                $"{baseUrl}/health",
                $"{baseUrl}/api/health",
                $"{baseUrl}/",
                baseUrl
            };

            HttpResponseMessage? response = null;
            foreach (var url in urlsToTry)
            {
                try
                {
                    response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode) break;
                }
                catch
                {
                    continue;
                }
            }

            sw.Stop();

            bool isHealthy = response?.IsSuccessStatusCode == true;

            return new ServiceHealth
            {
                Status = isHealthy ? "healthy" : "unhealthy",
                Url = baseUrl,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Details = isHealthy ? "OK" : $"Failed ({response?.StatusCode})"
            };
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
        {
            sw.Stop();
            return new ServiceHealth
            {
                Status = "unhealthy",
                Url = baseUrl,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                Details = $"Timeout or unreachable ({ex.GetType().Name})"
            };
        }
    }
}

// DTOs used by HealthCheck
public class HealthCheckResponse
{
    public string Status { get; set; } = "healthy";
    public DateTime Timestamp { get; set; }
    public string OverallHealth { get; set; } = "";
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();
}

public class ServiceHealth
{
    public string Status { get; set; } = "healthy"; // healthy, unhealthy
    public string? Url { get; set; }
    public int ResponseTimeMs { get; set; }
    public string? Details { get; set; }
}