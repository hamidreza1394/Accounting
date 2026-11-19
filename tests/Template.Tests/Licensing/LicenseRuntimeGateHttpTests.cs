using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// HTTP-level checks for the license-first runtime gate (Issue #396). The gate runs against the
/// real middleware pipeline; without a license the installation is Unlicensed, so normal
/// operations must fail closed while activation/status paths stay reachable.
/// </summary>
public sealed class LicenseRuntimeGateHttpTests : IClassFixture<GateEnabledFaranouApplicationFactory>
{
    private readonly GateEnabledFaranouApplicationFactory factory;

    public LicenseRuntimeGateHttpTests(GateEnabledFaranouApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task WithoutLicense_StatusEndpointReportsUnlicensedAndStaysReachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/licensing/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl.NoStore);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var state = document.RootElement.GetProperty("state").GetString();
        var deploymentMode = document.RootElement.GetProperty("deploymentMode").GetString();
        Assert.Equal("unlicensed", state);
        Assert.Equal("dedicated", deploymentMode);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("license").ValueKind);
    }

    [Fact]
    public async Task WithoutLicense_NormalApiRequestsFailClosed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("license_required", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutLicense_NormalLoginPageIsClosed()
    {
        var client = factory.CreateBrowser();

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("license_required", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProvisioningEnrollmentAntiforgeryStaysReachableWhileUnlicensed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/provisioning/enrollment/antiforgery");

        // The endpoint itself stays reachable in the Unlicensed state; its own auth/antiforgery
        // rules are independent of the gate.
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

public sealed class GateDisabledLicenseRuntimeGateHttpTests : IClassFixture<GateDisabledFaranouApplicationFactory>
{
    private readonly GateDisabledFaranouApplicationFactory factory;

    public GateDisabledLicenseRuntimeGateHttpTests(GateDisabledFaranouApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task DisabledGate_PreservesExistingBehaviour()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/session");

        // Authentication answers normally; the gate adds no license interference.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var statusResponse = await client.GetAsync("/api/v1/licensing/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }
}

/// <summary>Application factory with the license runtime gate enabled; no license installed.</summary>
public sealed class GateEnabledFaranouApplicationFactory : FaranouApplicationFactory
{
    protected override IDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Licensing:RuntimeGate:Enabled"] = "true",
    };
}

/// <summary>Application factory keeping the gate disabled (default rollout state).</summary>
public sealed class GateDisabledFaranouApplicationFactory : FaranouApplicationFactory
{
    protected override IDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Licensing:RuntimeGate:Enabled"] = "false",
    };
}
