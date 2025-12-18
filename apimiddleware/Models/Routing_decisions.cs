using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiMiddleware.Models;

public class RoutingDecision
{
    [Key]
    public int Id { get; set; }

    // Unique request identifier (same as in CachedRequest)
    [Required]
    [MaxLength(100)]
    public string RequestId { get; set; } = string.Empty;

    // Foreign key to the cached request
    [Required]
    public int CachedRequestId { get; set; }

    // Navigation property
    public CachedRequest? CachedRequest { get; set; }

    // Result from Isolation Forest ML model
    [Required]
    public bool IsAnomaly { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal Confidence { get; set; }

    [MaxLength(50)]
    public string? ModelVersion { get; set; }

    // Where the request was routed: "Honeypot" or "RealSystem"
    [Required]
    [MaxLength(20)]
    public string RoutedTo { get; set; } = string.Empty;

    // Response from the target system
    [Required]
    public int ResponseStatusCode { get; set; }

    [Required]
    public long ResponseTimeMs { get; set; }

    [Column(TypeName = "TEXT")]
    public string? ResponseHeadersJson { get; set; }

    [Column(TypeName = "TEXT")]
    public string? ResponseBodyPreview { get; set; } // First 1000 chars or null

    // Timestamps
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ForwardedAt { get; set; }

    public DateTime? RespondedAt { get; set; }
}