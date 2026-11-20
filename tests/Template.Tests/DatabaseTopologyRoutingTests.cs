using Infrastructure.DatabaseTopology;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Unit tests for the D-115 database routing registry: per-topology resolution, the
/// fail-closed error codes, the single-authoritative-destination rule, cross-tenant safety,
/// secret non-disclosure and the fail-fast configuration validation. Pure resolution logic;
/// no database is needed.
/// </summary>
public sealed class DatabaseTopologyRoutingTests
{
	private const string MainConnectionString = "Server=sql-main;Database=Main;Trusted_Connection=True;";
	private const string CompanyAConnectionString = "Server=sql-company;Database=CompanyA;Trusted_Connection=True;";
	private const string PeriodConnectionString = "Server=sql-period;Database=Period1405;Trusted_Connection=True;";

	private static readonly Guid TenantOne = Guid.NewGuid();
	private static readonly Guid TenantTwo = Guid.NewGuid();
	private static readonly Guid CompanyOne = Guid.NewGuid();
	private static readonly Guid FiscalPeriodOne = Guid.NewGuid();

	private sealed class StaticOptionsMonitor(DatabaseTopologyOptions options) :
		IOptionsMonitor<DatabaseTopologyOptions>
	{
		public DatabaseTopologyOptions CurrentValue => options;

		public DatabaseTopologyOptions Get(string? name) => options;

		public IDisposable? OnChange(Action<DatabaseTopologyOptions, string?> listener) => null;
	}

	private static DatabaseTopologyOptions Options(
		string topology,
		Dictionary<string, string?>? connections,
		params DatabaseAssignmentOption[] assignments) => new()
		{
			Enabled = true,
			Topology = topology,
			Assignments = [.. assignments],
			Connections = connections ?? new Dictionary<string, string?>(StringComparer.Ordinal)
			{
				["main"] = MainConnectionString,
				["company-a"] = CompanyAConnectionString,
				["period-1405"] = PeriodConnectionString,
			},
		};

	private static ConfigurationDatabaseTargetRegistry Registry(DatabaseTopologyOptions options) =>
		new(new StaticOptionsMonitor(options));

	[Fact]
	public void TenantDatabaseTopology_RoutesByTenantId()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" },
			new DatabaseAssignmentOption { TenantId = TenantTwo, DatabaseId = "company-a" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.True(resolution.Success);
		Assert.Equal("main", resolution.Target!.DatabaseId);
		Assert.Equal(MainConnectionString, resolution.Target.ConnectionString);
	}

