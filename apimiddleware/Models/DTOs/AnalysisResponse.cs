// Models/DTOs/AnalysisResponse.cs
using System.Text.Json.Serialization;

namespace ApiMiddleware.Models.DTOs;

public class AnalysisResponse
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("isAnomaly")]
    public bool IsAnomaly { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = "1.0.0";

    [JsonPropertyName("analyzed_at")]
    public DateTime ProcessedAt { get; set; }
}