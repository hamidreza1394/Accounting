using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Protects the composition-root route registration: an unmapped /api path falls through to
/// the SPA fallback as 404, while the mapped readiness endpoint must challenge anonymously.
/// No database access is needed because authorization stops the request before the handler.
/// </summary>
public sealed class VendorLifecycleReadinessRouteTests
{
    [Fact]
    public async Task ReadinessRouteIsMappedAndRequiresAuthentication()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHost => webHost.ConfigureTestServices(services =>
                services.RemoveAll<IHostedService>()));

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(
            $"/api/vendor/customers/{Guid.NewGuid():D}/readiness",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
