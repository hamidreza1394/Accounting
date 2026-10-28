using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditChainIntegrationTests
{
    [Fact]
    public async Task ConcurrentWritersCreateOneValidChainAndDetectContentTampering()
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
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        };
        await using var provider = VendorTestServiceProviderFactory.Create(connection.ConnectionString);

        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var setup = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await setup.Database.MigrateAsync(cancellationToken);
            }

            const int writersCount = 16;
            var writes = Enumerable.Range(0, writersCount).Select(async index =>
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var writer = new VendorAuditWriter(db, TimeProvider.System);
                return await writer.AppendAsync(new VendorAuditWriteRequest(
                    ActorUserId: null,
                    ActionCode: $"test.parallel-event-{index:D2}",
                    SubjectType: "IntegrationTest",
                    SubjectId: Guid.NewGuid().ToString("N"),
                    CorrelationId: Guid.NewGuid().ToString("N")), cancellationToken);
            });

            var receipts = await Task.WhenAll(writes);
            Assert.Equal(writersCount, receipts.Length);
            Assert.Equal(writersCount, receipts.Select(receipt => receipt.EventId).Distinct().Count());

            await using (var verificationScope = provider.CreateAsyncScope())
            {
                var verificationContext = verificationScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var result = await VendorAuditChainVerifier.VerifyAsync(verificationContext, cancellationToken);
                Assert.True(result.IsValid, $"Audit chain verification failed: {result.FailureCode}");
                Assert.Equal(writersCount, result.EventCount);
                Assert.NotNull(result.HeadHash);

                var rootId = await verificationContext.AuditEvents
                    .Where(item => item.PreviousHash == VendorAuditChainVerifier.GenesisHash)
                    .Select(item => item.Id)
                    .SingleAsync(cancellationToken);

                await verificationContext.AuditEvents
                    .Where(item => item.Id == rootId)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.ActionCode, "tampered.audit-event"), cancellationToken);
            }

            await using (var verificationAfterTamperingScope = provider.CreateAsyncScope())
            {
                var verificationAfterTampering = verificationAfterTamperingScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var result = await VendorAuditChainVerifier.VerifyAsync(verificationAfterTampering, cancellationToken);
                Assert.False(result.IsValid);
                Assert.Equal("event-content-mismatch", result.FailureCode);
                Assert.Null(result.HeadHash);
            }
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
    [Fact]
    public async Task DatabaseRejectsForksAndBackfillsHashVersion()
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
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        };
        await using var provider = VendorTestServiceProviderFactory.Create(connection.ConnectionString);

        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var setup = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await setup.Database.MigrateAsync(cancellationToken);
                var writer = new VendorAuditWriter(setup, TimeProvider.System);
                await writer.AppendAsync(new VendorAuditWriteRequest(
                    ActorUserId: null,
                    ActionCode: "test.root-event",
                    SubjectType: "IntegrationTest",
                    SubjectId: Guid.NewGuid().ToString("N"),
                    CorrelationId: Guid.NewGuid().ToString("N")), cancellationToken);

                var root = await setup.AuditEvents.SingleAsync(
                    item => item.PreviousHash == VendorAuditChainVerifier.GenesisHash, cancellationToken);
                Assert.Equal(1, root.HashVersion);

                var fork = new VendorAuditEvent
                {
                    Id = Guid.NewGuid(),
                    HashVersion = 1,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    ActionCode = "test.fork-event",
                    SubjectType = "IntegrationTest",
                    SubjectId = Guid.NewGuid().ToString("N"),
                    PreviousHash = VendorAuditChainVerifier.GenesisHash,
                    EventHash = new string('f', 64)
                };

                setup.AuditEvents.Add(fork);
                await Assert.ThrowsAsync<DbUpdateException>(() => setup.SaveChangesAsync(cancellationToken));

                setup.ChangeTracker.Clear();
                var duplicateHash = new VendorAuditEvent
                {
                    Id = Guid.NewGuid(),
                    HashVersion = 1,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    ActionCode = "test.duplicate-hash",
                    SubjectType = "IntegrationTest",
                    SubjectId = Guid.NewGuid().ToString("N"),
                    PreviousHash = new string('d', 64),
                    EventHash = root.EventHash
                };
                setup.AuditEvents.Add(duplicateHash);
                await Assert.ThrowsAsync<DbUpdateException>(() => setup.SaveChangesAsync(cancellationToken));
            }
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task MigrationRefusesExistingForkedAuditDataWithoutRepairingIt()
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
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        };
        await using var provider = VendorTestServiceProviderFactory.Create(connection.ConnectionString);

        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var setup = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var migrator = setup.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
                await migrator.MigrateAsync("20261027193000_VendorApprovalRequests", cancellationToken);

                await setup.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO dbo.VendorAuditEvents
                        (Id, OccurredAtUtc, ActionCode, PreviousHash, EventHash)
                    VALUES
                        (NEWID(), SYSUTCDATETIME(), N'test.root-one', {0}, REPLICATE('a', 64)),
                        (NEWID(), SYSUTCDATETIME(), N'test.root-two', {0}, REPLICATE('b', 64));
                    """,
                    new object[] { VendorAuditChainVerifier.GenesisHash },
                    cancellationToken);

                await Assert.ThrowsAsync<SqlException>(() => setup.Database.MigrateAsync(cancellationToken));
                var connectionForCheck = setup.Database.GetDbConnection();
                await connectionForCheck.OpenAsync(cancellationToken);
                await using var command = connectionForCheck.CreateCommand();
                command.CommandText = "SELECT CASE WHEN COL_LENGTH('dbo.VendorAuditEvents', 'HashVersion') IS NULL THEN 0 ELSE 1 END";
                var hashVersionColumnExists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                Assert.Equal(0, hashVersionColumnExists);
            }
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

}
