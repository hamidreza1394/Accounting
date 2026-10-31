using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faranou.Modules.Trade.Infrastructure.Persistence;

/// <summary>
/// Registers Trade persistence against the host's shared physical SQL Server database.
/// </summary>
public static class TradePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddTradePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<TradeDbContext>(options =>
            TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, connectionString));

        return services;
    }
}
