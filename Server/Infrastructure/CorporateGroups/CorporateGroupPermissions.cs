namespace Infrastructure.CorporateGroups;

/// <summary>
/// The permission codes of the corporate group model (D-034). The catalog rows are
/// inserted by the migration <c>AddCorporateGroups</c>; assigning them to company roles is
/// a decision of each company administrator.
/// </summary>
public static class CorporateGroupPermissions
{
	/// <summary>See a group. Checked in the owner company.</summary>
	public const string View = "corporate-groups.view";

	/// <summary>Create a group and its consolidation profiles. Checked in the owner company.</summary>
	public const string Manage = "corporate-groups.manage";

	/// <summary>Add or end a membership. Checked in the owner company and in the member company.</summary>
	public const string ManageMembers = "corporate-groups.members.manage";

	/// <summary>Record an ownership relationship. Checked in the parent and in the subsidiary.</summary>
	public const string ManageOwnership = "corporate-groups.ownership.manage";

	/// <summary>Run an aggregate report. Checked in the owner company and every company of the scope.</summary>
	public const string ViewAggregateReport = "reporting.group-aggregate.view";

	/// <summary>Run a consolidated report. Checked in the owner company and every company of the scope.</summary>
	public const string RunConsolidatedReport = "reporting.group-consolidated.run";

	/// <summary>Approve consolidation adjustments. Catalog only; no adjustment exists yet.</summary>
	public const string ApproveAdjustments = "consolidation.adjustments.approve";

	/// <summary>Export a group report. Catalog only; no export exists yet.</summary>
	public const string ExportGroupReport = "reporting.group.export";
}
