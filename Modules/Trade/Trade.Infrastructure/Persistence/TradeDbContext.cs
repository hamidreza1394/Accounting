using Microsoft.EntityFrameworkCore;

namespace Faranou.Modules.Trade.Infrastructure.Persistence;

/// <summary>Trade-owned EF persistence boundary in the shared physical database.</summary>
/// <remarks>Fiscal periods are owned by the Core accounting context; Trade stores only their identifiers on its own records.</remarks>
/// <remarks>
/// No global query filter is used (D-013). Application queries must include an explicit,
/// server-validated TenantId predicate and enforce authorization before data access.
/// </remarks>
public sealed class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("trade");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeDbContext).Assembly);
    }
}
