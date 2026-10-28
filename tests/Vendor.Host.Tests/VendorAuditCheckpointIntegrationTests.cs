using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditCheckpointIntegrationTests
{
    [Fact]
    public async Task CheckpointAdvancesOnlyAlongAValidVerifiedChain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable SQL Server connection. " +
                                   "Use FARANO_REQUIRE_VENDOR_SQLSERVER=1 to require this integration test in CI.";
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
            {
                Assert.Fail(message);
            }

            Assert.Skip(message);
            return;
        }

        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FaranouVendorCheckpointTests_{Guid.NewGuid():N}"
        };
        await using var provider = VendorTestServiceProviderFactory.Create(connection.ConnectionString);

        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var setup = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await setup.Database.MigrateAsync(cancellationToken);
            }

            var store = new InMemoryAppendOnlyCheckpointStore();
            await using (var writeScope = provider.CreateAsyncScope())
            {
                var db = writeScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var writer = new VendorAuditWriter(db, TimeProvider.System);
                await writer.AppendAsync(new VendorAuditWriteRequest(null, "test.checkpoint-first"), cancellationToken);
                await writer.AppendAsync(new VendorAuditWriteRequest(null, "test.checkpoint-second"), cancellationToken);
            }

            VendorAuditCheckpoint first;
            await using (var checkpointScope = provider.CreateAsyncScope())
            {
                var db = checkpointScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var service = new VendorAuditCheckpointService(db, store, TimeProvider.System);
                first = await service.CreateCheckpointAsync(cancellationToken);
            }

            Assert.Equal(2, first.EventCount);
            Assert.True(first.IsValid());
            Assert.Single(store.History);

            var divergentStore = new InMemoryAppendOnlyCheckpointStore();
            divergentStore.History.Add(VendorAuditCheckpoint.Create(
                eventCount: 2,
                headHash: new string('f', 64),
                createdAtUtc: TimeProvider.System.GetUtcNow()));
            await using (var divergentScope = provider.CreateAsyncScope())
            {
                var db = divergentScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var service = new VendorAuditCheckpointService(db, divergentStore, TimeProvider.System);
                var exception = await Assert.ThrowsAsync<VendorAuditChainIntegrityException>(
                    () => service.CreateCheckpointAsync(cancellationToken));
                Assert.Equal("checkpoint-head-not-in-current-chain", exception.FailureCode);
            }

            var aheadStore = new InMemoryAppendOnlyCheckpointStore();
            aheadStore.History.Add(VendorAuditCheckpoint.Create(
                eventCount: 3,
                headHash: new string('e', 64),
                createdAtUtc: TimeProvider.System.GetUtcNow()));
            await using (var aheadScope = provider.CreateAsyncScope())
            {
                var db = aheadScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var service = new VendorAuditCheckpointService(db, aheadStore, TimeProvider.System);
                var exception = await Assert.ThrowsAsync<VendorAuditChainIntegrityException>(
                    () => service.CreateCheckpointAsync(cancellationToken));
                Assert.Equal("audit-database-behind-checkpoint", exception.FailureCode);
            }

            await using (var writeScope = provider.CreateAsyncScope())
            {
                var db = writeScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var writer = new VendorAuditWriter(db, TimeProvider.System);
                await writer.AppendAsync(new VendorAuditWriteRequest(null, "test.checkpoint-third"), cancellationToken);
            }

            VendorAuditCheckpoint second;
            await using (var checkpointScope = provider.CreateAsyncScope())
            {
                var db = checkpointScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var service = new VendorAuditCheckpointService(db, store, TimeProvider.System);
                second = await service.CreateCheckpointAsync(cancellationToken);
            }

            Assert.Equal(3, second.EventCount);
            Assert.NotEqual(first.CheckpointHash, second.CheckpointHash);
            Assert.Equal(2, store.History.Count);

            await using (var verificationScope = provider.CreateAsyncScope())
            {
                var db = verificationScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var service = new VendorAuditCheckpointService(db, store, TimeProvider.System);
                var sameCheckpoint = await service.CreateCheckpointAsync(cancellationToken);
                Assert.Equal(second, sameCheckpoint);
                Assert.Equal(2, store.History.Count);
            }

            await using (var tamperScope = provider.CreateAsyncScope())
            {
                var db = tamperScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.AuditEvents
                    .Where(item => item.EventHash == second.HeadHash)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.ActionCode, "tampered.checkpoint-event"), cancellationToken);
            }

            await using (var verificationAfterTamperingScope = provider.CreateAsyncScope())
            {
                var db = verificationAfterTamperingScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var service = new VendorAuditCheckpointService(db, store, TimeProvider.System);
                var exception = await Assert.ThrowsAsync<VendorAuditChainIntegrityException>(
                    () => service.CreateCheckpointAsync(cancellationToken));
                Assert.Equal("event-content-mismatch", exception.FailureCode);
            }
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private sealed class InMemoryAppendOnlyCheckpointStore : IVendorAuditCheckpointStore
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
                throw new InvalidOperationException("Only valid audit checkpoints may be appended.");
            }

            if (History.Count > 0 && checkpoint.EventCount <= History[^1].EventCount)
            {
                throw new InvalidOperationException("Audit checkpoints must be appended with strictly increasing event counts.");
            }

            History.Add(checkpoint);
            return Task.CompletedTask;
        }
    }
}
