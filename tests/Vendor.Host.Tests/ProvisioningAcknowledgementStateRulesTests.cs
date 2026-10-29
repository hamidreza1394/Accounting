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

    [Fact]
    public void RejectedOutcomeFailsAndRetainsOnlyBoundedErrorCode()
    {
        Assert.True(ProvisioningAcknowledgementStateRules.TryResolveOutcome(
            "Rejected", "customer_validation_failed", out var status, out var failureCode));
        Assert.Equal("Failed", status);
        Assert.Equal("customer_validation_failed", failureCode);

        Assert.False(ProvisioningAcknowledgementStateRules.TryResolveOutcome(
            "Rejected", new string('x', 129), out _, out _));
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
