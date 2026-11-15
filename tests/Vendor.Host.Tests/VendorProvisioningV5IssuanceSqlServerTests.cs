using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

using Fixture = Faranou.Vendor.Host.Tests.VendorProvisioningLicenseResolverSqlServerTests.Fixture;
using Helpers = Faranou.Vendor.Host.Tests.VendorProvisioningLicenseResolverSqlServerTests;
using SeededLicense = Faranou.Vendor.Host.Tests.VendorProvisioningLicenseResolverSqlServerTests.SeededLicense;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Vendor-side readiness for company-bound provisioning schema v5. The CompanyReference must come
/// only from the verified, signed LicensePayload v2; the Gate is disabled by default and the Customer
/// still rejects v5 (so nothing issued here is operational yet).
/// </summary>
public sealed class VendorProvisioningV5IssuanceSqlServerTests
{
    private const string RefA = "company-ref-0001";
    private const string RefB = "company-ref-0002";
    private const string RefC = "company-ref-0003";
    private const string ProvisioningKeyId = "v5-provisioning-test-key";
    private static readonly VendorProvisioningCompanyReferenceGate On = new(enabled: true);

    [Fact]
    public async Task GateOnBindsTheSelectedReferenceFromTheSignedEnvelopeNotFromLicenseColumns()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, key, Entry(RefA, "finance"), Entry(RefB, "sales", "treasury"));
            // The installation-wide legacy column must never turn into company grants. (MaxCompanies cannot
            // diverge from the signed value: the resolver already rejects that as a binding mismatch.)
            seed.License.SubsystemsJson = "[\"finance\",\"sales\",\"inventory\",\"treasury\"]";
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var resolver = Helpers.CreateResolver(fixture.Db, key, On);
            var binding = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, RefB, TestContext.Current.CancellationToken);

            Assert.True(binding.Succeeded, binding.ErrorCode);
            Assert.Equal(RefB, binding.CompanyReference);
            Assert.Equal(new[] { "sales", "treasury" }, binding.Subsystems);
            Assert.Equal(2, binding.Sequence);

            var unrelated = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, RefC, TestContext.Current.CancellationToken);
            Assert.False(unrelated.Succeeded);
            Assert.Equal("provisioning_company_reference_not_entitled", unrelated.ErrorCode);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOffCompanyScopedLicenseStillFailsClosedEvenWhenASelectorIsSupplied()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, key, Entry(RefA, "finance"));

            foreach (var gate in new VendorProvisioningCompanyReferenceGate?[]
                         { null, VendorProvisioningCompanyReferenceGate.Disabled, new(enabled: false) })
            {
                var resolver = Helpers.CreateResolver(fixture.Db, key, gate);
                var result = await resolver.ResolveLatestAsync(
                    seed.License.Id, seed.Customer, seed.Installation, null, RefA,
                    TestContext.Current.CancellationToken);

                Assert.False(result.Succeeded);
                Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
                Assert.Equal("provisioning_company_reference_mapping_required", result.ErrorCode);
                Assert.Null(result.CompanyReference);
                Assert.Null(result.Subsystems);
            }
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnRequiresAnExplicitSelectorAndNeverInfersOneEvenForASingleCompany()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, key, Entry(RefA, "finance"));

            var result = await Helpers.CreateResolver(fixture.Db, key, On).ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Equal("provisioning_company_reference_required", result.ErrorCode);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnLegacyV1LicenseKeepsTheV4PathAndRejectsASelector()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await Helpers.SeedAsync(fixture.Db, key, 1, ["finance", "sales"]);
            var resolver = Helpers.CreateResolver(fixture.Db, key, On);

            var legacy = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, TestContext.Current.CancellationToken);
            Assert.True(legacy.Succeeded, legacy.ErrorCode);
            Assert.Null(legacy.CompanyReference);
            Assert.Equal(new[] { "finance", "sales" }, legacy.Subsystems);

            var withSelector = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, RefA, TestContext.Current.CancellationToken);
            Assert.False(withSelector.Succeeded);
            Assert.Equal("provisioning_company_reference_not_applicable", withSelector.ErrorCode);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnReferenceSetSurvivesReissueAndIsIndependentOfEntitlementOrder()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, key, Entry(RefA, "finance"), Entry(RefB, "sales"));
            // Sequence 3: same references, reversed array order, modules in a different order.
            await Helpers.AddCompanyScopedEnvelopeAsync(
                fixture.Db, seed.License, seed.Installation, key, 3,
                [Entry(RefB, "sales"), Entry(RefA, "finance")]);
            seed.License.LastIssuedSequence = 3;
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var resolver = Helpers.CreateResolver(fixture.Db, key, On);
            var latest = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, RefA, TestContext.Current.CancellationToken);
            Assert.True(latest.Succeeded, latest.ErrorCode);
            Assert.Equal(3, latest.Sequence);
            Assert.Equal(RefA, latest.CompanyReference);
            Assert.Equal(new[] { "finance" }, latest.Subsystems);

            var historical = await resolver.ResolveSequenceAsync(
                seed.License.Id, 2, seed.Customer, seed.Installation, null, RefA, TestContext.Current.CancellationToken);
            Assert.True(historical.Succeeded, historical.ErrorCode);
            Assert.Equal(latest.CompanyReference, historical.CompanyReference);
            Assert.Equal(latest.Subsystems, historical.Subsystems);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnALicenseOfAnotherInstallationOrCustomerCannotBeUsed()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, key, Entry(RefA, "finance"));
            var otherInstallation = new VendorInstallation
            {
                Id = Guid.NewGuid(),
                CustomerId = seed.Customer.Id,
                Customer = seed.Customer,
                InstallationCode = "I-" + Guid.NewGuid().ToString("N")[..12],
                Name = "Another installation",
                Environment = "Production",
                Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            fixture.Db.Installations.Add(otherInstallation);
            var otherCustomer = new VendorCustomer
            {
                Id = Guid.NewGuid(),
                CustomerNumber = "C-" + Guid.NewGuid().ToString("N")[..12],
                LegalName = "Another customer",
                DisplayName = "Another customer",
                Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            fixture.Db.Customers.Add(otherCustomer);
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var resolver = Helpers.CreateResolver(fixture.Db, key, On);
            var wrongInstallation = await resolver.ResolveLatestAsync(
                seed.License.Id, seed.Customer, otherInstallation, null, RefA, TestContext.Current.CancellationToken);
            Assert.False(wrongInstallation.Succeeded);
            Assert.Equal(StatusCodes.Status404NotFound, wrongInstallation.StatusCode);
            Assert.Null(wrongInstallation.CompanyReference);

            var wrongCustomer = await resolver.ResolveLatestAsync(
                seed.License.Id, otherCustomer, seed.Installation, null, RefA, TestContext.Current.CancellationToken);
            Assert.False(wrongCustomer.Succeeded);
            Assert.Equal(StatusCodes.Status404NotFound, wrongCustomer.StatusCode);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnAnEnvelopeSignedByAnUntrustedKeyCannotSupplyAReference()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var trustedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await Helpers.SeedAsync(fixture.Db, trustedKey, 1, ["finance"]);
            // Signature produced by a key the Vendor does not trust, carrying an attacker-chosen reference.
            await Helpers.AddCompanyScopedEnvelopeAsync(
                fixture.Db, seed.License, seed.Installation, attackerKey, 2, [Entry("company-ref-evil", "finance")]);
            seed.License.LastIssuedSequence = 2;
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await Helpers.CreateResolver(fixture.Db, trustedKey, On).ResolveLatestAsync(
                seed.License.Id, seed.Customer, seed.Installation, null, "company-ref-evil",
                TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal("provisioning_license_signature_or_payload_invalid", result.ErrorCode);
            Assert.Null(result.CompanyReference);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnIssuesAV5PackageWhoseReferenceComesFromTheSignedLicenseAndTheCustomerStillRejectsIt()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provisioningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, licenseKey, Entry(RefA, "finance"), Entry(RefB, "sales"));
            var signer = new CountingSigner(provisioningKey);
            var scenario = await CreatePendingRequestAsync(fixture, licenseKey, seed, On, RefB);
            var service = CreateService(fixture, licenseKey, provisioningKey, signer, On);

            var result = await IssueAsync(fixture, service, scenario);

            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Equal(1, signer.Calls);
            Assert.Equal("PendingDelivery", scenario.Request.Status);

            var package = new CustomerProvisioningPackage(
                CustomerProvisioningPackageValidator.CurrentFormatVersion,
                scenario.Request.KeyId!, scenario.Request.Algorithm!,
                scenario.Request.PayloadJson, scenario.Request.SignatureBase64!);
            var publicKey = provisioningKey.ExportSubjectPublicKeyInfo();

            Assert.True(CustomerProvisioningPackageValidator.TryValidateAndVerify(
                package, publicKey, DateTimeOffset.UtcNow, out var payload, out var error,
                allowCompanyReference: true), error);
            Assert.Equal(5, payload!.SchemaVersion);
            Assert.Equal(RefB, payload.CompanyReference);
            Assert.Equal(new[] { "sales" }, payload.Subsystems);
            Assert.Equal(seed.License.Id.ToString("D"), payload.LicenseId);
            Assert.Equal(2, payload.Sequence);

            // The Customer consumer (default validator options) cannot verify v5 yet: nothing here is operational.
            Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
                package, publicKey, DateTimeOffset.UtcNow, out _, out var customerError));
            Assert.Equal("provisioning_payload_invalid", customerError);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOffAV5RequestIsNeverSigned()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provisioningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, licenseKey, Entry(RefA, "finance"));
            var scenario = await CreatePendingRequestAsync(fixture, licenseKey, seed, On, RefA);
            var signer = new CountingSigner(provisioningKey);
            // Same stored v5 request, but the gate is closed at signing time.
            var service = CreateService(fixture, licenseKey, provisioningKey, signer, gate: null);

            var result = await IssueAsync(fixture, service, scenario);

            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.Equal("provisioning_payload_invalid", result.ErrorCode);
            Assert.Equal(0, signer.Calls);
            Assert.Equal("AwaitingSigner", scenario.Request.Status);
            Assert.Null(scenario.Request.SignatureBase64);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnARequestWhoseReferenceIsNotInTheSignedSetIsNeverSigned()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provisioningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, licenseKey, Entry(RefA, "finance"));
            // A stored request (e.g. written directly or tampered) naming a reference the license never granted.
            var scenario = await CreatePendingRequestAsync(
                fixture, licenseKey, seed, On, RefA, payloadReferenceOverride: RefC);
            var signer = new CountingSigner(provisioningKey);
            var service = CreateService(fixture, licenseKey, provisioningKey, signer, On);

            var result = await IssueAsync(fixture, service, scenario);

            Assert.False(result.Succeeded);
            Assert.Equal("provisioning_company_reference_not_entitled", result.ErrorCode);
            Assert.Equal(0, signer.Calls);
            Assert.Equal("AwaitingSigner", scenario.Request.Status);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnALegacyV4RequestCannotBeSignedAgainstACompanyScopedLicense()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provisioningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, licenseKey, Entry(RefA, "finance"));
            // v4 request without any company reference (created before the license was reissued as v2).
            var scenario = await CreatePendingRequestAsync(
                fixture, licenseKey, seed, On, RefA, forceSchema4: true);
            var signer = new CountingSigner(provisioningKey);
            var service = CreateService(fixture, licenseKey, provisioningKey, signer, On);

            var result = await IssueAsync(fixture, service, scenario);

            Assert.False(result.Succeeded);
            Assert.Equal("provisioning_company_reference_required", result.ErrorCode);
            Assert.Equal(0, signer.Calls);
            Assert.Equal("AwaitingSigner", scenario.Request.Status);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    [Fact]
    public async Task GateOnAV5RequestIsStaleOnceTheLicenseSequenceMoves()
    {
        var fixture = await Helpers.OpenAsync();
        if (fixture is null) return;
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provisioningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var seed = await SeedCompanyScopedAsync(fixture, licenseKey, Entry(RefA, "finance"));
            var scenario = await CreatePendingRequestAsync(fixture, licenseKey, seed, On, RefA);
            await Helpers.AddCompanyScopedEnvelopeAsync(
                fixture.Db, seed.License, seed.Installation, licenseKey, 3, [Entry(RefA, "finance")]);
            seed.License.LastIssuedSequence = 3;
            await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var signer = new CountingSigner(provisioningKey);
            var service = CreateService(fixture, licenseKey, provisioningKey, signer, On);

            var result = await IssueAsync(fixture, service, scenario);

            Assert.False(result.Succeeded);
            Assert.Equal("provisioning_license_sequence_stale", result.ErrorCode);
            Assert.Equal(0, signer.Calls);
        }
        finally
        {
            await Helpers.CleanupAsync(fixture);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static CompanyLicenseEntitlement Entry(string reference, params string[] modules) =>
        new(reference, Array.AsReadOnly(modules), 5, Array.Empty<LicenseFeatureEntitlement?>());

    /// <summary>Sequence 1 legacy v1 envelope, then sequence 2 company-scoped v2 envelope as the latest.</summary>
    private static async Task<SeededLicense> SeedCompanyScopedAsync(
        Fixture fixture, ECDsa key, params CompanyLicenseEntitlement[] companies)
    {
        var seed = await Helpers.SeedAsync(fixture.Db, key, 1, ["finance"]);
        await Helpers.AddCompanyScopedEnvelopeAsync(
            fixture.Db, seed.License, seed.Installation, key, 2, companies);
        seed.License.LastIssuedSequence = 2;
        seed.License.SubsystemsJson = "[]";
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    private sealed record Scenario(VendorProvisioningRequest Request, VendorApprovalRequest Approval);

    private static async Task<Scenario> CreatePendingRequestAsync(
        Fixture fixture,
        ECDsa licenseKey,
        SeededLicense seed,
        VendorProvisioningCompanyReferenceGate gate,
        string selector,
        string? payloadReferenceOverride = null,
        bool forceSchema4 = false)
    {
        var now = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var createRequest = new CreateVendorProvisioningRequest(
            requestId, seed.Customer.Id, seed.Installation.Id, seed.License.Id,
            "Company bound request", "independent", "initial-admin", null,
            "سال مالی ۱۴۰۵", new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20),
            null, "admin@example.com", selector);

        var binding = await Helpers.CreateResolver(fixture.Db, licenseKey, gate).ResolveLatestAsync(
            seed.License.Id, seed.Customer, seed.Installation, null, selector,
            TestContext.Current.CancellationToken);
        Assert.True(binding.Succeeded, binding.ErrorCode);

        var payload = VendorProvisioningRequestRules.BuildPayload(
            createRequest, requestId.ToString("D"), seed.Customer.CustomerNumber,
            seed.Installation.InstallationCode, binding, now, "0123456789abcdefghijklmnopqrstuvwxyz");
        if (payloadReferenceOverride is not null)
            payload = payload with { CompanyReference = payloadReferenceOverride };
        if (forceSchema4)
            payload = payload with { SchemaVersion = 4, CompanyReference = null };

        var payloadJson = CustomerProvisioningCanonicalJson.Serialize(payload);
        var request = new VendorProvisioningRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = seed.Customer.Id,
            InstallationId = seed.Installation.Id,
            RequestId = requestId.ToString("D"),
            Nonce = payload.Nonce,
            PayloadJson = payloadJson,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant(),
            Status = "AwaitingSigner",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        fixture.Db.ProvisioningRequests.Add(request);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var approval = new VendorApprovalRequest
        {
            Id = Guid.NewGuid(),
            ResourceType = "provisioning",
            ResourceId = request.Id,
            TargetStatus = "PendingDelivery",
            ExpectedResourceRowVersion = request.RowVersion.ToArray(),
            RequestedByUserId = Guid.NewGuid(),
            RequestedAtUtc = now,
            Status = "Pending"
        };
        fixture.Db.ApprovalRequests.Add(approval);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Scenario(request, approval);
    }

    private static VendorProvisioningIssuanceService CreateService(
        Fixture fixture,
        ECDsa licenseKey,
        ECDsa provisioningKey,
        IProvisioningSigningProvider signer,
        VendorProvisioningCompanyReferenceGate? gate) =>
        new(
            fixture.Db,
            signer,
            new ProvisioningPackageSigningTrustStore([
                new ProvisioningPackageSigningTrustedKeyOptions
                {
                    KeyId = ProvisioningKeyId,
                    PublicKeySubjectPublicKeyInfoBase64 =
                        Convert.ToBase64String(provisioningKey.ExportSubjectPublicKeyInfo())
                }
            ]),
            new VendorAuditWriter(fixture.Db, TimeProvider.System),
            Helpers.CreateResolver(fixture.Db, licenseKey, gate),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VendorProvisioningIssuanceService>.Instance,
            gate);

    private static async Task<VendorProvisioningIssuanceResult> IssueAsync(
        Fixture fixture, VendorProvisioningIssuanceService service, Scenario scenario)
    {
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        return await service.IssueApprovedAsync(
            scenario.Approval,
            Guid.NewGuid(),
            scenario.Approval.RowVersion.ToArray(),
            "v5 readiness",
            Guid.NewGuid().ToString("N"),
            TestContext.Current.CancellationToken);
    }

    private sealed class CountingSigner(ECDsa key) : IProvisioningSigningProvider
    {
        public int Calls { get; private set; }

        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload, CancellationToken cancellationToken = default)
        {
            Calls++;
            var signature = key.SignData(
                canonicalPayload.Span,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return ValueTask.FromResult(LicenseSigningResult.Success(
                ProvisioningKeyId, LicenseAlgorithms.EcdsaP256Sha256, signature));
        }
    }
}
