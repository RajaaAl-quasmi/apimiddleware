using System.Text.Json.Serialization;

namespace ApiMiddleware.Models.DTOs;

public class AnalysisRequest
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("http_method")]
    public string HttpMethod { get; set; } = string.Empty;

    [JsonPropertyName("query_string")]
    public string QueryString { get; set; } = string.Empty;

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Optional – helps debugging in logs and admin panel
    [JsonIgnore] // Don't send this back to ML server
    public string FullUrl => $"{Endpoint}{QueryString}";
}