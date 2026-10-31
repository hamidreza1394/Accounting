using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditCheckpointStartupServiceTests
{
    [Fact]
    public async Task Development_DoesNotResolveCheckpointServices()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new VendorAuditCheckpointStartupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestHostEnvironment(Environments.Development),
            new VendorAuditCheckpointHealthState(),
            TimeProvider.System,
            NullLogger<VendorAuditCheckpointStartupService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Production_CreatesInitialCheckpointAndIsIdempotentOnRestart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = GetTestConnectionString();
        if (baseConnectionString is null)
        {
            return;
        }

        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FaranouVendorCheckpointStartup_{Guid.NewGuid():N}"
        };
        var store = new TestProductionStore();
        await using var provider = CreateProvider(connection.ConnectionString, store);

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                var writer = new VendorAuditWriter(db, TimeProvider.System);
                await writer.AppendAsync(new VendorAuditWriteRequest(null, "test.checkpoint-startup"), cancellationToken);
            }

            var service = CreateStartupService(provider);
            await service.StartAsync(cancellationToken);

            var checkpoint = Assert.Single(store.History);
            Assert.Equal(1, checkpoint.EventCount);
            Assert.True(checkpoint.IsValid());

            await service.StartAsync(cancellationToken);
            Assert.Single(store.History);
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Production_WhenIndependentStoreIsUnavailable_FailsStartupWithoutExposingProviderMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = GetTestConnectionString();
        if (baseConnectionString is null)
        {
            return;
        }

        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FaranouVendorCheckpointUnavailable_{Guid.NewGuid():N}"
        };
        await using var provider = CreateProvider(connection.ConnectionString, new UnavailableProductionStore());

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
            }

            var service = CreateStartupService(provider);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.StartAsync(cancellationToken));

            Assert.Equal(
                "Vendor Production cannot start because the independent audit checkpoint could not be verified and advanced.",
                exception.Message);
            Assert.DoesNotContain("private-connection-detail", exception.Message);
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Production_WhenRequiredCheckpointAppendFails_FailsStartupWithoutExposingProviderMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = GetTestConnectionString();
        if (baseConnectionString is null)
        {
            return;
        }

        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FaranouVendorCheckpointAppendUnavailable_{Guid.NewGuid():N}"
        };
        await using var provider = CreateProvider(connection.ConnectionString, new AppendUnavailableProductionStore());

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                var writer = new VendorAuditWriter(db, TimeProvider.System);
                await writer.AppendAsync(new VendorAuditWriteRequest(null, "test.checkpoint-append-failure"), cancellationToken);
            }

            var service = CreateStartupService(provider);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.StartAsync(cancellationToken));

            Assert.Equal(
                "Vendor Production cannot start because the independent audit checkpoint could not be verified and advanced.",
                exception.Message);
            Assert.DoesNotContain("private-connection-detail", exception.Message);
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static VendorAuditCheckpointStartupService CreateStartupService(ServiceProvider provider) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestHostEnvironment(Environments.Production),
            provider.GetRequiredService<VendorAuditCheckpointHealthState>(),
            provider.GetRequiredService<TimeProvider>(),
            NullLogger<VendorAuditCheckpointStartupService>.Instance);

    private static ServiceProvider CreateProvider(
        string connectionString,
        IProductionVendorAuditCheckpointStore store)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VendorDb"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVendorPersistence(configuration);
        services.AddSingleton<VendorAuditCheckpointHealthState>();
        services.AddSingleton<IProductionVendorAuditCheckpointStore>(store);
        services.AddScoped<IVendorAuditCheckpointStore>(
            provider => provider.GetRequiredService<IProductionVendorAuditCheckpointStore>());
        services.AddScoped<VendorAuditCheckpointService>();
        return services.BuildServiceProvider();
    }

    private static string? GetTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable SQL Server connection. " +
                               "Use FARANO_REQUIRE_VENDOR_SQLSERVER=1 to require this integration test in CI.";
        if (string.Equals(
                Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Fail(message);
        }

        Assert.Skip(message);
        return null;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Faranou.Vendor.Host.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestProductionStore : IProductionVendorAuditCheckpointStore
    {
        public List<VendorAuditCheckpoint> History { get; } = [];

        public Task<VendorAuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(History.LastOrDefault());
        }

        public Task AppendAsync(VendorAuditCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!checkpoint.IsValid())
            {
                throw new InvalidOperationException("Only valid checkpoints may be appended.");
            }

            if (History.Count > 0 && checkpoint.EventCount <= History[^1].EventCount)
            {
                throw new InvalidOperationException("Checkpoint event count must increase.");
            }

            History.Add(checkpoint);
            return Task.CompletedTask;
        }
    }

    private sealed class AppendUnavailableProductionStore : IProductionVendorAuditCheckpointStore
    {
        public Task<VendorAuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<VendorAuditCheckpoint?>(null);
        }

        public Task AppendAsync(VendorAuditCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("private-connection-detail");
    }

    private sealed class UnavailableProductionStore : IProductionVendorAuditCheckpointStore
    {
        public Task<VendorAuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("private-connection-detail");

        public Task AppendAsync(VendorAuditCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("private-connection-detail");
    }
}
