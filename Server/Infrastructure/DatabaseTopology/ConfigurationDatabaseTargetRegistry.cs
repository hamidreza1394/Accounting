using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.DatabaseTopology;

/// <summary>
/// The configuration-backed <see cref="IDatabaseTargetRegistry"/>: the single authoritative
/// server-side mapping from logical scopes to database targets (D-115 §1-3). It is also the
/// only holder of the connection strings: they leave it solely through the internal
/// <see cref="IDatabaseConnectionSecrets"/> path for the connection factory — the public
/// routing API answers with <see cref="DatabaseTarget"/> references only.
/// </summary>
/// <remarks>
/// <para>
/// Resolution matches the full identity of the scope against the assignments of the active
/// topology — a company is never routed by its identifier alone if the scope carries another
/// tenant boundary, so a spoofed or cross-tenant scope cannot reach a foreign database.
/// Matching keys per kind (D-111 §2): <c>TenantDatabase</c> → TenantId;
/// <c>CompanyDatabase</c> → TenantId+CompanyId; <c>CompanyFiscalPeriodDatabase</c> →
/// TenantId+CompanyId+FiscalPeriodId.
/// </para>
/// <para>
/// Exactly one assignment may match (D-115 §2): zero matches fail closed with
/// <c>assignment_missing</c> and more than one with <c>assignment_conflict</c>; there is no
/// fallback to a default database. The resolved connection string comes from the same secure
/// configuration source as the main connection string, is read through
/// <see cref="IDatabaseConnectionSecrets"/> only, and never appears in a log, exception
/// or API response (D-115 §3).
/// </para>
/// <para>
/// The <see cref="IOptions{TOptions}"/> snapshot is the deliberate lifetime choice (D-115 §6):
/// the registry is validated once at startup (<c>ValidateOnStart</c>) and binds once with
/// it — <c>IOptionsMonitor</c>/<c>IOptionsSnapshot</c> are deliberately not used and no
/// change listener is subscribed, so a configuration file reload can never retarget running
/// requests, concurrent requests always read one consistent configuration, and a destination
/// change happens only through the controlled, audited change path and a restart.
/// </para>
/// <para>
/// The <c>TenantFiscalPeriodDatabase</c> topology (D-111 §2, option 3) is a
/// <b>pending owner decision</b> (D-115 §8): it is never routable here — a configured
/// pending topology fails closed with <c>topology_kind_pending_owner_decision</c> — so it
/// can never be activated through configuration alone (D-115 §8).
/// </para>
/// </remarks>
public sealed class ConfigurationDatabaseTargetRegistry(IOptions<DatabaseTopologyOptions> options) :
	IDatabaseTargetRegistry, IDatabaseConnectionSecrets
{
	public DatabaseTargetResolution Resolve(DatabaseScope scope)
	{
		var current = options.Value;
		if (!current.Enabled)
		{
			return DatabaseTargetResolution.Fail("topology_disabled");
		}

		if (!DatabaseTopologyKindParser.TryParse(current.Topology, out var kind))
		{
			return DatabaseTargetResolution.Fail("topology_kind_invalid");
		}

		if (kind == DatabaseTopologyKind.TenantFiscalPeriodDatabase)
		{
			// Pending owner decision (D-111 §2 option 3, D-115 §8): structurally parsed but
			// never routable, so no configuration can activate it (fail closed).
			return DatabaseTargetResolution.Fail("topology_kind_pending_owner_decision");
		}

		if (scope is null || !scope.IsCoherent)
		{
			return DatabaseTargetResolution.Fail("scope_invalid");
		}

		var matches = current.Assignments.Where(assignment => Matches(kind, assignment, scope)).ToList();
		if (matches.Count == 0)
		{
			return DatabaseTargetResolution.Fail("assignment_missing");
		}

		if (matches.Count > 1)
		{
			return DatabaseTargetResolution.Fail("assignment_conflict");
		}

		var databaseId = matches[0].DatabaseId;
		if (!TryGetConnectionString(databaseId, out var connectionString))
		{
			return DatabaseTargetResolution.Fail("connection_missing");
		}

		// The safe answer carries the registry reference only; the secret stays here.
		return DatabaseTargetResolution.Ok(new DatabaseTarget(databaseId));
	}

	/// <summary>
	/// The only path to the held connection strings: the internal interface, so the future
	/// connection factory assembly (InternalsVisibleTo: Infrastructure.Persistence) can open
	/// the connection while public routing consumers cannot obtain the secret at all.
	/// </summary>
	bool IDatabaseConnectionSecrets.TryGetConnectionString(string databaseId, out string connectionString) =>
		TryGetConnectionString(databaseId, out connectionString);

	private bool TryGetConnectionString(string databaseId, out string connectionString)
	{
		connectionString = string.Empty;
		if (string.IsNullOrWhiteSpace(databaseId) ||
			!options.Value.Connections.TryGetValue(databaseId, out var value) ||
			string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		connectionString = value;
		return true;
	}

	private static bool Matches(DatabaseTopologyKind kind, DatabaseAssignmentOption assignment, DatabaseScope scope) =>
		kind switch
		{
			DatabaseTopologyKind.TenantDatabase =>
				assignment.TenantId == scope.TenantId,
			DatabaseTopologyKind.CompanyDatabase =>
				assignment.TenantId == scope.TenantId && assignment.CompanyId == scope.CompanyId,
			DatabaseTopologyKind.CompanyFiscalPeriodDatabase =>
				assignment.TenantId == scope.TenantId && assignment.CompanyId == scope.CompanyId &&
				assignment.FiscalPeriodId == scope.FiscalPeriodId,
			// The pending TenantFiscalPeriodDatabase kind never reaches this method: it is
			// rejected before the scope is even considered (fail closed, D-115 §8).
			_ => false,
		};
}

/// <summary>
/// Registration of the routing registry and its fail-fast configuration validation
/// (D-115 §1). The resolver is inert while <c>DatabaseTopology:Enabled</c> is false; the
/// operational switch of <c>Data.DatabaseContext</c> to it is a separate owner-approved
/// change, so current behavior is untouched.
/// </summary>
public static class DatabaseTopologyServiceCollectionExtensions
{
	public static IServiceCollection AddDatabaseTopologyResolution(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddOptions<DatabaseTopologyOptions>()
			.BindConfiguration(DatabaseTopologyOptions.SectionName)
			.ValidateOnStart();
		services.AddSingleton<IValidateOptions<DatabaseTopologyOptions>, DatabaseTopologyOptionsValidator>();
		services.AddSingleton<IDatabaseTargetRegistry, ConfigurationDatabaseTargetRegistry>();
		return services;
	}
}
