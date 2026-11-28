using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// The development database setup surface (Issue #797) must never be reachable unless it is
/// explicitly enabled, running on Windows, and called from a loopback address.
/// </summary>
public sealed class VendorDevelopmentDatabaseSetupExposureTests
{
    private static readonly string[] SetupApiPaths =
    [
        "/api/development/database-setup/status",
        "/api/development/database-setup/antiforgery"
    ];

    private static readonly string[] SetupAssetPaths =
    [
        "/development/database-setup.css",
        "/development/database-setup.js",
        "/development/database-setup-state.mjs"
    ];

    [Fact]
    public async Task SetupApiIsNotMappedWithoutTheExplicitDevelopmentFlag()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        foreach (var path in SetupApiPaths)
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        AssertAssetsAreNeverServedAnonymously(client);

        using var post = await client.PostAsync(
            "/api/development/database-setup/save",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task SetupSurfaceRejectsNonLoopbackOrNonWindowsCallersEvenWhenEnabled()
    {
        // TestServer requests carry no loopback RemoteIpAddress. On Windows this exercises the
        // loopback guard; elsewhere the Windows-only gate keeps the routes unmapped.
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("DevelopmentSetup:Enabled", "true"));
        using var client = factory.CreateClient();

        foreach (var path in SetupApiPaths)
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        AssertAssetsAreNeverServedAnonymously(client);

        using var setupPage = await client.GetAsync("/development/database-setup", TestContext.Current.CancellationToken);
        var body = await setupPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("connection-form", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The embedded setup assets are file paths. While the setup surface is unmapped (flag off
    /// or a non-Windows host) they match no endpoint — <c>MapFallback</c> only covers non-file
    /// paths — so the global fallback authorization policy rejects them with a 401 challenge.
    /// Either rejection is fail-closed: the asset is never served to an anonymous caller.
    /// </summary>
    private static void AssertAssetsAreNeverServedAnonymously(HttpClient client)
    {
        foreach (var path in SetupAssetPaths)
        {
            using var response = client.GetAsync(path, TestContext.Current.CancellationToken)
                .GetAwaiter().GetResult();
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
                $"Expected a fail-closed rejection for {path} but got {(int)response.StatusCode}.");
        }
    }
}
