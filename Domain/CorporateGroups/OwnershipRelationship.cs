namespace Domain;

/// <summary>
/// A legal or economic ownership and control relationship between two companies for a
/// validity period. It is independent of any <see cref="CorporateGroup" /> (D-033).
/// </summary>
/// <remarks>
/// Ownership share, voting share and the basis of control are separate facts: a company
/// can be controlled with a minority share, or owned without control.
/// </remarks>
public sealed class OwnershipRelationship : Seedwork.Entity
{
	public Guid ParentCompanyId { get; set; }
	public Company ParentCompany { get; set; } = null!;
	public Guid SubsidiaryCompanyId { get; set; }
	public Company SubsidiaryCompany { get; set; } = null!;

	/// <summary>Share of the equity in percent (0..100, six decimals).</summary>
	public decimal OwnershipPercentage { get; set; }

	/// <summary>Share of the voting rights in percent (0..100, six decimals).</summary>
	public decimal VotingPercentage { get; set; }

	public ControlBasis ControlBasis { get; set; } = ControlBasis.Investment;
	public DateOnly ValidFrom { get; set; }
	public DateOnly? ValidTo { get; set; }
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
}

public enum ControlBasis
{
	/// <summary>The parent controls the subsidiary (majority of votes or by contract).</summary>
	Control = 1,

	/// <summary>The parent shares control with other parties by agreement.</summary>
	JointControl = 2,

	/// <summary>The parent has significant influence without control.</summary>
	SignificantInfluence = 3,

	/// <summary>A plain investment without control or significant influence.</summary>
	Investment = 4
}
