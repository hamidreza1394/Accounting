using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// CHARACTERIZATION of the current wire format of UTC timestamps (docs/TIME_CONTRACT.md, D-067).
/// It documents how data stored on SQL Server reaches the API: EF materializes a stored
/// <c>datetime2</c> with <c>Kind=Unspecified</c>, so System.Text.Json writes it WITHOUT a <c>Z</c>.
/// The ClientApp therefore treats every <c>*Utc</c> field as UTC (lib/time.ts). When the Backend
/// starts materializing <c>*Utc</c> columns as <c>Kind=Utc</c> (proposed in D-067), this test must be
/// updated to expect the <c>Z</c>; until then it protects the assumption the client relies on.
/// </summary>
public sealed class UtcWireFormatSqlServerTests
{
    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static Data.DatabaseContext CreateContext(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        };
        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    [Fact]
    public async Task AUtcInstantStoredOnSqlServerComesBackWithoutAUtcMarker()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_UTC_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var stored = new DateTime(2026, 11, 3, 10, 15, 30, 250, DateTimeKind.Utc);
            Guid id;
            await using (var write = CreateContext(name))
            {
                await write.Database.MigrateAsync(cancellationToken);
                var audit = new Domain.SecurityAuditEvent { EventType = "UtcWireFormatProbe", OccurredAtUtc = stored };
                id = audit.Id;
                write.SecurityAuditEvents.Add(audit);
                await write.SaveChangesAsync(cancellationToken);
            }

            await using var read = CreateContext(name);
            var loaded = await read.SecurityAuditEvents.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);

            // The instant itself survives the round trip ...
            Assert.Equal(stored.Ticks, loaded.OccurredAtUtc.Ticks);
            // ... but its Kind does not, which is why the JSON carries no Z.
            Assert.Equal(DateTimeKind.Unspecified, loaded.OccurredAtUtc.Kind);
            var json = JsonSerializer.Serialize(new { occurredAtUtc = loaded.OccurredAtUtc }, JsonSerializerOptions.Web);
            Assert.Contains("2026-11-03T10:15:30.25", json);
            Assert.DoesNotContain("Z\"", json);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
