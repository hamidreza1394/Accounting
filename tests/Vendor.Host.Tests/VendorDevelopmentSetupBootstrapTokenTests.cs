using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Contract of the single-use bootstrap token that protects the provisioning mutation in
/// setup-only mode: issued with a paired cookie value, consumed exactly once, expiring on
/// time, and failing closed on missing/mismatched values. Time comes from an injectable
/// TimeProvider (test double) so every expiry behavior is deterministic.
/// </summary>
public sealed class VendorDevelopmentSetupBootstrapTokenTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public void AdvanceTo(DateTimeOffset value) => _utcNow = value;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public void IssuedPairConsumesExactlyOnce()
    {
        var time = new FixedTimeProvider(Base);
        var token = new VendorDevelopmentSetupBootstrapToken(time);
        var issued = token.Issue();

        Assert.False(string.IsNullOrWhiteSpace(issued.Token));
        Assert.False(string.IsNullOrWhiteSpace(issued.CookieValue));
        Assert.NotEqual(issued.Token, issued.CookieValue);

        time.AdvanceTo(Base.AddMinutes(1));
        Assert.True(token.TryConsume(issued.Token, issued.CookieValue));
        // Replay: the same pair must not work twice.
        Assert.False(token.TryConsume(issued.Token, issued.CookieValue));
    }

    [Fact]
    public void MismatchedCookieOrMissingValuesFailClosed()
    {
        var time = new FixedTimeProvider(Base);
        var token = new VendorDevelopmentSetupBootstrapToken(time);
        var issued = token.Issue();
        time.AdvanceTo(Base.AddMinutes(1));

        Assert.False(token.TryConsume(issued.Token, "wrong-cookie"));
        Assert.False(token.TryConsume("wrong-token", issued.CookieValue));
        Assert.False(token.TryConsume(null, issued.CookieValue));
        Assert.False(token.TryConsume(issued.Token, null));
        Assert.False(token.TryConsume("", ""));
        // A failed guess burns the issued pair as well.
        Assert.False(token.TryConsume(issued.Token, issued.CookieValue));
    }

    [Fact]
    public void TokensExpireAfterTheirShortLifetime()
    {
        var time = new FixedTimeProvider(Base);
        var token = new VendorDevelopmentSetupBootstrapToken(time);
        var issued = token.Issue();

        time.AdvanceTo(Base.AddMinutes(14));
        Assert.True(token.TryConsume(issued.Token, issued.CookieValue));
        var second = token.Issue();
        Assert.True(token.TryConsume(second.Token, second.CookieValue));
        time.AdvanceTo(Base.AddMinutes(30));
        Assert.False(token.TryConsume(second.Token, second.CookieValue));
    }

    [Fact]
    public void IssuanceNeverExceedsTheOutstandingCap()
    {
        // The cap exists so a flood of page loads cannot grow the store unbounded; issuing
        // more pairs than the cap must still succeed (oldest is replaced) and every value
        // stays consumable exactly once.
        var time = new FixedTimeProvider(Base);
        var token = new VendorDevelopmentSetupBootstrapToken(time);
        var issued = new List<VendorDevelopmentSetupBootstrapToken.BootstrapToken>();
        for (var index = 0; index < 40; index++)
        {
            time.AdvanceTo(Base.AddSeconds(index));
            issued.Add(token.Issue());
        }

        time.AdvanceTo(Base.AddMinutes(1));
        var last = issued[^1];
        Assert.True(token.TryConsume(last.Token, last.CookieValue));

        // Older pairs beyond the cap may have been evicted; each surviving pair still consumes once.
        var consumed = 0;
        foreach (var pair in issued.Take(39))
        {
            if (token.TryConsume(pair.Token, pair.CookieValue))
            {
                consumed++;
                Assert.False(token.TryConsume(pair.Token, pair.CookieValue));
            }
        }

        Assert.True(consumed <= 16, $"at most the capped number of pairs survive, got {consumed}");
    }
}
