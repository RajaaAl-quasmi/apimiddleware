using ApiMiddleware.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiMiddleware.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<CachedRequest> CachedRequests { get; set; }
        public DbSet<RoutingDecision> RoutingDecisions { get; set; }
    }
}