using System;

namespace ApiMiddleware.Models.DTOs
{
    public class AnalysisResponse
    {
        public string RequestId { get; set; }
        public bool IsAnomaly { get; set; }
        public float Confidence { get; set; }
        public string ModelVersion { get; set; }
        public DateTime AnalyzedAt { get; set; }
    }
}