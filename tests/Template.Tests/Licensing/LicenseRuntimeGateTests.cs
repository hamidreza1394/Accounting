using System.Net;
using Infrastructure.Licensing;
using Infrastructure.Provisioning;
using Faranou.Licensing.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Licensing;

public sealed class LicenseRuntimeGateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static LicensePayload Payload() => new(
        LicensePayload.CurrentFormatVersion,
        "lic-1",
        "installation-1",
        "faranou-core",
        LicensePayload.KindPerpetual,
        Now,
        null,
        null,
        0,
        3,
        new LicenseLimits(5, [LicenseSubsystemCodes.Finance]))
    {
        SupportUntilUtc = Now.AddDays(30),
        UpdatesUntilUtc = Now.AddDays(60),
        CompanyEntitlements =
        [
            new CompanyLicenseEntitlement("guid-ref-1", [LicenseSubsystemCodes.Finance], 10, [])
        ],
    };

    private static LicenseRuntimeGateOptions GateOptions(bool enabled, string mode = LicenseDeploymentModes.Dedicated) =>
        new() { Enabled = enabled, DeploymentMode = mode };

    private static LicenseRuntimeStateSnapshot Snapshot(
        LicenseRuntimeState state,
        string mode = LicenseDeploymentModes.Dedicated,
        string? errorCode = null,
        LicensePayload? payload = null) =>
        new(state, mode, state is LicenseRuntimeState.Active ? LicenseStatus.Valid : null, errorCode, payload, Now);

    private static (LicenseRuntimeGateMiddleware Middleware, List<HttpContext> NextCalls) CreateMiddleware(
        LicenseRuntimeStateSnapshot snapshot,
        LicenseRuntimeGateOptions gateOptions)
    {
        var nextCalls = new List<HttpContext>();
        var middleware = new LicenseRuntimeGateMiddleware(_ =>
        {
            nextCalls.Add(_);
            return Task.CompletedTask;
        });
        return (middleware, nextCalls);
    }

    private static async Task<HttpContext> InvokeAsync(
        LicenseRuntimeGateMiddleware middleware,
        LicenseRuntimeStateSnapshot snapshot,
        LicenseRuntimeGateOptions gateOptions,
        string method,
        string path,
        bool registerStateService = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var services = new ServiceCollection();
        if (registerStateService)
        {
            services.AddSingleton<ILicenseRuntimeStateService>(new FakeStateService(snapshot));
        }

        services.AddSingleton(Options.Create(gateOptions));
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<LicenseRuntimeGateMiddleware>>(
            NullLogger<LicenseRuntimeGateMiddleware>.Instance);
        context.RequestServices = services.BuildServiceProvider();

        await middleware.InvokeAsync(context);
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    private sealed class FakeStateService(LicenseRuntimeStateSnapshot snapshot) : ILicenseRuntimeStateService
    {
        public int CallCount { get; private set; }

        public Task<LicenseRuntimeStateSnapshot> ResolveAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(snapshot);
        }
    }

    [Fact]
    public async Task DisabledGatePassesEveryRequestThroughWithoutResolvingState()
    {
        var (middleware, nextCalls) = CreateMiddleware(Snapshot(LicenseRuntimeState.Unlicensed), GateOptions(enabled: false));

        var context = await InvokeAsync(middleware, Snapshot(LicenseRuntimeState.Unlicensed), GateOptions(enabled: false), "POST", "/api/v1/companies");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Single(nextCalls);
    }

    [Fact]
    public async Task DisabledGateNeverRequiresTheLicensingStackToBeRegistered()
    {
        // First-run setup mode has no licensing stack registered; the disabled gate must not
        // resolve it, so /DatabaseSetup keeps working.
        var (middleware, nextCalls) = CreateMiddleware(Snapshot(LicenseRuntimeState.Unlicensed), GateOptions(enabled: false));

        var context = await InvokeAsync(
            middleware,
            Snapshot(LicenseRuntimeState.Unlicensed),
            GateOptions(enabled: false),
            "GET",
            "/DatabaseSetup",
            registerStateService: false);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Single(nextCalls);
    }

    [Fact]
    public async Task ActiveStateAllowsNormalRequests()
    {
        var snapshot = Snapshot(LicenseRuntimeState.Active, payload: Payload());
        var (middleware, nextCalls) = CreateMiddleware(snapshot, GateOptions(enabled: true));

        var context = await InvokeAsync(middleware, snapshot, GateOptions(enabled: true), "POST", "/api/v1/companies");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Single(nextCalls);
    }

    [Fact]
    public async Task UnlicensedStateBlocksNormalRequestsButKeepsActivationPaths()
    {
        var snapshot = Snapshot(LicenseRuntimeState.Unlicensed, errorCode: "license_missing");
        var gate = GateOptions(enabled: true);

        var blocked = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/api/v1/companies");
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, blocked.Response.StatusCode);
        var body = await ReadBodyAsync(blocked);
        Assert.Contains("license_required", body, StringComparison.Ordinal);
        Assert.Contains("unlicensed", body, StringComparison.Ordinal);
        Assert.Equal("no-store", blocked.Response.Headers.CacheControl.ToString());

        var wizard = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/api/v1/licensing/status");
        Assert.Equal(StatusCodes.Status200OK, wizard.Response.StatusCode);

        var loginPage = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/Account/Login");
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, loginPage.Response.StatusCode);
    }

    [Fact]
    public async Task InvalidStateFailsClosed()
    {
        var snapshot = Snapshot(LicenseRuntimeState.Invalid, errorCode: "envelope_json_invalid");
        var gate = GateOptions(enabled: true);

        var context = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/api/v1/companies");

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.Contains("license_invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestrictedDedicatedKeepsReadsAndBlocksOrdinaryWrites()
    {
        var snapshot = Snapshot(LicenseRuntimeState.Restricted);
        var gate = GateOptions(enabled: true);

        var read = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/api/v1/companies");
        Assert.Equal(StatusCodes.Status200OK, read.Response.StatusCode);

        var head = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "HEAD", "/app/index.html");
        Assert.Equal(StatusCodes.Status200OK, head.Response.StatusCode);

        var write = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "POST", "/api/v1/companies");
        Assert.Equal(StatusCodes.Status403Forbidden, write.Response.StatusCode);
        var body = await ReadBodyAsync(write);
        Assert.Contains("license_restricted", body, StringComparison.Ordinal);

        var logout = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "POST", "/Account/Logout");
        Assert.Equal(StatusCodes.Status200OK, logout.Response.StatusCode);
    }

    [Fact]
    public async Task RestrictedCloudBlocksNormalOperationsPerD098()
    {
        var snapshot = Snapshot(LicenseRuntimeState.Restricted, mode: LicenseDeploymentModes.Cloud);
        var gate = GateOptions(enabled: true, mode: LicenseDeploymentModes.Cloud);

        var read = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/api/v1/session");
        Assert.Equal(StatusCodes.Status403Forbidden, read.Response.StatusCode);
        var body = await ReadBodyAsync(read);
        Assert.Contains("license_cloud_expired", body, StringComparison.Ordinal);

        var renewal = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "GET", "/api/v1/licensing/status");
        Assert.Equal(StatusCodes.Status200OK, renewal.Response.StatusCode);
    }

    [Fact]
    public async Task AlwaysAllowedPathsReachThePipelineInEveryState()
    {
        foreach (var state in new[] { LicenseRuntimeState.Unlicensed, LicenseRuntimeState.Invalid, LicenseRuntimeState.Restricted })
        {
            var snapshot = Snapshot(state, errorCode: "license_missing");
            var gate = GateOptions(enabled: true);
            var context = await InvokeAsync(CreateMiddleware(snapshot, gate).Middleware, snapshot, gate, "POST", "/api/v1/provisioning/apply");
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public void DeploymentModeMustBeExplicitlyKnown()
    {
        Assert.Throws<InvalidOperationException>(
            () => new LicenseRuntimeGateOptions { DeploymentMode = "Hybrid" }.Validate());

        new LicenseRuntimeGateOptions { DeploymentMode = LicenseDeploymentModes.Cloud }.Validate();
        new LicenseRuntimeGateOptions { DeploymentMode = LicenseDeploymentModes.Dedicated }.Validate();
    }
}

public sealed class LicenseRuntimeStateServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static LicensePayload Payload() => new(
        LicensePayload.CurrentFormatVersion,
        "lic-1",
        "installation-1",
        "faranou-core",
        LicensePayload.KindPerpetual,
        Now,
        null,
        null,
        0,
        3,
        new LicenseLimits(5, [LicenseSubsystemCodes.Finance]));

    private static LicenseRuntimeStateService CreateService(
        FakeEvaluator evaluator,
        string installationId = "installation-1",
        string deploymentMode = LicenseDeploymentModes.Dedicated) =>
        new(
            evaluator,
            Options.Create(new CustomerProvisioningSettings { InstallationId = installationId }),
            Options.Create(new LicenseRuntimeGateOptions
            {
                Enabled = true,
                DeploymentMode = deploymentMode
            }));

    private sealed class FakeEvaluator(CustomerLicenseRuntimeResult result) : ICustomerLicenseRuntimeEvaluator
    {
        public string? LastInstallationId { get; private set; }

        public Task<CustomerLicenseRuntimeResult> EvaluateCurrentAsync(
            string localInstallationId,
            CancellationToken cancellationToken = default)
        {
            LastInstallationId = localInstallationId;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task ValidatedPayloadMapsToActiveStateAndCarriesPayload()
    {
        var evaluator = new FakeEvaluator(CustomerLicenseRuntimeResult.Usable(LicenseStatus.Valid, Payload()));

        var snapshot = await CreateService(evaluator).ResolveAsync();

        Assert.Equal(LicenseRuntimeState.Active, snapshot.State);
        Assert.Equal(LicenseStatus.Valid, snapshot.Status);
        Assert.Null(snapshot.ErrorCode);
        Assert.NotNull(snapshot.ValidatedPayload);
        Assert.Equal("installation-1", evaluator.LastInstallationId);
    }

    [Fact]
    public async Task RestrictedPayloadMapsToRestrictedState()
    {
        var evaluator = new FakeEvaluator(CustomerLicenseRuntimeResult.Usable(LicenseStatus.Restricted, Payload()));

        var snapshot = await CreateService(evaluator).ResolveAsync();

        Assert.Equal(LicenseRuntimeState.Restricted, snapshot.State);
        Assert.Equal(LicenseStatus.Restricted, snapshot.Status);
        Assert.NotNull(snapshot.ValidatedPayload);
    }

    [Fact]
    public async Task MissingLicenseIsUnlicensedWithoutPayload()
    {
        var evaluator = new FakeEvaluator(CustomerLicenseRuntimeResult.Rejected("license_missing"));

        var snapshot = await CreateService(evaluator).ResolveAsync();

        Assert.Equal(LicenseRuntimeState.Unlicensed, snapshot.State);
        Assert.Null(snapshot.ValidatedPayload);
        Assert.Equal("license_missing", snapshot.ErrorCode);
    }

    [Fact]
    public async Task ValidationFailuresAreInvalidFailClosed()
    {
        var evaluator = new FakeEvaluator(CustomerLicenseRuntimeResult.Rejected("offline_revocation_unavailable"));

        var snapshot = await CreateService(evaluator).ResolveAsync();

        Assert.Equal(LicenseRuntimeState.Invalid, snapshot.State);
        Assert.Null(snapshot.ValidatedPayload);
        Assert.Equal("offline_revocation_unavailable", snapshot.ErrorCode);
    }

    [Fact]
    public async Task MissingInstallationIdentityIsUnlicensedWithoutCallingTheEvaluator()
    {
        var evaluator = new FakeEvaluator(CustomerLicenseRuntimeResult.Rejected("license_missing"));

        var snapshot = await CreateService(evaluator, installationId: "").ResolveAsync();

        Assert.Equal(LicenseRuntimeState.Unlicensed, snapshot.State);
        Assert.Null(evaluator.LastInstallationId);
    }

    [Fact]
    public async Task DeploymentModeIsNormalisedIntoTheSnapshot()
    {
        var evaluator = new FakeEvaluator(CustomerLicenseRuntimeResult.Rejected("license_missing"));

        var snapshot = await CreateService(evaluator, deploymentMode: "cloud").ResolveAsync();

        Assert.Equal(LicenseDeploymentModes.Cloud, snapshot.DeploymentMode);
    }
}
