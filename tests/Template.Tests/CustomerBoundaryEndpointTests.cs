using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class CustomerBoundaryEndpointTests
{
    [Fact]
    public async Task Diagnostics_IsHiddenFromCompanyUsers_AndExposesIdentifiersOnlyToInfrastructure()
    {
        await using var fixture = new Fixture();
        var companyUser = Principal("User", "company-user");
        var infrastructure = Principal("Infrastructure", "master");

        var denied = await Server.Api.CustomerBoundaryEndpoints.DiagnoseAsync(companyUser, fixture.Database, default);
        Assert.Equal(StatusCodes.Status404NotFound, (denied as IStatusCodeHttpResult)?.StatusCode);

        var allowed = await Server.Api.CustomerBoundaryEndpoints.DiagnoseAsync(infrastructure, fixture.Database, default);
        var ok = Assert.IsType<Ok<Server.Api.CustomerBoundaryDiagnostics>>(allowed);
        Assert.Equal(1, ok.Value!.Companies);
        Assert.Equal(1, ok.Value.UnprovisionedCompanies);
        Assert.Contains(fixture.Company.Id, ok.Value.UnresolvedCompanyIds);

        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains(fixture.Company.Id.ToString(), json);
        Assert.DoesNotContain(fixture.Company.Name, json);
        Assert.DoesNotContain("NationalId", json);
    }

    [Fact]
    public async Task Assignment_IsHiddenFromCompanyUsersBeforeAntiforgeryOrDatabaseMutation()
    {
        await using var fixture = new Fixture();
        var antiforgery = new StubAntiforgery(isValid: false);

        var result = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            new DefaultHttpContext(),
            Principal("User", "company-user"),
            new Server.Api.CustomerBoundaryProvisionRequest(Guid.NewGuid(), [fixture.Company.Id]),
            antiforgery,
            fixture.Database,
            default);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(0, antiforgery.ValidationCalls);
        Assert.Null((await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Assignment_RejectsMissingAntiforgeryAndAuditsTheRejection()
    {
        await using var fixture = new Fixture();
        var result = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            new DefaultHttpContext(),
            Principal("Infrastructure", "master"),
            new Server.Api.CustomerBoundaryProvisionRequest(Guid.NewGuid(), [fixture.Company.Id]),
            new StubAntiforgery(isValid: false),
            fixture.Database,
            default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        var audit = Assert.Single(await fixture.Database.SecurityAuditEvents.ToListAsync());
        Assert.Equal("CustomerBoundaryProvisionRejected", audit.EventType);
        Assert.Contains("AntiforgeryRejected", audit.DetailsJson);
        Assert.DoesNotContain("boundary-secret", audit.DetailsJson);
    }

    [Fact]
    public async Task InfrastructureAssignment_SucceedsAndCannotMoveACompanyToAnotherBoundary()
    {
        await using var fixture = new Fixture();
        var principal = Principal("Infrastructure", "master");
        var firstBoundary = Guid.NewGuid();

        var assigned = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            new DefaultHttpContext(),
            principal,
            new Server.Api.CustomerBoundaryProvisionRequest(firstBoundary, [fixture.Company.Id]),
            new StubAntiforgery(isValid: true),
            fixture.Database,
            default);

        Assert.Equal(StatusCodes.Status204NoContent, (assigned as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(firstBoundary, (await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        var successAudit = Assert.Single(await fixture.Database.SecurityAuditEvents.ToListAsync());
        Assert.Equal("CustomerBoundaryProvisioned", successAudit.EventType);
        Assert.Contains(firstBoundary.ToString(), successAudit.EntityId);

        var secondBoundary = Guid.NewGuid();
        var reassignment = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            new DefaultHttpContext(),
            principal,
            new Server.Api.CustomerBoundaryProvisionRequest(secondBoundary, [fixture.Company.Id]),
            new StubAntiforgery(isValid: true),
            fixture.Database,
            default);

        Assert.Equal(StatusCodes.Status409Conflict, (reassignment as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(firstBoundary, (await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        var audits = await fixture.Database.SecurityAuditEvents.OrderBy(item => item.EventType).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains(audits, item =>
            item.EventType == "CustomerBoundaryProvisionRejected" &&
            item.DetailsJson.Contains("Conflict", StringComparison.Ordinal));
    }

    private static ClaimsPrincipal Principal(string accountKind, string name) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, name),
            new Claim(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType, accountKind),
        ],
        "test"));

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            Company = new Domain.Company { Name = "شرکت آزمایش", NationalId = "1234567890", IsActive = true };
            Database.Companies.Add(Company);
            Database.SaveChanges();
        }

        public Data.DatabaseContext Database { get; }
        public Domain.Company Company { get; }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
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
}
