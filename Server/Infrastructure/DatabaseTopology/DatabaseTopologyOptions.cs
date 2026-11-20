using Microsoft.Extensions.Options;

namespace Infrastructure.DatabaseTopology;

/// <summary>One authoritative scope-to-database assignment of the registry (D-115 §2).</summary>
public sealed class DatabaseAssignmentOption
{
	/// <summary>The tenant of the scope; never empty (D-114: the tenant boundary of every scope).</summary>
	public Guid? TenantId { get; set; }

	/// <summary>The company for company-scoped topologies; must stay empty otherwise.</summary>
	public Guid? CompanyId { get; set; }

	/// <summary>The fiscal period for period-scoped topologies; must stay empty otherwise.</summary>
	public Guid? FiscalPeriodId { get; set; }

	/// <summary>The registry reference of the target database (no credential, D-115 §3).</summary>
	public string DatabaseId { get; set; } = string.Empty;
}

/// <summary>
/// Configuration binding of the server-side routing registry (D-115 §1). The section is the
/// installation's authoritative registry reference; the connection strings it points at are
/// read from the same secure configuration source the main connection string already uses
/// (first-run settings store or the host secret configuration) and never appear in logs.
/// </summary>
public sealed class DatabaseTopologyOptions
{
	public const string SectionName = "DatabaseTopology";

	/// <summary>
	/// Opt-in gate. While <c>false</c> (the default) the resolver fails closed with
	/// <c>topology_disabled</c> and the host behaves exactly as before: a single database.
	/// Switching the operational <c>Data.DatabaseContext</c> to the resolver is a separate,
	/// owner-approved change (D-115 implementation plan).
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>The active topology kind; one of the D-111 §2 names, case-insensitive.</summary>
	public string Topology { get; set; } = string.Empty;

	/// <summary>The scope-to-database assignments; at most one match per scope (D-115 §2).</summary>
	public List<DatabaseAssignmentOption> Assignments { get; set; } = [];

	/// <summary>DatabaseId → connection string; references only, never logged (D-115 §3).</summary>
	public Dictionary<string, string?> Connections { get; set; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Validates the whole registry configuration and returns stable, non-sensitive error
	/// codes. An empty, disabled configuration is valid; a partially configured registry
	/// (assignments without a topology, unknown connection references, incoherent keys)
	/// fails fast at startup. Routing that is <c>Enabled</c> without any assignment, the
	/// pending <c>TenantFiscalPeriodDatabase</c> topology, a topology value that is not a
	/// defined <see cref="DatabaseTopologyKind"/> member (undefined numeric strings such as
	/// <c>999</c> included) and duplicate assignments for the same scope are configuration
	/// errors as well — they must stop the startup instead of surfacing as the first
	/// runtime request failure.
	/// </summary>
	public List<string> Validate()
	{
		var errors = new List<string>();
		var assignmentsPresent = Assignments.Count > 0;

		// Enabled routing without a single assignment is a broken registry (D-115 §2):
		// fail fast here instead of an assignment_missing on the first request.
		if (Enabled && !assignmentsPresent)
		{
			errors.Add("registry_empty");
			return errors;
		}

		if (!assignmentsPresent)
		{
			return errors;
		}

		// Strict parsing (Enum.IsDefined): undefined numeric values such as "999" must not
		// become a routable topology (fail closed, D-115 §2).
		if (!DatabaseTopologyKindParser.TryParse(Topology, out var kind))
		{
			errors.Add("topology_kind_invalid");
			return errors;
		}

		// Pending owner decision (D-115 §8): the registry must not start with a topology
		// that the resolver refuses to route, so no configuration can half-activate it.
		if (kind == DatabaseTopologyKind.TenantFiscalPeriodDatabase)
		{
			errors.Add("topology_kind_pending_owner_decision");
			return errors;
		}

		// The identity keys of each topology (D-111 §2): the required keys must be present
		// and the keys of finer scopes must stay empty, so an assignment can never be
		// interpreted under a different topology.
		var needsCompany = kind is DatabaseTopologyKind.CompanyDatabase or DatabaseTopologyKind.CompanyFiscalPeriodDatabase;
		var needsPeriod = kind is DatabaseTopologyKind.CompanyFiscalPeriodDatabase;

		// One authoritative destination per scope (D-115 §2): the same identity keys must
		// not appear twice, whatever the target references are.
		var seenKeys = new HashSet<(Guid Tenant, Guid? Company, Guid? Period)>();

		for (var index = 0; index < Assignments.Count; index++)
		{
			var assignment = Assignments[index];
			var prefix = $"assignment_{index + 1}";
			if (string.IsNullOrWhiteSpace(assignment.DatabaseId) || assignment.DatabaseId.Length > 128)
			{
				errors.Add($"{prefix}:database_id_invalid");
				continue;
			}

			var keysValid =
				assignment.TenantId is { } tenant && tenant != Guid.Empty &&
				needsCompany == assignment.CompanyId.HasValue &&
				needsPeriod == assignment.FiscalPeriodId.HasValue &&
				(!assignment.CompanyId.HasValue || assignment.CompanyId.Value != Guid.Empty) &&
				(!assignment.FiscalPeriodId.HasValue || assignment.FiscalPeriodId.Value != Guid.Empty);
			if (!keysValid)
			{
				errors.Add($"{prefix}:keys_invalid_for_topology");
			}
			else if (!seenKeys.Add((assignment.TenantId.Value, assignment.CompanyId, assignment.FiscalPeriodId)))
			{
				errors.Add($"{prefix}:duplicate");
			}

			// Fail fast on both a present-but-empty and a completely missing connection
			// entry: a registry that cannot resolve its own reference must never start.
			if (!Connections.TryGetValue(assignment.DatabaseId, out var connectionString) ||
				string.IsNullOrWhiteSpace(connectionString))
			{
				errors.Add($"{prefix}:connection_missing");
			}
		}

		return errors;
	}
}

/// <summary>Fail-fast startup validation with the stable error codes from <see cref="DatabaseTopologyOptions.Validate"/>.</summary>
public sealed class DatabaseTopologyOptionsValidator : IValidateOptions<DatabaseTopologyOptions>
{
	public ValidateOptionsResult Validate(string? name, DatabaseTopologyOptions options)
	{
		var errors = options.Validate();
		return errors.Count == 0
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(string.Join(", ", errors));
	}
}
