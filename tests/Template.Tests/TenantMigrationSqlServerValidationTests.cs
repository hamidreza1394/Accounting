using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Template.Tests.TenantValidation;
using Xunit;

namespace Template.Tests;

/// <summary>
/// S3-0g gate G3 (PR-D): what a real SQL Server does with the operations the tenant migration plan relies
/// on (pinned rename, Up/Down, composite foreign key over a nullable column, NOT NULL for Guid, and the
/// unpinned rename R1). Every fact below was observed on a real SQL Server by running these tests; nothing
/// is assumed. Scope: a sample structure with hand-written migrations that live only in this test
/// assembly; no application migration, no real table, no real data.
///
/// Isolation (D6): each test creates its own database <c>FaranouTests_Tv_*</c> and drops it in
/// <c>finally</c>. Before anything is created the target is validated (<see cref="Target.Validate"/>).
/// The tests run only when <c>FARANOU_TEST_SQLSERVER</c> is set (CI sets <c>FARANOU_REQUIRE_SQLSERVER=1</c>);
/// otherwise they are reported as Skipped (NotRun), never as Passed.
/// </summary>
public sealed class TenantMigrationSqlServerValidationTests
{
	private const string CreateStructure = "20271202000001_Tv1CreateStructure";
	private const string RenamePinned = "20271202000002_Tv2RenamePinned";
	private const string NotNullGuid = "20271202000003_Tv3NotNullGuid";

	// ---- D6: the target is validated before any database is created ----

	internal static class Target
	{
		private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1", ".", "(local)"];

		/// <summary>
		/// A disposable test server only: a local address and the shared test database name prefix. Anything
		/// else (a customer server, a real database name) is refused before a connection is opened. The
		/// message never contains the connection string.
		/// </summary>
		public static void Validate(string connectionString)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);
			var host = builder.DataSource.Split(',')[0].Trim();
			if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
			{
				host = host[4..];
			}

			if (!LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("The SQL Server test target is not a local server; refusing to create or drop databases there.");
			}

