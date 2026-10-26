using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        try
        {
            await using (var setup = new VendorDbContext(options))
            {
                await setup.Database.MigrateAsync(cancellationToken);
            }

            const int writersCount = 16;
            var writes = Enumerable.Range(0, writersCount).Select(async index =>
            {
                await using var db = new VendorDbContext(options);
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

            await using (var verificationContext = new VendorDbContext(options))
            {
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

            await using (var verificationAfterTampering = new VendorDbContext(options))
            {
                var result = await VendorAuditChainVerifier.VerifyAsync(verificationAfterTampering, cancellationToken);
                Assert.False(result.IsValid);
                Assert.Equal("event-content-mismatch", result.FailureCode);
                Assert.Null(result.HeadHash);
            }
        }
        finally
        {
            await using var cleanup = new VendorDbContext(options);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
