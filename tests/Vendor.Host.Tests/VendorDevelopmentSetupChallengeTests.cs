using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Contract of the setup-only operator challenge: the Host process prints a per-process code
/// on its own console (never over HTTP), the mutating endpoints require it, the value is
/// stored only as a hash, compared in constant time, and expires with the process session.
/// </summary>
public sealed class VendorDevelopmentSetupChallengeTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public void AdvanceTo(DateTimeOffset value) => _utcNow = value;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public void IssuedCodeValidatesAndIsStableWithinTheSession()
    {
        var time = new FixedTimeProvider(Base);
        var challenge = new VendorDevelopmentSetupChallenge(time);

        var code = challenge.Issue();
        Assert.False(string.IsNullOrWhiteSpace(code));

        // The same session keeps accepting the same code (repeat calls do not rotate it).
        Assert.Equal(code, challenge.Issue());
        Assert.True(challenge.TryValidate(code));
    }

    [Fact]
    public void WrongMissingOrEmptyCodesFailClosed()
    {
        var time = new FixedTimeProvider(Base);
        var challenge = new VendorDevelopmentSetupChallenge(time);
        var code = challenge.Issue();

        Assert.False(challenge.TryValidate("wrong-code"));
        Assert.False(challenge.TryValidate(null));
        Assert.False(challenge.TryValidate(""));
        Assert.False(challenge.TryValidate(" " + code));
        // Case sensitivity is deliberate: the console code is copied verbatim.
        Assert.False(challenge.TryValidate(code.ToLowerInvariant()));
        // Even a failed guess never reveals or rotates the code.
        Assert.Equal(code, challenge.Issue());
    }

    [Fact]
    public void ChallengeExpiresAndARestartPrintsAFreshCode()
    {
        var time = new FixedTimeProvider(Base);
        var challenge = new VendorDevelopmentSetupChallenge(time);
        var code = challenge.Issue();

        time.AdvanceTo(Base.AddHours(12).AddSeconds(1));
        Assert.False(challenge.TryValidate(code));

        // A fresh session (simulated restart with a new challenge instance) prints a new code.
        var restarted = new VendorDevelopmentSetupChallenge(time);
        var freshCode = restarted.Issue();
        Assert.NotEqual(code, freshCode);
        Assert.True(restarted.TryValidate(freshCode));
        Assert.False(restarted.TryValidate(code));
    }
}
