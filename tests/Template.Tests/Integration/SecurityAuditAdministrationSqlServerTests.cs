using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies that the security audit report filters and actor/company projections translate on SQL Server.
/// </summary>
public sealed class SecurityAuditAdministrationSqlServerTests
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

    private sealed class StubAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    [Fact]
    public async Task AuditReportFiltersAndPaginatesOnSqlServerWithoutSensitiveFields()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_AUD_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت ممیزی", IsActive = true };
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User("audit-admin@example.test")
            {
                Username = "audit-admin",
                FirstName = "مدیر",
                LastName = "آزمون",
                IsActive = true,
                Role = role,
            };
            var occurredAt = new DateTime(2026, 11, 3, 12, 30, 0, DateTimeKind.Utc);
            database.AddRange(company, role, admin,
                new Domain.SecurityAuditEvent
                {
                    UserId = admin.Id,
                    TenantId = company.Id,
                    EventType = "LoginSucceeded",
                    EntityType = "User",
                    EntityId = admin.Id.ToString(),
                    OccurredAtUtc = occurredAt,
                    CorrelationId = "00-sql-audit",
                    IpAddress = "203.0.113.10",
                    DetailsJson = "{\"secret\":\"sql-private-payload\"}",
                },
                new Domain.SecurityAuditEvent
                {
                    UserId = admin.Id,
                    TenantId = company.Id,
                    EventType = "LogoutSucceeded",
                    OccurredAtUtc = occurredAt.AddMinutes(5),
                });
            await database.SaveChangesAsync(cancellationToken);

            var result = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
                new DefaultHttpContext(),
                Principal(admin.Id),
                database,
                eventType: "LoginSucceeded",
                userId: admin.Id,
                companyId: company.Id,
                fromUtc: new DateTimeOffset(occurredAt.AddMinutes(-1), TimeSpan.Zero),
                toUtc: new DateTimeOffset(occurredAt.AddMinutes(1), TimeSpan.Zero),
                page: 1,
                pageSize: 10,
                cancellationToken: cancellationToken);

            var ok = Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(result);
            Assert.Equal(1, ok.Value!.TotalCount);
            var item = Assert.Single(ok.Value.Items);
            Assert.Equal("audit-admin", item.ActorUsername);
            Assert.Equal("مدیر آزمون", item.ActorDisplayName);
            Assert.Equal("شرکت ممیزی", item.CompanyName);
            Assert.Equal("LoginSucceeded", item.EventType);
            Assert.Equal(DateTimeKind.Utc, item.OccurredAtUtc.Kind);

            var sorted = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
                new DefaultHttpContext(), Principal(admin.Id), database,
                sort: "eventType", descending: false,
                page: 1, pageSize: 10, cancellationToken: cancellationToken);
            var sortedItems = Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(sorted).Value!.Items;
            Assert.Equal(new[] { "LoginSucceeded", "LogoutSucceeded" }, sortedItems.Select(row => row.EventType));
            Assert.All(sortedItems, row => Assert.Equal(DateTimeKind.Utc, row.OccurredAtUtc.Kind));

            var eventTypes = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventTypesAsync(
                new DefaultHttpContext(), Principal(admin.Id), database, cancellationToken);
            var typeItems = Assert.IsType<Ok<Server.Api.SecurityAuditEventTypeResponse[]>>(eventTypes).Value!;
            Assert.Contains(typeItems, type => type.Code == "LoginSucceeded" && type.Label == "ورود موفق");

            var companies = await Server.Api.SecurityAuditAdministrationEndpoints.GetAdminCompaniesAsync(
                new DefaultHttpContext(), Principal(admin.Id), database, search: "شرکت",
                page: 1, pageSize: 10, cancellationToken: cancellationToken);
            var companyItems = Assert.IsType<Ok<Server.Api.SecurityAuditCompanyListResponse>>(companies).Value!;
            Assert.Contains(companyItems.Items, companyItem => companyItem.Id == company.Id);

            var json = JsonSerializer.Serialize(ok.Value);
            Assert.DoesNotContain("203.0.113.10", json);
            Assert.DoesNotContain("sql-private-payload", json);

            var exportContext = new DefaultHttpContext();
            var export = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
                exportContext,
                Principal(admin.Id),
                new Server.Api.SecurityAuditExportRequest(
                    "LoginSucceeded", admin.Id, company.Id,
                    new DateTimeOffset(occurredAt.AddMinutes(-1), TimeSpan.Zero),
                    new DateTimeOffset(occurredAt.AddMinutes(1), TimeSpan.Zero)),
                new StubAntiforgery(),
                database,
                cancellationToken);
            var exportFile = Assert.IsType<FileContentHttpResult>(export);
            var csv = Encoding.UTF8.GetString(exportFile.FileContents.Span);
            Assert.Contains("LoginSucceeded", csv);
            Assert.DoesNotContain("203.0.113.10", csv);
            Assert.DoesNotContain("sql-private-payload", csv);
            Assert.Equal("no-store", exportContext.Response.Headers.CacheControl.ToString());
            Assert.True(await database.SecurityAuditEvents.AnyAsync(
                item => item.EventType == "SecurityAuditExported" && item.UserId == admin.Id,
                cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
