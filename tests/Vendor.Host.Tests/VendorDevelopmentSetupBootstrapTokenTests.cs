using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Contract of the single-use bootstrap token that protects the provisioning mutation in
/// setup-only mode: issued with a paired cookie value, consumed exactly once, expiring on
/// time, and failing closed on missing/mismatched values.
/// </summary>
public sealed class VendorDevelopmentSetupBootstrapTokenTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssuedPairConsumesExactlyOnce()
    {
        var issued = VendorDevelopmentSetupBootstrapToken.Issue(Base);

        Assert.False(string.IsNullOrWhiteSpace(issued.Token));
        Assert.False(string.IsNullOrWhiteSpace(issued.CookieValue));
        Assert.NotEqual(issued.Token, issued.CookieValue);

        Assert.True(VendorDevelopmentSetupBootstrapToken.TryConsume(issued.Token, issued.CookieValue, Base.AddMinutes(1)));
        // Replay: the same pair must not work twice.
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(issued.Token, issued.CookieValue, Base.AddMinutes(2)));
    }

    [Fact]
    public void MismatchedCookieOrMissingValuesFailClosed()
    {
        var issued = VendorDevelopmentSetupBootstrapToken.Issue(Base);

        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(issued.Token, "wrong-cookie", Base.AddMinutes(1)));
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume("wrong-token", issued.CookieValue, Base.AddMinutes(1)));
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(null, issued.CookieValue, Base.AddMinutes(1)));
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(issued.Token, null, Base.AddMinutes(1)));
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume("", "", Base.AddMinutes(1)));
        // A failed guess burns the issued pair as well.
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(issued.Token, issued.CookieValue, Base.AddMinutes(2)));
    }

    [Fact]
    public void TokensExpireAfterTheirShortLifetime()
    {
        var issued = VendorDevelopmentSetupBootstrapToken.Issue(Base);

        Assert.True(VendorDevelopmentSetupBootstrapToken.TryConsume(issued.Token, issued.CookieValue, Base.AddMinutes(14)));
        var second = VendorDevelopmentSetupBootstrapToken.Issue(Base);
        Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(second.Token, second.CookieValue, Base.AddMinutes(16)));
    }

    [Fact]
    public void IssuanceNeverExceedsTheOutstandingCap()
    {
        // The cap exists so a flood of page loads cannot grow the store unbounded; issuing
        // more pairs than the cap must still succeed (oldest is replaced) and every value
        // stays consumable exactly once.
        var issued = new List<VendorDevelopmentSetupBootstrapToken.BootstrapToken>();
        for (var index = 0; index < 40; index++)
        {
            issued.Add(VendorDevelopmentSetupBootstrapToken.Issue(Base.AddSeconds(index)));
        }

        var last = issued[^1];
        Assert.True(VendorDevelopmentSetupBootstrapToken.TryConsume(last.Token, last.CookieValue, Base.AddMinutes(1)));

        // Older pairs beyond the cap may have been evicted; each surviving pair still consumes once.
        var consumed = 0;
        foreach (var pair in issued.Take(39))
        {
            if (VendorDevelopmentSetupBootstrapToken.TryConsume(pair.Token, pair.CookieValue, Base.AddMinutes(1)))
            {
                consumed++;
                Assert.False(VendorDevelopmentSetupBootstrapToken.TryConsume(pair.Token, pair.CookieValue, Base.AddMinutes(1)));
            }
        }

        Assert.True(consumed <= 16, $"at most the capped number of pairs survive, got {consumed}");
    }
}
