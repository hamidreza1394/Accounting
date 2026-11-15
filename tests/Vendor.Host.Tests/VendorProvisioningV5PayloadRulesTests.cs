using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure rules for building and re-validating provisioning payloads: schema v4 for legacy licenses,
/// schema v5 only when the resolved binding carries a CompanyReference taken from the signed license.
/// </summary>
public sealed class VendorProvisioningV5PayloadRulesTests
{
    private const string RefA = "company-ref-0001";
    private static readonly Guid LicenseId = Guid.Parse("3f2b8b6e-2f1a-4c55-9d7a-0c6d3b1a9e11");
    private static readonly Guid RequestId = Guid.Parse("9a1d5e32-7c44-4b8e-8f10-2d6a1c9b7e55");
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
    private const string Nonce = "0123456789abcdefghijklmnopqrstuvwxyz";

    [Fact]
    public void LegacyBindingBuildsTheUnchangedSchemaV4Payload()
    {
        var payload = Build(Request(null), Binding(null, "finance", "sales"));

        Assert.Equal(CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion, payload.SchemaVersion);
        Assert.Null(payload.CompanyReference);
        Assert.Equal(new[] { "finance", "sales" }, payload.Subsystems);
        Assert.Equal(LicenseId.ToString("D"), payload.LicenseId);
        Assert.Equal(2, payload.Sequence);
        // Canonical JSON of a v4 payload must stay byte-identical to what v4 issuance produced before v5.
        Assert.DoesNotContain("companyReference", CustomerProvisioningCanonicalJson.Serialize(payload), StringComparison.Ordinal);
    }

    [Fact]
    public void CompanyBoundBindingBuildsSchemaV5WithTheReferenceFromTheBinding()
    {
        var payload = Build(Request(RefA), Binding(RefA, "sales"));

        Assert.Equal(CustomerProvisioningPackageValidator.CompanyReferencePayloadSchemaVersion, payload.SchemaVersion);
        Assert.Equal(RefA, payload.CompanyReference);
        Assert.Equal(new[] { "sales" }, payload.Subsystems);
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            CustomerProvisioningCanonicalJson.Serialize(payload), Now, out _, out var error,
            allowCompanyReference: true), error);
    }

    [Fact]
    public void TheEmittedReferenceIsAlwaysTheBindingValueNeverTheBrowserValue()
    {
        // Defense in depth: even if a caller handed an unrelated selector, only the signed value is emitted.
        var payload = Build(Request("company-ref-evil"), Binding(RefA, "finance"));

        Assert.Equal(RefA, payload.CompanyReference);
    }

    [Fact]
    public void V5PayloadIsRejectedByTheDefaultValidatorWhichIsWhatTheCustomerStillUses()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Build(Request(RefA), Binding(RefA, "finance")));

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("company-ref-0001")]
    public void ValidateAcceptsAnAbsentOrWellFormedSelector(string? selector)
    {
        Assert.Null(VendorProvisioningRequestRules.Validate(Request(selector)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" company-ref-0001")]
    [InlineData("company ref")]
    [InlineData("company-ref-\u0000")]
    [InlineData("<script>")]
    public void ValidateRejectsAMalformedSelector(string selector)
    {
        Assert.Equal("invalid_provisioning_company_reference", VendorProvisioningRequestRules.Validate(Request(selector)));
    }

    [Fact]
    public void ValidateRejectsAnOverlongSelector()
    {
        Assert.Equal(
            "invalid_provisioning_company_reference",
            VendorProvisioningRequestRules.Validate(Request(new string('a', 129))));
    }

    [Fact]
    public void ExistingV5RequestMatchesOnlyTheSameSelectorAndReference()
    {
        var existing = Build(Request(RefA), Binding(RefA, "finance"));

        Assert.True(VendorProvisioningRequestRules.MatchesExisting(
            existing, Request(RefA), "C-1", "I-1", Binding(RefA, "finance")));
        // A different selector is a different request, not an idempotent replay.
        Assert.False(VendorProvisioningRequestRules.MatchesExisting(
            existing, Request("company-ref-0002"), "C-1", "I-1", Binding(RefA, "finance")));
        // Omitting the selector must not replay a company-bound request.
        Assert.False(VendorProvisioningRequestRules.MatchesExisting(
            existing, Request(null), "C-1", "I-1", Binding(RefA, "finance")));
    }

    [Fact]
    public void ExistingV4AndV5RequestsNeverMatchTheOtherSchema()
    {
        var v4 = Build(Request(null), Binding(null, "finance"));
        var v5 = Build(Request(RefA), Binding(RefA, "finance"));

        Assert.True(VendorProvisioningRequestRules.MatchesExisting(
            v4, Request(null), "C-1", "I-1", Binding(null, "finance")));
        Assert.False(VendorProvisioningRequestRules.MatchesExisting(
            v4, Request(RefA), "C-1", "I-1", Binding(RefA, "finance")));
        Assert.False(VendorProvisioningRequestRules.MatchesExisting(
            v5, Request(null), "C-1", "I-1", Binding(null, "finance")));
    }

    [Fact]
    public void BindingMustMatchSchemaAndReferenceExactly()
    {
        var v4 = Build(Request(null), Binding(null, "finance"));
        var v5 = Build(Request(RefA), Binding(RefA, "finance"));

        Assert.True(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(v4, Binding(null, "finance")));
        Assert.True(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(v5, Binding(RefA, "finance")));

        // v4 package against a company-bound license, and the reverse, must be refused.
        Assert.False(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(v4, Binding(RefA, "finance")));
        Assert.False(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(v5, Binding(null, "finance")));
        // A different (even well-formed) reference is a mismatch.
        Assert.False(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(
            v5, Binding("company-ref-0002", "finance")));
        // Case differences are mismatches: references are opaque, not normalized.
        Assert.False(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(
            v5, Binding("COMPANY-REF-0001", "finance")));
    }

    [Fact]
    public void BindingStillComparesSubsystemsAndSequenceForV5()
    {
        var v5 = Build(Request(RefA), Binding(RefA, "finance"));

        Assert.False(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(v5, Binding(RefA, "sales")));
        Assert.False(VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(
            v5, Binding(RefA, sequence: 3, "finance")));
    }

    private static CustomerProvisioningPayload Build(
        CreateVendorProvisioningRequest request, VendorProvisioningLicenseBindingResult binding) =>
        VendorProvisioningRequestRules.BuildPayload(
            request, RequestId.ToString("D"), "C-1", "I-1", binding, Now, Nonce);

    private static CreateVendorProvisioningRequest Request(string? companyReference) =>
        new(
            RequestId, Guid.NewGuid(), Guid.NewGuid(), LicenseId,
            "Company", "independent", "initial-admin", null,
            "FY 2026", new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20),
            null, "admin@example.com", companyReference);

    private static VendorProvisioningLicenseBindingResult Binding(
        string? companyReference, params string[] subsystems) =>
        Binding(companyReference, 2, subsystems);

    private static VendorProvisioningLicenseBindingResult Binding(
        string? companyReference, int sequence, params string[] subsystems) =>
        VendorProvisioningLicenseBindingResult.Success(
            LicenseId.ToString("D"), sequence, subsystems, subsystems, companyReference);
}
