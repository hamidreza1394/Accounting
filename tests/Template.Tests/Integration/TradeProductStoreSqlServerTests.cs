using Faranou.Modules.Trade.Application.Products;
using Faranou.Modules.Trade.Domain.Products;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Deterministic proof that the filtered unique index race (a duplicate inserted after the service pre-check)
/// is mapped to <see cref="ProductSaveOutcome.DuplicateCode" /> on the storage path instead of a raw 500.
/// </summary>
public sealed class TradeProductStoreSqlServerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static string ConnectionFor(string databaseName) =>
        new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;

    private static Product NewProduct(string code) =>
        new(TenantId, code, "Name " + code, UserId, DateTime.UtcNow, null, null, "TON");

    private static async Task WithDatabaseAsync(
        Func<IServiceProvider, CancellationToken, Task> body)
    {
        Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var cancellationToken = TestContext.Current.CancellationToken;
        var name = "FaranouTests_TradeProductStore_" + Guid.NewGuid().ToString("N")[..12];
        var services = new ServiceCollection();
        services.AddTradePersistence(ConnectionFor(name));
        await using var provider = services.BuildServiceProvider();
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<TradeDbContext>().Database.MigrateAsync(cancellationToken);
            }

            await body(provider, cancellationToken);
        }
        finally
        {
            await using var cleanup = provider.CreateAsyncScope();
            await cleanup.ServiceProvider.GetRequiredService<TradeDbContext>().Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task SaveChanges_WhenAnotherActiveProductTakesTheCodeAfterThePreCheck_ReturnsDuplicateCodeAndClearsTracker()
    {
        await WithDatabaseAsync(async (provider, cancellationToken) =>
        {
            await using var scopeA = provider.CreateAsyncScope();
            await using var scopeB = provider.CreateAsyncScope();
            var contextA = scopeA.ServiceProvider.GetRequiredService<TradeDbContext>();
            var storeA = scopeA.ServiceProvider.GetRequiredService<IProductStore>();

            var seeded = NewProduct("A-1");
            contextA.Products.Add(seeded);
            await contextA.SaveChangesAsync(cancellationToken);
            contextA.ChangeTracker.Clear();

            var loaded = await storeA.FindForUpdateAsync(TenantId, seeded.Id, cancellationToken);
            Assert.NotNull(loaded);

            // The competing writer wins the code between the pre-check and the save.
            var contextB = scopeB.ServiceProvider.GetRequiredService<TradeDbContext>();
            contextB.Products.Add(NewProduct("TARGET"));
            await contextB.SaveChangesAsync(cancellationToken);

            loaded.UpdateDetails("TARGET", "Renamed", null, null, "TON", UserId, DateTime.UtcNow);
            var outcome = await storeA.SaveChangesAsync(loaded, loaded.RowVersion, cancellationToken);

            Assert.Equal(ProductSaveOutcome.DuplicateCode, outcome);
            Assert.Empty(contextA.ChangeTracker.Entries());
        });
    }

    [Fact]
    public async Task Add_WhenTheCodeIsAlreadyActive_BypassingThePreCheck_ReturnsDuplicateCodeAndClearsTracker()
    {
        await WithDatabaseAsync(async (provider, cancellationToken) =>
        {
            await using var scopeA = provider.CreateAsyncScope();
            await using var scopeB = provider.CreateAsyncScope();
            var contextA = scopeA.ServiceProvider.GetRequiredService<TradeDbContext>();
            var storeA = scopeA.ServiceProvider.GetRequiredService<IProductStore>();

            var contextB = scopeB.ServiceProvider.GetRequiredService<TradeDbContext>();
            contextB.Products.Add(NewProduct("TARGET"));
            await contextB.SaveChangesAsync(cancellationToken);

            var outcome = await storeA.AddAsync(NewProduct("TARGET"), cancellationToken);

            Assert.Equal(ProductSaveOutcome.DuplicateCode, outcome);
            Assert.Empty(contextA.ChangeTracker.Entries());
        });
    }
}
