using System.Net;
using System.Net.Http.Json;
using Faranou.Licensing.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Database-free endpoint guards for the amendment acknowledgement intake: an untrusted key is
/// rejected before any database access and a malformed envelope is rejected before trust.
/// Full signed cycles are covered by the SQL Server integration tests.
/// </summary>
public sealed class VendorLicenseAmendmentAcknowledgementEndpointTests
{
    [Fact]
    public async Task UnknownAcknowledgementKeyIsRejectedWithoutDatabaseAccess()
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
            "/api/vendor/license-amendment-requests/9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8/acknowledgement",
            envelope,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedAcknowledgementEnvelopeIsRejectedBeforeTrust()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // Acknowledgement JSON beyond the canonical validator bound never reaches the database.
        using var oversized = await client.PostAsJsonAsync(
            "/api/vendor/license-amendment-requests/9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8/acknowledgement",
            new CustomerProvisioningAcknowledgementEnvelope(
                1, "some-key", "ES256", new string('x', CustomerProvisioningAcknowledgementValidator.MaxAcknowledgementJsonChars + 1),
                Convert.ToBase64String(new byte[64])),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        using var missingBody = await client.PostAsJsonAsync(
            "/api/vendor/license-amendment-requests/9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8/acknowledgement",
            new { formatVersion = 1, keyId = "some-key", algorithm = "ES256", signatureBase64 = "" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingBody.StatusCode);
    }
}
