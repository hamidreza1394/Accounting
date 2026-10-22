namespace Domain;

/// <summary>
/// An ownership relationship as the rules see it: either a stored one or a candidate.
/// </summary>
public sealed record OwnershipFact(
	Guid ParentCompanyId,
	Guid SubsidiaryCompanyId,
	decimal OwnershipPercentage,
	decimal VotingPercentage,
	ControlBasis ControlBasis,
	DateOnly ValidFrom,
	DateOnly? ValidTo);

/// <summary>
/// The reason a membership or an ownership relationship is rejected.
/// </summary>
public enum CorporateGroupRuleViolation
{
	InvalidPeriod = 1,
	InvalidMemberRole,
	OverlappingMembership,
	SameCompany,
	InvalidPercentage,
	ZeroShareWithoutControl,
	InvalidControlBasis,
	OverlappingRelationship,
	OwnershipExceedsHundredPercent,
	VotingExceedsHundredPercent,
	MultipleControllingParents,
	ControlCycle
}

/// <summary>
/// A company that is a member of a group, as the scope resolution sees it.
/// </summary>
public sealed record GroupMembershipFact(
	Guid CompanyId,
	CorporateGroupMemberRole MemberRole,
	DateOnly ValidFrom,
	DateOnly? ValidTo);

/// <summary>
/// A fiscal period of a company, as the scope resolution sees it.
/// </summary>
public sealed record FiscalPeriodFact(
	Guid FiscalPeriodId,
	Guid CompanyId,
	DateOnly StartDate,
	DateOnly EndDate,
	FiscalPeriodStatus Status);

/// <summary>A company in the resolved scope of a group report run.</summary>
public sealed record ResolvedScopeCompany(
	Guid CompanyId,
	Guid FiscalPeriodId,
	CorporateGroupMemberRole MemberRole);

/// <summary>
/// The result of <see cref="CorporateGroupRules.ResolveScope" />: either the companies of the
/// scope or the machine readable reason why no scope exists.
/// </summary>
public sealed record ScopeResolution(
	IReadOnlyList<ResolvedScopeCompany> Companies,
	string? FailureCode)
{
	public bool Succeeded => FailureCode is null;
}

/// <summary>
/// The rules of the corporate group model (D-033). Pure functions: no database, no clock,
/// so the same rules are tested in isolation and applied by the server-side services.
/// </summary>
public static class CorporateGroupRules
{
	public const decimal MaximumPercentage = 100m;

	/// <summary>The number of decimals of a stored percentage (decimal(9,6)).</summary>
	public const int PercentageScale = 6;

	public const string FailureEmptyScope = "EmptyScope";
	public const string FailurePartialMembership = "PartialMembershipInPeriod";
	public const string FailureNoCoveringFiscalPeriod = "NoCoveringFiscalPeriod";
	public const string FailureAmbiguousFiscalPeriod = "AmbiguousFiscalPeriod";

	/// <summary>A period is valid when it has no end or ends on or after its start.</summary>
	public static bool IsValidPeriod(DateOnly validFrom, DateOnly? validTo) =>
		validTo is null || validTo.Value >= validFrom;

	/// <summary>Two inclusive periods overlap when they share at least one day.</summary>
	public static bool Overlaps(DateOnly firstFrom, DateOnly? firstTo, DateOnly secondFrom, DateOnly? secondTo) =>
		(firstTo is null || firstTo.Value >= secondFrom) &&
		(secondTo is null || secondTo.Value >= firstFrom);

	/// <summary>The period [validFrom, validTo] contains every day of [start, end].</summary>
	public static bool Covers(DateOnly validFrom, DateOnly? validTo, DateOnly start, DateOnly end) =>
		validFrom <= start && (validTo is null || validTo.Value >= end);

	/// <summary>The period [validFrom, validTo] contains the day.</summary>
	public static bool Contains(DateOnly validFrom, DateOnly? validTo, DateOnly day) =>
		validFrom <= day && (validTo is null || validTo.Value >= day);

	/// <summary>0..100 with at most <see cref="PercentageScale" /> decimals.</summary>
	public static bool IsValidPercentage(decimal value) =>
		value >= 0m &&
		value <= MaximumPercentage &&
		decimal.Round(value, PercentageScale) == value;

