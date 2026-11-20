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
    private static VendorLifecycleReadinessRules.LifecycleSnapshot Snapshot(
        string customerStatus = "Active",
        bool hasOrder = true,
        int verified = 0,
        int recorded = 0,
        int rejected = 0,
        int issued = 0,
        int draft = 0,
        bool hasInstallations = false,
        string? provisioningStatus = null,
        string? ack = null,
        string? amendmentStatus = null,
        string? amendmentFailureCode = null,
        bool gateEnabled = false) => new(
        customerStatus, hasOrder, "SO-1", null,
        verified, recorded, rejected,
        issued, draft, hasInstallations, "I-1", "Active", null,
        provisioningStatus is not null, provisioningStatus, null, ack, null,
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
            issued: 1, hasInstallations: true, provisioningStatus: "Rejected",
            ack: "Rejected", amendmentStatus: "Rejected", amendmentFailureCode: "amendment_rejected"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "ack"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "provisioning"));
        Assert.Equal(VendorLifecycleReadinessRules.Failed, StatusOf(stages, "amendment"));
        var provisioning = stages.Single(stage => stage.Key == "provisioning");
        Assert.Contains("Provisioning", provisioning.Reason);
        Assert.NotNull(provisioning.CorrectiveAction);
    }

    [Fact]
    public void ClosedCompanyReferenceGateIsBlockedNeverReady()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(gateEnabled: false));
        var gate = stages.Single(stage => stage.Key == "runtimeGate");
        Assert.Equal(VendorLifecycleReadinessRules.Blocked, gate.Status);
        Assert.Contains("#392", gate.Reason);
    }

    [Fact]
    public void AwaitingSignerBlocksSigningInsteadOfFakingSuccess()
    {
        var stages = VendorLifecycleReadinessRules.Resolve(Snapshot(provisioningStatus: "AwaitingSigner"));
        Assert.Equal(VendorLifecycleReadinessRules.Blocked, StatusOf(stages, "signing"));
        Assert.Equal(VendorLifecycleReadinessRules.Pending, StatusOf(stages, "delivery"));
    }
}
