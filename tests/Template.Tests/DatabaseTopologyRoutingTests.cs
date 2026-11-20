using System.Collections.Concurrent;
using System.Text.Json;
using Infrastructure.DatabaseTopology;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Unit tests for the D-115 database routing registry: per-topology resolution over the
/// three approved topologies, the rejected pending Tenant+FiscalPeriod topology, the
/// fail-closed error codes, the single-authoritative-destination rule, cross-scope safety,
/// secret non-disclosure (ToString, JSON, logging, exceptions) and the fail-fast
/// configuration validation. Pure resolution logic; no database is needed.
///
/// Coverage of the mission's required routing scenarios:
/// 1 approved topologies (TenantDatabase, CompanyDatabase, CompanyFiscalPeriodDatabase);
/// 2 pending Tenant+FiscalPeriod rejected (resolve + validate); 3 empty TenantId;
/// 4 missing CompanyId in Company topology; 5 missing FiscalPeriodId in
/// Company+FiscalPeriod topology; 6 unrelated Company+Period identity; 7 FiscalPeriod of
/// another company; 8 unrelated tenant; 9 duplicate assignment (validate);
/// 10 conflicting assignment (resolve); 11 missing assignment; 12/13 invalid target
/// connection; 14 ToString; 15 JSON; 16 structured logging; 17 enabled without
/// assignments; 18 disabled configuration; 19 invalid scope fail-closed;
/// 20 concurrent scope isolation.
/// </summary>
public sealed class DatabaseTopologyRoutingTests
{
	private const string MainConnectionString = "Server=sql-main;Database=Main;Trusted_Connection=True;";
	private const string CompanyAConnectionString = "Server=sql-company;Database=CompanyA;Trusted_Connection=True;";
	private const string PeriodConnectionString = "Server=sql-period;Database=Period1405;Trusted_Connection=True;";

	private static readonly Guid TenantOne = Guid.NewGuid();
	private static readonly Guid TenantTwo = Guid.NewGuid();
	private static readonly Guid CompanyOne = Guid.NewGuid();
	private static readonly Guid CompanyTwo = Guid.NewGuid();
	private static readonly Guid FiscalPeriodOne = Guid.NewGuid();
	private static readonly Guid FiscalPeriodTwo = Guid.NewGuid();

	private sealed class StaticOptionsMonitor(DatabaseTopologyOptions options) :
		IOptionsMonitor<DatabaseTopologyOptions>
	{
		public DatabaseTopologyOptions CurrentValue => options;

		public DatabaseTopologyOptions Get(string? name) => options;

		public IDisposable? OnChange(Action<DatabaseTopologyOptions, string?> listener) => null;
	}

	/// <summary>A minimal capturing logger that records formatted and destructured output.</summary>
	private sealed class CapturingLogger : ILogger
	{
		public List<string> FormattedMessages { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			FormattedMessages.Add(formatter(state, exception));
			if (exception is not null)
			{
				FormattedMessages.Add(exception.ToString());
			}
		}
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

	// 1 — the three approved topologies resolve to their own destination.

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
		Assert.Equal(MainConnectionString, resolution.Target.GetConnectionString());
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
		Assert.Equal(CompanyAConnectionString, resolution.Target!.GetConnectionString());
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
		Assert.Equal(PeriodConnectionString, resolution.Target!.GetConnectionString());
	}

	// 2 — the pending Tenant+FiscalPeriod topology is never routable and never valid.

