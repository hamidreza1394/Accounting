using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// HTTP-level regression coverage for the Vendor business mutation boundary.
/// These tests deliberately use the production host and its authorization pipeline.
/// </summary>
public sealed class VendorBusinessMutationAuthorizationIntegrationTests
{
    [Fact]
    public async Task AnonymousCallersCannotReachAnyBusinessMutationEndpoint()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        var id = Guid.NewGuid();
        var body = new
        {
            rowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            customerNumber = "anonymous-test",
            legalName = "Anonymous Test",
            displayName = "Anonymous Test",
            installationCode = "anonymous-test",
            name = "Anonymous Test",
            environment = "Development",
            status = "Suspended",
            contractNumber = "anonymous-test",
            currencyCode = "EUR",
            licenseNumber = "anonymous-test",
            productCode = "FARANOU"
        };

        var requests = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Put, $"/api/vendor/customers/{id:D}"),
            (HttpMethod.Post, $"/api/vendor/customers/{id:D}/status"),
            (HttpMethod.Put, $"/api/vendor/installations/{id:D}"),
            (HttpMethod.Post, $"/api/vendor/installations/{id:D}/status"),
            (HttpMethod.Put, $"/api/vendor/contracts/{id:D}"),
            (HttpMethod.Put, $"/api/vendor/licenses/{id:D}"),
            (HttpMethod.Post, $"/api/vendor/licenses/{id:D}/approval-request"),
            (HttpMethod.Post, "/api/vendor/approvals/status-change"),
            (HttpMethod.Post, $"/api/vendor/approvals/{id:D}/decision")
        };

        foreach (var (method, path) in requests)
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(body)
            };

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task AnonymousCallerCannotReadVendorBusinessCollections()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        foreach (var path in new[]
        {
            "/api/vendor/customers/",
            "/api/vendor/installations/",
            "/api/vendor/contracts/",
            "/api/vendor/licenses/",
            "/api/vendor/approvals/"
        })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
