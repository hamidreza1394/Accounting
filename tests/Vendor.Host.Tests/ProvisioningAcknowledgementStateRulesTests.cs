using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class ProvisioningAcknowledgementStateRulesTests
{
    [Theory]
    [InlineData("Applied")]
    [InlineData("AlreadyApplied")]
    public void SuccessfulOutcomesConfirmWithoutFailure(string result)
    {
        Assert.True(ProvisioningAcknowledgementStateRules.TryResolveOutcome(
            result, null, out var status, out var failureCode));
        Assert.Equal("Confirmed", status);
        Assert.Null(failureCode);
    }

    [Theory]
    [InlineData("provisioning_parent_reference_invalid")]
    [InlineData("provisioning_initial_admin_conflict")]
    public void RejectedOutcomeAcceptsOnlyKnownStableErrorCodes(string errorCode)
    {
        Assert.True(ProvisioningAcknowledgementStateRules.TryResolveOutcome(
            "Rejected", errorCode, out var status, out var failureCode));
        Assert.Equal("Failed", status);
        Assert.Equal(errorCode, failureCode);
    }

    [Theory]
    [InlineData("customer_validation_failed")]
    [InlineData("provisioning_apply_conflict")]
    [InlineData("SqlException: database unavailable")]
    [InlineData("admin@example.com")]
    [InlineData("provisioning_parent_reference_invalid\nstack trace")]
    public void RejectedOutcomeRejectsUnknownOrPotentiallySensitiveErrorCodes(string errorCode)
    {
        Assert.False(ProvisioningAcknowledgementStateRules.TryResolveOutcome(
            "Rejected", errorCode, out _, out _));
    }

    [Theory]
    [InlineData("Applied", "unexpected")]
    [InlineData("Rejected", null)]
    [InlineData("Unknown", null)]
    public void InvalidOutcomeCombinationsAreRejected(string result, string? errorCode)
    {
        Assert.False(ProvisioningAcknowledgementStateRules.TryResolveOutcome(
            result, errorCode, out _, out _));
    }
}
