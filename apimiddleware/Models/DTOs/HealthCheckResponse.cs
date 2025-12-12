using System;
using System.Collections.Generic;

namespace ApiMiddleware.Models.DTOs
{
    public class HealthCheckResponse
    {
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, ServiceHealth> Services { get; set; }
        public string OverallHealth { get; set; }
    }

    public class ServiceHealth
    {
        public string Status { get; set; }
        public string Url { get; set; }
        public int ResponseTimeMs { get; set; }
    }
}