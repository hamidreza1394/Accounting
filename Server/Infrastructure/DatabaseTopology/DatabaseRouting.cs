namespace Infrastructure.DatabaseTopology;

/// <summary>
/// The physical layout an installation uses for its operational accounting data (D-111 §2).
/// The identifiers stay logical: the number or name of a physical database never changes the
/// Tenant/Company/FiscalPeriod model (D-087, D-114).
/// </summary>
public enum DatabaseTopologyKind
{
	TenantDatabase = 1,
	CompanyDatabase = 2,
	TenantFiscalPeriodDatabase = 3,
	CompanyFiscalPeriodDatabase = 4,
}

/// <summary>
/// The logical data scope an operation runs in, built only from server-side identifiers —
/// entities and the authoritative work context — never from client input (D-115 §1: the
/// destination is never accepted as a client parameter, header, claim or query value).
/// </summary>
/// <remarks>
/// <para><c>TenantId</c> is always present; <c>CompanyId</c> and <c>FiscalPeriodId</c> are
/// present when the operation is company- or period-scoped. While the legacy schema keeps
/// <c>TenantId == Company.Id</c> (D-103), callers pass the current identifiers and the
/// approved D-087 transition keeps this contract unchanged.</para>
/// <para>
/// A tenant+period scope without a company is coherent: in the Tenant + FiscalPeriod
/// topology (D-111 §2) the physical database stores several companies' data of one period,
/// so the routing key is TenantId+FiscalPeriodId while every read and write still enforces
/// the Company scope in queries — logical ownership stays with the owning Company (D-114 §5).
/// </para>
/// </remarks>
public sealed record DatabaseScope(Guid TenantId, Guid? CompanyId = null, Guid? FiscalPeriodId = null)
{
	/// <summary>True when the scope can be routed under any topology kind.</summary>
	public bool IsCoherent =>
		TenantId != Guid.Empty &&
		(CompanyId is null || CompanyId != Guid.Empty) &&
		(FiscalPeriodId is null || FiscalPeriodId != Guid.Empty);
}

/// <summary>
/// The authoritative routing answer for one logical scope: exactly one physical target
/// (D-115 §2). <see cref="ConnectionString"/> is a secret (D-115 §3).
/// </summary>
public sealed record DatabaseTarget(string DatabaseId, string ConnectionString)
{
	/// <summary>
	/// The default textual representation is the identifier only, so an accidental
	/// <c>ToString()</c>, log line or exception message can never leak the secret.
	/// </summary>
	public override string ToString() => $"DatabaseId={DatabaseId}";
}

/// <summary>Fail-closed routing outcome with a stable, non-sensitive error code (D-115 §2).</summary>
public sealed record DatabaseTargetResolution(bool Success, DatabaseTarget? Target, string? ErrorCode)
{
	public static DatabaseTargetResolution Ok(DatabaseTarget target) => new(true, target, null);

	public static DatabaseTargetResolution Fail(string errorCode) => new(false, null, errorCode);
}

/// <summary>
/// The server-side registry that owns the single authoritative mapping from logical scopes
/// to database targets (D-115 §1). It holds references, never credentials; connection
/// strings come only from the secure configuration source of the installation.
/// </summary>
public interface IDatabaseTargetRegistry
{
	/// <summary>
	/// Resolves the database target for the scope, or a failure with a stable error code:
	/// <c>topology_disabled</c>, <c>topology_kind_invalid</c>, <c>scope_invalid</c>,
	/// <c>scope_incomplete</c>, <c>assignment_missing</c>, <c>assignment_conflict</c>,
	/// <c>connection_missing</c>. Failures never fall back to a default database.
	/// </summary>
	DatabaseTargetResolution Resolve(DatabaseScope scope);
}
