using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure readiness rules: statuses derive only from the persisted snapshot, a failed stage is
/// never hidden, the #392 gate is never Ready while closed, and no payment state can mark the
/// amendment stage (D-108).
/// </summary>
public sealed class VendorLifecycleReadinessRulesTests
{
    private static readonly DateTimeOffset DeliveredAt =
        new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);

    private static VendorLifecycleReadinessRules.LifecycleSnapshot Snapshot(
        string customerStatus = "Active",
        bool hasOrder = true,
        int verified = 0,
        int recorded = 0,
        int rejected = 0,
        int issued = 0,
        int draft = 0,
        bool hasInstallations = false,
        string? installationStatus = "Active",
        string? provisioningStatus = null,
        DateTimeOffset? deliveredAtUtc = null,
        string? ack = null,
        string? amendmentStatus = null,
        string? amendmentFailureCode = null,
        bool gateEnabled = false) => new(
        customerStatus, hasOrder, "SO-1", null,
        verified, recorded, rejected,
        issued, draft, hasInstallations, "I-1", installationStatus, null,
        provisioningStatus is not null, provisioningStatus, null, ack, null, deliveredAtUtc,
        0, amendmentStatus, amendmentFailureCode, null, gateEnabled);

    private static string StatusOf(IReadOnlyList<VendorLifecycleReadinessRules.LifecycleStage> stages, string key) =>
        stages.Single(stage => stage.Key == key).Status;

    [Fact]
    public void EmptyLifecycleIsPendingOrNotApplicableNeverReady()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(
            hasOrder: false, hasInstallations: false));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "customerRegistration"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(stages, "commercialOrder"));
        Assert.Equal(VendorLifecycleReadinessRules.NotApplicable, StatusOf(stages, "payment"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(stages, "entitlement"));
        Assert.Equal(VendorLifecycleReadinessRules.NotApplicable, StatusOf(stages, "ack"));
        Assert.NotEqual(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "runtimeGate"));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("recorded")]
    [InlineData("rejected")]
    [InlineData("verified")]
    public void PaymentStateNeverGatesOrTriggersTheAmendmentStage(string paymentState)
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(
            verified: paymentState == "verified" ? 1 : 0,
            recorded: paymentState == "recorded" ? 1 : 0,
            rejected: paymentState == "rejected" ? 1 : 0,
            amendmentStatus: null));
        // With no amendment request, the stage stays NotApplicable regardless of payment state.
        Assert.Equal(VendorLifecycleReadinessRules.NotApplicable, StatusOf(stages, "amendment"));
    }

    [Fact]
    public void FailedAckAndRejectedAmendmentSurfaceAsFailedWithReason()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(
            issued: 1, hasInstallations: true, provisioningStatus: "Failed", deliveredAtUtc: DeliveredAt,
            ack: "Rejected", amendmentStatus: "Rejected", amendmentFailureCode: "amendment_rejected"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "signing"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "delivery"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "ack"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "bootstrap"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "provisioning"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "amendment"));
        var provisioning = stages.Single(stage => stage.Key == "provisioning");
        Assert.Contains("Provisioning", provisioning.Reason);
        Assert.NotNull(provisioning.CorrectiveAction);
    }

    [Fact]
    public void IssuedLicenseStageDoesNotClaimRuntimeValidity()
    {
        var entitlement = VendorLifecycleReadinessRules.Resolve(Snapshot(issued: 1))
            .Single(stage => stage.Key == "entitlement");

        Assert.Equal(VendorLifecycleReadinessRules.Ready, entitlement.Status);
        Assert.Contains("صادرشده ثبت شده", entitlement.Reason);
        Assert.Contains("اعتبار زمانی و runtime جداگانه", entitlement.Reason);
        Assert.DoesNotContain("فعال است", entitlement.Reason);
    }

    [Fact]
    public void ClosedCompanyReferenceGateIsBlockedNeverReady()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(gateEnabled: false));
        var gate = stages.Single(stage => stage.Key == "runtimeGate");
        Assert.Equal(VendorLifecycleReadinessRules.Blocked, gate.Status);
        Assert.Contains("#392", gate.Reason);
    }

    [Theory]
    [InlineData("Active", VendorLifecycleReadinessRules.Ready)]
    [InlineData("Suspended", VendorLifecycleReadinessRules.Blocked)]
    [InlineData("Unexpected", VendorLifecycleReadinessRules.Failed)]
    public void InstallationStageReflectsPersistedStatus(string installationStatus, string expected)
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(
            hasInstallations: true, installationStatus: installationStatus));
        Assert.Equal(expected, StatusOf(stages, "installation"));
    }

    [Fact]
    public void AwaitingSignerRemainsPendingUntilSigningActuallySucceeds()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(provisioningStatus: "AwaitingSigner"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(stages, "signing"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(stages, "delivery"));
        Assert.Equal(VendorLifecycleReadinessRules.NotApplicable, StatusOf(stages, "ack"));
    }

    [Fact]
    public void PendingDeliveryUsesDeliveredAtUtcToDistinguishExportFromAwaitingAck()
    {
        var notDelivered = VendorLifecycleReadinessRules.Resolve(Snapshot(
            provisioningStatus: "PendingDelivery"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(notDelivered, "signing"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(notDelivered, "delivery"));
        Assert.Equal(VendorLifecycleReadinessRules.NotApplicable, StatusOf(notDelivered, "ack"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(notDelivered, "provisioning"));

        var delivered = VendorLifecycleReadinessRules.Resolve(Snapshot(
            provisioningStatus: "PendingDelivery", deliveredAtUtc: DeliveredAt));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(delivered, "delivery"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(delivered, "ack"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(delivered, "bootstrap"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(delivered, "provisioning"));
    }

    [Fact]
    public void AlreadyAppliedAcknowledgementIsSuccessfulForAckAndBootstrap()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(
            provisioningStatus: "Confirmed", ack: "AlreadyApplied", deliveredAtUtc: DeliveredAt));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "signing"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "signatureVerification"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "delivery"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "ack"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "bootstrap"));
        Assert.Equal(VendorLifecycleReadinessRules.Ready, StatusOf(stages, "provisioning"));
    }

    [Fact]
    public void UnknownProvisioningStatusFailsClosed()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(
            provisioningStatus: "Unexpected"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "signing"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "delivery"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "provisioning"));
    }
}
