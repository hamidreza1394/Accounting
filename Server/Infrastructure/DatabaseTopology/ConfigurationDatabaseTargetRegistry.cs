using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.DatabaseTopology;

/// <summary>
/// The configuration-backed <see cref="IDatabaseTargetRegistry"/>: the single authoritative
/// server-side mapping from logical scopes to database targets (D-115 §1-3).
/// </summary>
/// <remarks>
/// <para>
/// Resolution matches the full identity of the scope against the assignments of the active
/// topology — a company is never routed by its identifier alone if the scope carries another
/// tenant boundary, so a spoofed or cross-tenant scope cannot reach a foreign database.
/// Matching keys per kind (D-111 §2): <c>TenantDatabase</c> → TenantId;
/// <c>CompanyDatabase</c> → TenantId+CompanyId; <c>TenantFiscalPeriodDatabase</c> →
/// TenantId+FiscalPeriodId; <c>CompanyFiscalPeriodDatabase</c> → TenantId+CompanyId+FiscalPeriodId.
/// </para>
/// <para>
/// Exactly one assignment may match (D-115 §2): zero matches fail closed with
/// <c>assignment_missing</c> and more than one with <c>assignment_conflict</c>; there is no
/// fallback to a default database. The resolved connection string comes from the same secure
/// configuration source as the main connection string and never appears in a log, exception
/// or API response (D-115 §3; <see cref="DatabaseTarget"/> keeps it private and
/// <c>ToString()</c> stays identifier-only).
/// </para>
/// <para>
/// The <c>TenantFiscalPeriodDatabase</c> topology (D-111 §2, option 3) is a
/// <b>pending owner decision</b> (D-115 §8): it is never routable here — a configured
/// pending topology fails closed with <c>topology_kind_pending_owner_decision</c> — so it
/// can never be activated through configuration alone (D-115 §8).
/// </para>
/// </remarks>
public sealed class ConfigurationDatabaseTargetRegistry(IOptionsMonitor<DatabaseTopologyOptions> options) :
	IDatabaseTargetRegistry
{
	public DatabaseTargetResolution Resolve(DatabaseScope scope)
	{
		var current = options.CurrentValue;
		if (!current.Enabled)
		{
			return DatabaseTargetResolution.Fail("topology_disabled");
		}

		if (!Enum.TryParse<DatabaseTopologyKind>(current.Topology, ignoreCase: true, out var kind) ||
			kind == default)
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
		if (!current.Connections.TryGetValue(databaseId, out var connectionString) ||
			string.IsNullOrWhiteSpace(connectionString))
		{
			return DatabaseTargetResolution.Fail("connection_missing");
		}

		return DatabaseTargetResolution.Ok(new DatabaseTarget(databaseId, connectionString!));
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
