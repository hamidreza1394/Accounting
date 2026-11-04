using Xunit;

namespace Template.Tests;

/// <summary>
/// Pure delegation-ceiling rules (D-071): a company manager may only delegate permissions
/// inside the ceiling they themselves hold; everything else is reported and refused.
/// </summary>
public sealed class DelegationCeilingRulesTests
{
    [Fact]
    public void Allows_AcceptsACandidateInsideTheCeiling()
    {
        var ceiling = ToSet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var candidate = new[] { ceiling.First(), ceiling.Last() };

        var allowed = Infrastructure.Tenancy.DelegationCeilingRules.Allows(
            ceiling, candidate, out var exceeding);

        Assert.True(allowed);
        Assert.Empty(exceeding);
    }

    [Fact]
    public void Allows_ReportsEveryPermissionBeyondTheCeiling()
    {
        var inside = Guid.NewGuid();
        var ceiling = ToSet(inside);
        var firstOutside = Guid.NewGuid();
        var secondOutside = Guid.NewGuid();

        var allowed = Infrastructure.Tenancy.DelegationCeilingRules.Allows(
            ceiling, [inside, firstOutside, secondOutside], out var exceeding);

        Assert.False(allowed);
        Assert.Equal(2, exceeding.Count);
        Assert.Contains(firstOutside, exceeding);
        Assert.Contains(secondOutside, exceeding);
    }

    [Fact]
    public void Allows_AnEmptyCandidateNeverExceedsTheCeiling()
    {
        var allowed = Infrastructure.Tenancy.DelegationCeilingRules.Allows(
            ToSet(Guid.NewGuid()), Array.Empty<Guid>(), out var exceeding);

        Assert.True(allowed);
        Assert.Empty(exceeding);
    }

    [Fact]
    public void Allows_AnEmptyCeilingRefusesEveryCandidate()
    {
        var allowed = Infrastructure.Tenancy.DelegationCeilingRules.Allows(
            new HashSet<Guid>(), [Guid.NewGuid()], out _);

        Assert.False(allowed);
    }

    [Fact]
    public void AllowsCodes_ComparesCodesExactlyAndDistinctly()
    {
        var ceiling = new[] { "users.list", "roles.view" };

        var allowed = Infrastructure.Tenancy.DelegationCeilingRules.AllowsCodes(
            ceiling, ["roles.view", "users.list", "users.list"], out var exceeding);
        Assert.True(allowed);
        Assert.Empty(exceeding);

        var refused = Infrastructure.Tenancy.DelegationCeilingRules.AllowsCodes(
            ceiling, ["users.list", "trade.form.party.view"], out var exceedingCodes);
        Assert.False(refused);
        Assert.Equal(["trade.form.party.view"], exceedingCodes);
    }

    [Fact]
    public void AllowsCodes_DetectsCaseDifferencesAsBeyondTheCeiling()
    {
        // Codes are server-owned Ordinal strings; a case difference is not the same code.
        var refused = Infrastructure.Tenancy.DelegationCeilingRules.AllowsCodes(
            ["users.list"], ["Users.List"], out var exceeding);

        Assert.False(refused);
        Assert.Equal(["Users.List"], exceeding);
    }

    private static IReadOnlySet<Guid> ToSet(params Guid[] ids) => ids.ToHashSet();
}
