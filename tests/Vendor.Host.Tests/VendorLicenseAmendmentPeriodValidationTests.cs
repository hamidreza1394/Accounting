using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Request-time validation for support/updates amendment requests (review fix C): the requested
/// periods are measured against the current SIGNATURE-VERIFIED payload — never operator input —
/// so an invalid request is rejected before it can be stored as Requested. Payment state is
/// deliberately absent (D-108 §1) and nothing here creates or issues anything.
/// </summary>
public sealed class VendorLicenseAmendmentPeriodValidationTests
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static LicensePayload PayloadWithPeriods(DateTimeOffset? supportUntilUtc, DateTimeOffset? updatesUntilUtc) =>
        new(
            FormatVersion: LicensePayload.CurrentFormatVersion,
            LicenseId: Guid.NewGuid().ToString("D"),
            InstallationId: Guid.NewGuid().ToString("D"),
            ProductCode: "FARANOU",
            LicenseKind: LicensePayload.KindPerpetual,
            IssuedAtUtc: IssuedAt.AddDays(-365),
            NotBeforeUtc: IssuedAt.AddDays(-365),
            ExpiresAtUtc: null,
            GraceDays: 30,
            Sequence: 1,
            Limits: new LicenseLimits(5, Array.Empty<string>()))
        {
            SupportUntilUtc = supportUntilUtc,
            UpdatesUntilUtc = updatesUntilUtc
        };

    [Fact]
    public void StrictExtensionOfBothPeriodsIsValid()
    {
        var payload = PayloadWithPeriods(IssuedAt.AddDays(180), IssuedAt.AddDays(180));
        Assert.Null(VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(
            payload, IssuedAt.AddDays(365), IssuedAt.AddDays(365), IssuedAt));
    }

    [Fact]
    public void ExtensionOfOnlyOnePeriodKeepsTheOtherUnchanged()
    {
        var payload = PayloadWithPeriods(IssuedAt.AddDays(180), IssuedAt.AddDays(180));
        Assert.Null(VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(
            payload, IssuedAt.AddDays(365), null, IssuedAt));
        Assert.Null(VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(
            payload, null, IssuedAt.AddDays(365), IssuedAt));
    }

    [Fact]
    public void RequestWithoutAnyPeriodIncreaseIsRejected()
    {
        var payload = PayloadWithPeriods(IssuedAt.AddDays(180), IssuedAt.AddDays(180));
        Assert.Equal("amendment_has_no_period_increase",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(payload, null, null, IssuedAt));
    }

    [Fact]
    public void NoPreviouslyGrantedPeriodIsRejectedWithAnActionableCode()
    {
        var payload = PayloadWithPeriods(null, null);
        Assert.Equal("support_period_not_previously_granted",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(payload, IssuedAt.AddDays(365), null, IssuedAt));
        Assert.Equal("updates_period_not_previously_granted",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(payload, null, IssuedAt.AddDays(365), IssuedAt));
    }

    [Fact]
    public void NonIncreasingOrPastDatesAreRejected()
    {
        var payload = PayloadWithPeriods(IssuedAt.AddDays(180), IssuedAt.AddDays(180));
        Assert.Equal("support_period_not_increased",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(payload, IssuedAt.AddDays(180), null, IssuedAt));
        Assert.Equal("updates_period_not_increased",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(payload, null, IssuedAt.AddDays(90), IssuedAt));
        // requested > current but still in the past: not_future wins.
        var expired = PayloadWithPeriods(IssuedAt.AddDays(-20), IssuedAt.AddDays(-20));
        Assert.Equal("support_period_not_future",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(expired, IssuedAt.AddDays(-10), null, IssuedAt));
    }

    [Fact]
    public void UnsupportedPayloadFormatIsRejected()
    {
        var payload = PayloadWithPeriods(IssuedAt.AddDays(180), null) with { FormatVersion = 1 };
        Assert.Equal("support_update_periods_require_payload_v2",
            VendorLicenseAmendmentPayloadBuilder.ValidateRequestedPeriods(payload, IssuedAt.AddDays(365), null, IssuedAt));
    }
}
