using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorProvisioningLicenseResolverSqlServerTests
{
    [Fact]
    public async Task UsesSignedLatestEntitlementsAndAllowsOnlyASubset()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance", "sales"]);
            var resolver = CreateResolver(fixture.Db, key);

            var all = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.True(all.Succeeded, all.ErrorCode);
            Assert.Equal(1, all.Sequence);
            Assert.Equal(new[] { "finance", "sales" }, all.Subsystems);

            var subset = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, ["sales"], TestContext.Current.CancellationToken);
            Assert.True(subset.Succeeded, subset.ErrorCode);
            Assert.Equal(new[] { "sales" }, subset.Subsystems);

            var unpurchased = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, ["inventory"], TestContext.Current.CancellationToken);
            Assert.False(unpurchased.Succeeded);
            Assert.Equal("provisioning_subsystem_not_entitled", unpurchased.ErrorCode);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task LatestResolutionRejectsStaleSequenceButHistoricalResolutionCanVerifyAnExistingBinding()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance", "sales"]);
            await AddEnvelopeAsync(fixture.Db, seed.License, seed.Installation, key, 2, ["finance"]);
            seed.License.LastIssuedSequence = 2;
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var resolver = CreateResolver(fixture.Db, key);

            var latest = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.True(latest.Succeeded, latest.ErrorCode);
            Assert.Equal(2, latest.Sequence);
            Assert.Equal(new[] { "finance" }, latest.Subsystems);

            var historical = await resolver.ResolveSequenceAsync(
                seed.License.Id, 1, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.True(historical.Succeeded, historical.ErrorCode);
            Assert.Equal(1, historical.Sequence);
            Assert.Equal(new[] { "finance", "sales" }, historical.Subsystems);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task ProvisioningIssuanceRejectsLicenseSequenceChangedAfterRequestCreation()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance", "sales"]);
            var now = DateTimeOffset.UtcNow;
            var requestId = Guid.NewGuid();
            const string nonce = "0123456789abcdefghijklmnopqrstuvwxyz";
            var payload = new CustomerProvisioningPayload(
                4,
                requestId.ToString("D"),
                seed.Customer.CustomerNumber,
                seed.Installation.InstallationCode,
                "Provisioning sequence test",
                "independent",
                "initial-admin",
                ["finance", "sales"],
                now,
                now.AddHours(24),
                nonce,
                "سال مالی ۱۴۰۵",
                new DateOnly(2026, 3, 21),
                new DateOnly(2027, 3, 20),
                null,
                "admin@example.com",
                seed.License.Id.ToString("D"),
                1);
            var payloadJson = CustomerProvisioningCanonicalJson.Serialize(payload);
            var provisioningRequest = new VendorProvisioningRequest
            {
                Id = Guid.NewGuid(),
                CustomerId = seed.Customer.Id,
                InstallationId = seed.Installation.Id,
                RequestId = requestId.ToString("D"),
                Nonce = nonce,
                PayloadJson = payloadJson,
                PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant(),
                Status = "AwaitingSigner",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            fixture.Db.ProvisioningRequests.Add(provisioningRequest);
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var approval = new VendorApprovalRequest
            {
                Id = Guid.NewGuid(),
                ResourceType = "provisioning",
                ResourceId = provisioningRequest.Id,
                TargetStatus = "PendingDelivery",
                ExpectedResourceRowVersion = provisioningRequest.RowVersion.ToArray(),
                RequestedByUserId = Guid.NewGuid(),
                RequestedAtUtc = now,
                Status = "Pending"
            };
            fixture.Db.ApprovalRequests.Add(approval);
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // A newer signed revision with the same entitlements must still invalidate the old sequence.
            await AddEnvelopeAsync(fixture.Db, seed.License, seed.Installation, key, 2, ["finance", "sales"]);
            seed.License.LastIssuedSequence = 2;
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var resolver = CreateResolver(fixture.Db, key);
            var service = new VendorProvisioningIssuanceService(
                fixture.Db,
                new UnavailableProvisioningSigningProvider(),
                new ProvisioningPackageSigningTrustStore([]),
                new VendorAuditWriter(fixture.Db, TimeProvider.System),
                resolver,
                TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<VendorProvisioningIssuanceService>.Instance);

            await using var transaction = await fixture.Db.Database.BeginTransactionAsync(
                TestContext.Current.CancellationToken);
            var result = await service.IssueApprovedAsync(
                approval,
                Guid.NewGuid(),
                approval.RowVersion.ToArray(),
                "sequence revalidation",
                Guid.NewGuid().ToString("N"),
                TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.Equal("provisioning_license_sequence_stale", result.ErrorCode);
            Assert.Equal("AwaitingSigner", provisioningRequest.Status);
            Assert.Equal("Pending", approval.Status);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task RejectsLicenseForAnotherCustomerOrInstallation()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance"]);
            var otherCustomer = new VendorCustomer
            {
                Id = Guid.NewGuid(),
                CustomerNumber = "C-" + Guid.NewGuid().ToString("N")[..12],
                LegalName = "Other customer",
                DisplayName = "Other customer",
                Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            var otherInstallation = new VendorInstallation
            {
                Id = Guid.NewGuid(),
                CustomerId = seed.Customer.Id,
                InstallationCode = "I-" + Guid.NewGuid().ToString("N")[..12],
                Name = "Other installation",
                Environment = "Production",
                Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            fixture.Db.AddRange(otherCustomer, otherInstallation);
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var resolver = CreateResolver(fixture.Db, key);
            var wrongCustomer = await resolver.ResolveLatestAsync(
                seed.License.Id, otherCustomer, otherInstallation, null, TestContext.Current.CancellationToken);
            Assert.False(wrongCustomer.Succeeded);
            Assert.Equal(StatusCodes.Status404NotFound, wrongCustomer.StatusCode);

            var wrongInstallation = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, otherInstallation, null, TestContext.Current.CancellationToken);
            Assert.False(wrongInstallation.Succeeded);
            Assert.Equal(StatusCodes.Status404NotFound, wrongInstallation.StatusCode);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task RejectsSuspendedAndOutOfValidityLicenses()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance"]);
            var resolver = CreateResolver(fixture.Db, key);

            seed.License.Status = "Suspended";
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var suspended = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.False(suspended.Succeeded);
            Assert.Equal("provisioning_license_inactive", suspended.ErrorCode);

            seed.License.Status = "Issued";
            seed.License.LicenseKind = LicensePayload.KindTerm;
            seed.License.ValidFromUtc = DateTimeOffset.UtcNow.AddDays(-5);
            seed.License.ValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            seed.License.LastIssuedSequence = 2;
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await AddEnvelopeAsync(fixture.Db, seed.License, seed.Installation, key, 2, ["finance"]);
            var expired = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.False(expired.Succeeded);
            Assert.Equal("provisioning_license_outside_validity", expired.ErrorCode);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task RejectsStoredEnvelopeDigestMismatch()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance"]);
            var stored = await fixture.Db.SignedLicenseEnvelopes
                .SingleAsync(item => item.LicenseId == seed.License.Id, TestContext.Current.CancellationToken);
            stored.PayloadSha256 = new string('0', 64);
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await CreateResolver(fixture.Db, key).ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal("provisioning_license_envelope_invalid", result.ErrorCode);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task RejectsEnvelopeWithInvalidSignatureEvenWhenPayloadDigestMatches()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance"]);
            var stored = await fixture.Db.SignedLicenseEnvelopes
                .SingleAsync(item => item.LicenseId == seed.License.Id, TestContext.Current.CancellationToken);
            var envelope = System.Text.Json.JsonSerializer.Deserialize<LicenseEnvelope>(stored.EnvelopeJson)!;
            stored.EnvelopeJson = System.Text.Json.JsonSerializer.Serialize(
                envelope with { SignatureBase64 = Convert.ToBase64String(new byte[64]) });
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await CreateResolver(fixture.Db, key).ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal("provisioning_license_signature_or_payload_invalid", result.ErrorCode);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task RevokedOrUnknownSigningKeyFailsClosed()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedAsync(fixture.Db, key, 1, ["finance"]);
            var trustStore = CreateTrustStore(key, revoked: true);
            var resolver = new VendorProvisioningLicenseResolver(fixture.Db, trustStore, TimeProvider.System);

            var result = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            Assert.Equal("provisioning_license_signing_key_untrusted", result.ErrorCode);
        }
        finally
        {
            await CleanupAsync(fixture);
        }
    }

    private static VendorProvisioningLicenseResolver CreateResolver(VendorDbContext db, ECDsa key) =>
        new(db, CreateTrustStore(key), TimeProvider.System);

    private static LicenseSigningTrustStore CreateTrustStore(ECDsa key, bool revoked = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:TrustedSigningKeys:license-test-key:PublicKeySubjectPublicKeyInfoBase64"] =
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["Licensing:TrustedSigningKeys:license-test-key:Revoked"] = revoked.ToString()
            })
            .Build();
        return new LicenseSigningTrustStore(configuration);
    }

    private static async Task<SeededLicense> SeedAsync(
        VendorDbContext db, ECDsa key, int sequence, string[] subsystems)
    {
        var now = DateTimeOffset.UtcNow;
        var customer = new VendorCustomer
        {
            Id = Guid.NewGuid(),
            CustomerNumber = "C-" + Guid.NewGuid().ToString("N")[..12],
            LegalName = "Provisioning license test customer",
            DisplayName = "Provisioning license test",
            Status = "Active",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var installation = new VendorInstallation
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            InstallationCode = "I-" + Guid.NewGuid().ToString("N")[..12],
            Name = "Provisioning test installation",
            Environment = "Production",
            Status = "Active",
            CreatedAtUtc = now
        };
        var license = new VendorLicense
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            InstallationId = installation.Id,
            Installation = installation,
            LicenseNumber = "L-" + Guid.NewGuid().ToString("N")[..12],
            ProductCode = "FARANOU",
            Status = "Issued",
            LicenseKind = LicensePayload.KindPerpetual,
            MaxCompanies = 10,
            SubsystemsJson = "[\"finance\",\"sales\"]",
            GraceDays = 30,
            LastIssuedSequence = sequence,
            ValidFromUtc = now.AddMinutes(-1),
            CreatedAtUtc = now
        };

        db.AddRange(customer, installation, license);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AddEnvelopeAsync(db, license, installation, key, sequence, subsystems);
        return new SeededLicense(customer, installation, license);
    }

    private static async Task AddEnvelopeAsync(
        VendorDbContext db,
        VendorLicense license,
        VendorInstallation installation,
        ECDsa key,
        int sequence,
        string[] subsystems)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new LicensePayload(
            1,
            license.Id.ToString("D"),
            installation.Id.ToString("D"),
            license.ProductCode,
            license.LicenseKind,
            now,
            license.ValidFromUtc,
            license.ValidUntilUtc,
            license.GraceDays,
            sequence,
            new LicenseLimits(license.MaxCompanies, subsystems));
        var payloadJson = CanonicalJson.SerializePayload(payload);
        var signature = key.SignData(
            Encoding.UTF8.GetBytes(payloadJson),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var envelope = new LicenseEnvelope(
            1, "license-test-key", LicenseAlgorithms.EcdsaP256Sha256,
            payloadJson, Convert.ToBase64String(signature));
        db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
        {
            Id = Guid.NewGuid(),
            LicenseId = license.Id,
            Sequence = sequence,
            KeyId = envelope.Kid,
            Algorithm = envelope.Algorithm,
            EnvelopeJson = System.Text.Json.JsonSerializer.Serialize(envelope),
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant(),
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Fixture?> OpenAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            const string message = "A disposable SQL Server is required for Vendor provisioning license resolver tests.";
            if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_VENDOR_SQLSERVER") == "1") Assert.Fail(message);
            Assert.Skip(message);
            return null;
        }

        var connection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        }.ConnectionString;
        var provider = VendorTestServiceProviderFactory.Create(connection);
        try
        {
            var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            return new Fixture(provider, scope, db);
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(Fixture fixture)
    {
        await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        fixture.Scope.Dispose();
        await fixture.Provider.DisposeAsync();
    }

    private sealed record SeededLicense(
        VendorCustomer Customer, VendorInstallation Installation, VendorLicense License);

    private sealed record Fixture(
        Microsoft.Extensions.DependencyInjection.ServiceProvider Provider,
        Microsoft.Extensions.DependencyInjection.IServiceScope Scope,
        VendorDbContext Db);
}
