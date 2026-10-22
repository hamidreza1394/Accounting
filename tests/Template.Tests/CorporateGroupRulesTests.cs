using System;
using System.Collections.Generic;
using Domain;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The pure rules of the corporate group model (D-033): periods, percentages, totals,
/// single control, control cycles and the scope of a report run.
/// </summary>
public sealed class CorporateGroupRulesTests
{
	private static readonly Guid A = Guid.NewGuid();
	private static readonly Guid B = Guid.NewGuid();
	private static readonly Guid C = Guid.NewGuid();
	private static readonly Guid D = Guid.NewGuid();

	private static readonly DateOnly Jan1 = new(2026, 1, 1);
	private static readonly DateOnly Jun30 = new(2026, 6, 30);
	private static readonly DateOnly Jul1 = new(2026, 7, 1);
	private static readonly DateOnly Dec31 = new(2026, 12, 31);

	private static OwnershipFact Fact(
		Guid parent,
		Guid subsidiary,
		decimal ownership,
		ControlBasis basis = ControlBasis.Control,
		DateOnly? from = null,
		DateOnly? to = null,
		decimal? voting = null) =>
		new(parent, subsidiary, ownership, voting ?? ownership, basis, from ?? Jan1, to);

	[Fact]
	public void Periods_Overlap_WhenTheyShareADay_AndAdjacentPeriodsDoNot()
	{
		Assert.True(CorporateGroupRules.Overlaps(Jan1, Jun30, Jun30, Dec31));
		Assert.False(CorporateGroupRules.Overlaps(Jan1, Jun30, Jul1, Dec31));
		Assert.True(CorporateGroupRules.Overlaps(Jan1, null, Jul1, Dec31));
		Assert.True(CorporateGroupRules.IsValidPeriod(Jan1, Jan1));
		Assert.False(CorporateGroupRules.IsValidPeriod(Jul1, Jun30));
	}

	[Theory]
	[InlineData("0", true)]
	[InlineData("100", true)]
	[InlineData("33.333333", true)]
	[InlineData("33.3333333", false)]
	[InlineData("-0.000001", false)]
	[InlineData("100.000001", false)]
	public void Percentages_AreZeroToHundred_WithSixDecimals(string value, bool valid)
	{
		Assert.Equal(valid, CorporateGroupRules.IsValidPercentage(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
	}

	[Fact]
	public void AMembership_ThatOverlapsAnotherOfTheSameCompany_IsRejected()
	{
		var existing = new[] { new GroupMembershipFact(A, CorporateGroupMemberRole.Subsidiary, Jan1, Jun30) };

		Assert.Equal(
			CorporateGroupRuleViolation.OverlappingMembership,
			CorporateGroupRules.ValidateMembership(new GroupMembershipFact(A, CorporateGroupMemberRole.Subsidiary, Jun30, null), existing));
		Assert.Null(CorporateGroupRules.ValidateMembership(new GroupMembershipFact(A, CorporateGroupMemberRole.Subsidiary, Jul1, null), existing));
		Assert.Null(CorporateGroupRules.ValidateMembership(new GroupMembershipFact(B, CorporateGroupMemberRole.Subsidiary, Jan1, null), existing));
	}

	[Fact]
	public void AMembership_WithAnInvalidPeriodOrRole_IsRejected()
	{
		Assert.Equal(
			CorporateGroupRuleViolation.InvalidPeriod,
			CorporateGroupRules.ValidateMembership(new GroupMembershipFact(A, CorporateGroupMemberRole.Parent, Jul1, Jun30), []));
		Assert.Equal(
			CorporateGroupRuleViolation.InvalidMemberRole,
			CorporateGroupRules.ValidateMembership(new GroupMembershipFact(A, (CorporateGroupMemberRole)99, Jan1, null), []));
	}

	[Fact]
	public void Ownership_OfACompanyInItself_IsRejected()
	{
		Assert.Equal(CorporateGroupRuleViolation.SameCompany, CorporateGroupRules.ValidateOwnership(Fact(A, A, 10), []));
	}

	[Fact]
	public void Ownership_WithInvalidPercentages_IsRejected()
	{
		Assert.Equal(CorporateGroupRuleViolation.InvalidPercentage, CorporateGroupRules.ValidateOwnership(Fact(A, B, 101), []));
		Assert.Equal(CorporateGroupRuleViolation.InvalidPercentage, CorporateGroupRules.ValidateOwnership(Fact(A, B, -1), []));
		Assert.Equal(CorporateGroupRuleViolation.InvalidControlBasis, CorporateGroupRules.ValidateOwnership(Fact(A, B, 10, (ControlBasis)9), []));
	}

	[Fact]
	public void ZeroShares_AreOnlyAllowed_ForContractualControl()
	{
		Assert.Equal(
			CorporateGroupRuleViolation.ZeroShareWithoutControl,
			CorporateGroupRules.ValidateOwnership(Fact(A, B, 0, ControlBasis.Investment), []));
		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(A, B, 0, ControlBasis.Control), []));
	}

