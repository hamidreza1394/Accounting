using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        var deniedContext = CreateHttpContext();
        var denied = await Server.Api.CustomerBoundaryEndpoints.DiagnoseAsync(companyUser, fixture.Database, default);
        await denied.ExecuteAsync(deniedContext);
        Assert.Equal(StatusCodes.Status404NotFound, deniedContext.Response.StatusCode);

        var allowedContext = CreateHttpContext();
        var allowed = await Server.Api.CustomerBoundaryEndpoints.DiagnoseAsync(infrastructure, fixture.Database, default);
        await allowed.ExecuteAsync(allowedContext);
        Assert.Equal(StatusCodes.Status200OK, allowedContext.Response.StatusCode);
        allowedContext.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(allowedContext.Response.Body);
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("companies").GetInt32());
        Assert.Equal(1, root.GetProperty("unprovisionedCompanies").GetInt32());
        Assert.Empty(root.GetProperty("unresolvedCompanyIds").EnumerateArray());

        var serialized = root.GetRawText();
        Assert.DoesNotContain(fixture.Company.Id.ToString(), serialized);
        Assert.DoesNotContain(fixture.Company.Name, serialized);
        Assert.DoesNotContain("NationalId", serialized);
    }

    [Fact]
    public async Task Assignment_IsHiddenFromCompanyUsersBeforeAntiforgeryOrDatabaseMutation()
    {
        await using var fixture = new Fixture();
        var antiforgery = new StubAntiforgery(isValid: false);

        var httpContext = CreateHttpContext();
        var result = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            httpContext,
            Principal("User", "company-user"),
            new Server.Api.CustomerBoundaryProvisionRequest(Guid.NewGuid(), [fixture.Company.Id]),
            antiforgery,
            fixture.Database,
            default);

        await result.ExecuteAsync(httpContext);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal(0, antiforgery.ValidationCalls);
        Assert.Null((await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Assignment_RejectsMissingAntiforgeryAndAuditsTheRejection()
    {
        await using var fixture = new Fixture();
        var httpContext = CreateHttpContext();
        var result = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            httpContext,
            Principal("Infrastructure", "master"),
            new Server.Api.CustomerBoundaryProvisionRequest(Guid.NewGuid(), [fixture.Company.Id]),
            new StubAntiforgery(isValid: false),
            fixture.Database,
            default);

        await result.ExecuteAsync(httpContext);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
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

        var assignedContext = CreateHttpContext();
        var assigned = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            assignedContext,
            principal,
            new Server.Api.CustomerBoundaryProvisionRequest(firstBoundary, [fixture.Company.Id]),
            new StubAntiforgery(isValid: true),
            fixture.Database,
            default);

        await assigned.ExecuteAsync(assignedContext);
        Assert.Equal(StatusCodes.Status204NoContent, assignedContext.Response.StatusCode);
        Assert.Equal(firstBoundary, (await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        var successAudit = Assert.Single(await fixture.Database.SecurityAuditEvents.ToListAsync());
        Assert.Equal("CustomerBoundaryProvisioned", successAudit.EventType);
        Assert.Contains(firstBoundary.ToString(), successAudit.EntityId);

        var secondBoundary = Guid.NewGuid();
        var reassignmentContext = CreateHttpContext();
        var reassignment = await Server.Api.CustomerBoundaryEndpoints.AssignAsync(
            reassignmentContext,
            principal,
            new Server.Api.CustomerBoundaryProvisionRequest(secondBoundary, [fixture.Company.Id]),
            new StubAntiforgery(isValid: true),
            fixture.Database,
            default);

        await reassignment.ExecuteAsync(reassignmentContext);
        Assert.Equal(StatusCodes.Status409Conflict, reassignmentContext.Response.StatusCode);
        Assert.Equal(firstBoundary, (await fixture.Database.Companies.SingleAsync()).CustomerBoundaryId);
        var audits = await fixture.Database.SecurityAuditEvents.OrderBy(item => item.EventType).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains(audits, item =>
            item.EventType == "CustomerBoundaryProvisionRejected" &&
            item.DetailsJson.Contains("Conflict", StringComparison.Ordinal));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .AddProblemDetails()
            .BuildServiceProvider();
        return new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };
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
