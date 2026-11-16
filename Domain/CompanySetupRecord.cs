namespace Domain;

/// <summary>
/// Installation-scoped, append-only mapping of a vendor-signed company reference (provisioning
/// payload v5) to a provisioned company on this customer installation (Issue #392 / D-059).
/// One row is created together with the company inside the provisioning apply transaction.
/// </summary>
/// <remarks>
/// The unique binary-collated index on <see cref="CompanyReference"/> makes one reference map to
/// exactly one company per installation and keeps case-distinct references distinct, as required
/// by the vendor review (L-2). The unique index on <see cref="CompanyId"/> keeps the mapping
/// one-to-one with the company, and the foreign key is protected against accidental deletes.
/// </remarks>
public sealed class CompanySetupRecord : Seedwork.Entity
{
	/// <summary>The provisioned company this reference is bound to; one-to-one, delete-restricted.</summary>
	public Guid CompanyId { get; set; }

	/// <summary>The vendor-signed company reference taken verbatim from the signed payload.</summary>
	public string CompanyReference { get; set; } = string.Empty;

	/// <summary>The customer installation the package was bound to when it was applied.</summary>
	public string InstallationId { get; set; } = string.Empty;

	/// <summary>UTC timestamp recorded when the package was applied.</summary>
	public DateTime CreatedAtUtc { get; set; }

	public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
