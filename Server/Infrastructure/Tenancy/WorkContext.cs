namespace Infrastructure.Tenancy;

/// <summary>A fiscal period as offered to, or selected by, a signed-in user.</summary>
public sealed record FiscalPeriodInfo(
	Guid Id,
	string Title,
	DateOnly StartDate,
	DateOnly EndDate,
	Domain.FiscalPeriodStatus Status)
{
	/// <summary>Whether a user may enter this period at all (see <see cref="FiscalPeriodSelectionPolicy" />).</summary>
	public bool IsSelectable => FiscalPeriodSelectionPolicy.IsSelectable(Status);

	/// <summary>What the user may do inside this period; <c>null</c> when it cannot be entered.</summary>
	public WorkAccessMode? AccessMode => FiscalPeriodSelectionPolicy.GetAccessMode(Status);
}

/// <summary>What a working context allows (D-031, amended).</summary>
public enum WorkAccessMode
{
	/// <summary>An open period: viewing, reporting and the writes the user's permissions allow.</summary>
	ReadWrite = 1,

	/// <summary>A closed period: viewing and reporting only. Writes follow separate rules.</summary>
	ReadOnly = 2,
}

/// <summary>A company the user may work in, with the fiscal periods of that company.</summary>
public sealed record CompanyWithPeriods(
	CompanyAccess Company,
	IReadOnlyList<FiscalPeriodInfo> FiscalPeriods)
{
	public int SelectablePeriodCount => FiscalPeriods.Count(period => period.IsSelectable);
}

/// <summary>The verified working context of a session: one company and one of its fiscal periods.</summary>
public sealed record WorkContext(CompanyAccess Company, FiscalPeriodInfo FiscalPeriod)
{
	/// <summary>
	/// Whether this context may change data. Every future write operation must check this on the
	/// server; a read-only (closed) period is entered for viewing and reporting only.
	/// </summary>
	public bool AllowsWrites => FiscalPeriod.AccessMode == WorkAccessMode.ReadWrite;
}

/// <summary>What the client has to do after sign-in (decided by the server, never by the client).</summary>
public enum EntryMode
{
	/// <summary>Exactly one company with exactly one enterable period (open or closed): enter without asking.</summary>
	DirectEntry = 1,

	/// <summary>More than one company, or more than one selectable period: the user chooses.</summary>
	SelectionRequired = 2,

	/// <summary>The user has no company they may work in.</summary>
	NoCompany = 3,

	/// <summary>The user has companies, but none of them has an enterable fiscal period.</summary>
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
/// Which fiscal periods can be entered, and with what access (decision D-031, amended 2026-10-21).
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="Domain.FiscalPeriodStatus.Open" /> period is the normal way in: full access as far
/// as the user's permissions allow. A <see cref="Domain.FiscalPeriodStatus.Closed" /> period stays
/// available for viewing and reporting (<see cref="WorkAccessMode.ReadOnly" />); posting, editing,
/// voiding and the like follow their own rules and must check <see cref="WorkContext.AllowsWrites" />.
/// An <see cref="Domain.FiscalPeriodStatus.Archived" /> period cannot be entered until the product
/// decides how archived periods are viewed (<c>NeedsApproval</c>). The calendar date is deliberately
/// <em>not</em> used. Everything is kept in this single place.
/// </para>
/// </remarks>
public static class FiscalPeriodSelectionPolicy
{
	public static bool IsSelectable(Domain.FiscalPeriodStatus status)
	{
		return GetAccessMode(status) is not null;
	}

	public static WorkAccessMode? GetAccessMode(Domain.FiscalPeriodStatus status)
	{
		return status switch
		{
			Domain.FiscalPeriodStatus.Open => WorkAccessMode.ReadWrite,
			Domain.FiscalPeriodStatus.Closed => WorkAccessMode.ReadOnly,
			_ => null,
		};
	}
}
