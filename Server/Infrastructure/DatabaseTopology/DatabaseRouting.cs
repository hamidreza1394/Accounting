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
/// <see cref="IsCoherent"/> is a structural consistency check only (no empty identifiers).
/// It cannot and must not be read as a proof of real ownership: whether a FiscalPeriod
/// really belongs to the scope's Company (D-114) is validated server-side against the
/// authoritative data model during the operational wiring step (D-115 §7). The registry's
/// full-identity matching keeps a scope from reaching another scope's database, but only
/// the authoritative source proves ownership.
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
/// (D-115 §2). The connection string is a secret (D-115 §3): it is stored in a private
/// field and is only handed out through <see cref="GetConnectionString"/> for the layer
/// that actually opens the connection — so it cannot leak through a public property,
/// <see cref="ToString"/>, JSON serialization, structured-logging destructuring, audit
/// events, error messages or API responses.
/// </summary>
public sealed class DatabaseTarget
{
	private readonly string _connectionString;

	public DatabaseTarget(string databaseId, string connectionString)
	{
		DatabaseId = databaseId;
		_connectionString = connectionString;
	}

	/// <summary>The registry reference of the target database (not a credential).</summary>
	public string DatabaseId { get; }

	/// <summary>
	/// The only way to obtain the connection string; callers outside the connection layer
	/// must not use it. The value never appears in <see cref="ToString"/>, logs or errors.
	/// </summary>
	public string GetConnectionString() => _connectionString;

	/// <summary>
	/// The default textual representation is the identifier only, so an accidental
	/// <c>ToString()</c>, log line or exception message can never leak the secret.
	/// </summary>
	public override string ToString() => $"DatabaseId={DatabaseId}";

	public bool Equals(DatabaseTarget? other) =>
		other is not null && DatabaseId == other.DatabaseId && _connectionString == other._connectionString;

	public override bool Equals(object? obj) => obj is DatabaseTarget other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(DatabaseId, _connectionString);
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
	/// <c>topology_disabled</c>, <c>topology_kind_invalid</c>,
	/// <c>topology_kind_pending_owner_decision</c>, <c>scope_invalid</c>,
	/// <c>assignment_missing</c>, <c>assignment_conflict</c>, <c>connection_missing</c>.
	/// Failures never fall back to a default database.
	/// </summary>
	DatabaseTargetResolution Resolve(DatabaseScope scope);
}
