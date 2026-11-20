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
/// The authoritative routing answer for one logical scope: exactly one physical target,
/// named by its registry reference only (D-115 §2). This type deliberately carries no
/// credential — the connection string never leaves the <see cref="IDatabaseTargetRegistry"/>
/// implementation (see <see cref="IDatabaseConnectionSecrets"/>) — so <see cref="ToString"/>,
/// JSON serialization, structured-logging destructuring, audit events, error messages, test
/// failure messages and API responses cannot leak it, and public routing consumers have no
/// way to obtain it at all.
/// </summary>
public sealed record DatabaseTarget(string DatabaseId);

/// <summary>
/// The restricted access path to the connection strings held by the registry (D-115 §3).
/// Deliberately <c>internal</c> and implemented explicitly on
/// <see cref="ConfigurationDatabaseTargetRegistry"/>: the secret is reachable only from the
/// assembly that actually opens the connection — the <c>Data.DatabaseContext</c> factory in
/// <c>Infrastructure.Persistence</c>, granted visibility through <c>InternalsVisibleTo</c>
/// (see Server.csproj) — never through the public routing API. Stated C# limit: visibility
/// is enforced per assembly, not per call site, so code compiled inside the Server assembly
/// would also see internals; keeping production consumers out of it is a code-review rule.
/// </summary>
internal interface IDatabaseConnectionSecrets
{
	/// <summary>
	/// Returns the connection string of the referenced target, or <c>false</c> when the
	/// reference is unknown or its configured value is missing/empty. Never throws, never
	/// logs, and the out value stays empty on failure.
	/// </summary>
	bool TryGetConnectionString(string databaseId, out string connectionString);
}

/// <summary>
/// Strict topology-name parsing (D-111 §2 names, case-insensitive; the exact numeric value
/// of a defined member is also accepted because the configuration is server-side and
/// owner-controlled). Plain <c>Enum.TryParse</c> would accept undefined numeric strings
/// such as <c>999</c> — <see cref="Enum.IsDefined{TEnum}(TEnum)"/> rejects them so no
/// unknown topology can become a valid routing target (fail closed, D-115 §2). The pending
/// <c>TenantFiscalPeriodDatabase</c> kind parses as defined; callers reject it with their
/// own distinct <c>topology_kind_pending_owner_decision</c> code (D-115 §8).
/// </summary>
internal static class DatabaseTopologyKindParser
{
	public static bool TryParse(string? value, out DatabaseTopologyKind kind)
	{
		kind = default;
		return Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);
	}
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
