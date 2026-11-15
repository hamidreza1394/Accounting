using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseCompanyDraftTests
{
    private static VendorCompanyDraftInput Company(
        string? reference = null, string descriptor = "Main company", int maxUsers = 10,
        string[]? modules = null, LicenseFeatureEntitlement?[]? features = null) =>
        new(reference, descriptor, modules ?? ["finance", "sales"], maxUsers, features);

    private static string Normalize(
        IReadOnlyList<VendorCompanyDraftInput> inputs, string? existing = null, int? maxCompanies = null)
    {
        Assert.True(
            VendorLicenseCompanyDraft.TryNormalize(inputs, existing, maxCompanies, out var json, out var error), error);
        return json!;
    }

    private static string Reject(
        IReadOnlyList<VendorCompanyDraftInput>? inputs, string? existing = null, int? maxCompanies = null)
    {
        Assert.False(VendorLicenseCompanyDraft.TryNormalize(inputs, existing, maxCompanies, out var json, out var error));
        Assert.Null(json);
        return error!;
    }

    [Fact]
    public void NewCompanyReceivesServerGeneratedGuidDReference()
    {
        var json = Normalize([Company(), Company(descriptor: "Branch")]);

        Assert.True(VendorLicenseCompanyDraft.TryParse(json, out var draft));
        Assert.Equal(2, draft!.Companies.Count);
        foreach (var company in draft.Companies)
        {
            Assert.True(Guid.TryParseExact(company.CompanyReference, "D", out _));
            Assert.True(StructuralValidation.IsValidCompanyReference(company.CompanyReference));
        }
        Assert.NotEqual(draft.Companies[0].CompanyReference, draft.Companies[1].CompanyReference);
    }

    [Fact]
    public void ClientSuppliedReferenceForNewCompanyIsRejected()
    {
        Assert.Equal("company_reference_unknown",
            Reject([Company(reference: Guid.NewGuid().ToString("D"))]));
    }

    [Fact]
    public void ExistingReferenceIsKeptWhenItMatchesTheSameDraft()
    {
        var first = Normalize([Company(), Company(descriptor: "Branch")]);
        VendorLicenseCompanyDraft.TryParse(first, out var parsed);
        var keep = parsed!.Companies[1].CompanyReference;

        var second = Normalize([Company(reference: keep, descriptor: "Branch renamed", maxUsers: 99)], first);

        VendorLicenseCompanyDraft.TryParse(second, out var updated);
        Assert.Single(updated!.Companies);
        Assert.Equal(keep, updated.Companies[0].CompanyReference);
        Assert.Equal(99, updated.Companies[0].MaxUsers);
    }

    [Fact]
    public void ReferenceFromAnotherDraftIsRejected()
    {
        var mine = Normalize([Company()]);
        var other = Normalize([Company()]);
        VendorLicenseCompanyDraft.TryParse(other, out var otherDraft);

        Assert.Equal("company_reference_unknown",
            Reject([Company(reference: otherDraft!.Companies[0].CompanyReference)], mine));
    }

    [Fact]
    public void DuplicateReferenceInOneRequestIsRejected()
    {
        var first = Normalize([Company()]);
        VendorLicenseCompanyDraft.TryParse(first, out var parsed);
        var reference = parsed!.Companies[0].CompanyReference;

        Assert.Equal("company_reference_duplicate",
            Reject([Company(reference: reference), Company(reference: reference)], first));
    }

    [Fact]
    public void RemovedCompanyReferenceCannotBeReAddedLater()
    {
        var first = Normalize([Company(), Company(descriptor: "Branch")]);
        VendorLicenseCompanyDraft.TryParse(first, out var parsed);
        var removed = parsed!.Companies[1].CompanyReference;
        var afterRemoval = Normalize([Company(reference: parsed.Companies[0].CompanyReference)], first);

        Assert.Equal("company_reference_unknown",
            Reject([Company(reference: removed)], afterRemoval));
    }

    [Fact]
    public void EmptyOrMissingCompanyListIsRejected()
    {
        Assert.Equal("company_list_empty", Reject([]));
        Assert.Equal("company_list_empty", Reject(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    public void MaxUsersOutsideContractRangeIsRejected(int maxUsers)
    {
        Assert.Equal("company_entitlements_invalid", Reject([Company(maxUsers: maxUsers)]));
    }

    [Fact]
    public void UnknownModuleAndInvalidFeatureAreRejectedByTheContract()
    {
        Assert.Equal("company_entitlements_invalid", Reject([Company(modules: ["not-a-module"])]));
        Assert.Equal("company_entitlements_invalid", Reject([Company(features:
            [new LicenseFeatureEntitlement("unknown.feature", LicenseFeatureValueTypes.Enabled, Enabled: true)])]));
    }

    [Fact]
    public void NullModuleOrNullFeatureEntryIsRejectedInsteadOfSilentlyDropped()
    {
        Assert.Equal("company_entitlements_invalid", Reject([Company(modules: ["finance", null!])]));
        Assert.Equal("company_entitlements_invalid", Reject([Company(features: [null])]));
    }

    [Fact]
    public void CompanyCountAboveMaxCompaniesIsRejected()
    {
        Assert.Equal("company_entitlements_invalid",
            Reject([Company(), Company(descriptor: "B")], maxCompanies: 1));
        Normalize([Company(), Company(descriptor: "B")], maxCompanies: 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\ncontrol")]
    public void DescriptorMustBeNonBlankSingleLine(string descriptor)
    {
        Assert.Equal("company_descriptor_invalid", Reject([Company(descriptor: descriptor)]));
    }

    [Fact]
    public void DescriptorLongerThanLimitIsRejected()
    {
        Assert.Equal("company_descriptor_invalid",
            Reject([Company(descriptor: new string('x', VendorLicenseCompanyDraft.MaxDescriptorLength + 1))]));
    }

    [Fact]
    public void DraftLargerThanServerLimitIsRejected()
    {
        var many = Enumerable.Range(0, 129).Select(i => Company(descriptor: $"c{i}")).ToArray();
        var error = Reject(many);
        Assert.Contains(error, new[] { "company_entitlements_invalid", "company_draft_too_large" });
    }

    [Fact]
    public void ReduceKeepsOnlyReferenceAndDescriptor()
    {
        var json = Normalize([Company(features:
            [new LicenseFeatureEntitlement("finance.cost_centers", LicenseFeatureValueTypes.MaxCount, MaxCount: 5)])]);

        var reduced = VendorLicenseCompanyDraft.Reduce(json);

        using var doc = JsonDocument.Parse(reduced);
        var company = doc.RootElement.GetProperty("companies")[0];
        var names = company.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "companyReference", "descriptor" }, names);
        VendorLicenseCompanyDraft.TryParse(json, out var original);
        Assert.Equal(original!.Companies[0].CompanyReference, company.GetProperty("companyReference").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"companies\":null}")]
    public void ParseRejectsMissingOrMalformedStoredDraft(string? json)
    {
        Assert.False(VendorLicenseCompanyDraft.TryParse(json, out var draft));
        Assert.Null(draft);
    }

    [Theory]
    [InlineData("a\u202Eb")]
    [InlineData("a\u2066b")]
    [InlineData("a\u200Bb")]
    public void DescriptorWithBidiOrInvisibleFormatCharactersIsRejected(string descriptor)
    {
        Assert.Equal("company_descriptor_invalid", Reject([Company(descriptor: descriptor)]));
    }

    [Fact]
    public void DescriptorMayContainPersianTextAndZeroWidthNonJoiner()
    {
        Normalize([Company(descriptor: "شرکت می\u200Cخواهم")]);
    }

    [Fact]
    public void ResponseSubsystemsAreEmptyForCompanyScopedLicenseAndLegacyForV1()
    {
        Assert.Empty(VendorLicenseCompanyDraft.EffectiveSubsystems("{\"companies\":[]}", "[\"sales\"]"));
        Assert.Equal(new[] { "sales" }, VendorLicenseCompanyDraft.EffectiveSubsystems(null, "[\"sales\"]"));
    }
}
