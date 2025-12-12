namespace ApiMiddleware.Models.DTOs
{
    public class AnalysisRequest
    {
        public string RequestId { get; set; }
        public string IpAddress { get; set; }
        public string Endpoint { get; set; }
        public string HttpMethod { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public object Payload { get; set; }
        public DateTime Timestamp { get; set; }
    }
}