// Data/AppDbContext.cs
using ApiMiddleware.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiMiddleware.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CachedRequest> CachedRequests => Set<CachedRequest>();
    public DbSet<RoutingDecision> RoutingDecisions => Set<RoutingDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CachedRequest configuration
        modelBuilder.Entity<CachedRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.HttpMethod).IsRequired().HasMaxLength(10);
            entity.Property(e => e.RequestPath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ClientIp).IsRequired().HasMaxLength(45);
            entity.Property(e => e.HeadersJson).HasColumnType("TEXT");
            entity.Property(e => e.RequestBody).HasColumnType("TEXT");
            entity.Property(e => e.QueryString).HasColumnType("TEXT");
            entity.Property(e => e.ContentType).HasMaxLength(200);

            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.RequestId).IsUnique();
        });

        // RoutingDecision configuration
        modelBuilder.Entity<RoutingDecision>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RequestId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsAnomaly).IsRequired();
            entity.Property(e => e.Confidence).HasPrecision(18, 8);
            entity.Property(e => e.ModelVersion).HasMaxLength(50);
            entity.Property(e => e.RoutedTo).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ResponseStatusCode).IsRequired();
            entity.Property(e => e.ResponseTimeMs).IsRequired();

            entity.HasOne(d => d.CachedRequest)
                  .WithMany()
                  .HasForeignKey(d => d.CachedRequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.DecidedAt);
            entity.HasIndex(e => e.IsAnomaly);
        });
    }
}