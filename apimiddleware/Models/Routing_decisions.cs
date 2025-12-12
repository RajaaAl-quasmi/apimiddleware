
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiMiddleware.Models
{
    public class RoutingDecision
    {
        [Key]
        public int Id { get; set; }

        // ===== Request metadata =====
        [Required]
        [MaxLength(150)]
        public string RequestId { get; set; }

        public bool IsAnomaly { get; set; }

        public float Confidence { get; set; }

        [MaxLength(100)]
        public string MLModelVersion { get; set; }

        // ===== Routing result =====
        [MaxLength(300)]
        public string RoutedTo { get; set; }

        [MaxLength(500)]
        public string TargetUrl { get; set; }

        public int ResponseStatusCode { get; set; }

        public int ResponseTimeMs { get; set; }

        // ===== Response body (TEXT) =====
        [Column(TypeName = "TEXT")]
        public string ResponseBodyJson { get; set; }

        // ===== Response headers (TEXT) =====
        [Column(TypeName = "TEXT")]
        public string ResponseHeadersJson { get; set; }

        // ===== Error handling =====
        public bool RoutingError { get; set; }

        [MaxLength(600)]
        public string ErrorMessage { get; set; }

        // ===== Date =====
        public DateTime RoutedAt { get; set; } = DateTime.UtcNow;

        // ===== Navigation إلى CachedRequest =====
        public int? CachedRequestId { get; set; }   // مفتاح أجنبي اختياري

        [ForeignKey(nameof(CachedRequestId))]
        public CachedRequest CachedRequest { get; set; }
    }
}