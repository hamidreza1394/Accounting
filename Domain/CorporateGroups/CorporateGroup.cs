namespace Domain;

/// <summary>
/// An economic or reporting group of companies of one installation (D-033).
/// </summary>
/// <remarks>
/// <see cref="OwnerCompanyId" /> is the company in which the permissions of the group are
/// checked (usually the holding). It is an administrative owner only: it grants no access
/// to the data of the member companies (D-034).
/// </remarks>
public sealed class CorporateGroup : Seedwork.Entity,
	Dtat.Seedwork.Abstractions.IEntityHasIsActive
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public Guid OwnerCompanyId { get; set; }
	public Company OwnerCompany { get; set; } = null!;
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public DateTime? UpdatedAtUtc { get; set; }
	public ICollection<CorporateGroupMember> Members { get; set; } = new List<CorporateGroupMember>();
	public ICollection<ConsolidationProfile> Profiles { get; set; } = new List<ConsolidationProfile>();
	public ICollection<ConsolidationRun> Runs { get; set; } = new List<ConsolidationRun>();
}
