using System;
using System.Linq;
using System.Threading.Tasks;
using Domain;
using Infrastructure.CorporateGroups;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// The migration <c>AddCorporateGroups</c> and the corporate group service on real SQL Server
/// (D-033, D-034): upgrade of a database that already holds data, idempotent catalog rows,
/// Down/Up, the database constraints, and the serializable service paths.
/// </summary>
/// <remarks>
/// Runs only when <c>FARANOU_TEST_SQLSERVER</c> is set (CI). Every test creates and drops its
/// own database whose name starts with <c>FaranouTests_CG_</c>; the shared integration
/// database is not touched. With <c>FARANOU_REQUIRE_SQLSERVER=1</c> a missing server fails.
/// </remarks>
public sealed class CorporateGroupSqlServerTests
{
	private const string PreviousMigration = "20261019071146_WidenUserPasswordHash";
	private const string CorporateGroupsMigration = "20261022150943_AddCorporateGroups";

	private static bool SqlServerAvailable()
	{
		if (IntegrationDatabase.UsesSqlServer)
		{
			return true;
		}

		if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
		{
			throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
		}

		return false;
	}

	private static Data.DatabaseContext CreateContext(string databaseName)
	{
		var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString)
		{
			InitialCatalog = databaseName,
		};

		return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseSqlServer(builder.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
			.Options);
	}

	private static async Task WithDatabaseAsync(Func<string, Task> test)
	{
		var name = "FaranouTests_CG_" + Guid.NewGuid().ToString("N")[..12];
		try
		{
			await test(name);
		}
		finally
		{
			await using var cleanup = CreateContext(name);
			await cleanup.Database.EnsureDeletedAsync();
		}
	}

	[Fact]
	public async Task TheMigration_UpgradesADatabaseWithData_KeepsTheData_AndIsReversible()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async name =>
		{
			// 1. The schema before this change, with data in it.
			await using (var before = CreateContext(name))
			{
				await before.GetService<IMigrator>().MigrateAsync(PreviousMigration, TestContext.Current.CancellationToken);

				// The company row is inserted with SQL because the current model has columns
				// (for example CustomerBoundaryId) that do not exist yet in the old schema.
				var company = new Company { Name = "شرکت موجود", NationalId = "10101010101", IsActive = true };
				await before.Database.ExecuteSqlInterpolatedAsync(
					$"INSERT INTO Companies (Id, Ordering, InsertDateTime, Name, NationalId, IsActive) VALUES ({company.Id}, 0, SYSUTCDATETIME(), {company.Name}, {company.NationalId}, 1)",
					TestContext.Current.CancellationToken);
				var role = new CompanyRole { TenantId = company.Id, Code = "accountant", Name = "حسابدار", IsActive = true };
				before.CompanyRoles.Add(role);
				before.FiscalPeriods.Add(new FiscalPeriod
				{
					TenantId = company.Id,
					Title = "سال مالی ۱۴۰۵",
					StartDate = new DateOnly(2026, 3, 21),
					EndDate = new DateOnly(2027, 3, 20),
				});
				before.UserCompanyMemberships.Add(new UserCompanyMembership { UserId = Guid.NewGuid(), TenantId = company.Id, RoleId = role.Id });

				// An administrator already created a row with one of the new codes.
				before.PermissionDefinitions.Add(new PermissionDefinition { Code = CorporateGroupPermissions.View, Name = "نام مدیر" });
				await before.SaveChangesAsync(TestContext.Current.CancellationToken);
			}

			Guid existingViewId;
			await using (var context = CreateContext(name))
			{
				existingViewId = await context.PermissionDefinitions
					.Where(permission => permission.Code == CorporateGroupPermissions.View)
					.Select(permission => permission.Id)
					.SingleAsync(TestContext.Current.CancellationToken);

				// 2. Up.
				await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
				Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));

				Assert.Equal(1, await context.Companies.CountAsync(TestContext.Current.CancellationToken));
				Assert.Equal(1, await context.FiscalPeriods.CountAsync(TestContext.Current.CancellationToken));
				Assert.Equal(1, await context.UserCompanyMemberships.CountAsync(TestContext.Current.CancellationToken));
				Assert.Equal("شرکت موجود", (await context.Companies.SingleAsync(TestContext.Current.CancellationToken)).Name);

								// PermissionDefinitions is shared across modules; inspect only this module's catalog.
				var catalog = await context.PermissionDefinitions
					.Where(permission => CorporateGroupScenario.AllPermissionCodes.Contains(permission.Code))
					.ToListAsync(TestContext.Current.CancellationToken);
				Assert.Equal(CorporateGroupScenario.AllPermissionCodes.Length, catalog.Count);
				Assert.All(CorporateGroupScenario.AllPermissionCodes, code => Assert.Single(catalog, permission => permission.Code == code));
				var view = catalog.Single(permission => permission.Code == CorporateGroupPermissions.View);
				Assert.Equal(existingViewId, view.Id);
				Assert.Equal("نام مدیر", view.Name);

				// The catalog statements are idempotent: running them again adds nothing.
				foreach (var statement in CatalogStatements())
				{
					await context.Database.ExecuteSqlRawAsync(statement, TestContext.Current.CancellationToken);
				}

				Assert.Equal(
					CorporateGroupScenario.AllPermissionCodes.Length,
					await context.PermissionDefinitions
						.CountAsync(permission => CorporateGroupScenario.AllPermissionCodes.Contains(permission.Code), TestContext.Current.CancellationToken));

				// A role uses one of the seeded permissions.
				var roleId = await context.CompanyRoles.Select(role => role.Id).SingleAsync(TestContext.Current.CancellationToken);
				var exportId = catalog.Single(permission => permission.Code == CorporateGroupPermissions.ExportGroupReport).Id;
				context.CompanyRolePermissions.Add(new CompanyRolePermission { RoleId = roleId, PermissionId = exportId });
				await context.SaveChangesAsync(TestContext.Current.CancellationToken);

				// 3. Down: the new tables go, the existing data and the used catalog row stay.
				await context.GetService<IMigrator>().MigrateAsync(PreviousMigration, TestContext.Current.CancellationToken);
			}

			await using (var after = CreateContext(name))
			{
				Assert.Equal(1, await after.Companies.CountAsync(TestContext.Current.CancellationToken));
				Assert.Equal(1, await after.FiscalPeriods.CountAsync(TestContext.Current.CancellationToken));
				Assert.Equal(1, await after.CompanyRolePermissions.CountAsync(TestContext.Current.CancellationToken));
				var remaining = await after.PermissionDefinitions.Select(permission => permission.Code).ToListAsync(TestContext.Current.CancellationToken);
				Assert.Contains(CorporateGroupPermissions.ExportGroupReport, remaining);
				Assert.Contains(CorporateGroupPermissions.View, remaining); // the administrator's own row (other id)
				Assert.DoesNotContain(CorporateGroupPermissions.Manage, remaining);

				var tables = await after.Database
					.SqlQueryRaw<string>("SELECT [name] AS [Value] FROM sys.tables WHERE [name] LIKE 'CorporateGroup%' OR [name] LIKE 'Consolidation%' OR [name] = 'OwnershipRelationships'")
					.ToListAsync(TestContext.Current.CancellationToken);
				Assert.Empty(tables);

				// 4. Up again.
				await after.Database.MigrateAsync(TestContext.Current.CancellationToken);
				Assert.Equal(CorporateGroupScenario.AllPermissionCodes.Length, await after.PermissionDefinitions.CountAsync(TestContext.Current.CancellationToken));
			}
		});
	}

	[Fact]
	public async Task TheDatabase_RejectsRowsThatBreakTheModel()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async name =>
		{
			await using var context = CreateContext(name);
			await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
			var s = await CorporateGroupScenario.CreateAsync(context);
			var groupId = await s.CreateHoldingGroupAsync();
			var otherGroupId = await s.CreateHoldingGroupAsync();
			var otherProfile = await s.Service.CreateProfileAsync(s.Admin, otherGroupId, "p", AccountingFramework.Ifrs, "IRR");
			var run = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Aggregate, null, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));
			Assert.True(run.Succeeded, run.ErrorCode);

			async Task AssertRejectedAsync(Action<Data.DatabaseContext> add)
			{
				context.ChangeTracker.Clear();
				add(context);
				await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
				context.ChangeTracker.Clear();
			}

			// A fiscal period of another company (composite FK to FiscalPeriods(Id, TenantId)).
			var freshRun = new ConsolidationRun { CorporateGroupId = groupId, Kind = GroupReportKind.Aggregate, PeriodStart = CorporateGroupScenario.YearStart, PeriodEnd = CorporateGroupScenario.QuarterEnd, Status = ConsolidationRunStatus.Pending };
			context.ConsolidationRuns.Add(freshRun);
			await context.SaveChangesAsync(TestContext.Current.CancellationToken);
			await AssertRejectedAsync(db => db.ConsolidationRunCompanies.Add(new ConsolidationRunCompany
			{
				ConsolidationRunId = freshRun.Id,
				CompanyId = s.SubA,
				FiscalPeriodId = s.YearPeriod[s.SubB],
				MemberRole = CorporateGroupMemberRole.Subsidiary,
			}));

			// A profile of another group (composite FK to ConsolidationProfiles(Id, CorporateGroupId)).
			await AssertRejectedAsync(db => db.ConsolidationRuns.Add(new ConsolidationRun { CorporateGroupId = groupId, ConsolidationProfileId = otherProfile.Value, Kind = GroupReportKind.Consolidated, PeriodStart = CorporateGroupScenario.YearStart, PeriodEnd = CorporateGroupScenario.QuarterEnd }));

			// A consolidated run without a profile; a period that ends before it starts.
			await AssertRejectedAsync(db => db.ConsolidationRuns.Add(new ConsolidationRun { CorporateGroupId = groupId, Kind = GroupReportKind.Consolidated, PeriodStart = CorporateGroupScenario.YearStart, PeriodEnd = CorporateGroupScenario.QuarterEnd }));
			await AssertRejectedAsync(db => db.ConsolidationRuns.Add(new ConsolidationRun { CorporateGroupId = groupId, Kind = GroupReportKind.Aggregate, PeriodStart = CorporateGroupScenario.QuarterEnd, PeriodEnd = CorporateGroupScenario.YearStart }));

			// Ownership: same company, percentages outside 0..100, invalid period.
			await AssertRejectedAsync(db => db.OwnershipRelationships.Add(new OwnershipRelationship { ParentCompanyId = s.SubA, SubsidiaryCompanyId = s.SubA, OwnershipPercentage = 10, VotingPercentage = 10, ControlBasis = ControlBasis.Investment, ValidFrom = CorporateGroupScenario.YearStart }));
			await AssertRejectedAsync(db => db.OwnershipRelationships.Add(new OwnershipRelationship { ParentCompanyId = s.Holding, SubsidiaryCompanyId = s.Other, OwnershipPercentage = 150, VotingPercentage = 10, ControlBasis = ControlBasis.Investment, ValidFrom = CorporateGroupScenario.YearStart }));
			await AssertRejectedAsync(db => db.OwnershipRelationships.Add(new OwnershipRelationship { ParentCompanyId = s.Holding, SubsidiaryCompanyId = s.Other, OwnershipPercentage = 10, VotingPercentage = 10, ControlBasis = ControlBasis.Investment, ValidFrom = CorporateGroupScenario.YearEnd, ValidTo = CorporateGroupScenario.YearStart }));

			// Membership: an invalid period, an unknown role, an unknown company, a duplicate start.
			await AssertRejectedAsync(db => db.CorporateGroupMembers.Add(new CorporateGroupMember { CorporateGroupId = groupId, CompanyId = s.Other, ValidFrom = CorporateGroupScenario.YearEnd, ValidTo = CorporateGroupScenario.YearStart }));
			await AssertRejectedAsync(db => db.CorporateGroupMembers.Add(new CorporateGroupMember { CorporateGroupId = groupId, CompanyId = s.Other, MemberRole = (CorporateGroupMemberRole)42, ValidFrom = CorporateGroupScenario.YearStart }));
			await AssertRejectedAsync(db => db.CorporateGroupMembers.Add(new CorporateGroupMember { CorporateGroupId = groupId, CompanyId = Guid.NewGuid(), ValidFrom = CorporateGroupScenario.YearStart }));
			await AssertRejectedAsync(db => db.CorporateGroupMembers.Add(new CorporateGroupMember { CorporateGroupId = groupId, CompanyId = s.SubA, ValidFrom = CorporateGroupScenario.YearStart }));

			// A lower-case currency code.
			await AssertRejectedAsync(db => db.ConsolidationProfiles.Add(new ConsolidationProfile { CorporateGroupId = groupId, Name = "x", ReportingCurrencyCode = "irr" }));

		});
	}

	[Fact]
	public async Task TheService_WorksOnSqlServer_WithItsSerializableTransactions()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async name =>
		{
			await using var context = CreateContext(name);
			await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
			var s = await CorporateGroupScenario.CreateAsync(context);
			var start = CorporateGroupScenario.YearStart;

			var groupId = await s.CreateHoldingGroupAsync();
			Assert.Contains(await s.Service.GetVisibleGroupsAsync(s.Viewer, TestContext.Current.CancellationToken), group => group.Id == groupId);
			Assert.DoesNotContain(await s.Service.GetVisibleGroupsAsync(s.Outsider, TestContext.Current.CancellationToken), group => group.Id == groupId);

			Assert.True((await s.Service.RecordOwnershipAsync(s.Admin, new(s.Holding, s.SubA, 80m, 80m, ControlBasis.Control, start, null), TestContext.Current.CancellationToken)).Succeeded);
			Assert.True((await s.Service.RecordOwnershipAsync(s.Admin, new(s.SubA, s.SubB, 33.333333m, 51m, ControlBasis.Control, start, null), TestContext.Current.CancellationToken)).Succeeded);
			var cycle = await s.Service.RecordOwnershipAsync(s.Admin, new(s.SubB, s.Holding, 51m, 51m, ControlBasis.Control, start, null), TestContext.Current.CancellationToken);
			Assert.Equal(nameof(CorporateGroupRuleViolation.ControlCycle), cycle.ErrorCode);

			var stored = await context.OwnershipRelationships.AsNoTracking().SingleAsync(relationship => relationship.SubsidiaryCompanyId == s.SubB, TestContext.Current.CancellationToken);
			Assert.Equal(33.333333m, stored.OwnershipPercentage);

			var overlapping = await s.Service.AddMemberAsync(s.Admin, groupId, s.SubA, CorporateGroupMemberRole.Associate, start.AddDays(10), null, TestContext.Current.CancellationToken);
			Assert.Equal(nameof(CorporateGroupRuleViolation.OverlappingMembership), overlapping.ErrorCode);

			var denied = await s.Service.StartReportRunAsync(s.HoldingOnly, new(groupId, GroupReportKind.Aggregate, null, start, CorporateGroupScenario.QuarterEnd), TestContext.Current.CancellationToken);
			Assert.Equal(CorporateGroupOutcome.AccessDenied, denied.Outcome);

			var run = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Aggregate, null, start, CorporateGroupScenario.QuarterEnd), TestContext.Current.CancellationToken);
			Assert.True(run.Succeeded, run.ErrorCode);
			Assert.Equal(ConsolidationRunStatus.ScopeResolved, run.Value!.Status);
			Assert.Equal(3, await context.ConsolidationRunCompanies.CountAsync(company => company.ConsolidationRunId == run.Value.RunId, TestContext.Current.CancellationToken));
		});
	}

	[Fact]
	public async Task ConcurrentRequests_ForTheSameGroupCode_GiveOneGroupAndOneConflict()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async name =>
		{
			CorporateGroupScenario s;
			await using (var seed = CreateContext(name))
			{
				await seed.Database.MigrateAsync(TestContext.Current.CancellationToken);
				s = await CorporateGroupScenario.CreateAsync(seed);
			}

			var code = CorporateGroupScenario.Unique("race");

			async Task<CorporateGroupOutcome> CreateAsync()
			{
				await using var context = CreateContext(name);
				var service = new CorporateGroupService(context, new Infrastructure.Tenancy.CompanyAccessService(context), TimeProvider.System);
				return (await service.CreateGroupAsync(s.Admin, s.Holding, code, "گروه", TestContext.Current.CancellationToken)).Outcome;
			}

			for (var attempt = 0; attempt < 5; attempt++)
			{
				var outcomes = await Task.WhenAll(CreateAsync(), CreateAsync());
				Assert.Equal(1, outcomes.Count(outcome => outcome == CorporateGroupOutcome.Succeeded));
				Assert.All(outcomes.Where(outcome => outcome != CorporateGroupOutcome.Succeeded), outcome => Assert.Equal(CorporateGroupOutcome.Conflict, outcome));
				code = CorporateGroupScenario.Unique("race");
			}

			await using var check = CreateContext(name);
			Assert.Equal(5, await check.CorporateGroups.CountAsync(group => group.Code.StartsWith("race-"), TestContext.Current.CancellationToken));
		});
	}

	private static string[] CatalogStatements()
	{
		// The same statements the migration runs (scripts/generate_migrations.py). Read from
		// the compiled migration so the test can not drift from it.
		var migration = new Data.Migrations.AddCorporateGroups();
		var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
		typeof(Migration)
			.GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.Invoke(migration, [builder]);

		return builder.Operations
			.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
			.Select(operation => operation.Sql)
			.Where(sql => sql.StartsWith("IF NOT EXISTS", StringComparison.Ordinal))
			.ToArray();
	}
}
