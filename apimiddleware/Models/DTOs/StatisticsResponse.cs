using System;
using System.Collections.Generic;

namespace ApiMiddleware.Models.DTOs
{
    public class StatisticsResponse
    {
        public TimePeriod TimePeriod { get; set; }
        public int TotalRequests { get; set; }
        public RoutingBreakdown RoutingBreakdown { get; set; }
        public AnomalyDetection AnomalyDetection { get; set; }
        public Performance Performance { get; set; }
        public Dictionary<string, int> StatusCodes { get; set; }
        public List<AnomalousIp> TopAnomalousIps { get; set; }
        public List<TargetedEndpoint> TopTargetedEndpoints { get; set; }
    }

    public class TimePeriod
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public class RoutingBreakdown
    {
        public RouteStats Honeypot { get; set; }
        public RouteStats RealSystem { get; set; }
    }

    public class RouteStats
    {
        public int Count { get; set; }
        public float Percentage { get; set; }
    }

    public class AnomalyDetection
    {
        public int TotalAnomalies { get; set; }
        public float AverageConfidence { get; set; }
        public ConfidenceDistribution ConfidenceDistribution { get; set; }
    }

    public class ConfidenceDistribution
    {
        public int HighConfidence { get; set; }
        public int MediumConfidence { get; set; }
        public int LowConfidence { get; set; }
    }

    public class Performance
    {
        public int AverageResponseTimeMs { get; set; }
        public int HoneypotAvgResponseMs { get; set; }
        public int RealSystemAvgResponseMs { get; set; }
    }

    public class AnomalousIp
    {
        public string Ip { get; set; }
        public int AnomalyCount { get; set; }
        public float AverageConfidence { get; set; }
    }

    public class TargetedEndpoint
    {
        public string Endpoint { get; set; }
        public int TotalRequests { get; set; }
        public float AnomalyRate { get; set; }
    }
}