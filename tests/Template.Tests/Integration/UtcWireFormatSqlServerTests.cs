using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the UTC wire contract after the EF UTC-kind converter (D-067).
/// SQL Server datetime2 does not persist DateTimeKind, so EF must restore Utc for *Utc properties
/// before System.Text.Json serializes them with a Z suffix.
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
    public async Task AUtcInstantStoredOnSqlServerComesBackAsUtcAndSerializesWithMarker()
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

            // SQL Server preserves the instant; the EF converter restores its UTC kind.
            Assert.Equal(stored.Ticks, loaded.OccurredAtUtc.Ticks);
            Assert.Equal(DateTimeKind.Utc, loaded.OccurredAtUtc.Kind);
            var json = JsonSerializer.Serialize(new { occurredAtUtc = loaded.OccurredAtUtc }, JsonSerializerOptions.Web);
            Assert.Contains("2026-11-03T10:15:30.25Z", json);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
