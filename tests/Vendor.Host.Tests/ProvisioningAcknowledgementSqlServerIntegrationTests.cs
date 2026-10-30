using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class ProvisioningAcknowledgementSqlServerIntegrationTests
{
    [Fact]
    public async Task SignedAcknowledgementConfirmsOnceAndRejectsConflictingRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to run the acknowledgement SQL Server integration test.";
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail(message);
            Assert.Skip(message);
            return;
        }

        var databaseName = "FaranouVendorAck_" + Guid.NewGuid().ToString("N");
        var adminConnectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" }.ConnectionString;
        var testConnectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trustedKey = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo());
        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var nonce = "nonce-" + Guid.NewGuid().ToString("N");
        const string customerReference = "CUSTOMER-ACK-TEST";
        const string installationCode = "INSTALL-ACK-TEST";
        var payloadHash = new string('a', 64);

        await using (var adminConnection = new SqlConnection(adminConnectionString))
        {
            await adminConnection.OpenAsync(cancellationToken);
            await using var createDatabase = adminConnection.CreateCommand();
            createDatabase.CommandText = $"CREATE DATABASE [{databaseName}]";
            await createDatabase.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
            {
                webHost.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:VendorDb"] = testConnectionString,
                        ["ProvisioningAcknowledgementTrust:Keys:0:KeyId"] = "customer-ack-key-v1",
                        ["ProvisioningAcknowledgementTrust:Keys:0:PublicKeySubjectPublicKeyInfoBase64"] = trustedKey,
                        ["ProvisioningAcknowledgementTrust:Keys:0:Revoked"] = "false"
                    }));
            });

            await using (var setupScope = factory.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                db.Customers.Add(new VendorCustomer
                {
                    Id = customerId,
                    CustomerNumber = customerReference,
                    LegalName = "Acknowledgement Integration Test",
                    DisplayName = "Acknowledgement Integration Test",
                    Status = "Active",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                db.Installations.Add(new VendorInstallation
                {
                    Id = installationId,
                    CustomerId = customerId,
                    InstallationCode = installationCode,
                    Name = "Integration Test Installation",
                    Environment = "Test",
                    Status = "Active",
                    CreatedAtUtc = now
                });
                db.ProvisioningRequests.Add(new VendorProvisioningRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    InstallationId = installationId,
                    RequestId = requestId.ToString("D"),
                    Nonce = nonce,
                    PayloadJson = "{}",
                    PayloadSha256 = payloadHash,
                    Status = "PendingDelivery",
                    CreatedAtUtc = now.AddMinutes(-2),
                    UpdatedAtUtc = now.AddMinutes(-1),
                    DeliveredAtUtc = now.AddMinutes(-1),
                    KeyId = "vendor-test-key",
                    Algorithm = LicenseAlgorithms.EcdsaP256Sha256,
                    SignatureBase64 = Convert.ToBase64String(new byte[64])
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            using var client = factory.CreateClient();

            // A trusted signer must not be able to persist free-form diagnostic text or PII
            // in the Vendor failureCode column. The package remains pending and retryable.
            var unapprovedErrorEnvelope = CreateEnvelope(
                signingKey, requestId, nonce, payloadHash, customerReference, installationCode,
                "Rejected", now, "SqlException: database unavailable for admin@example.com");
            using (var rejectedResponse = await client.PostAsJsonAsync(
                $"/api/vendor/provisioning-requests/{requestId:D}/acknowledgement",
                unapprovedErrorEnvelope,
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.BadRequest, rejectedResponse.StatusCode);
            }

            await using (var unchangedScope = factory.Services.CreateAsyncScope())
            {
                var db = unchangedScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var unchanged = await db.ProvisioningRequests.SingleAsync(
                    item => item.RequestId == requestId.ToString("D"), cancellationToken);
                Assert.Equal("PendingDelivery", unchanged.Status);
                Assert.Null(unchanged.AcknowledgementSha256);
                Assert.Null(unchanged.AcknowledgementResult);
                Assert.Null(unchanged.FailureCode);
                Assert.Empty(await db.AuditEvents.ToListAsync(cancellationToken));
            }

            var appliedEnvelope = CreateEnvelope(
                signingKey, requestId, nonce, payloadHash, customerReference, installationCode, "Applied", now);
            using var firstResponse = await client.PostAsJsonAsync(
                $"/api/vendor/provisioning-requests/{requestId:D}/acknowledgement",
                appliedEnvelope,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            await using (var verifyScope = factory.Services.CreateAsyncScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var stored = await db.ProvisioningRequests.SingleAsync(
                    item => item.RequestId == requestId.ToString("D"), cancellationToken);
                Assert.Equal("Confirmed", stored.Status);
                Assert.Equal("Applied", stored.AcknowledgementResult);
                Assert.Equal("customer-ack-key-v1", stored.AcknowledgementKeyId);
                Assert.NotNull(stored.AcknowledgementSha256);
                Assert.NotNull(stored.AcknowledgedAtUtc);
                Assert.NotNull(stored.ConfirmedAtUtc);
                Assert.Null(stored.FailureCode);
                Assert.Contains(await db.AuditEvents.ToListAsync(cancellationToken),
                    auditEvent => auditEvent.ActionCode == "vendor.provisioning.acknowledgement-confirmed");
            }

            using var repeatedResponse = await client.PostAsJsonAsync(
                $"/api/vendor/provisioning-requests/{requestId:D}/acknowledgement",
                appliedEnvelope,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
            var repeatedBody = await repeatedResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(cancellationToken);
            Assert.True(repeatedBody is not null && repeatedBody.TryGetValue("repeated", out var repeated) && repeated.GetBoolean());

            var conflictingEnvelope = CreateEnvelope(
                signingKey, requestId, nonce, payloadHash, customerReference, installationCode, "Rejected", now);
            using var conflictResponse = await client.PostAsJsonAsync(
                $"/api/vendor/provisioning-requests/{requestId:D}/acknowledgement",
                conflictingEnvelope,
                cancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            await using (var finalScope = factory.Services.CreateAsyncScope())
            {
                var db = finalScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var stored = await db.ProvisioningRequests.SingleAsync(
                    item => item.RequestId == requestId.ToString("D"), cancellationToken);
                Assert.Equal("Confirmed", stored.Status);
                Assert.Equal("Applied", stored.AcknowledgementResult);
                Assert.Equal(2, await db.AuditEvents.CountAsync(
                    auditEvent => auditEvent.ActionCode == "vendor.provisioning.acknowledgement-confirmed" || auditEvent.ActionCode == "vendor.provisioning.acknowledgement-retried",
                    cancellationToken));
            }
        }
        finally
        {
            await using var adminConnection = new SqlConnection(adminConnectionString);
            await adminConnection.OpenAsync(cancellationToken);
            await using var dropDatabase = adminConnection.CreateCommand();
            dropDatabase.CommandText = $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END
                """;
            await dropDatabase.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static CustomerProvisioningAcknowledgementEnvelope CreateEnvelope(
        ECDsa signingKey,
        Guid requestId,
        string nonce,
        string payloadHash,
        string customerReference,
        string installationCode,
        string result,
        DateTimeOffset processedAtUtc,
        string? errorCode = null)
    {
        var acknowledgement = new CustomerProvisioningAcknowledgement(
            CustomerProvisioningAcknowledgementValidator.CurrentSchemaVersion,
            requestId.ToString("D"),
            nonce,
            payloadHash,
            customerReference,
            installationCode,
            result,
            processedAtUtc,
            result == "Rejected" ? errorCode ?? "provisioning_parent_reference_invalid" : null);
        var acknowledgementJson = CustomerProvisioningAcknowledgementValidator.Serialize(acknowledgement);
        var signature = signingKey.SignData(
            Encoding.UTF8.GetBytes(acknowledgementJson),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new CustomerProvisioningAcknowledgementEnvelope(
            CustomerProvisioningAcknowledgementValidator.CurrentSchemaVersion,
            "customer-ack-key-v1",
            LicenseAlgorithms.EcdsaP256Sha256,
            acknowledgementJson,
            Convert.ToBase64String(signature));
    }
}
