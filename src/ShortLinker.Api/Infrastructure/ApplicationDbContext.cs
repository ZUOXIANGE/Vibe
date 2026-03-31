using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Infrastructure;

public class ApplicationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<ShortLink>()
            .HasIndex(s => new { s.TenantId, s.ShortCode })
            .IsUnique();

        // Global Query Filter for Tenant Isolation
        modelBuilder.Entity<ShortLink>().HasQueryFilter(s => EF.Property<string>(s, "TenantId") == _tenantContext.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ShortLink>().Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrEmpty(entry.Entity.TenantId) && !string.IsNullOrEmpty(_tenantContext.TenantId))
            {
                entry.Entity.TenantId = _tenantContext.TenantId;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
