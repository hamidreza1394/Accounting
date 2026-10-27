using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorBusinessMutationRulesTests
{
    [Fact]
    public void TryDecodeRowVersion_accepts_exactly_eight_bytes()
    {
        var expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var valid = VendorBusinessMutationRules.TryDecodeRowVersion(Convert.ToBase64String(expected), out var actual);

        Assert.True(valid);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AQID")]
    [InlineData("AQIDBAUGBwgJCg==")]
    public void TryDecodeRowVersion_rejects_missing_malformed_or_wrong_length_values(string? value)
    {
        Assert.False(VendorBusinessMutationRules.TryDecodeRowVersion(value, out var actual));
        Assert.Empty(actual);
    }

    [Theory]
    [InlineData("Active", "Suspended", true)]
    [InlineData("Suspended", "Active", true)]
    [InlineData("Active", "Active", false)]
    [InlineData("Draft", "Active", false)]
    [InlineData("Active", "Revoked", false)]
    [InlineData(null, "Active", false)]
    public void Operational_status_transitions_are_explicit(string? current, string? target, bool expected) =>
        Assert.Equal(expected, VendorBusinessMutationRules.IsOperationalStatusTransitionAllowed(current, target));

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Active", false)]
    [InlineData("Suspended", false)]
    [InlineData(null, false)]
    public void Only_draft_records_are_editable_for_contract_and_license(string? status, bool expected) =>
        Assert.Equal(expected, VendorBusinessMutationRules.IsDraft(status));

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", true)]
    [InlineData("production", false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void Environment_values_are_allow_listed(string? value, bool expected) =>
        Assert.Equal(expected, VendorBusinessMutationRules.IsSupportedEnvironment(value));
}
