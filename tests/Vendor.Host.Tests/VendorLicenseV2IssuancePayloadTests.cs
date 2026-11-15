using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence.Domain;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseV2IssuancePayloadTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static (VendorLicense License, VendorInstallation Installation) Fixture(string? draftJson, int? maxCompanies = 5)
    {
        var customerId = Guid.NewGuid();
        var installation = new VendorInstallation { Id = Guid.NewGuid(), CustomerId = customerId, Status = "Active" };
        var license = new VendorLicense
        {
            Id = Guid.NewGuid(), CustomerId = customerId, InstallationId = installation.Id,
            LicenseNumber = "LIC-V2", ProductCode = "FARANOU", Status = "Draft",
            LicenseKind = LicensePayload.KindPerpetual, MaxCompanies = maxCompanies,
            SubsystemsJson = "[\"sales\",\"finance\"]", GraceDays = 30, LastIssuedSequence = 0,
            ValidFromUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            CompanyEntitlementsDraftJson = draftJson
        };
        return (license, installation);
    }

    private static string Draft(params VendorCompanyDraftInput[] companies)
    {
        Assert.True(VendorLicenseCompanyDraft.TryNormalize(companies, null, null, out var json, out var error), error);
        return json!;
    }

    private static VendorCompanyDraftInput Company(string name, int users, params string[] modules) =>
        new(null, name, modules, users, null);

    [Fact]
    public void DraftProducesV2PayloadWithCompanyEntitlementsAndNoInstallationSubsystems()
    {
        var (license, installation) = Fixture(Draft(Company("A", 10, "finance"), Company("B", 3, "sales", "finance")));

        Assert.True(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out var error), error);

        Assert.Equal(LicensePayload.CurrentFormatVersion, payload!.FormatVersion);
        Assert.Equal(5, payload.Limits.MaxCompanies);
        Assert.Empty(payload.Limits.Subsystems);
        Assert.Equal(1, payload.Sequence);
        Assert.Equal(2, payload.CompanyEntitlements!.Count);
        Assert.Empty(StructuralValidation.ValidatePayload(payload));
        Assert.DoesNotContain("sales\",\"finance", CanonicalJson.SerializePayload(payload).Replace(" ", ""));
    }

    [Fact]
    public void LegacySubsystemsJsonNeverEntersTheV2PayloadAndIsNotChanged()
    {
        var (license, installation) = Fixture(Draft(Company("A", 10, "finance")));

        Assert.True(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out _));

        Assert.Empty(payload!.Limits.Subsystems);
        Assert.Equal("[\"sales\",\"finance\"]", license.SubsystemsJson);
    }

    [Fact]
    public void EmptyLegacySubsystemsDoNotBlockV2()
    {
        var (license, installation) = Fixture(Draft(Company("A", 10, "finance")));
        license.SubsystemsJson = "[]";

        Assert.True(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out var error), error);
        Assert.Equal(2, payload!.FormatVersion);
    }

    [Fact]
    public void NullDraftKeepsTheV1PathUnchanged()
    {
        var (license, installation) = Fixture(null, maxCompanies: null);

        Assert.True(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out _));

        Assert.Equal(1, payload!.FormatVersion);
        Assert.Null(payload.CompanyEntitlements);
        Assert.Equal(new[] { "finance", "sales" }, payload.Limits.Subsystems);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"companies\":[]}")]
    [InlineData("{\"companies\":[{\"companyReference\":\"00000000-0000-0000-0000-000000000001\",\"descriptor\":\"x\"}]}")]
    public void CorruptOrReducedDraftFailsClosed(string draftJson)
    {
        var (license, installation) = Fixture(draftJson);

        Assert.False(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("license_company_draft_invalid", error);
    }

    [Fact]
    public void DraftWithMoreCompaniesThanMaxCompaniesFailsClosed()
    {
        var (license, installation) = Fixture(Draft(Company("A", 1, "finance"), Company("B", 1, "finance")), maxCompanies: 1);

        Assert.False(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out _, out var error));
        Assert.Equal("license_company_draft_invalid", error);
    }

    [Fact]
    public void V2WithoutMaxCompaniesIsAllowedByTheContractButStillValidated()
    {
        var (license, installation) = Fixture(Draft(Company("A", 1, "finance")), maxCompanies: null);

        Assert.True(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out var error), error);
        Assert.Null(payload!.Limits.MaxCompanies);
    }

    [Fact]
    public async Task SignedV2EnvelopeCarriesExactlyTheDraftContentAndAllowsAmendmentRecovery()
    {
        var (license, installation) = Fixture(Draft(Company("A", 10, "finance"), Company("B", 3, "sales")));
        var customer = new VendorCustomer { Id = license.CustomerId, Status = "Active" };
        Assert.True(VendorLicensePayloadBuilder.TryBuild(license, installation, IssuedAt, out var payload, out _));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var factory = new VendorLicenseEnvelopeFactory(new SigningProvider(key));

        var created = await factory.CreateAsync(payload!, TestContext.Current.CancellationToken);

        Assert.True(created.Succeeded, created.ErrorCode);
        Assert.Equal(CanonicalJson.SerializePayload(payload!), created.Envelope!.PayloadJson);
        Assert.True(new EcdsaP256Sha256Verifier().TryVerify(created.Envelope, key.ExportSubjectPublicKeyInfo(), out _));

        // Recover the prior license content only from the signed envelope (draft already reduced).
        license.CompanyEntitlementsDraftJson = VendorLicenseCompanyDraft.Reduce(license.CompanyEntitlementsDraftJson!);
        license.Status = "Issued";
        license.LastIssuedSequence = payload!.Sequence;
        Assert.True(CanonicalJson.TryParsePayload(created.Envelope.PayloadJson, out var recovered, out var parseError), parseError);
        Assert.Equal(
            payload.CompanyEntitlements!.Select(c => c!.CompanyReference),
            recovered!.CompanyEntitlements!.Select(c => c!.CompanyReference));

        Assert.True(VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            recovered, license, installation, customer, 9, IssuedAt.AddDays(1), out var amendment, out var error), error);
        Assert.Equal(
            recovered.CompanyEntitlements!.Select(c => (c!.CompanyReference, c.MaxUsers)),
            amendment!.CompanyEntitlements!.Select(c => (c!.CompanyReference, c.MaxUsers)));
        Assert.True(LicenseRevisionRules.TryValidateSuccessor(recovered, amendment, out var successorError), successorError);
    }

    private sealed class SigningProvider(ECDsa key) : ILicenseSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default) =>
            ValueTask.FromResult(LicenseSigningResult.Success(
                "test-kid", LicenseAlgorithms.EcdsaP256Sha256,
                key.SignData(payload.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
    }
}