			if (!builder.InitialCatalog.StartsWith("FaranouTests", StringComparison.Ordinal))
			{
				throw new InvalidOperationException("The SQL Server test target database name does not start with 'FaranouTests'; refusing to run.");
			}
		}
	}

	private static bool SqlServerAvailable()
	{
		if (Integration.IntegrationDatabase.UsesSqlServer)
		{
			return true;
		}

		if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
		{
			throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
		}

		return false;
	}

	private static string ConnectionFor(string database) =>
		new SqlConnectionStringBuilder(Integration.IntegrationDatabase.SqlServerConnectionString) { InitialCatalog = database }.ConnectionString;

	private static TenantValidationDbContext Context(string database) =>
		new(new DbContextOptionsBuilder<TenantValidationDbContext>()
			.UseSqlServer(ConnectionFor(database), sql => sql.MigrationsAssembly(typeof(TenantMigrationSqlServerValidationTests).Assembly.FullName))
			.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
			.Options);

	private static async Task DropAsync(string name)
	{
		var master = new SqlConnectionStringBuilder(Integration.IntegrationDatabase.SqlServerConnectionString) { InitialCatalog = "master" };
		await using var connection = new SqlConnection(master.ConnectionString);
		await connection.OpenAsync();
		await using var drop = new SqlCommand(
			"IF DB_ID(@name) IS NOT NULL BEGIN " +
			"DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name); " +
			"EXEC (@sql) END",
			connection);
		drop.Parameters.AddWithValue("@name", name);
		await drop.ExecuteNonQueryAsync();
	}

	/// <summary>Creates a disposable database (after validating the target), hands it over, always drops it.</summary>
	private static async Task WithDatabaseAsync(Func<string, TenantValidationDbContext, Task> test)
	{
		Target.Validate(Integration.IntegrationDatabase.SqlServerConnectionString!);
		var name = "FaranouTests_Tv_" + Guid.NewGuid().ToString("N")[..12];
		await using var context = Context(name);
		try
		{
			await context.Database.EnsureCreatedAsync(); // empty model: creates only the empty database
			await test(name, context);
		}
		finally
		{
			await DropAsync(name);
		}
	}

	private static Task MigrateAsync(TenantValidationDbContext context, string target) =>
		context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(target);

	private static string Script(TenantValidationDbContext context, string from, string to) =>
		context.GetInfrastructure().GetRequiredService<IMigrator>().GenerateScript(from, to);

	private static async Task<List<string>> QueryAsync(string database, string sql)
	{
		await using var connection = new SqlConnection(ConnectionFor(database));
		await connection.OpenAsync();
		await using var command = new SqlCommand(sql, connection);
		var rows = new List<string>();
		await using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i)) ?? "NULL")));
		}

		return rows;
	}

	private static async Task ExecuteAsync(string database, string sql)
	{
		await using var connection = new SqlConnection(ConnectionFor(database));
		await connection.OpenAsync();
		await using var command = new SqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<SqlException> RefusedAsync(string database, string sql) =>
		await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(database, sql));

	// the structure/identity listings of PR-SS-1, applied to the Tv* tables
	private static readonly string StructureQuery = RenameMigrationSqlServerTests.StructureQuery.Replace("'Rename%'", "'Tv%'", StringComparison.Ordinal);
	private static readonly string IdentityQuery = RenameMigrationSqlServerTests.IdentityQuery.Replace("'Rename%'", "'Tv%'", StringComparison.Ordinal);

	private static async Task<List<string>> StructureAsync(string database) => Sorted(await QueryAsync(database, StructureQuery));

	private static async Task<List<string>> IdentityAsync(string database) => Sorted(await QueryAsync(database, IdentityQuery));

	private static List<string> Sorted(IEnumerable<string> lines) => lines.OrderBy(line => line, StringComparer.Ordinal).ToList();

	/// <summary>The expected structure after a rename: only the renamed name tokens differ.</summary>
	private static List<string> WithRenames(IEnumerable<string> structure, params (string Old, string New)[] renames) =>
		Sorted(structure.Select(line => !line.Contains("|[TvParents]|", StringComparison.Ordinal) && !line.Contains("|[TvChildren]|", StringComparison.Ordinal)
			? line // the other sample tables (D3, D4) are not part of this rename
			: renames.Aggregate(line, (current, rename) => current.Replace($"[{rename.Old}]", $"[{rename.New}]", StringComparison.Ordinal))));

	private static readonly Guid OwnerA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
	private static readonly Guid OwnerB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
	private static readonly Guid ParentA1 = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");
	private static readonly Guid ParentA2 = Guid.Parse("a2a2a2a2-0000-0000-0000-000000000002");
	private static readonly Guid ParentB1 = Guid.Parse("b1b1b1b1-0000-0000-0000-000000000003");

	private static Task SeedStructureAsync(string database, string tenantColumn) => ExecuteAsync(database, $@"
INSERT INTO TvOwners (Id, Name) VALUES ('{OwnerA}', N'Owner A'), ('{OwnerB}', N'Owner B');
INSERT INTO TvParents (Id, [{tenantColumn}], Code, Name) VALUES
 ('{ParentA1}', '{OwnerA}', N'P-1', N'first'), ('{ParentA2}', '{OwnerA}', N'P-2', N'second'), ('{ParentB1}', '{OwnerB}', N'P-1', N'third');
INSERT INTO TvChildren (Id, [{tenantColumn}], ParentId, Note) VALUES
 (NEWID(), '{OwnerA}', '{ParentA1}', N'c1'), (NEWID(), '{OwnerA}', '{ParentA1}', N'c2'), (NEWID(), '{OwnerB}', '{ParentB1}', N'c3');");

	private static async Task<List<string>> DataAsync(string database, string tenantColumn)
	{
		var rows = new List<string>();
		rows.AddRange(await QueryAsync(database, $"SELECT CONCAT('owner|', Id, '|', Name) FROM TvOwners"));
		rows.AddRange(await QueryAsync(database, $"SELECT CONCAT('parent|', Id, '|', [{tenantColumn}], '|', Code, '|', Name) FROM TvParents"));
		rows.AddRange(await QueryAsync(database, $"SELECT CONCAT('child|', [{tenantColumn}], '|', ParentId, '|', Note) FROM TvChildren"));
		return Sorted(rows);
	}

	private static async Task AssertConstraintsStillEnforcedAsync(string database, string tenantColumn)
	{
		// FK to the owners
		var owner = await RefusedAsync(database, $"INSERT INTO TvParents (Id, [{tenantColumn}], Code, Name) VALUES (NEWID(), NEWID(), N'X', N'orphan')");
		Assert.Contains("FK_TvParents_TvOwners_TenantId", owner.Message, StringComparison.Ordinal);

		// unique index (tenant, code)
		var unique = await RefusedAsync(database, $"INSERT INTO TvParents (Id, [{tenantColumn}], Code, Name) VALUES (NEWID(), '{OwnerA}', N'P-1', N'duplicate')");
		Assert.Contains("UX_TvParents_", unique.Message, StringComparison.Ordinal);

		// composite FK that references the alternate key: right parent id, wrong owner
		var composite = await RefusedAsync(database, $"INSERT INTO TvChildren (Id, [{tenantColumn}], ParentId, Note) VALUES (NEWID(), '{OwnerB}', '{ParentA1}', N'cross-owner')");
		Assert.Contains("FK_TvChildren_TvParents_TenantId_ParentId", composite.Message, StringComparison.Ordinal);
	}

	// ---- D1 + D2 ----

	[Fact]
	public async Task D1_PinnedRename_MovesOnlyColumnAndIndexNames_AndKeepsEveryConstraint()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedStructureAsync(database, "TenantId");
			var structureBefore = await StructureAsync(database);
			var identityBefore = await IdentityAsync(database);

			// the sample really has every object kind of the scenario, under the legacy name
			foreach (var expected in new[]
			{
				"column|[TvParents]|", "key_constraint|[TvParents]|[AK_TvParents_TenantId_Id]|UNIQUE_CONSTRAINT",
				"index|[TvParents]|[UX_TvParents_TenantId_Code]|NONCLUSTERED|unique=1|pk=0|uc=0",
				"index|[TvParents]|[IX_TvParents_TenantId]|NONCLUSTERED|unique=0",
				"foreign_key|[TvParents]|[FK_TvParents_TvOwners_TenantId]|parent=[TenantId]",
				"foreign_key|[TvChildren]|[FK_TvChildren_TvParents_TenantId_ParentId]|parent=[TenantId],[ParentId]|ref=[TvParents][TenantId],[Id]|refindex=[AK_TvParents_TenantId_Id]",
			})
			{
				Assert.Contains(structureBefore, line => line.StartsWith(expected, StringComparison.Ordinal));
			}

			await MigrateAsync(context, RenamePinned);
			var structureAfter = await StructureAsync(database);

			// only the renamed tokens differ: the column, and the three index names that were renamed
			Assert.Equal(
				WithRenames(structureBefore,
					("TenantId", "CompanyId"),
					("UX_TvParents_TenantId_Code", "UX_TvParents_CompanyId_Code"),
					("IX_TvParents_TenantId", "IX_TvParents_CompanyId"),
					("IX_TvChildren_TenantId_ParentId", "IX_TvChildren_CompanyId_ParentId")),
				structureAfter);

			// nothing dropped and created again (object identity of every constraint and index is unchanged)
			Assert.Equal(identityBefore, await IdentityAsync(database));

			// the pinned AK and FK keep their legacy physical names (the OD-15 "keep" state), column is renamed
			Assert.Contains(structureAfter, line => line.StartsWith("key_constraint|[TvParents]|[AK_TvParents_TenantId_Id]|UNIQUE_CONSTRAINT", StringComparison.Ordinal));
			Assert.Contains(structureAfter, line => line.StartsWith("foreign_key|[TvChildren]|[FK_TvChildren_TvParents_TenantId_ParentId]|parent=[CompanyId],[ParentId]|ref=[TvParents][CompanyId],[Id]|refindex=[AK_TvParents_TenantId_Id]", StringComparison.Ordinal));
			Assert.DoesNotContain(structureAfter, line => line.Contains("column|[TvParents]|", StringComparison.Ordinal) && line.Contains("[TenantId]", StringComparison.Ordinal));

			await AssertConstraintsStillEnforcedAsync(database, "CompanyId");
		});
	}

	[Fact]
	public async Task D1b_RenameColumn_IsBlockedByAFilteredIndexThatNamesTheColumn_AndAllowedWhenTheFilterUsesOtherColumns()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);

			// the filter predicate mentions the column that is renamed (what an automatic "IS NOT NULL" filter on a
			// nullable tenant column would look like)
			await ExecuteAsync(database, "CREATE UNIQUE INDEX [UX_TvD4Items_TenantId_Filtered] ON [TvD4Items] ([TenantId]) WHERE [TenantId] IS NOT NULL;");
			var blocked = await RefusedAsync(database, "EXEC sp_rename N'[TvD4Items].[TenantId]', N'CompanyId', 'COLUMN';");
			Assert.Equal(5074, blocked.Number);
			Assert.Contains("UX_TvD4Items_TenantId_Filtered", blocked.Message, StringComparison.Ordinal);
			// nothing was renamed
			Assert.Equal(["TenantId"], await QueryAsync(database, "SELECT c.name COLLATE DATABASE_DEFAULT FROM sys.columns c WHERE c.object_id = OBJECT_ID('TvD4Items') AND c.name COLLATE DATABASE_DEFAULT IN ('TenantId','CompanyId')"));

			// a filter over other columns (like the HR index on Status/EndOnUtc) does not block the rename
			await ExecuteAsync(database, "DROP INDEX [UX_TvD4Items_TenantId_Filtered] ON [TvD4Items]; CREATE UNIQUE INDEX [UX_TvD4Items_TenantId_ByName] ON [TvD4Items] ([TenantId]) WHERE [Name] <> N'';");
			await ExecuteAsync(database, "EXEC sp_rename N'[TvD4Items].[TenantId]', N'CompanyId', 'COLUMN';");
			Assert.Equal(["CompanyId"], await QueryAsync(database, "SELECT c.name COLLATE DATABASE_DEFAULT FROM sys.columns c WHERE c.object_id = OBJECT_ID('TvD4Items') AND c.name COLLATE DATABASE_DEFAULT IN ('TenantId','CompanyId')"));
		});
	}

	[Fact]
	public async Task D1c_ARenameOfTheKeptLegacyConstraintNames_IsPossibleWithSpRename_ButTheOldNameCanNotBeTakenAgainOnTheSameTable()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, RenamePinned); // legacy constraint names are kept, the columns are renamed
			await SeedStructureAsync(database, "CompanyId");
			var identityBefore = await IdentityAsync(database);

			// OD-15 "keep": a NEW real TenantId index named like the kept legacy index would collide on the same table
			var collision = await RefusedAsync(database, "CREATE INDEX [AK_TvParents_TenantId_Id] ON [TvParents] ([Code]);");
			Assert.Equal(1913, collision.Number);

			// OD-15 "rename later": EF has no operation for it, it needs hand-written sp_rename ... 'OBJECT' (a schema-qualified constraint name, not
			// table.name); on a real server it keeps the
			// object identity and the foreign key that references the alternate key keeps pointing at it
			await ExecuteAsync(database, @"
EXEC sp_rename N'dbo.AK_TvParents_TenantId_Id', N'AK_TvParents_CompanyId_Id', 'OBJECT';
EXEC sp_rename N'dbo.FK_TvChildren_TvParents_TenantId_ParentId', N'FK_TvChildren_TvParents_CompanyId_ParentId', 'OBJECT';
EXEC sp_rename N'dbo.FK_TvParents_TvOwners_TenantId', N'FK_TvParents_TvOwners_CompanyId', 'OBJECT';");
			Assert.Equal(identityBefore, await IdentityAsync(database));
			var structure = await StructureAsync(database);
			Assert.Contains(structure, line => line.StartsWith("foreign_key|[TvChildren]|[FK_TvChildren_TvParents_CompanyId_ParentId]|parent=[CompanyId],[ParentId]|ref=[TvParents][CompanyId],[Id]|refindex=[AK_TvParents_CompanyId_Id]", StringComparison.Ordinal));
			Assert.DoesNotContain(structure, line => line.Contains("_TenantId_Id]", StringComparison.Ordinal) && line.Contains("|[TvParents]|", StringComparison.Ordinal));
			await AssertReferencedKeyStillEnforcedAsync(database);
		});
	}

	private static async Task AssertReferencedKeyStillEnforcedAsync(string database)
	{
		var composite = await RefusedAsync(database, $"INSERT INTO TvChildren (Id, [CompanyId], ParentId, Note) VALUES (NEWID(), '{OwnerB}', '{ParentA1}', N'cross-owner')");
		Assert.Contains("FK_TvChildren_TvParents_CompanyId_ParentId", composite.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task D1_ThePinnedRenameScripts_ContainNoDropOrCreateOfConstraintsOrIndexes()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync((_, context) =>
		{
			foreach (var script in new[] { Script(context, CreateStructure, RenamePinned), Script(context, RenamePinned, CreateStructure) })
			{
				Assert.Contains("sp_rename", script, StringComparison.OrdinalIgnoreCase);
				foreach (var forbidden in new[] { "DROP CONSTRAINT", "ADD CONSTRAINT", "DROP INDEX", "CREATE INDEX", "CREATE UNIQUE INDEX", "DROP TABLE", "DROP COLUMN", "ADD [" })
				{
					Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
				}
			}

			return Task.CompletedTask;
		});
	}

	[Fact]
	public async Task D2_UpThenDown_RestoresTheExactStructureIdentityAndData()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedStructureAsync(database, "TenantId");
			var structureBefore = await StructureAsync(database);
			var identityBefore = await IdentityAsync(database);
			var dataBefore = await DataAsync(database, "TenantId");
			var countsBefore = await QueryAsync(database, "SELECT CONCAT((SELECT COUNT(*) FROM TvOwners), '/', (SELECT COUNT(*) FROM TvParents), '/', (SELECT COUNT(*) FROM TvChildren))");
			Assert.Equal(["2/3/3"], countsBefore);

			// Up: the data values and row counts are kept under the new column name
			await MigrateAsync(context, RenamePinned);
			Assert.Equal(dataBefore, await DataAsync(database, "CompanyId"));
			Assert.Equal(countsBefore, await QueryAsync(database, "SELECT CONCAT((SELECT COUNT(*) FROM TvOwners), '/', (SELECT COUNT(*) FROM TvParents), '/', (SELECT COUNT(*) FROM TvChildren))"));
			Assert.NotEqual(structureBefore, await StructureAsync(database));

			// Down: exactly the original structure, with the original identity of every object, and the data
			await MigrateAsync(context, CreateStructure);
			Assert.Equal(structureBefore, await StructureAsync(database));
			Assert.Equal(identityBefore, await IdentityAsync(database));
			Assert.Equal(dataBefore, await DataAsync(database, "TenantId"));
			Assert.Equal(countsBefore, await QueryAsync(database, "SELECT CONCAT((SELECT COUNT(*) FROM TvOwners), '/', (SELECT COUNT(*) FROM TvParents), '/', (SELECT COUNT(*) FROM TvChildren))"));
			Assert.Equal(["20271202000001_Tv1CreateStructure"], await QueryAsync(database, "SELECT MigrationId FROM __EFMigrationsHistory"));
			await AssertConstraintsStillEnforcedAsync(database, "TenantId");
		});
	}

	// ---- D3: composite FK (CompanyId, TenantId) with a nullable TenantId ----

	private static readonly Guid CompanyOne = Guid.Parse("c1c1c1c1-0000-0000-0000-000000000001");
	private static readonly Guid CompanyTwo = Guid.Parse("c2c2c2c2-0000-0000-0000-000000000002");
	private static readonly Guid CompanyNoTenant = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000003");
	private static readonly Guid TenantOne = Guid.Parse("11111111-aaaa-0000-0000-000000000001");
	private static readonly Guid TenantTwo = Guid.Parse("22222222-aaaa-0000-0000-000000000002");

	private static Task InsertD3ChildAsync(string database, Guid companyId, Guid? tenantId) => ExecuteAsync(database,
		$"INSERT INTO TvD3Children (Id, CompanyId, TenantId, Name) VALUES (NEWID(), '{companyId}', {(tenantId is null ? "NULL" : $"'{tenantId}'")}, N'child');");

	private static async Task<long> D3ChildCountAsync(string database) =>
		long.Parse((await QueryAsync(database, "SELECT COUNT(*) FROM TvD3Children"))[0]);

	[Fact]
	public async Task D3_CompositeForeignKeyWithANullableTenantId_IsNotCheckedWhenTenantIdIsNull_ButIsCheckedWhenItIsSet()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await ExecuteAsync(database, $@"INSERT INTO TvD3Companies (Id, TenantId, Name) VALUES
 ('{CompanyOne}', '{TenantOne}', N'one'), ('{CompanyTwo}', '{TenantTwo}', N'two'), ('{CompanyNoTenant}', NULL, N'no tenant yet');");

			// ---- valid ----
			await InsertD3ChildAsync(database, CompanyOne, TenantOne);       // the pair exists
			await InsertD3ChildAsync(database, CompanyNoTenant, null);       // company without tenant, child without tenant
			await InsertD3ChildAsync(database, CompanyOne, null);            // NULL tenant: the constraint is not evaluated
			Assert.Equal(3, await D3ChildCountAsync(database));

			// ---- invalid: every non-NULL pair must exist exactly ----
			foreach (var (company, tenant) in new (Guid, Guid)[]
			{
				(CompanyOne, TenantTwo),        // right company, another company's tenant
				(CompanyTwo, TenantOne),        // swapped
				(Guid.NewGuid(), TenantOne),    // unknown company
				(CompanyNoTenant, TenantOne),   // company whose own TenantId is still NULL
			})
			{
				var refusal = await Assert.ThrowsAsync<SqlException>(() => InsertD3ChildAsync(database, company, tenant));
				Assert.Equal(547, refusal.Number);
				Assert.Contains("FK_TvD3Children_TvD3Companies_CompanyId_TenantId", refusal.Message, StringComparison.Ordinal);
			}

			Assert.Equal(3, await D3ChildCountAsync(database));

			// ---- the hole: NULL in the child turns the check off completely (MATCH SIMPLE) ----
			await InsertD3ChildAsync(database, Guid.NewGuid(), null);        // a company that does not exist at all is accepted
			Assert.Equal(4, await D3ChildCountAsync(database));

			// the parent is protected while a child with a set TenantId points at it ...
			var protectedParent = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(database, $"DELETE FROM TvD3Companies WHERE Id = '{CompanyOne}';"));
			Assert.Equal(547, protectedParent.Number);
			// ... but once that child is gone, the parent can be deleted although a child with a NULL TenantId still points at it
			await ExecuteAsync(database, $"DELETE FROM TvD3Children WHERE CompanyId = '{CompanyOne}' AND TenantId IS NOT NULL;");
			await ExecuteAsync(database, $"DELETE FROM TvD3Companies WHERE Id = '{CompanyOne}';");
			Assert.Equal(["0"], await QueryAsync(database, $"SELECT COUNT(*) FROM TvD3Companies WHERE Id = '{CompanyOne}'"));
			Assert.Equal(["1"], await QueryAsync(database, $"SELECT COUNT(*) FROM TvD3Children WHERE CompanyId = '{CompanyOne}' AND TenantId IS NULL"));
		});
	}

	// ---- D4: NOT NULL for a Guid column ----

	private static readonly Guid ItemOne = Guid.Parse("d1d1d1d1-0000-0000-0000-000000000001");
	private static readonly Guid ItemTwo = Guid.Parse("d2d2d2d2-0000-0000-0000-000000000002");
	private static readonly Guid ItemThree = Guid.Parse("d3d3d3d3-0000-0000-0000-000000000003");

	private static Task SeedD4Async(string database) => ExecuteAsync(database, $@"
INSERT INTO TvOwners (Id, Name) VALUES ('{OwnerA}', N'Owner A');
INSERT INTO TvD4Items (Id, Name, TenantId) VALUES ('{ItemOne}', N'valid', '{OwnerA}'), ('{ItemTwo}', N'no tenant', NULL), ('{ItemThree}', N'no tenant either', NULL);");

	private static Task<List<string>> D4StateAsync(string database) => QueryAsync(database, @"
SELECT CONCAT('column|', c.name COLLATE DATABASE_DEFAULT, '|nullable=', c.is_nullable) FROM sys.columns c WHERE c.object_id = OBJECT_ID('TvD4Items') AND c.name COLLATE DATABASE_DEFAULT = 'TenantId'
UNION ALL SELECT CONCAT('default|', d.name COLLATE DATABASE_DEFAULT) FROM sys.default_constraints d WHERE d.parent_object_id = OBJECT_ID('TvD4Items')
UNION ALL SELECT CONCAT('fk|', f.name COLLATE DATABASE_DEFAULT) FROM sys.foreign_keys f WHERE f.parent_object_id = OBJECT_ID('TvD4Items')
UNION ALL SELECT CONCAT('index|', i.name COLLATE DATABASE_DEFAULT) FROM sys.indexes i WHERE i.object_id = OBJECT_ID('TvD4Items') AND i.type > 0 AND i.is_primary_key = 0");

	private static async Task<List<string>> D4DataAsync(string database) =>
		Sorted(await QueryAsync(database, "SELECT CONCAT(Id, '|', Name, '|', ISNULL(CONVERT(varchar(36), TenantId), 'NULL')) FROM TvD4Items"));

	private static IReadOnlyList<string> GenerateSql(TenantValidationDbContext context, params MigrationOperation[] operations) =>
		context.GetInfrastructure().GetRequiredService<IMigrationsSqlGenerator>().Generate(operations, model: null).Select(command => command.CommandText).ToList();

	private static AlterColumnOperation NotNullGuid_AlterColumn(object? defaultValue = null) => new()
	{
		Table = "TvD4Items",
		Name = "TenantId",
		ClrType = typeof(Guid),
		ColumnType = "uniqueidentifier",
		IsNullable = false,
		DefaultValue = defaultValue,
		OldColumn = new AddColumnOperation { Table = "TvD4Items", Name = "TenantId", ClrType = typeof(Guid), ColumnType = "uniqueidentifier", IsNullable = true },
	};

	[Fact]
	public async Task D4_TheManualNotNullMigration_ThrowsWhileNullRowsExist_AndChangesNothing()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedD4Async(database);
			var stateBefore = await D4StateAsync(database);
			var dataBefore = await D4DataAsync(database);
			Assert.Contains("column|TenantId|nullable=1", stateBefore);
			Assert.DoesNotContain(stateBefore, line => line.StartsWith("default|", StringComparison.Ordinal));

			// the THROW guard stops the migration; EF surfaces the SQL error as it is
			var refusal = await Assert.ThrowsAsync<SqlException>(() => MigrateAsync(context, NotNullGuid));
			Assert.Equal(50001, refusal.Number);
			Assert.Contains("backfill first", refusal.Message, StringComparison.Ordinal);

			// nothing changed: still nullable, no default added, no NULL turned into a Guid, rows intact, migration not recorded
			Assert.Equal(stateBefore, await D4StateAsync(database));
			Assert.Equal(dataBefore, await D4DataAsync(database));
			Assert.Equal(["2"], await QueryAsync(database, "SELECT COUNT(*) FROM TvD4Items WHERE TenantId IS NULL"));
			Assert.Equal(["0"], await QueryAsync(database, "SELECT COUNT(*) FROM TvD4Items WHERE TenantId = '00000000-0000-0000-0000-000000000000'"));
			Assert.DoesNotContain(await QueryAsync(database, "SELECT MigrationId FROM __EFMigrationsHistory"), id => id == NotNullGuid);
		});
	}

	[Fact]
	public async Task D4_TheManualNotNullMigration_SucceedsOnlyAfterTheDataIsComplete_AndKeepsTheDependentForeignKeyAndIndex()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedD4Async(database);
			await Assert.ThrowsAsync<SqlException>(() => MigrateAsync(context, NotNullGuid));

			// the reviewed, separate data step: every NULL row gets a real owner id
			await ExecuteAsync(database, $"UPDATE TvD4Items SET TenantId = '{OwnerA}' WHERE TenantId IS NULL;");
			var dataBefore = await D4DataAsync(database);
			var stateBefore = await D4StateAsync(database);
			var identityBefore = await QueryAsync(database, "SELECT CONCAT('constraint|', o.object_id, '|', CONVERT(varchar(30), o.create_date, 121)) FROM sys.objects o WHERE o.parent_object_id = OBJECT_ID('TvD4Items') AND o.type IN ('PK','F')");

			await MigrateAsync(context, NotNullGuid);

			var stateAfter = await D4StateAsync(database);
			Assert.Equal(WithNullability(stateBefore, "column|TenantId|nullable=1", "column|TenantId|nullable=0"), stateAfter);
			Assert.DoesNotContain(stateAfter, line => line.StartsWith("default|", StringComparison.Ordinal)); // no DEFAULT added
			Assert.Equal(dataBefore, await D4DataAsync(database));                                            // values untouched
			Assert.Equal(["0"], await QueryAsync(database, "SELECT COUNT(*) FROM TvD4Items WHERE TenantId = '00000000-0000-0000-0000-000000000000'"));
			Assert.Equal(identityBefore, await QueryAsync(database, "SELECT CONCAT('constraint|', o.object_id, '|', CONVERT(varchar(30), o.create_date, 121)) FROM sys.objects o WHERE o.parent_object_id = OBJECT_ID('TvD4Items') AND o.type IN ('PK','F')"));

			// the column now refuses NULL, and the dependent FK still refuses an unknown owner
			var nullRefusal = await RefusedAsync(database, "INSERT INTO TvD4Items (Id, Name, TenantId) VALUES (NEWID(), N'x', NULL);");
			Assert.Equal(515, nullRefusal.Number);
			var fkRefusal = await RefusedAsync(database, "INSERT INTO TvD4Items (Id, Name, TenantId) VALUES (NEWID(), N'x', NEWID());");
			Assert.Contains("FK_TvD4Items_TvOwners_TenantId", fkRefusal.Message, StringComparison.Ordinal);

			// Down restores the nullable column and keeps the data
			await MigrateAsync(context, CreateStructure);
			Assert.Equal(stateBefore, await D4StateAsync(database));
			Assert.Equal(dataBefore, await D4DataAsync(database));
		});
	}

	private static List<string> WithNullability(IEnumerable<string> state, string from, string to) =>
		state.Select(line => line == from ? to : line).ToList();

	[Fact]
	public async Task D4_AlterColumnToNotNull_IsRefusedByTheServerWhileAnIndexDependsOnTheColumn()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedD4Async(database);
			await ExecuteAsync(database, $"UPDATE TvD4Items SET TenantId = '{OwnerA}' WHERE TenantId IS NULL;"); // the data is complete
			var stateBefore = await D4StateAsync(database);

			// complete data is not enough: the plain index IX_TvD4Items_TenantId blocks the column change
			var blocked = await RefusedAsync(database, "ALTER TABLE [TvD4Items] ALTER COLUMN [TenantId] uniqueidentifier NOT NULL;");
			Assert.Equal(5074, blocked.Number);
			Assert.Contains("IX_TvD4Items_TenantId", blocked.Message, StringComparison.Ordinal);
			Assert.Equal(stateBefore, await D4StateAsync(database));

			// with the index dropped first (and created again after) the same statement is accepted, with the foreign key in place
			await ExecuteAsync(database, "DROP INDEX [IX_TvD4Items_TenantId] ON [TvD4Items]; ALTER TABLE [TvD4Items] ALTER COLUMN [TenantId] uniqueidentifier NOT NULL; CREATE INDEX [IX_TvD4Items_TenantId] ON [TvD4Items] ([TenantId]);");
			Assert.Equal(WithNullability(stateBefore, "column|TenantId|nullable=1", "column|TenantId|nullable=0"), await D4StateAsync(database));
		});
	}

	[Fact]
	public async Task D4_AnEfGeneratedAlterColumnNeverFillsNullsOnItsOwn_AndOnlyAnExplicitDefaultTurnsNullsIntoAnEmptyGuid()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedD4Async(database);
			// the dependent index is out of the way, so only the NULL rows can be the reason for a refusal
			await ExecuteAsync(database, "DROP INDEX [IX_TvD4Items_TenantId] ON [TvD4Items];");
			var stateBefore = await D4StateAsync(database);

			// without a default value EF emits only the ALTER COLUMN: no UPDATE, no DEFAULT ...
			var plain = string.Join("\n", GenerateSql(context, NotNullGuid_AlterColumn()));
			Assert.Contains("ALTER COLUMN [TenantId] uniqueidentifier NOT NULL", plain, StringComparison.Ordinal);
			Assert.DoesNotContain("UPDATE", plain, StringComparison.OrdinalIgnoreCase);
			// (EF still emits the lookup that drops an existing default constraint; it never ADDs one)
			Assert.DoesNotContain("ADD DEFAULT", plain, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("DEFAULT (", plain, StringComparison.OrdinalIgnoreCase);

			// ... so SQL Server refuses it while NULL rows exist, and nothing changes
			var refusal = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(database, string.Join("\n", GenerateSql(context, NotNullGuid_AlterColumn()))));
			Assert.Equal(515, refusal.Number);
			Assert.Equal(stateBefore, await D4StateAsync(database));
			Assert.Equal(["2"], await QueryAsync(database, "SELECT COUNT(*) FROM TvD4Items WHERE TenantId IS NULL"));

			// the hazard: EF scaffolds defaultValue = Guid.Empty for a non-nullable Guid. That SQL silently rewrites the NULLs
			// to an owner that does not exist (and the foreign key then refuses the column change). A tenant migration must
			// never use it: it is why the NOT NULL step is hand-written with the THROW guard.
			var withDefault = string.Join("\n", GenerateSql(context, NotNullGuid_AlterColumn(Guid.Empty)));
			Assert.Contains("UPDATE [TvD4Items]", withDefault, StringComparison.Ordinal);
			Assert.Contains("00000000-0000-0000-0000-000000000000", withDefault, StringComparison.Ordinal);
		});
	}

	// ---- D5: scenario R1, the rename WITHOUT pinned constraint names ----

	private static string UnpinnedConnection(string database) => ConnectionFor(database);

	private static IReadOnlyList<MigrationOperation> UnpinnedOperations(string database, bool initialOnly)
	{
		using var before = new UnpinnedBeforeContext(new DbContextOptionsBuilder<UnpinnedBeforeContext>().UseSqlServer(UnpinnedConnection(database)).Options);
		using var after = new UnpinnedAfterContext(new DbContextOptionsBuilder<UnpinnedAfterContext>().UseSqlServer(UnpinnedConnection(database)).Options);
		var beforeModel = before.GetInfrastructure().GetRequiredService<IDesignTimeModel>().Model.GetRelationalModel();
		var afterModel = after.GetInfrastructure().GetRequiredService<IDesignTimeModel>().Model.GetRelationalModel();
		var differ = before.GetInfrastructure().GetRequiredService<IMigrationsModelDiffer>();
		return initialOnly ? differ.GetDifferences(null, beforeModel) : differ.GetDifferences(beforeModel, afterModel);
	}

	private static IReadOnlyList<string> UnpinnedSql(string database, IReadOnlyList<MigrationOperation> operations)
	{
		using var before = new UnpinnedBeforeContext(new DbContextOptionsBuilder<UnpinnedBeforeContext>().UseSqlServer(UnpinnedConnection(database)).Options);
		var generator = before.GetInfrastructure().GetRequiredService<IMigrationsSqlGenerator>();
		return generator.Generate(operations, model: null).Select(command => command.CommandText).ToList();
	}

	private static string Describe(MigrationOperation operation) => operation switch
	{
		RenameColumnOperation rename => $"RenameColumn {rename.Table}.{rename.Name}->{rename.NewName}",
		RenameIndexOperation rename => $"RenameIndex {rename.Name}->{rename.NewName}",
		DropForeignKeyOperation drop => $"DropForeignKey {drop.Name}",
		AddForeignKeyOperation add => $"AddForeignKey {add.Name}",
		DropUniqueConstraintOperation drop => $"DropUniqueConstraint {drop.Name}",
		AddUniqueConstraintOperation add => $"AddUniqueConstraint {add.Name}",
		DropIndexOperation drop => $"DropIndex {drop.Name}",
		CreateIndexOperation create => $"CreateIndex {create.Name}",
		_ => operation.GetType().Name,
	};

	private static readonly string UnpinnedStructureQuery = RenameMigrationSqlServerTests.StructureQuery.Replace("'Rename%'", "'Tvr%'", StringComparison.Ordinal);
	private static readonly string UnpinnedIdentityQuery = RenameMigrationSqlServerTests.IdentityQuery.Replace("'Rename%'", "'Tvr%'", StringComparison.Ordinal);

	[Fact]
	public async Task D5_R1_TheUnpinnedRename_IsGeneratedAsDropAndAddOfTheKeysAndTheForeignKey_AndIsRefusedWhileAForeignKeyReferencesTheAlternateKey()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, _) =>
		{
			// what EF generates for the property rename without pinned names
			var operations = UnpinnedOperations(database, initialOnly: false);
			var described = operations.Select(Describe).ToList();
			Assert.Contains("RenameColumn TvrPeriods.TenantId->CompanyId", described);
			Assert.Contains("DropUniqueConstraint AK_TvrPeriods_Id_TenantId", described);
			Assert.Contains("AddUniqueConstraint AK_TvrPeriods_Id_CompanyId", described);
			Assert.Contains("DropForeignKey FK_TvrPeriods_TvrOwners_TenantId", described);
			Assert.Contains("AddForeignKey FK_TvrPeriods_TvrOwners_CompanyId", described);
			Assert.Contains("RenameIndex IX_TvrPeriods_TenantId_Status->IX_TvrPeriods_CompanyId_Status", described);

			// the "before" schema, created from the model, with data
			foreach (var sql in UnpinnedSql(database, UnpinnedOperations(database, initialOnly: true)))
			{
				await ExecuteAsync(database, sql);
			}

			var owner = Guid.NewGuid();
			var period = Guid.NewGuid();
			await ExecuteAsync(database, $@"INSERT INTO TvrOwners (Id) VALUES ('{owner}');
INSERT INTO TvrPeriods (Id, TenantId, Status) VALUES ('{period}', '{owner}', N'Open');
INSERT INTO TvrClaims (Id, PeriodId, CompanyId) VALUES (NEWID(), '{period}', '{owner}');");
			var structureBefore = Sorted(await QueryAsync(database, UnpinnedStructureQuery));
			var identityBefore = Sorted(await QueryAsync(database, UnpinnedIdentityQuery));
			Assert.Contains(structureBefore, line => line.Contains("[AK_TvrPeriods_Id_TenantId]", StringComparison.Ordinal));
			Assert.Contains(structureBefore, line => line.Contains("[FK_TvrClaims_TvrPeriods_PeriodId_CompanyId]", StringComparison.Ordinal) && line.Contains("refindex=[AK_TvrPeriods_Id_TenantId]", StringComparison.Ordinal));

			// run the generated "Up" the way the migrator does: one transaction, rolled back on failure
			await using var connection = new SqlConnection(ConnectionFor(database));
			await connection.OpenAsync();
			await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync())
			{
				var failure = await Record.ExceptionAsync(async () =>
				{
					foreach (var sql in UnpinnedSql(database, operations))
					{
						await using var command = new SqlCommand(sql, connection, transaction);
						await command.ExecuteNonQueryAsync();
					}
				});
				var refusal = Assert.IsType<SqlException>(failure);
				Assert.Equal(3725, refusal.Number);
				await transaction.RollbackAsync();
			}

			// nothing was applied: structure, identity and data are exactly as before (the migration is atomic)
			Assert.Equal(structureBefore, Sorted(await QueryAsync(database, UnpinnedStructureQuery)));
			Assert.Equal(identityBefore, Sorted(await QueryAsync(database, UnpinnedIdentityQuery)));
			Assert.Equal(["1/1/1"], await QueryAsync(database, "SELECT CONCAT((SELECT COUNT(*) FROM TvrOwners), '/', (SELECT COUNT(*) FROM TvrPeriods), '/', (SELECT COUNT(*) FROM TvrClaims))"));
		});
	}

	[Fact]
	public async Task D5_R1_WithoutADependentForeignKey_TheUnpinnedRenameSucceedsButReplacesEveryKeyAndForeignKeyWithNewObjects()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, _) =>
		{
			var operations = UnpinnedOperations(database, initialOnly: false);
			foreach (var sql in UnpinnedSql(database, UnpinnedOperations(database, initialOnly: true)))
			{
				await ExecuteAsync(database, sql);
			}

			// no referencing table: the situation in which R1 does not fail
			await ExecuteAsync(database, "DROP TABLE TvrClaims;");
			var owner = Guid.NewGuid();
			await ExecuteAsync(database, $"INSERT INTO TvrOwners (Id) VALUES ('{owner}'); INSERT INTO TvrPeriods (Id, TenantId, Status) VALUES (NEWID(), '{owner}', N'Open');");
			var identityBefore = Sorted(await QueryAsync(database, UnpinnedIdentityQuery));

			await using (var connection = new SqlConnection(ConnectionFor(database)))
			{
				await connection.OpenAsync();
				await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
				foreach (var sql in UnpinnedSql(database, operations.Where(operation => operation is not DropForeignKeyOperation { Table: "TvrClaims" } and not AddForeignKeyOperation { Table: "TvrClaims" }).ToList()))
				{
					await using var command = new SqlCommand(sql, connection, transaction);
					await command.ExecuteNonQueryAsync();
				}

				await transaction.CommitAsync();
			}

			var structureAfter = Sorted(await QueryAsync(database, UnpinnedStructureQuery));
			Assert.Contains(structureAfter, line => line.Contains("[AK_TvrPeriods_Id_CompanyId]", StringComparison.Ordinal));
			Assert.Contains(structureAfter, line => line.Contains("[FK_TvrPeriods_TvrOwners_CompanyId]", StringComparison.Ordinal));
			Assert.DoesNotContain(structureAfter, line => line.Contains("[AK_TvrPeriods_Id_TenantId]", StringComparison.Ordinal));
			Assert.Equal(["1/1"], await QueryAsync(database, "SELECT CONCAT((SELECT COUNT(*) FROM TvrOwners), '/', (SELECT COUNT(*) FROM TvrPeriods))"));

			// the keys are NEW objects (dropped and created again): this is what the pinned rename (D1) avoids
			var constraintsBefore = identityBefore.Where(line => line.StartsWith("constraint|", StringComparison.Ordinal)).ToList();
			var constraintsAfter = (await QueryAsync(database, UnpinnedIdentityQuery)).Where(line => line.StartsWith("constraint|", StringComparison.Ordinal)).ToList();
			Assert.Equal(constraintsBefore.Count, constraintsAfter.Count);
			Assert.NotEmpty(constraintsBefore.Except(constraintsAfter));
		});
	}

	// ---- D6 ----

	[Theory]
	[InlineData("Server=prod-sql.customer.example,1433;Database=FaranouTests;User Id=sa;Password=not-a-real-secret;")]
	[InlineData("Server=10.1.2.3;Database=FaranouTests;User Id=sa;Password=not-a-real-secret;")]
	[InlineData("Server=localhost,1433;Database=Farano;User Id=sa;Password=not-a-real-secret;")]
	[InlineData("Server=localhost;Database=FaranouProduction;User Id=sa;Password=not-a-real-secret;")]
	public void D6_ATargetThatIsNotADisposableTestServer_IsRefusedBeforeAnythingHappens(string connectionString)
	{
		var refusal = Assert.Throws<InvalidOperationException>(() => Target.Validate(connectionString));
		Assert.DoesNotContain("not-a-real-secret", refusal.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("customer", refusal.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData("Server=localhost,1433;Database=FaranouTests;User Id=sa;Password=x;")]
	[InlineData("Server=127.0.0.1,14333;Database=FaranouTests;User Id=sa;Password=x;")]
	[InlineData("Server=tcp:localhost,1433;Database=FaranouTests;User Id=sa;Password=x;")]
	public void D6_TheLocalDisposableTestTarget_IsAccepted(string connectionString) => Target.Validate(connectionString);

	[Fact]
	public async Task D6_TheTestDatabase_HasADedicatedName_AndIsDroppedEvenWhenTheTestFails()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		string? created = null;
		await Assert.ThrowsAsync<InvalidOperationException>(() => WithDatabaseAsync((name, _) =>
		{
			created = name;
			Assert.StartsWith("FaranouTests_Tv_", name, StringComparison.Ordinal);
			throw new InvalidOperationException("the test fails on purpose");
		}));

		Assert.NotNull(created);
		var master = new SqlConnectionStringBuilder(Integration.IntegrationDatabase.SqlServerConnectionString) { InitialCatalog = "master" };
		await using var connection = new SqlConnection(master.ConnectionString);
		await connection.OpenAsync();
		await using var exists = new SqlCommand("SELECT DB_ID(@name)", connection);
		exists.Parameters.AddWithValue("@name", created);
		Assert.True(await exists.ExecuteScalarAsync() is null or DBNull);
	}
}