	[Fact]
	public void PendingTenantFiscalPeriodTopology_FailsClosedOnResolve()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantFiscalPeriodDatabase),
			connections: null,
			new DatabaseAssignmentOption
			{
				TenantId = TenantOne,
				FiscalPeriodId = FiscalPeriodOne,
				DatabaseId = "period-1405"
			}));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne, FiscalPeriodId: FiscalPeriodOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("topology_kind_pending_owner_decision", resolution.ErrorCode);
	}

	[Fact]
	public void PendingTenantFiscalPeriodTopology_FailsFastOnValidate_EvenWithExactCaseOrOddCase()
	{
		foreach (var topology in new[]
		         {
			         nameof(DatabaseTopologyKind.TenantFiscalPeriodDatabase),
			         "tenantfiscalperioddatabase",
		         })
		{
			var options = Options(
				topology,
				connections: null,
				new DatabaseAssignmentOption
				{
					TenantId = TenantOne,
					FiscalPeriodId = FiscalPeriodOne,
					DatabaseId = "period-1405"
				});

			Assert.Equal(
				(IEnumerable<string>)["topology_kind_pending_owner_decision"],
				options.Validate());
		}
	}

	// 3, 4, 5, 19 — incomplete or invalid scopes are rejected fail-closed.

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

		// 3 — an empty tenant boundary is never a routable scope.
		var emptyTenant = registry.Resolve(new DatabaseScope(Guid.Empty, CompanyOne));
		Assert.False(emptyTenant.Success);
		Assert.Equal("scope_invalid", emptyTenant.ErrorCode);

		// 5 — a period without its company is incomplete in the Company+FiscalPeriod
		// topology: it matches no assignment and is never silently rerouted.
		var periodWithoutCompany = registry.Resolve(
			new DatabaseScope(TenantOne, FiscalPeriodId: FiscalPeriodOne));
		Assert.False(periodWithoutCompany.Success);
		Assert.Null(periodWithoutCompany.Target);
		Assert.Equal("assignment_missing", periodWithoutCompany.ErrorCode);

		// 5 — the same for a company scope without its period.
		var companyWithoutPeriod = registry.Resolve(new DatabaseScope(TenantOne, CompanyOne));
		Assert.False(companyWithoutPeriod.Success);
		Assert.Null(companyWithoutPeriod.Target);
		Assert.Equal("assignment_missing", companyWithoutPeriod.ErrorCode);
	}

	[Fact]
	public void CompanyDatabaseTopology_DoesNotFallBackForATenantOnlyScope()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "company-a" }));

		// 4 — a tenant-level operation in a CompanyDatabase topology has no destination; the
		// resolver never silently falls back to another assignment (D-115 §2).
		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("assignment_missing", resolution.ErrorCode);
	}

	// 6, 7, 8 — identities that belong to another scope never reach a foreign database.
	//
	// These are identity-level routing guarantees: the full-identity match keeps a scope
	// from reaching another scope's database. Proof of real ownership (whether a
	// FiscalPeriod really belongs to a Company, D-114) happens server-side against the
	// authoritative data model in the operational wiring step (D-115 §7) — the registry
	// alone never claims it.

	[Fact]
	public void UnrelatedCompanyAndPeriodIdentity_CannotReachAForeignDatabase()
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

		// 6 — the same company with a period of another identity matches nothing.
		var unrelatedPeriod = registry.Resolve(new DatabaseScope(TenantOne, CompanyOne, FiscalPeriodTwo));
		Assert.False(unrelatedPeriod.Success);
		Assert.Null(unrelatedPeriod.Target);
		Assert.Equal("assignment_missing", unrelatedPeriod.ErrorCode);
	}

	[Fact]
	public void FiscalPeriodOfAnotherCompany_CannotReachTheCompanyDatabase()
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

		// 7 — the same period under a different company of the same tenant matches nothing.
		var foreignCompany = registry.Resolve(new DatabaseScope(TenantOne, CompanyTwo, FiscalPeriodOne));
		Assert.False(foreignCompany.Success);
		Assert.Null(foreignCompany.Target);
		Assert.Equal("assignment_missing", foreignCompany.ErrorCode);
	}

	[Fact]
	public void UnrelatedTenant_CannotReachAnyAssignedDatabase()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" }));

		// 8 — a scope with an unassigned tenant matches nothing.
		var unrelatedTenant = registry.Resolve(new DatabaseScope(TenantTwo));
		Assert.False(unrelatedTenant.Success);
		Assert.Null(unrelatedTenant.Target);
		Assert.Equal("assignment_missing", unrelatedTenant.ErrorCode);
	}

	[Fact]
	public void CompanyDatabaseTopology_CannotBeReachedWithAnotherTenantBoundary()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "company-a" }));

		// 8 — the same company identifier under a different tenant boundary matches nothing:
		// a spoofed or cross-tenant scope can never reach a foreign database.
		var resolution = registry.Resolve(new DatabaseScope(TenantTwo, CompanyOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("assignment_missing", resolution.ErrorCode);
	}

	// 10 — overlapping assignments fail closed; there is no first-match or default pick.

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

	// 9 — the same duplicate scope is already a startup configuration error.

	[Fact]
	public void DuplicateAssignmentForTheSameScope_FailsFastOnValidate()
	{
		var options = Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" },
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "company-a" });

		Assert.Equal((IEnumerable<string>)["assignment_2:duplicate"], options.Validate());
	}

	// 11, 12, 13 — a missing assignment or an unusable connection fails closed.

	[Fact]
	public void UnassignedScope_FailsClosedWithAssignmentMissing()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.CompanyDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, CompanyId = CompanyOne, DatabaseId = "company-a" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne, CompanyTwo));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("assignment_missing", resolution.ErrorCode);
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

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void EmptyOrWhitespaceConnectionValue_FailsClosed(string? brokenValue)
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: new Dictionary<string, string?>(StringComparer.Ordinal) { ["main"] = brokenValue },
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" }));

		var resolution = registry.Resolve(new DatabaseScope(TenantOne));

		Assert.False(resolution.Success);
		Assert.Null(resolution.Target);
		Assert.Equal("connection_missing", resolution.ErrorCode);
	}

	// 18 — the disabled default keeps the host exactly on its current single-database path.

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

	// 14, 15, 16 — the connection string is a secret and cannot leak through any surface.

	[Fact]
	public void DatabaseTarget_TextAndExceptionPaths_NeverContainTheConnectionString()
	{
		var target = new DatabaseTarget("main", MainConnectionString);

		Assert.Equal("DatabaseId=main", target.ToString());
		Assert.DoesNotContain(MainConnectionString, target.ToString(), StringComparison.Ordinal);

		var exceptionMessage = new InvalidOperationException($"Routing failed for {target}").Message;
		Assert.DoesNotContain(MainConnectionString, exceptionMessage, StringComparison.Ordinal);
	}

	[Fact]
	public void DatabaseTarget_JsonSerialization_NeverContainsTheConnectionString()
	{
		var target = new DatabaseTarget("main", MainConnectionString);

		var targetJson = JsonSerializer.Serialize(target);
		Assert.DoesNotContain(MainConnectionString, targetJson, StringComparison.Ordinal);
		Assert.DoesNotContain("ConnectionString", targetJson, StringComparison.Ordinal);

		var resolutionJson = JsonSerializer.Serialize(DatabaseTargetResolution.Ok(target));
		Assert.DoesNotContain(MainConnectionString, resolutionJson, StringComparison.Ordinal);
		// The registry reference stays visible; only the secret is protected.
		Assert.Contains("main", resolutionJson, StringComparison.Ordinal);
	}

	[Fact]
	public void DatabaseTarget_StructuredLogging_NeverContainsTheConnectionString()
	{
		var target = new DatabaseTarget("main", MainConnectionString);
		var logger = new CapturingLogger();

		logger.Log(
			LogLevel.Information,
			new EventId(1),
			new Dictionary<string, object?> { ["Target"] = target, ["Scope"] = "tenant" },
			null,
			(state, _) => string.Join(", ", state.Select(pair => $"{pair.Key}={pair.Value}")));

		Assert.NotEmpty(logger.FormattedMessages);
		Assert.All(logger.FormattedMessages, message =>
			Assert.DoesNotContain(MainConnectionString, message, StringComparison.Ordinal));
		Assert.Contains("DatabaseId=main", logger.FormattedMessages[0], StringComparison.Ordinal);
	}

	[Fact]
	public void DatabaseTarget_ValueEquality_CoversTheConnectionString()
	{
		var first = new DatabaseTarget("main", MainConnectionString);
		var same = new DatabaseTarget("main", MainConnectionString);
		var other = new DatabaseTarget("main", CompanyAConnectionString);

		Assert.Equal(first, same);
		Assert.NotEqual(first, other);
		Assert.Equal(first.GetHashCode(), same.GetHashCode());
	}

	// 17 — enabled routing without a single assignment is a startup configuration error.

	[Fact]
	public void EnabledRegistryWithoutAssignments_FailsFastOnValidate()
	{
		Assert.Equal(
			(IEnumerable<string>)["registry_empty"],
			new DatabaseTopologyOptions { Enabled = true }.Validate());

		// Enabled with a topology but still no assignments is equally broken.
		Assert.Equal(
			(IEnumerable<string>)["registry_empty"],
			new DatabaseTopologyOptions
			{
				Enabled = true,
				Topology = nameof(DatabaseTopologyKind.TenantDatabase),
			}.Validate());
	}

	// — the fail-fast validation keeps its original stable error codes.

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

	// 20 — concurrent resolutions never leak one scope's target into another request.

	[Fact]
	public void ConcurrentResolves_NeverLeakScopeAcrossRequests()
	{
		var registry = Registry(Options(
			nameof(DatabaseTopologyKind.TenantDatabase),
			connections: null,
			new DatabaseAssignmentOption { TenantId = TenantOne, DatabaseId = "main" },
			new DatabaseAssignmentOption { TenantId = TenantTwo, DatabaseId = "company-a" }));

		var outcomes = new ConcurrentBag<(Guid Tenant, DatabaseTargetResolution Resolution)>();

		Parallel.For(0, 8, worker =>
		{
			for (var request = 0; request < 200; request++)
			{
				var tenant = (worker + request) % 2 == 0 ? TenantOne : TenantTwo;
				outcomes.Add((tenant, registry.Resolve(new DatabaseScope(tenant))));
			}
		});

		Assert.Equal(1600, outcomes.Count);
		foreach (var (tenant, resolution) in outcomes)
		{
			Assert.True(resolution.Success);
			Assert.Equal(
				tenant == TenantOne ? "main" : "company-a",
				resolution.Target!.DatabaseId);
			Assert.Equal(
				tenant == TenantOne ? MainConnectionString : CompanyAConnectionString,
				resolution.Target.GetConnectionString());
		}
	}
}
