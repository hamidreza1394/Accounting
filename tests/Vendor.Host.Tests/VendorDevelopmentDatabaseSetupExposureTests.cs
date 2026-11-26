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

        using var setupPage = await client.GetAsync("/development/database-setup", TestContext.Current.CancellationToken);
        var body = await setupPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("connection-form", body, StringComparison.Ordinal);
    }
}