	/// <summary>
	/// Validates a new membership of a company in a group against the other memberships
	/// of the same company in the same group.
	/// </summary>
	public static CorporateGroupRuleViolation? ValidateMembership(
		GroupMembershipFact candidate,
		IEnumerable<GroupMembershipFact> sameCompanyMemberships)
	{
		ArgumentNullException.ThrowIfNull(candidate);
		ArgumentNullException.ThrowIfNull(sameCompanyMemberships);

		if (!IsValidPeriod(candidate.ValidFrom, candidate.ValidTo))
		{
			return CorporateGroupRuleViolation.InvalidPeriod;
		}

		if (!Enum.IsDefined(candidate.MemberRole))
		{
			return CorporateGroupRuleViolation.InvalidMemberRole;
		}

		return sameCompanyMemberships.Any(existing =>
			existing.CompanyId == candidate.CompanyId &&
			Overlaps(existing.ValidFrom, existing.ValidTo, candidate.ValidFrom, candidate.ValidTo))
			? CorporateGroupRuleViolation.OverlappingMembership
			: null;
	}

	/// <summary>
	/// Validates a new ownership relationship against the stored relationships of the
	/// installation (the caller passes every relationship that may be relevant: those of
	/// the subsidiary and every control edge).
	/// </summary>
	/// <remarks>
	/// The totals and the control graph change only on the first day of a relationship, so
	/// the rules are evaluated on the first day of the candidate and on every first day of
	/// an existing relationship inside the period of the candidate. That is exact: every
	/// day of the period has the relationships of one of these days or a subset of them.
	/// </remarks>
	public static CorporateGroupRuleViolation? ValidateOwnership(
		OwnershipFact candidate,
		IReadOnlyCollection<OwnershipFact> existing)
	{
		ArgumentNullException.ThrowIfNull(candidate);
		ArgumentNullException.ThrowIfNull(existing);

		if (candidate.ParentCompanyId == candidate.SubsidiaryCompanyId)
		{
			return CorporateGroupRuleViolation.SameCompany;
		}

		if (!IsValidPeriod(candidate.ValidFrom, candidate.ValidTo))
		{
			return CorporateGroupRuleViolation.InvalidPeriod;
		}

		if (!Enum.IsDefined(candidate.ControlBasis))
		{
			return CorporateGroupRuleViolation.InvalidControlBasis;
		}

		if (!IsValidPercentage(candidate.OwnershipPercentage) || !IsValidPercentage(candidate.VotingPercentage))
		{
			return CorporateGroupRuleViolation.InvalidPercentage;
		}

		if (candidate.OwnershipPercentage == 0m &&
			candidate.VotingPercentage == 0m &&
			!IsControl(candidate.ControlBasis))
		{
			return CorporateGroupRuleViolation.ZeroShareWithoutControl;
		}

		var overlapping = existing
			.Where(fact => Overlaps(fact.ValidFrom, fact.ValidTo, candidate.ValidFrom, candidate.ValidTo))
			.ToList();

		if (overlapping.Any(fact =>
			fact.ParentCompanyId == candidate.ParentCompanyId &&
			fact.SubsidiaryCompanyId == candidate.SubsidiaryCompanyId))
		{
			return CorporateGroupRuleViolation.OverlappingRelationship;
		}

		var criticalDays = overlapping
			.Select(fact => fact.ValidFrom)
			.Where(day => Contains(candidate.ValidFrom, candidate.ValidTo, day))
			.Append(candidate.ValidFrom)
			.Distinct()
			.Order();

		foreach (var day in criticalDays)
		{
			var valid = overlapping
				.Where(fact => Contains(fact.ValidFrom, fact.ValidTo, day))
				.Append(candidate)
				.ToList();

			var owners = valid.Where(fact => fact.SubsidiaryCompanyId == candidate.SubsidiaryCompanyId).ToList();

			if (owners.Sum(fact => fact.OwnershipPercentage) > MaximumPercentage)
			{
				return CorporateGroupRuleViolation.OwnershipExceedsHundredPercent;
			}

			if (owners.Sum(fact => fact.VotingPercentage) > MaximumPercentage)
			{
				return CorporateGroupRuleViolation.VotingExceedsHundredPercent;
			}

			if (owners.Count(fact => fact.ControlBasis == ControlBasis.Control) > 1)
			{
				return CorporateGroupRuleViolation.MultipleControllingParents;
			}

			if (IsControl(candidate.ControlBasis) &&
				ReachesThroughControl(valid, from: candidate.SubsidiaryCompanyId, to: candidate.ParentCompanyId))
			{
				return CorporateGroupRuleViolation.ControlCycle;
			}
		}

		return null;
	}

