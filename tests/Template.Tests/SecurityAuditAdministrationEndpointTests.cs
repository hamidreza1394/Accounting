using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class SecurityAuditAdministrationEndpointTests
{
    [Fact]
    public async Task AuditReport_RequiresAnActiveInstallationAdministratorFromTheDatabase()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var ordinary = fixture.AddUser("accountant", "Accountant");
        var inactiveAdmin = fixture.AddUser("inactive-admin", Constants.Role.Admin, active: false);
        await fixture.Database.SaveChangesAsync();

        var denied = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var inactiveDenied = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(inactiveAdmin.Id), fixture.Database);
        Assert.Equal(StatusCodes.Status403Forbidden, (inactiveDenied as IStatusCodeHttpResult)?.StatusCode);

        var allowed = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database);
        Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(allowed);
    }

    [Fact]
    public async Task AuditReport_ComposesFiltersPaginatesDeterministicallyAndOmitsSensitiveFields()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var alice = fixture.AddUser("alice", "Accountant");
        var bob = fixture.AddUser("bob", "Accountant");
        var companyA = new Domain.Company { Name = "شرکت الف", IsActive = true };
        var companyB = new Domain.Company { Name = "شرکت ب", IsActive = true };
        fixture.Database.Companies.AddRange(companyA, companyB);

        var t1 = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddHours(1);
        var t3 = t1.AddHours(2);
        var t4 = t1.AddHours(3);
        fixture.Database.SecurityAuditEvents.AddRange(
            NewEvent(alice.Id, companyA.Id, "LoginSucceeded", t2),
            NewEvent(alice.Id, companyA.Id, "LoginFailed", t1),
            NewEvent(bob.Id, companyB.Id, "LoginSucceeded", t3),
            NewEvent(alice.Id, companyA.Id, "RoleChanged", t4));
        await fixture.Database.SaveChangesAsync();

        var filteredContext = new DefaultHttpContext();
        var filtered = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            filteredContext,
            Principal(admin.Id),
            fixture.Database,
            eventType: "LoginSucceeded",
            userId: alice.Id,
            companyId: companyA.Id,
            fromUtc: new DateTimeOffset(t1.AddMinutes(30), TimeSpan.Zero),
            toUtc: new DateTimeOffset(t3.AddMinutes(-30), TimeSpan.Zero),
            page: 1,
            pageSize: 10);
        var filteredOk = Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(filtered);
        Assert.Equal(1, filteredOk.Value!.TotalCount);
        var item = Assert.Single(filteredOk.Value.Items);
        Assert.Equal(alice.Id, item.UserId);
        Assert.Equal(companyA.Id, item.CompanyId);
        Assert.Equal("alice", item.ActorUsername);
        Assert.Equal("شرکت الف", item.CompanyName);
        Assert.Equal("LoginSucceeded", item.EventType);
        Assert.Equal("no-store", filteredContext.Response.Headers.CacheControl.ToString());

        var firstPage = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database, page: 1, pageSize: 2);
        var firstPageOk = Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(firstPage);
        Assert.Equal(4, firstPageOk.Value!.TotalCount);
        Assert.Equal(new[] { t4, t3 }, firstPageOk.Value.Items.Select(row => row.OccurredAtUtc));

        var secondPage = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database, page: 2, pageSize: 2);
        var secondPageOk = Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(secondPage);
        Assert.Equal(new[] { t2, t1 }, secondPageOk.Value!.Items.Select(row => row.OccurredAtUtc));

        Assert.DoesNotContain(nameof(Domain.SecurityAuditEvent.IpAddress),
            typeof(Server.Api.SecurityAuditEventItemResponse).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain(nameof(Domain.SecurityAuditEvent.DetailsJson),
            typeof(Server.Api.SecurityAuditEventItemResponse).GetProperties().Select(property => property.Name));
        var json = JsonSerializer.Serialize(firstPageOk.Value);
        Assert.DoesNotContain("192.0.2.77", json);
        Assert.DoesNotContain("private-audit-payload", json);
    }

    [Fact]
    public async Task AuditReport_RejectsInvalidFilters()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();
        var principal = Principal(admin.Id);

        var invalidRange = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), principal, fixture.Database,
            fromUtc: new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(StatusCodes.Status400BadRequest, (invalidRange as IStatusCodeHttpResult)?.StatusCode);

        var longEventType = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), principal, fixture.Database, eventType: new string('x', 121));
        Assert.Equal(StatusCodes.Status400BadRequest, (longEventType as IStatusCodeHttpResult)?.StatusCode);

        var emptyUser = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), principal, fixture.Database, userId: Guid.Empty);
        Assert.Equal(StatusCodes.Status400BadRequest, (emptyUser as IStatusCodeHttpResult)?.StatusCode);

        var emptyCompany = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), principal, fixture.Database, companyId: Guid.Empty);
        Assert.Equal(StatusCodes.Status400BadRequest, (emptyCompany as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task AuditReportUsesOnlyAllowlistedSortKeysAndReturnsUtcWireValues()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var company = new Domain.Company { Name = "شرکت مرتب‌سازی", IsActive = true };
        fixture.Database.Companies.Add(company);
        var first = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Unspecified);
        var second = first.AddHours(1);
        fixture.Database.SecurityAuditEvents.AddRange(
            NewEvent(admin.Id, company.Id, "LoginSucceeded", first),
            NewEvent(admin.Id, company.Id, "LoginFailed", second));
        await fixture.Database.SaveChangesAsync();

        var sorted = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            sort: "eventType", descending: false);
        var response = Assert.IsType<Ok<Server.Api.SecurityAuditEventListResponse>>(sorted).Value!;
        Assert.Equal(new[] { "LoginFailed", "LoginSucceeded" }, response.Items.Select(item => item.EventType));
        Assert.All(response.Items, item => Assert.Equal(DateTimeKind.Utc, item.OccurredAtUtc.Kind));

        var injection = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            sort: "OccurredAtUtc DESC; DROP TABLE SecurityAuditEvents");
        Assert.Equal(StatusCodes.Status400BadRequest, (injection as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task AuditEventTypesExposeServerLabelsAndUnknownCodesSafely()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        fixture.Database.SecurityAuditEvents.AddRange(
            NewEvent(admin.Id, null, "LoginSucceeded", DateTime.UtcNow),
            NewEvent(admin.Id, null, "UnknownEventType", DateTime.UtcNow));
        await fixture.Database.SaveChangesAsync();

        var context = new DefaultHttpContext();
        var result = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventTypesAsync(
            context, Principal(admin.Id), fixture.Database);
        var items = Assert.IsType<Ok<Server.Api.SecurityAuditEventTypeResponse[]>>(result).Value!;
        Assert.Contains(items, item => item.Code == "LoginSucceeded" && item.Label == "ورود موفق");
        Assert.Contains(items, item => item.Code == "UnknownEventType" && item.Label == "UnknownEventType");
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());

        var ordinary = fixture.AddUser("ordinary", "Accountant");
        await fixture.Database.SaveChangesAsync();
        var denied = await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventTypesAsync(
            new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task AdminCompanyListIsInstallationAdminOnlyAndPaginated()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var ordinary = fixture.AddUser("ordinary", "Accountant");
        fixture.Database.Companies.AddRange(
            new Domain.Company { Name = "شرکت آلفا", IsActive = true },
            new Domain.Company { Name = "شرکت بتا", IsActive = false },
            new Domain.Company { Name = "موسسه خارج از جستجو", IsActive = true });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.SecurityAuditAdministrationEndpoints.GetAdminCompaniesAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            search: "شرکت", page: 1, pageSize: 1);
        var response = Assert.IsType<Ok<Server.Api.SecurityAuditCompanyListResponse>>(result).Value!;
        Assert.Equal(2, response.TotalCount);
        Assert.Single(response.Items);
        Assert.Contains(response.Items, item => item.Name == "شرکت آلفا" || item.Name == "شرکت بتا");

        var denied = await Server.Api.SecurityAuditAdministrationEndpoints.GetAdminCompaniesAsync(
            new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task AuditExport_ProducesSafeUtf8CsvAndOmitsSensitiveFields()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var actor = fixture.AddUser("alice", "Accountant");
        actor.Username = "=SUM(1,1)";
        actor.FirstName = "=HYPERLINK(\"https://example.invalid\")";
        var company = new Domain.Company { Name = " \t=1+1", IsActive = true };
        fixture.Database.Companies.Add(company);
        var occurredAt = new DateTime(2026, 11, 3, 12, 30, 0, DateTimeKind.Utc);
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actor.Id,
            TenantId = company.Id,
            EventType = "LoginSucceeded",
            EntityType = "-CMD",
            EntityId = "+CMD,\"quoted\"",
            OccurredAtUtc = occurredAt,
            CorrelationId = "@CMD",
            IpAddress = "198.51.100.99",
            DetailsJson = "{\"secret\":\"export-private-payload\"}",
        });
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actor.Id,
            TenantId = company.Id,
            EventType = "LoginSucceeded",
            EntityType = "\tleading-tab",
            EntityId = "\rleading-cr",
            OccurredAtUtc = occurredAt.AddSeconds(-1),
        });
        await fixture.Database.SaveChangesAsync();

        var context = new DefaultHttpContext();
        var result = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
            context,
            Principal(admin.Id),
            new Server.Api.SecurityAuditExportRequest(
                "LoginSucceeded", actor.Id, company.Id,
                new DateTimeOffset(occurredAt.AddMinutes(-1), TimeSpan.Zero),
                new DateTimeOffset(occurredAt.AddMinutes(1), TimeSpan.Zero)),
            new StubAntiforgery(isValid: true),
            fixture.Database);

        var file = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
        Assert.Equal(0xEF, file.FileContents.Span[0]);
        Assert.Equal(0xBB, file.FileContents.Span[1]);
        Assert.Equal(0xBF, file.FileContents.Span[2]);

        var csv = Encoding.UTF8.GetString(file.FileContents.Span);
        Assert.Contains("OccurredAtUtc", csv);
        Assert.Contains("2026-11-03T12:30:00.0000000Z", csv);
        Assert.Contains("\"'+CMD,\"\"quoted\"\"", csv);
        Assert.Contains("'=SUM(1,1)", csv);
        Assert.Contains("'=HYPERLINK", csv);
        Assert.Contains("' \t=1+1", csv);
        Assert.Contains("'@CMD", csv);
        Assert.Contains("\"'-CMD\"", csv);
        Assert.Contains("\"'\tleading-tab\"", csv);
        Assert.Contains("\"'\rleading-cr\"", csv);
        Assert.DoesNotContain("198.51.100.99", csv);
        Assert.DoesNotContain("export-private-payload", csv);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());

        var exportAudit = await fixture.Database.SecurityAuditEvents.SingleAsync(
            item => item.EventType == "SecurityAuditExported");
        Assert.Equal(admin.Id, exportAudit.UserId);
        using var details = JsonDocument.Parse(exportAudit.DetailsJson!);
        Assert.Equal(2, details.RootElement.GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task AuditExport_RequiresInstallationAdministratorAndBoundedDateRange()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var ordinary = fixture.AddUser("accountant", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);
        var request = new Server.Api.SecurityAuditExportRequest(null, null, null, from, to);
        var denied = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
            new DefaultHttpContext(), Principal(ordinary.Id), request,
            new StubAntiforgery(isValid: true), fixture.Database);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var rejectedContext = new DefaultHttpContext();
        var rejectedAntiforgery = new StubAntiforgery(isValid: false);
        var antiforgeryRejected = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
            rejectedContext, Principal(admin.Id), request, rejectedAntiforgery, fixture.Database);
        Assert.Equal(StatusCodes.Status400BadRequest, (antiforgeryRejected as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(1, rejectedAntiforgery.ValidationCalls);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync(item => item.EventType == "SecurityAuditExported"));

        var missingRange = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id),
            new Server.Api.SecurityAuditExportRequest(null, null, null, null, null),
            new StubAntiforgery(isValid: true), fixture.Database);
        Assert.Equal(StatusCodes.Status400BadRequest, (missingRange as IStatusCodeHttpResult)?.StatusCode);

        var wideRange = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
            new DefaultHttpContext(), Principal(admin.Id),
            new Server.Api.SecurityAuditExportRequest(null, null, null, from, from.AddDays(31).AddTicks(1)),
            new StubAntiforgery(isValid: true), fixture.Database);
        Assert.Equal(StatusCodes.Status400BadRequest, (wideRange as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task AuditExport_ReturnsPayloadTooLargeWhenMoreThanTenThousandRowsMatch()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        fixture.Database.SecurityAuditEvents.AddRange(Enumerable.Range(0, 10_001).Select(index =>
            new Domain.SecurityAuditEvent
            {
                EventType = "LoginSucceeded",
                OccurredAtUtc = from.AddSeconds(index),
            }));
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.SecurityAuditAdministrationEndpoints.ExportAuditEventsAsync(
            new DefaultHttpContext(),
            Principal(admin.Id),
            new Server.Api.SecurityAuditExportRequest(null, null, null,
                new DateTimeOffset(from, TimeSpan.Zero), new DateTimeOffset(from.AddDays(1), TimeSpan.Zero)),
            new StubAntiforgery(isValid: true),
            fixture.Database);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class StubAntiforgery(bool isValid) : IAntiforgery
    {
        public int ValidationCalls { get; private set; }
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext)
        {
            ValidationCalls++;
            return Task.FromResult(isValid);
        }
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private static Domain.SecurityAuditEvent NewEvent(Guid? userId, Guid? companyId, string eventType, DateTime occurredAtUtc) => new()
    {
        UserId = userId,
        TenantId = companyId,
        EventType = eventType,
        EntityType = "User",
        EntityId = userId?.ToString(),
        OccurredAtUtc = occurredAtUtc,
        CorrelationId = "00-audit-test",
        IpAddress = "192.0.2.77",
        DetailsJson = "{\"value\":\"private-audit-payload\"}",
    };

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        }

        public Data.DatabaseContext Database { get; }

        public Domain.User AddUser(string username, string roleName, bool active = true)
        {
            var role = new Domain.Role(roleName) { IsActive = true };
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = active,
                Role = role,
            };
            Database.Roles.Add(role);
            Database.Users.Add(user);
            return user;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