	[Fact]
	public void CompanyDatabaseTopology_RoutesByTenantAndCompany()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "company-a" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne, CompanyOne));

		Assert.True(resolution.Success);
		Assert.Equal(CompanyAConnectionString, resolution.Target!.ConnectionString);
	}

	[Fact]
	public void CompanyDatabaseTopology_DoesNotFallBackForATenantOnlyScope()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "company-a" }));

		// A tenant-level operation in a CompanyDatabase topology has no destination; the
		// resolver never silently falls back to another assignment (D-115 §2).
		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("assignment_missing", resolution.ErrorCode);
	}

	[Fact]
	public void CompanyDatabaseTopology_CannotBeReachedWithAnotherTenantBoundary()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "company-a" }));

		// The same company identifier under a different tenant boundary matches nothing:
		// a spoofed or cross-tenant scope can never reach a foreign database.
		var resolution = registry.Resolve(new DatabaseScope(TenantTwo, CompanyOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("assignment_missing", resolution.ErrorCode);
	}

	[Fact]
	public void TenantFiscalPeriodTopology_RoutesByTenantAndPeriod()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantFiscalPeriodDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, FiscalPeriodId = FiscalPeriodOne, DatabaseId = "period-1405" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne, FiscalPeriodId: FiscalPeriodOne));

		Assert.True(resolution.Success);
		Assert.Equal(PeriodConnectionString, resolution.Target!.ConnectionString);
	}

	[Fact]
	public void CompanyFiscalPeriodTopology_RoutesByFullIdentity()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyFiscalPeriodDatabase),
			connections: null,
			new DatabaseAssignmentOption
			{
				TenantId = TenantOne,
				CompanyId = CompanyOne,
				FiscalPeriodId = FiscalPeriodOne,
				DatabaseId = "period-1405"
			}));

		var resolution = registry.Resolve(
			new DatabaseScope(TenantOne, CompanyOne, FiscalPeriodOne));

		Assert.True(resolution.Success);
		Assert.Equal(PeriodConnectionString, resolution.Target!.ConnectionString);
	}

	[Fact]
	public void TwoMatchingAssignments_FailClosedWithAConflict()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" },
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "company-a" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("assignment_conflict", resolution.ErrorCode);
	}

	[Fact]
	public void MissingConnectionForTheAssignedDatabase_FailsClosedWithoutAnEmptyTarget()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: new Dictionary<string, string?>(StringComparer.Ordinal),
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("connection_missing", resolution.ErrorCode);
	}

	[Fact]
	public void DisabledRegistry_FailsClosedAndNeverRoutes()
	{
		var registry = Registry(new DatabaseTopologyOptions
		{
			Enabled = false,
			Topology = nameof(DatabaseTopologyKind.TenantDatabase),
			Assignments = [new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" }],
		});

		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("topology_disabled", resolution.ErrorCode);
	}

	[Fact]
	public void InvalidTopologyKind_FailsClosed()
	{
		var registry = Registry(Options(
			"SomethingElse",
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("topology_kind_invalid", resolution.ErrorCode);
	}

	[Fact]
	public void IncoherentScopes_AreRejected_AndPeriodScopesStayTopologyAware()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyFiscalPeriodDatabase),
			connections: null,
			new DatabaseAssignmentOption
			{
				TenantId = TenantOne,
				CompanyId = CompanyOne,
				FiscalPeriodId = FiscalPeriodOne,
				DatabaseId = "period-1405"
			}));

		// An empty tenant boundary is never a routable scope.
		var emptyTenant = registry.Resolve(new DatabaseScope(Guid.Empty, CompanyOne));
		Assert.False(emptyTenant.Success);
		Assert.Equal("scope_invalid", emptyTenant.ErrorCode);

		// A tenant+period scope without a company is coherent (D-114 §5): in the
		// CompanyFiscalPeriod topology it simply matches no assignment, because the company
		// is a required routing key there — it is never silently rerouted.
		var periodWithoutCompany = registry.Resolve(
			new DatabaseScope(TenantOne, FiscalPeriodId: FiscalPeriodOne));
		Assert.False(periodWithoutCompany.Success);
		Assert.Null(periodWithoutCompany.Target);
		Assert.Equal("assignment_missing", periodWithoutCompany.ErrorCode);
	}

	[Fact]
	public void DatabaseTarget_TextualRepresentation_NeverContainsTheConnectionString()
	{
		var target = new DatabaseTarget("main", MainConnectionString);

		Assert.DoesNotContain(MainConnectionString, target.ToString(), StringComparison.Ordinal);
		Assert.Equal("DatabaseId=main", target.ToString());
	}

	[Fact]
	public void PartialRegistryConfiguration_FailsFastWithStableCodes()
	{
		// An empty, disabled configuration is valid: the default host has no registry.
		Assert.Empty(new DatabaseTopologyOptions().Validate());

		// Assignments without a valid topology name.
		var withoutTopology = new DatabaseTopologyOptions
		{
			Topology = "Nope",
			Assignments = [new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" }],
			Connections = new Dictionary<string, string?>(StringComparer.Ordinal) { ["main"] = MainConnectionString },
		};
		Assert.Equal((IEnumerable<string>)["topology_kind_invalid"], withoutTopology.Validate());

		// Keys that do not belong to the active topology are a configuration error.
		var withExtraKeys = new DatabaseTopologyOptions
		{
			Topology = nameof(DatabaseTopologyKind.TenantDatabase),
			Assignments =
			[
				new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "main" },
			],
			Connections = new Dictionary<string, string?>(StringComparer.Ordinal) { ["main"] = MainConnectionString },
		};
		Assert.Equal((IEnumerable<string>)["assignment_1:keys_invalid_for_topology"], withExtraKeys.Validate());

		// An assignment whose connection reference is missing fails fast instead of
		// surfacing as a runtime connection_missing later.
		var withMissingConnection = new DatabaseTopologyOptions
		{
			Topology = nameof(DatabaseTopologyKind.TenantDatabase),
			Assignments = [new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "ghost" }],
			Connections = new Dictionary<string, string?>(StringComparer.Ordinal),
		};
		Assert.Equal((IEnumerable<string>)["assignment_1:connection_missing"], withMissingConnection.Validate());
	}
}