	/// <summary>
	/// Resolves the companies and fiscal periods of a group report for a reporting period
	/// with the <see cref="FiscalPeriodMappingPolicy.RequireCoveringPeriod" /> policy.
	/// </summary>
	/// <remarks>
	/// A company whose membership overlaps the period must be a member for the whole
	/// period (one membership, or consecutive memberships without a gap, for example after
	/// a change of role; the role in force on the last day is recorded), and it needs
	/// exactly one fiscal period that is not archived and covers the whole period. Anything
	/// else fails the whole run: a partial scope would be a misleading group report (D-034).
	/// </remarks>
	public static ScopeResolution ResolveScope(
		DateOnly periodStart,
		DateOnly periodEnd,
		IEnumerable<GroupMembershipFact> memberships,
		IEnumerable<FiscalPeriodFact> fiscalPeriods)
	{
		ArgumentNullException.ThrowIfNull(memberships);
		ArgumentNullException.ThrowIfNull(fiscalPeriods);

		if (periodEnd < periodStart)
		{
			throw new ArgumentException("The reporting period ends before it starts.", nameof(periodEnd));
		}

		var relevant = memberships
			.Where(membership => Overlaps(membership.ValidFrom, membership.ValidTo, periodStart, periodEnd))
			.ToList();

		if (relevant.Count == 0)
		{
			return new ScopeResolution([], FailureEmptyScope);
		}

		var periodsByCompany = fiscalPeriods.ToLookup(period => period.CompanyId);
		var companies = new List<ResolvedScopeCompany>();

		foreach (var company in relevant.GroupBy(membership => membership.CompanyId).OrderBy(group => group.Key))
		{
			var ordered = company.OrderBy(membership => membership.ValidFrom).ToList();
			if (!CoversWithoutGap(ordered, periodStart, periodEnd))
			{
				return new ScopeResolution([], FailurePartialMembership);
			}

			var membership = ordered.Last(candidate => candidate.ValidFrom <= periodEnd);

			var covering = periodsByCompany[membership.CompanyId]
				.Where(period => period.Status != FiscalPeriodStatus.Archived)
				.Where(period => period.StartDate <= periodStart && period.EndDate >= periodEnd)
				.ToList();

			if (covering.Count == 0)
			{
				return new ScopeResolution([], FailureNoCoveringFiscalPeriod);
			}

			if (covering.Count > 1)
			{
				return new ScopeResolution([], FailureAmbiguousFiscalPeriod);
			}

			companies.Add(new ResolvedScopeCompany(membership.CompanyId, covering[0].FiscalPeriodId, membership.MemberRole));
		}

		return new ScopeResolution(companies, null);
	}

	/// <summary>
	/// The memberships (ordered by start) cover every day of [start, end]: the first one
	/// starts on or before <paramref name="start" /> and each next one starts at most one day
	/// after the previous one ends.
	/// </summary>
	private static bool CoversWithoutGap(IReadOnlyList<GroupMembershipFact> ordered, DateOnly start, DateOnly end)
	{
		if (ordered.Count == 0 || ordered[0].ValidFrom > start)
		{
			return false;
		}

		var coveredUntil = ordered[0].ValidTo;
		foreach (var membership in ordered.Skip(1))
		{
			if (coveredUntil is null || coveredUntil.Value >= end)
			{
				break;
			}

			if (membership.ValidFrom > coveredUntil.Value.AddDays(1))
			{
				return false;
			}

			coveredUntil = membership.ValidTo is null || membership.ValidTo.Value > coveredUntil.Value
				? membership.ValidTo
				: coveredUntil;
		}

		return coveredUntil is null || coveredUntil.Value >= end;
	}

	private static bool IsControl(ControlBasis basis) =>
		basis is ControlBasis.Control or ControlBasis.JointControl;

	private static bool ReachesThroughControl(IReadOnlyCollection<OwnershipFact> facts, Guid from, Guid to)
	{
		var edges = facts
			.Where(fact => IsControl(fact.ControlBasis))
			.ToLookup(fact => fact.ParentCompanyId, fact => fact.SubsidiaryCompanyId);

		var visited = new HashSet<Guid>();
		var stack = new Stack<Guid>();
		stack.Push(from);

		while (stack.Count > 0)
		{
			var company = stack.Pop();
			if (company == to)
			{
				return true;
			}

			if (!visited.Add(company))
			{
				continue;
			}

			foreach (var next in edges[company])
			{
				stack.Push(next);
			}
		}

		return false;
	}
}
