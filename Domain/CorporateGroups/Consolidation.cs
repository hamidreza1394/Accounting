namespace Domain;

/// <summary>
/// The reporting policy of a <see cref="CorporateGroup" />.
/// </summary>
/// <remarks>
/// Only the policies that the code actually applies are stored. Intercompany elimination
/// has no column because no elimination engine exists yet (D-033, YAGNI).
/// </remarks>
public sealed class ConsolidationProfile : Seedwork.Entity,
	Dtat.Seedwork.Abstractions.IEntityHasIsActive
{
	public Guid CorporateGroupId { get; set; }
	public CorporateGroup CorporateGroup { get; set; } = null!;
	public string Name { get; set; } = string.Empty;
	public AccountingFramework AccountingFramework { get; set; } = AccountingFramework.IranianNationalStandards;

	/// <summary>ISO 4217 code of the presentation currency, for example IRR.</summary>
	public string ReportingCurrencyCode { get; set; } = "IRR";

	public FiscalPeriodMappingPolicy FiscalPeriodMappingPolicy { get; set; } = FiscalPeriodMappingPolicy.RequireCoveringPeriod;
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public ICollection<ConsolidationRun> Runs { get; set; } = new List<ConsolidationRun>();
}

/// <summary>
/// One requested run of a group report together with the companies and fiscal periods
/// that were in its scope. A run never contains financial figures in this version (D-034).
/// </summary>
public sealed class ConsolidationRun : Seedwork.Entity
{
	public Guid CorporateGroupId { get; set; }
	public CorporateGroup CorporateGroup { get; set; } = null!;
	public Guid? ConsolidationProfileId { get; set; }
	public ConsolidationProfile? ConsolidationProfile { get; set; }
	public GroupReportKind Kind { get; set; } = GroupReportKind.Aggregate;
	public DateOnly PeriodStart { get; set; }
	public DateOnly PeriodEnd { get; set; }
	public ConsolidationRunStatus Status { get; set; } = ConsolidationRunStatus.Pending;

	/// <summary>A machine readable reason of a failed run; never personal or financial data.</summary>
	public string? FailureCode { get; set; }

	public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
	public DateTime? CompletedAtUtc { get; set; }
	public Guid ExecutedByUserId { get; set; }
	public ICollection<ConsolidationRunCompany> Companies { get; set; } = new List<ConsolidationRunCompany>();
}

/// <summary>
/// A company in the scope of a <see cref="ConsolidationRun" /> and the fiscal period of
/// that company that covers the reporting period. A composite foreign key guarantees that
/// the fiscal period belongs to the same company.
/// </summary>
public sealed class ConsolidationRunCompany
{
	public Guid ConsolidationRunId { get; set; }
	public ConsolidationRun ConsolidationRun { get; set; } = null!;
	public Guid CompanyId { get; set; }
	public Company Company { get; set; } = null!;
	public Guid FiscalPeriodId { get; set; }
	public FiscalPeriod FiscalPeriod { get; set; } = null!;
	public CorporateGroupMemberRole MemberRole { get; set; }
}

public enum AccountingFramework
{
	IranianNationalStandards = 1,
	Ifrs = 2
}

public enum FiscalPeriodMappingPolicy
{
	/// <summary>
	/// Every company needs exactly one fiscal period (not archived) that covers the whole
	/// reporting period, and a membership that covers the whole reporting period.
	/// </summary>
	RequireCoveringPeriod = 1
}

public enum GroupReportKind
{
	Aggregate = 1,
	Consolidated = 2
}

public enum ConsolidationRunStatus
{
	Pending = 1,
	ScopeResolved = 2,

	/// <summary>Reserved for the reporting engine; not set by this version.</summary>
	Completed = 3,

	Failed = 4,
	Cancelled = 5
}