	[Fact]
	public void OwnershipAndVoting_AreSeparateFacts()
	{
		// Control with a minority share and majority of the votes.
		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(A, B, 30, ControlBasis.Control, voting: 60), []));
	}

	[Fact]
	public void TheSamePairOfCompanies_CanNotHaveOverlappingRelationships_ButCanChangeOverTime()
	{
		var existing = new[] { Fact(A, B, 60, to: Jun30) };

		Assert.Equal(
			CorporateGroupRuleViolation.OverlappingRelationship,
			CorporateGroupRules.ValidateOwnership(Fact(A, B, 80, from: Jun30), existing));
		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(A, B, 80, from: Jul1), existing));
	}

	[Fact]
	public void TheOwnersOfACompany_CanNotHoldMoreThanHundredPercent_OnAnyDay()
	{
		var existing = new[] { Fact(A, C, 60, ControlBasis.Control), Fact(B, C, 30, ControlBasis.Investment, from: Jul1) };

		Assert.Equal(
			CorporateGroupRuleViolation.OwnershipExceedsHundredPercent,
			CorporateGroupRules.ValidateOwnership(Fact(D, C, 15, ControlBasis.Investment, from: new DateOnly(2026, 3, 1)), existing));

		// Before B's share starts (and ending before it) there is room for 40%.
		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(D, C, 40, ControlBasis.Investment, from: Jan1, to: Jun30), existing));
	}

	[Fact]
	public void TheVotesOfACompany_CanNotExceedHundredPercent()
	{
		var existing = new[] { Fact(A, C, 10, ControlBasis.Control, voting: 90) };

		Assert.Equal(
			CorporateGroupRuleViolation.VotingExceedsHundredPercent,
			CorporateGroupRules.ValidateOwnership(Fact(B, C, 5, ControlBasis.Investment, voting: 20), existing));
	}

	[Fact]
	public void ACompany_HasAtMostOneControllingParent_OnADay()
	{
		var existing = new[] { Fact(A, C, 51, ControlBasis.Control, to: Jun30) };

		Assert.Equal(
			CorporateGroupRuleViolation.MultipleControllingParents,
			CorporateGroupRules.ValidateOwnership(Fact(B, C, 0, ControlBasis.Control), existing));
		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(B, C, 49, ControlBasis.Control, from: Jul1), existing));
	}

	[Fact]
	public void AControlCycle_IsRejected()
	{
		var existing = new[] { Fact(A, B, 80), Fact(B, C, 70) };

		Assert.Equal(CorporateGroupRuleViolation.ControlCycle, CorporateGroupRules.ValidateOwnership(Fact(C, A, 51), existing));
		Assert.Equal(CorporateGroupRuleViolation.ControlCycle, CorporateGroupRules.ValidateOwnership(Fact(C, A, 20, ControlBasis.JointControl), existing));
	}

	[Fact]
	public void AMinorityCrossHolding_IsAllowed()
	{
		var existing = new[] { Fact(A, B, 80) };

		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(B, A, 5, ControlBasis.Investment), existing));
		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(B, A, 20, ControlBasis.SignificantInfluence), existing));
	}

	[Fact]
	public void ControlRelationships_ThatNeverCoexist_DoNotFormACycle()
	{
		var existing = new[] { Fact(A, B, 80, to: Jun30) };

		Assert.Null(CorporateGroupRules.ValidateOwnership(Fact(B, A, 60, from: Jul1), existing));
	}

	[Fact]
	public void TheScope_ContainsEveryMember_WithTheFiscalPeriodThatCoversTheReport()
	{
		var periodA = Guid.NewGuid();
		var periodB = Guid.NewGuid();
		var memberships = new[]
		{
			new GroupMembershipFact(A, CorporateGroupMemberRole.Parent, Jan1, null),
			new GroupMembershipFact(B, CorporateGroupMemberRole.Subsidiary, Jan1, Dec31),
			// C left the group before the reporting period: it is not in the scope.
			new GroupMembershipFact(C, CorporateGroupMemberRole.Subsidiary, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)),
		};
		var periods = new[]
		{
			new FiscalPeriodFact(periodA, A, Jan1, Dec31, FiscalPeriodStatus.Open),
			new FiscalPeriodFact(periodB, B, Jan1, Dec31, FiscalPeriodStatus.Closed),
			new FiscalPeriodFact(Guid.NewGuid(), C, Jan1, Dec31, FiscalPeriodStatus.Open),
		};

		var resolution = CorporateGroupRules.ResolveScope(Jan1, Jun30, memberships, periods);

		Assert.True(resolution.Succeeded);
		Assert.Equal(2, resolution.Companies.Count);
		Assert.Contains(resolution.Companies, company => company.CompanyId == A && company.FiscalPeriodId == periodA);
		Assert.Contains(resolution.Companies, company => company.CompanyId == B && company.FiscalPeriodId == periodB);
	}

	[Fact]
	public void TheScope_Fails_ForAPartialMembership_AMissingOrArchivedPeriod_OrAnAmbiguousPeriod()
	{
		var membership = new[] { new GroupMembershipFact(A, CorporateGroupMemberRole.Parent, Jan1, null) };

		Assert.Equal(
			CorporateGroupRules.FailureEmptyScope,
			CorporateGroupRules.ResolveScope(Jan1, Jun30, [], []).FailureCode);
		Assert.Equal(
			CorporateGroupRules.FailurePartialMembership,
			CorporateGroupRules.ResolveScope(Jan1, Jun30, [new GroupMembershipFact(A, CorporateGroupMemberRole.Parent, new DateOnly(2026, 3, 1), null)], [new FiscalPeriodFact(Guid.NewGuid(), A, Jan1, Dec31, FiscalPeriodStatus.Open)]).FailureCode);
		Assert.Equal(
			CorporateGroupRules.FailureNoCoveringFiscalPeriod,
			CorporateGroupRules.ResolveScope(Jan1, Dec31, membership, [new FiscalPeriodFact(Guid.NewGuid(), A, Jan1, Jun30, FiscalPeriodStatus.Open)]).FailureCode);
		Assert.Equal(
			CorporateGroupRules.FailureNoCoveringFiscalPeriod,
			CorporateGroupRules.ResolveScope(Jan1, Jun30, membership, [new FiscalPeriodFact(Guid.NewGuid(), A, Jan1, Dec31, FiscalPeriodStatus.Archived)]).FailureCode);
		Assert.Equal(
			CorporateGroupRules.FailureAmbiguousFiscalPeriod,
			CorporateGroupRules.ResolveScope(Jan1, Jun30, membership,
			[
				new FiscalPeriodFact(Guid.NewGuid(), A, Jan1, Dec31, FiscalPeriodStatus.Open),
				new FiscalPeriodFact(Guid.NewGuid(), A, Jan1, Jun30, FiscalPeriodStatus.Open),
			]).FailureCode);
	}

	[Fact]
	public void ConsecutiveMemberships_CoverTheReport_AndTheLastRoleIsRecorded_ButAGapFails()
	{
		var period = new FiscalPeriodFact(Guid.NewGuid(), A, Jan1, Dec31, FiscalPeriodStatus.Open);
		var consecutive = new[]
		{
			new GroupMembershipFact(A, CorporateGroupMemberRole.Subsidiary, Jan1, Jun30),
			new GroupMembershipFact(A, CorporateGroupMemberRole.Associate, Jul1, null),
		};

		var resolution = CorporateGroupRules.ResolveScope(Jan1, Dec31, consecutive, [period]);

		Assert.True(resolution.Succeeded, resolution.FailureCode);
		var company = Assert.Single(resolution.Companies);
		Assert.Equal(CorporateGroupMemberRole.Associate, company.MemberRole);

		var withGap = new[]
		{
			new GroupMembershipFact(A, CorporateGroupMemberRole.Subsidiary, Jan1, Jun30),
			new GroupMembershipFact(A, CorporateGroupMemberRole.Associate, new DateOnly(2026, 7, 2), null),
		};

		Assert.Equal(
			CorporateGroupRules.FailurePartialMembership,
			CorporateGroupRules.ResolveScope(Jan1, Dec31, withGap, [period]).FailureCode);
	}

	[Fact]
	public void TheScope_DoesNotUseAFiscalPeriod_OfAnotherCompany()
	{
		var resolution = CorporateGroupRules.ResolveScope(
			Jan1,
			Jun30,
			[new GroupMembershipFact(A, CorporateGroupMemberRole.Parent, Jan1, null)],
			[new FiscalPeriodFact(Guid.NewGuid(), B, Jan1, Dec31, FiscalPeriodStatus.Open)]);

		Assert.Equal(CorporateGroupRules.FailureNoCoveringFiscalPeriod, resolution.FailureCode);
	}
}
