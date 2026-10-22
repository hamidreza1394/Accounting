namespace Domain;

/// <summary>
/// The membership of a company in a <see cref="CorporateGroup" /> for a validity period.
/// </summary>
/// <remarks>
/// Membership is not ownership (<see cref="OwnershipRelationship" />) and is not data access.
/// A closed membership is ended (<see cref="ValidTo" />), never deleted.
/// </remarks>
public sealed class CorporateGroupMember : Seedwork.Entity
{
	public Guid CorporateGroupId { get; set; }
	public CorporateGroup CorporateGroup { get; set; } = null!;
	public Guid CompanyId { get; set; }
	public Company Company { get; set; } = null!;
	public CorporateGroupMemberRole MemberRole { get; set; } = CorporateGroupMemberRole.Subsidiary;
	public DateOnly ValidFrom { get; set; }
	public DateOnly? ValidTo { get; set; }
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public DateTime? EndedAtUtc { get; set; }
	public Guid? EndedByUserId { get; set; }
}

public enum CorporateGroupMemberRole
{
	Parent = 1,
	Subsidiary = 2,
	Associate = 3,
	JointVenture = 4,
	ReportingOnly = 5
}
