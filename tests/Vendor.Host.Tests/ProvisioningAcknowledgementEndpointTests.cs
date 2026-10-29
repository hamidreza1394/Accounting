using System.Net;
using System.Net.Http.Json;
using Faranou.Licensing.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class ProvisioningAcknowledgementEndpointTests
{
    [Fact]
    public async Task UnknownAcknowledgementKeyIsRejectedWithoutCustomerOrVendorStateMutation()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var envelope = new CustomerProvisioningAcknowledgementEnvelope(
            1,
            "unconfigured-customer-key",
            "ES256",
            "{}",
            Convert.ToBase64String(new byte[64]));

        using var response = await client.PostAsJsonAsync(
            "/api/vendor/provisioning-requests/9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8/acknowledgement",
            envelope,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
