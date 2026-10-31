using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Faranou.Modules.Trade.Infrastructure.Persistence;

/// <summary>
/// Creates TradeDbContext for EF Core tooling without starting the Server host.
/// </summary>
/// <remarks>
/// The factory only configures the provider; migration creation does not open a connection.
/// For commands that connect to a database, provide a development/test connection using
/// FARANO_TRADE_CONNECTIONSTRING or the shared FARANO_CONNECTIONSTRING setting.
/// </remarks>
public sealed class TradeDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradeDbContext>
{
    public const string TradeConnectionStringEnvironmentVariableName = "FARANO_TRADE_CONNECTIONSTRING";
    private const string ConnectionStringEnvironmentVariableName = "FARANO_CONNECTIONSTRING";
    private const string LegacyConnectionStringEnvironmentVariableName = "TARAZAVA_CONNECTIONSTRING";

    private const string LocalDevelopmentConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Farano;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public TradeDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(TradeConnectionStringEnvironmentVariableName)
            ?? Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName)
            ?? Environment.GetEnvironmentVariable(LegacyConnectionStringEnvironmentVariableName)
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString")
            ?? LocalDevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<TradeDbContext>();
        TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, connectionString);

        return new TradeDbContext(options.Options);
    }
}
