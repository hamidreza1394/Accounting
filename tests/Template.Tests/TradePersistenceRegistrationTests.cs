using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests;

public sealed class TradePersistenceRegistrationTests
{
    private const string ConnectionString =
        "Server=localhost;Database=Farano_TradeRegistrationTests;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";

    [Fact]
    public void AddTradePersistence_RegistersScopedContext_WithoutOpeningAConnection()
    {
        var services = new ServiceCollection();
        services.AddTradePersistence(ConnectionString);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<TradeDbContext>();
        var sameScope = firstScope.ServiceProvider.GetRequiredService<TradeDbContext>();
        var otherScope = secondScope.ServiceProvider.GetRequiredService<TradeDbContext>();

        Assert.Same(first, sameScope);
        Assert.NotSame(first, otherScope);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", first.Database.ProviderName);

        var actualConnectionString = first.Database.GetConnectionString();
        Assert.NotNull(actualConnectionString);

        var expectedBuilder = new SqlConnectionStringBuilder(ConnectionString);
        var actualBuilder = new SqlConnectionStringBuilder(actualConnectionString);

        Assert.Equal(expectedBuilder.DataSource, actualBuilder.DataSource);
        Assert.Equal(expectedBuilder.InitialCatalog, actualBuilder.InitialCatalog);
        Assert.Equal(expectedBuilder.IntegratedSecurity, actualBuilder.IntegratedSecurity);
        Assert.Equal(expectedBuilder.Encrypt, actualBuilder.Encrypt);
        Assert.Equal(expectedBuilder.TrustServerCertificate, actualBuilder.TrustServerCertificate);
    }

    [Fact]
    public void TradeSqlServerOptions_UseTheTradeAssemblyAndIndependentHistoryTable()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>();
        TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, ConnectionString);

        var relational = Assert.Single(options.Options.Extensions.OfType<RelationalOptionsExtension>());

        Assert.Equal(typeof(TradeDbContext).Assembly.GetName().Name, relational.MigrationsAssembly);
        Assert.Equal("__TradeMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal("dbo", relational.MigrationsHistoryTableSchema);
    }

    [Fact]
    public void TradeSqlServerOptions_RejectMissingConnectionStrings()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>();

        Assert.Throws<ArgumentException>(() =>
            TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, " "));
    }
}
