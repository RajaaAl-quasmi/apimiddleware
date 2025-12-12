using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiMiddleware.Models
{
    public class CachedRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string RequestId { get; set; }

        [MaxLength(45)]
        public string? ClientIp { get; set; }

        [Required]
        [MaxLength(10)]
        public string HttpMethod { get; set; }

        [Required]
        [MaxLength(1000)]
        public string RequestPath { get; set; }

        [Column(TypeName = "TEXT")]
        public string QueryString { get; set; }

        [Column(TypeName = "TEXT")]
        public string HeadersJson { get; set; }

        [Column(TypeName = "TEXT")]
        public string RequestBody { get; set; }

        // ⬇⬇⬇ مهم – خليه مش Required و nullable
        [MaxLength(200)]
        public string? ContentType { get; set; }

        [Required]
        public DateTime ReceivedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation (اختياري حسب اللي عندك)
        // public RoutingDecision? RoutingDecision { get; set; }
    }
}