using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence.Domain;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure delivery/acknowledgement state rules for license amendments (#393, S-B owner decision):
/// only an issued amendment can be delivered, only a delivered one can be re-exported or
/// acknowledged, and only validated outcomes move the state past Delivered.
/// </summary>
public sealed class VendorLicenseAmendmentAcknowledgementRulesTests
{
    [Fact]
    public void OnlyAnIssuedRequestCanBeDeliveredForTheFirstTime()
    {
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusIssued));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusRequested));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusPendingApproval));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusDelivered));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusApplied));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusRejected));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanDeliver(
            VendorLicenseAmendmentRequest.StatusFailed));
    }

    [Fact]
    public void ReExportIsLimitedToDeliveredAndAppliedStates()
    {
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.CanReExport(
            VendorLicenseAmendmentRequest.StatusDelivered));
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.CanReExport(
            VendorLicenseAmendmentRequest.StatusApplied));
        foreach (var status in new[]
                 {
                     VendorLicenseAmendmentRequest.StatusRequested,
                     VendorLicenseAmendmentRequest.StatusPendingApproval,
                     VendorLicenseAmendmentRequest.StatusIssued,
                     VendorLicenseAmendmentRequest.StatusRejected,
                     VendorLicenseAmendmentRequest.StatusFailed
                 })
        {
            Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanReExport(status));
        }
    }

    [Fact]
    public void AcknowledgementRequiresTheFullDeliveryMetadata()
    {
        var deliveredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.CanAcceptAcknowledgement(
            VendorLicenseAmendmentRequest.StatusDelivered, deliveredAt,
            new string('a', 64), new string('b', 64)));
        // Missing any delivery stamp, or any other status, must fail closed.
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanAcceptAcknowledgement(
            VendorLicenseAmendmentRequest.StatusDelivered, null, new string('a', 64), new string('b', 64)));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanAcceptAcknowledgement(
            VendorLicenseAmendmentRequest.StatusDelivered, deliveredAt, null, new string('b', 64)));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanAcceptAcknowledgement(
            VendorLicenseAmendmentRequest.StatusDelivered, deliveredAt, new string('a', 64), null));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanAcceptAcknowledgement(
            VendorLicenseAmendmentRequest.StatusIssued, deliveredAt,
            new string('a', 64), new string('b', 64)));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.CanAcceptAcknowledgement(
            VendorLicenseAmendmentRequest.StatusApplied, deliveredAt,
            new string('a', 64), new string('b', 64)));
    }

    [Fact]
    public void DeliveryNonceIsSixtyFourLowercaseHexCharacters()
    {
        var nonce = VendorLicenseAmendmentAcknowledgementRules.NewDeliveryNonce();
        Assert.Equal(64, nonce.Length);
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.IsDeliveryNonceShape(nonce));
        // Two deliveries must never share a nonce: each delivery binds its own acknowledgement.
        Assert.NotEqual(nonce, VendorLicenseAmendmentAcknowledgementRules.NewDeliveryNonce());
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.IsDeliveryNonceShape(nonce.ToUpperInvariant()));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.IsDeliveryNonceShape("nonce"));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.IsDeliveryNonceShape(null));
    }

    [Fact]
    public void AppliedOutcomesResolveToTheAppliedStateWithoutFailureCode()
    {
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "Applied", null, out var appliedStatus, out var appliedFailureCode));
        Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, appliedStatus);
        Assert.Null(appliedFailureCode);

        // AlreadyApplied is deliberately idempotent: the amendment is in force either way.
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "AlreadyApplied", null, out var alreadyStatus, out var alreadyFailureCode));
        Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, alreadyStatus);
        Assert.Null(alreadyFailureCode);

        // An outcome can never smuggle an error code: validated Applied ACKs carry none.
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "Applied", "unexpected", out _, out _));
    }

    [Fact]
    public void OnlyAllowlistedRejectionCodesAreTerminalRejections()
    {
        foreach (var code in new[]
                 {
                     "license_validation_failed",
                     "license_status_policy_unapproved",
                     "license_id_change_requires_reactivation",
                     "license_sequence_replay_or_downgrade",
                     "license_import_conflict",
                     "license_import_concurrency_conflict"
                 })
        {
            Assert.True(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
                "Rejected", code, out var status, out var failureCode));
            Assert.Equal(VendorLicenseAmendmentRequest.StatusRejected, status);
            Assert.Equal("amendment_ack_rejected", failureCode);
        }

        // Unknown or free-form codes (raw exception text, PII) fail closed: no state advances.
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "Rejected", "SqlException: database unavailable for admin@example.com", out _, out _));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "Rejected", "customer_internal_error", out _, out _));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "Rejected", null, out _, out _));
    }

    [Fact]
    public void UnknownOutcomesNeverResolve()
    {
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "Pending", null, out _, out _));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            null, null, out _, out _));
        Assert.False(VendorLicenseAmendmentAcknowledgementRules.TryResolveOutcome(
            "applied", null, out _, out _));
    }

    [Fact]
    public void ADeliveredAmendmentStillBlocksASecondRequestForTheSameLine()
    {
        // The delivered envelope may not yet be imported by the customer: issuing another
        // amendment for the same purchased line would build from the same stale envelope.
        Assert.Contains(VendorLicenseAmendmentRequest.StatusDelivered,
            VendorLicenseAmendmentRules.BlockingStatuses);
        Assert.DoesNotContain(VendorLicenseAmendmentRequest.StatusApplied,
            VendorLicenseAmendmentRules.BlockingStatuses);
        Assert.DoesNotContain(VendorLicenseAmendmentRequest.StatusRejected,
            VendorLicenseAmendmentRules.BlockingStatuses);
    }
}
