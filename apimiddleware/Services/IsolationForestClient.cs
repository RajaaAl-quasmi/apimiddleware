using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ApiMiddleware.Models.DTOs;
using Microsoft.Extensions.Configuration;

namespace ApiMiddleware.Services
{
    public class IsolationForestClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public IsolationForestClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<AnalysisResponse> AnalyzeRequestAsync(AnalysisRequest request)
        {
            var baseUrl = _configuration["ServiceUrls:IsolationForestServer"];
            var analyzeUrl = $"{baseUrl}/analyze";

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(analyzeUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();

                var analysisResponse = JsonSerializer.Deserialize<AnalysisResponse>(
                    responseContent,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return analysisResponse!;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Failed to analyze request via IsolationForestServer: {ex.Message}", ex);
            }
        }

        public async Task<bool> CheckHealthAsync()
        {
            var baseUrl = _configuration["ServiceUrls:IsolationForestServer"];

            try
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}