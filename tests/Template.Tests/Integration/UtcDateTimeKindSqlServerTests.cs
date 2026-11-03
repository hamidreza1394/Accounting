using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies that EF restores DateTimeKind.Utc only for DateTime properties whose names end in Utc.
/// Legacy server-local timestamps must remain unchanged and must not acquire a JSON Z suffix.
/// </summary>
public sealed class UtcDateTimeKindSqlServerTests
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
    public async Task UtcSuffixedPropertiesSerializeWithZ_AndLegacyLocalTimestampsRemainUnmarked()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_UTCK_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        var stored = new DateTime(2026, 11, 3, 10, 15, 30, 250, DateTimeKind.Utc);
        Guid auditId;
        Guid companyId;
        Guid decidedRequestId;
        Guid pendingRequestId;

        try
        {
            await using (var write = CreateContext(name))
            {
                await write.Database.MigrateAsync(cancellationToken);

                var company = new Domain.Company { Name = "UTC converter probe", IsActive = true };
                var audit = new Domain.SecurityAuditEvent
                {
                    EventType = "UtcDateTimeKindProbe",
                    OccurredAtUtc = stored,
                };
                var decidedRequest = new Domain.CompanyMembershipRequest
                {
                    UserId = Guid.NewGuid(),
                    DestinationCompany = company,
                    DestinationTenantId = company.Id,
                    Status = Domain.CompanyMembershipRequestStatuses.Rejected,
                    RequestedAtUtc = stored,
                    DecidedByUserId = Guid.NewGuid(),
                    DecidedAtUtc = stored,
                };
                var pendingRequest = new Domain.CompanyMembershipRequest
                {
                    UserId = Guid.NewGuid(),
                    DestinationCompany = company,
                    DestinationTenantId = company.Id,
                    Status = Domain.CompanyMembershipRequestStatuses.Pending,
                    RequestedAtUtc = stored,
                    DecidedAtUtc = null,
                };

                write.AddRange(company, audit, decidedRequest, pendingRequest);
                await write.SaveChangesAsync(cancellationToken);

                auditId = audit.Id;
                companyId = company.Id;
                decidedRequestId = decidedRequest.Id;
                pendingRequestId = pendingRequest.Id;
            }

            await using var read = CreateContext(name);
            var loadedAudit = await read.SecurityAuditEvents.AsNoTracking()
                .SingleAsync(item => item.Id == auditId, cancellationToken);
            var loadedDecision = await read.CompanyMembershipRequests.AsNoTracking()
                .SingleAsync(item => item.Id == decidedRequestId && item.DestinationTenantId == companyId, cancellationToken);
            var loadedPending = await read.CompanyMembershipRequests.AsNoTracking()
                .SingleAsync(item => item.Id == pendingRequestId && item.DestinationTenantId == companyId, cancellationToken);

            Assert.Equal(stored.Ticks, loadedAudit.OccurredAtUtc.Ticks);
            Assert.Equal(DateTimeKind.Utc, loadedAudit.OccurredAtUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, loadedDecision.RequestedAtUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, loadedDecision.DecidedAtUtc?.Kind);
            Assert.Equal(DateTimeKind.Utc, loadedPending.RequestedAtUtc.Kind);
            Assert.Null(loadedPending.DecidedAtUtc);

            // InsertDateTime is a legacy server-local field, not part of the UTC convention.
            Assert.Equal(DateTimeKind.Unspecified, loadedAudit.InsertDateTime.Kind);
            var utcJson = JsonSerializer.Serialize(new { occurredAtUtc = loadedAudit.OccurredAtUtc }, JsonSerializerOptions.Web);
            Assert.Contains("2026-11-03T10:15:30.25Z", utcJson);
            var legacyJson = JsonSerializer.Serialize(new { insertDateTime = loadedAudit.InsertDateTime }, JsonSerializerOptions.Web);
            Assert.DoesNotContain("Z\"", legacyJson);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
