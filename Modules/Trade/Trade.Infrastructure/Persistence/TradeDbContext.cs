using Faranou.Modules.Trade.Domain;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Modules.Trade.Infrastructure.Persistence;

/// <summary>Trade-owned EF persistence boundary in the shared physical database.</summary>
/// <remarks>
/// No global query filter is used (D-013). Application queries must include an explicit,
/// server-validated TenantId predicate and enforce authorization before data access.
/// </remarks>
public sealed class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<TradeOperationalPeriod> OperationalPeriods => Set<TradeOperationalPeriod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("trade");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeDbContext).Assembly);
    }
}
