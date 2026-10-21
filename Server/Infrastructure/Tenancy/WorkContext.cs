namespace Infrastructure.Tenancy;

/// <summary>A fiscal period as offered to, or selected by, a signed-in user.</summary>
public sealed record FiscalPeriodInfo(
	Guid Id,
	string Title,
	DateOnly StartDate,
	DateOnly EndDate,
	Domain.FiscalPeriodStatus Status)
{
	/// <summary>Whether a user may start working in this period (see <see cref="FiscalPeriodSelectionPolicy" />).</summary>
	public bool IsSelectable => FiscalPeriodSelectionPolicy.IsSelectable(Status);
}

/// <summary>A company the user may work in, with the fiscal periods of that company.</summary>
public sealed record CompanyWithPeriods(
	CompanyAccess Company,
	IReadOnlyList<FiscalPeriodInfo> FiscalPeriods)
{
	public int SelectablePeriodCount => FiscalPeriods.Count(period => period.IsSelectable);
}

/// <summary>The verified working context of a session: one company and one of its fiscal periods.</summary>
public sealed record WorkContext(CompanyAccess Company, FiscalPeriodInfo FiscalPeriod);

/// <summary>What the client has to do after sign-in (decided by the server, never by the client).</summary>
public enum EntryMode
{
	/// <summary>Exactly one company with exactly one selectable period: enter without asking.</summary>
	DirectEntry = 1,

	/// <summary>More than one company, or more than one selectable period: the user chooses.</summary>
	SelectionRequired = 2,

	/// <summary>The user has no company they may work in.</summary>
	NoCompany = 3,

	/// <summary>The user has companies, but none of them has a selectable fiscal period.</summary>
	NoEligiblePeriod = 4,
}

public sealed record WorkContextOptions(
	IReadOnlyList<CompanyWithPeriods> Companies,
	EntryMode Entry,
	Guid? DirectCompanyId,
	Guid? DirectFiscalPeriodId);

public enum SelectionOutcome
{
	Selected = 1,

	/// <summary>The company does not exist, belongs to someone else, or the membership is not active.</summary>
	CompanyNotAccessible = 2,

	/// <summary>The period does not exist or does not belong to the verified company.</summary>
	PeriodNotFound = 3,

	/// <summary>The period exists in the company but cannot be worked in (for example it is closed).</summary>
	PeriodNotSelectable = 4,
}

public sealed record SelectionResult(SelectionOutcome Outcome, WorkContext? Context);

/// <summary>
/// Which fiscal periods can be chosen to start working.
/// </summary>
/// <remarks>
/// <para>
/// The product has not yet decided how closed, archived or future periods behave (decision
/// D-031 records this as <c>NeedsApproval</c>). Until then the conservative rule applies and is
/// kept in this single place: only a period whose status is <see cref="Domain.FiscalPeriodStatus.Open" />
/// can be chosen. The calendar date is deliberately <em>not</em> used: whether today lies inside
/// the period does not decide whether bookkeeping in it is allowed.
/// </para>
/// </remarks>
public static class FiscalPeriodSelectionPolicy
{
	public static bool IsSelectable(Domain.FiscalPeriodStatus status)
	{
		return status == Domain.FiscalPeriodStatus.Open;
	}
}
