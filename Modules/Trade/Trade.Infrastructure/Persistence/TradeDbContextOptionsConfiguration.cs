using Microsoft.EntityFrameworkCore;

namespace Faranou.Modules.Trade.Infrastructure.Persistence;

/// <summary>
/// Configures the Trade context to use the host's shared SQL Server connection while keeping
/// its migration assembly and history table separate from the accounting context.
/// </summary>
public static class TradeDbContextOptionsConfiguration
{
    public static void ConfigureSqlServer(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        options.UseSqlServer(connectionString, sqlServer =>
        {
            sqlServer.MigrationsAssembly(typeof(TradeDbContext).Assembly.GetName().Name);
            sqlServer.MigrationsHistoryTable("__TradeMigrationsHistory", "dbo");
        });
    }
}
