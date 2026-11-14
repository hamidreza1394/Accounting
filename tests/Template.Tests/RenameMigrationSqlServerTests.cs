using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests;

/// <summary>
/// PR-SS-1: what a real SQL Server does with <c>RenameColumn</c> / <c>RenameIndex</c> (the operations
/// the Migration Guard allows since PR-2b) to foreign keys, alternate keys and indexes, in both
/// directions (Up and Down).
///
/// Scope: a tiny sample structure (<c>RenameOwners</c>, <c>RenameChildren</c>,
/// <c>RenameGrandChildren</c>) with three hand-written migrations that live only in this test
/// assembly. No application migration, no real table and no real data is touched. Every test
/// creates its own database named <c>FaranouTests_Rename_*</c> and drops it in <c>finally</c>.
/// Runs only when <c>FARANOU_TEST_SQLSERVER</c> is set (same variable and rule as the other
/// SQL Server tests; CI sets <c>FARANOU_REQUIRE_SQLSERVER=1</c>).
/// </summary>
public sealed class RenameMigrationSqlServerTests
{
	private const string CreateStructure = "20271201000001_Rt1CreateStructure";
	private const string RenameColumnStep = "20271201000002_Rt2RenameColumn";
	private const string RenameIndexStep = "20271201000003_Rt3RenameIndex";

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

	private static string ConnectionFor(string database)
	{
		var builder = new SqlConnectionStringBuilder(Integration.IntegrationDatabase.SqlServerConnectionString)
		{
			InitialCatalog = database,
		};
		return builder.ConnectionString;
	}

	private static string MasterConnection()
	{
		var builder = new SqlConnectionStringBuilder(Integration.IntegrationDatabase.SqlServerConnectionString)
		{
			InitialCatalog = "master",
		};
		return builder.ConnectionString;
	}

	private static RenameValidation.RenameTestDbContext Context(string database) =>
		new(new DbContextOptionsBuilder<RenameValidation.RenameTestDbContext>()
			.UseSqlServer(ConnectionFor(database), sql => sql.MigrationsAssembly(typeof(RenameMigrationSqlServerTests).Assembly.FullName))
			.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
			.Options);

	/// <summary>Creates a disposable database, hands it to the test and always drops it again.</summary>
	private static async Task WithDatabaseAsync(Func<string, RenameValidation.RenameTestDbContext, Task> test)
	{
		var name = "FaranouTests_Rename_" + Guid.NewGuid().ToString("N")[..12];
		await using var context = Context(name);
		try
		{
			await context.Database.EnsureCreatedAsync(); // empty model: creates only the empty database
			await test(name, context);
		}
		finally
		{
			await using var master = new SqlConnection(MasterConnection());
			await master.OpenAsync();
			await using var drop = new SqlCommand(
				"IF DB_ID(@name) IS NOT NULL BEGIN " +
				"DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name); " +
				"EXEC (@sql) END",
				master);
			drop.Parameters.AddWithValue("@name", name);
			await drop.ExecuteNonQueryAsync();
		}
	}

	private static Task MigrateAsync(RenameValidation.RenameTestDbContext context, string target) =>
		context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(target);

	private static string Script(RenameValidation.RenameTestDbContext context, string from, string to) =>
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

	// Names are always written in brackets so that a column or index rename can be applied to a
	// snapshot as an exact token replacement ("[OwnerId]" -> "[CompanyId]").
	private const string StructureQuery = @"
SELECT 'column' AS kind, CONCAT('[', t.name COLLATE DATABASE_DEFAULT, ']|', c.column_id, '|[', c.name COLLATE DATABASE_DEFAULT, ']|', ty.name COLLATE DATABASE_DEFAULT, '|', c.max_length, '|', c.is_nullable, '|', c.is_identity) AS line
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.name COLLATE DATABASE_DEFAULT LIKE 'Rename%'
UNION ALL
SELECT 'index', CONCAT('[', t.name COLLATE DATABASE_DEFAULT, ']|[', i.name COLLATE DATABASE_DEFAULT, ']|', i.type_desc COLLATE DATABASE_DEFAULT, '|unique=', i.is_unique, '|pk=', i.is_primary_key, '|uc=', i.is_unique_constraint, '|filter=', ISNULL(i.filter_definition COLLATE DATABASE_DEFAULT, ''), '|keys=',
  (SELECT STRING_AGG(CONCAT('[', c.name COLLATE DATABASE_DEFAULT, ']', CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END), ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
   FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
   WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0))
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name COLLATE DATABASE_DEFAULT LIKE 'Rename%' AND i.type > 0
UNION ALL
SELECT 'key_constraint', CONCAT('[', t.name COLLATE DATABASE_DEFAULT, ']|[', k.name COLLATE DATABASE_DEFAULT, ']|', k.type_desc COLLATE DATABASE_DEFAULT)
FROM sys.key_constraints k JOIN sys.tables t ON t.object_id = k.parent_object_id
WHERE t.name COLLATE DATABASE_DEFAULT LIKE 'Rename%'
UNION ALL
SELECT 'foreign_key', CONCAT('[', pt.name COLLATE DATABASE_DEFAULT, ']|[', fk.name COLLATE DATABASE_DEFAULT, ']|parent=',
  (SELECT STRING_AGG(CONCAT('[', pc.name COLLATE DATABASE_DEFAULT, ']'), ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
   FROM sys.foreign_key_columns fkc JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
   WHERE fkc.constraint_object_id = fk.object_id),
  '|ref=[', rt.name COLLATE DATABASE_DEFAULT, ']',
  (SELECT STRING_AGG(CONCAT('[', rc.name COLLATE DATABASE_DEFAULT, ']'), ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
   FROM sys.foreign_key_columns fkc JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
   WHERE fkc.constraint_object_id = fk.object_id),
  '|refindex=[', ri.name COLLATE DATABASE_DEFAULT, ']|delete=', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, '|update=', fk.update_referential_action_desc COLLATE DATABASE_DEFAULT, '|disabled=', fk.is_disabled, '|untrusted=', fk.is_not_trusted)
FROM sys.foreign_keys fk JOIN sys.tables pt ON pt.object_id = fk.parent_object_id JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.indexes ri ON ri.object_id = fk.referenced_object_id AND ri.index_id = fk.key_index_id
WHERE pt.name COLLATE DATABASE_DEFAULT LIKE 'Rename%'";

	// Object identity: a constraint (PK, alternate key, foreign key) that was dropped and created
	// again gets a new object_id and create_date; sp_rename changes neither. A plain index that is
	// dropped and created again can get the SAME index_id back, so the identity listing can not prove
	// R1 for plain indexes: that part is covered by the script check (no DROP/CREATE INDEX in the SQL
	// that EF generates and runs). Mutation check done for this PR: an extra DropIndex/CreateIndex in
	// the rename migration is caught by that script check.
	private const string IdentityQuery = @"
SELECT CONCAT('constraint|', o.object_id, '|', CONVERT(varchar(30), o.create_date, 121), '|', o.type_desc COLLATE DATABASE_DEFAULT) FROM sys.objects o
JOIN sys.tables t ON t.object_id = o.parent_object_id WHERE t.name COLLATE DATABASE_DEFAULT LIKE 'Rename%' AND o.type IN ('PK','UQ','F')
UNION ALL
SELECT CONCAT('index|[', t.name COLLATE DATABASE_DEFAULT, ']|', i.index_id, '|', i.type_desc COLLATE DATABASE_DEFAULT, '|', i.is_unique_constraint) FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id WHERE t.name COLLATE DATABASE_DEFAULT LIKE 'Rename%' AND i.type > 0";

	private static async Task<List<string>> StructureAsync(string database) =>
		(await QueryAsync(database, StructureQuery)).ToList();

	private static async Task<List<string>> IdentityAsync(string database) =>
		(await QueryAsync(database, IdentityQuery)).ToList();

	/// <summary>The structure that a rename has to produce: only the renamed name tokens differ.</summary>
	private static List<string> WithRename(IEnumerable<string> structure, string oldName, string newName) =>
		structure.Select(line => line.Replace($"[{oldName}]", $"[{newName}]", StringComparison.Ordinal)).OrderBy(line => line, StringComparer.Ordinal).ToList();

	private static List<string> Sorted(IEnumerable<string> lines) => lines.OrderBy(line => line, StringComparer.Ordinal).ToList();

	private static async Task SeedAsync(string database, string ownerColumn = "OwnerId") =>
		await ExecuteAsync(database, $@"
INSERT INTO RenameOwners (Id, Name) VALUES (1, N'Owner 1'), (2, N'Owner 2');
INSERT INTO RenameChildren ([{ownerColumn}], Code, Name) VALUES (1, N'A', N'first'), (1, N'B', N'second'), (2, N'A', N'third');
INSERT INTO RenameGrandChildren (ChildOwnerId, ChildCode, Note) VALUES (1, N'A', N'g1'), (2, N'A', N'g2');");

	private static async Task<List<string>> DataAsync(string database, string ownerColumn) =>
		(await QueryAsync(database, $"SELECT CONCAT(c.[{ownerColumn}], '|', c.Code, '|', c.Name) FROM RenameChildren c ORDER BY 1")).ToList();

	private static async Task AssertConstraintsStillEnforcedAsync(string database, string ownerColumn)
	{
		// FK to the owners
		var foreignKey = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(database,
			$"INSERT INTO RenameChildren ([{ownerColumn}], Code, Name) VALUES (999, N'Z', N'orphan')"));
		Assert.Contains("FOREIGN KEY", foreignKey.Message, StringComparison.OrdinalIgnoreCase);

		// alternate key (owner, code)
		var alternateKey = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(database,
			$"INSERT INTO RenameChildren ([{ownerColumn}], Code, Name) VALUES (1, N'A', N'duplicate')"));
		Assert.Contains("AK_RenameChildren_Owner_Code", alternateKey.Message, StringComparison.Ordinal);

		// foreign key that references the alternate key
		var referencing = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(database,
			"INSERT INTO RenameGrandChildren (ChildOwnerId, ChildCode, Note) VALUES (1, N'NOPE', N'orphan')"));
		Assert.Contains("FOREIGN KEY", referencing.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task RenameColumn_Up_KeepsForeignKeyAlternateKeyAndIndex_AndDown_RestoresTheExactStructure()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			await SeedAsync(database);
			var structureBefore = await StructureAsync(database);
			var identityBefore = await IdentityAsync(database);
			var dataBefore = await DataAsync(database, "OwnerId");

			// the sample really has the three constraint kinds the scenario is about
			Assert.Contains(structureBefore, line => line.StartsWith("foreign_key|[RenameChildren]|[FK_RenameChildren_RenameOwners_OwnerId]", StringComparison.Ordinal));
			Assert.Contains(structureBefore, line => line.StartsWith("key_constraint|[RenameChildren]|[AK_RenameChildren_Owner_Code]|UNIQUE_CONSTRAINT", StringComparison.Ordinal));
			Assert.Contains(structureBefore, line => line.StartsWith("index|[RenameChildren]|[IX_RenameChildren_OwnerId]", StringComparison.Ordinal));
			Assert.Contains(structureBefore, line => line.StartsWith("foreign_key|[RenameGrandChildren]|[FK_RenameGrandChildren_RenameChildren]", StringComparison.Ordinal));

			// Up
			await MigrateAsync(context, RenameColumnStep);
			var structureAfter = await StructureAsync(database);
			Assert.Equal(WithRename(structureBefore, "OwnerId", "CompanyId"), Sorted(structureAfter));
			Assert.Equal(Sorted(identityBefore), Sorted(await IdentityAsync(database))); // R1: nothing dropped and created again
			Assert.Equal(dataBefore.Select(row => row), await DataAsync(database, "CompanyId"));
			Assert.DoesNotContain(structureAfter, line => line.Contains("[OwnerId]", StringComparison.Ordinal));
			await AssertConstraintsStillEnforcedAsync(database, "CompanyId");

			// Down: exactly the original structure, with the original identity of every object
			await MigrateAsync(context, CreateStructure);
			Assert.Equal(Sorted(structureBefore), Sorted(await StructureAsync(database)));
			Assert.Equal(Sorted(identityBefore), Sorted(await IdentityAsync(database)));
			Assert.Equal(dataBefore, await DataAsync(database, "OwnerId"));
			await AssertConstraintsStillEnforcedAsync(database, "OwnerId");
		});
	}

	[Fact]
	public async Task RenameIndex_Up_AndDown_ChangeOnlyTheIndexName()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, RenameColumnStep);
			await SeedAsync(database, "CompanyId");
			var structureBefore = await StructureAsync(database);
			var identityBefore = await IdentityAsync(database);

			await MigrateAsync(context, RenameIndexStep);
			Assert.Equal(
				WithRename(structureBefore, "IX_RenameChildren_OwnerId", "IX_RenameChildren_CompanyId"),
				Sorted(await StructureAsync(database)));
			Assert.Equal(Sorted(identityBefore), Sorted(await IdentityAsync(database)));
			await AssertConstraintsStillEnforcedAsync(database, "CompanyId");

			await MigrateAsync(context, RenameColumnStep);
			Assert.Equal(Sorted(structureBefore), Sorted(await StructureAsync(database)));
			Assert.Equal(Sorted(identityBefore), Sorted(await IdentityAsync(database)));
		});
	}

	[Fact]
	public async Task TheRenameScripts_ContainNoDropOrCreateOfConstraintsOrIndexes()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync((_, context) =>
		{
			var up = Script(context, CreateStructure, RenameIndexStep);
			var down = Script(context, RenameIndexStep, CreateStructure);

			foreach (var script in new[] { up, down })
			{
				Assert.Contains("sp_rename", script, StringComparison.OrdinalIgnoreCase);
				foreach (var forbidden in new[] { "DROP CONSTRAINT", "ADD CONSTRAINT", "DROP INDEX", "CREATE INDEX", "CREATE UNIQUE INDEX", "DROP TABLE", "DROP COLUMN", "ADD [" })
				{
					Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
				}
			}

			Assert.Contains("'COLUMN'", up, StringComparison.Ordinal);
			Assert.Contains("'INDEX'", up, StringComparison.Ordinal);
			return Task.CompletedTask;
		});
	}

	[Fact]
	public async Task NegativeControl_ADropAndCreateOfTheSameConstraintsKeepsTheStructureButIsDetectedByTheIdentityCheck()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (database, context) =>
		{
			await MigrateAsync(context, CreateStructure);
			var structureBefore = await StructureAsync(database);
			var identityBefore = await IdentityAsync(database);

			// What an unintended "rename as Drop + Add" would do: same names and definitions again.
			await ExecuteAsync(database, @"
ALTER TABLE RenameGrandChildren DROP CONSTRAINT FK_RenameGrandChildren_RenameChildren;
ALTER TABLE RenameChildren DROP CONSTRAINT FK_RenameChildren_RenameOwners_OwnerId;
DROP INDEX IX_RenameChildren_OwnerId ON RenameChildren;
ALTER TABLE RenameChildren DROP CONSTRAINT AK_RenameChildren_Owner_Code;
ALTER TABLE RenameChildren ADD CONSTRAINT AK_RenameChildren_Owner_Code UNIQUE (OwnerId, Code);
CREATE INDEX IX_RenameChildren_OwnerId ON RenameChildren (OwnerId);
ALTER TABLE RenameChildren ADD CONSTRAINT FK_RenameChildren_RenameOwners_OwnerId FOREIGN KEY (OwnerId) REFERENCES RenameOwners (Id);
ALTER TABLE RenameGrandChildren ADD CONSTRAINT FK_RenameGrandChildren_RenameChildren FOREIGN KEY (ChildOwnerId, ChildCode) REFERENCES RenameChildren (OwnerId, Code);");

			// the structure listing alone can not tell the difference ...
			Assert.Equal(Sorted(structureBefore), Sorted(await StructureAsync(database)));
			// ... the identity check does, so a real R1 violation would fail the tests above.
			Assert.NotEqual(Sorted(identityBefore), Sorted(await IdentityAsync(database)));
		});
	}

	[Fact]
	public async Task TheTestDatabase_IsDisposable_AndUsesADedicatedName()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		string? created = null;
		await Assert.ThrowsAsync<InvalidOperationException>(() => WithDatabaseAsync((name, _) =>
		{
			created = name;
			Assert.StartsWith("FaranouTests_Rename_", name, StringComparison.Ordinal);
			throw new InvalidOperationException("the test fails on purpose");
		}));

		// the database is dropped although the test body threw
		Assert.NotNull(created);
		await using var master = new SqlConnection(MasterConnection());
		await master.OpenAsync();
		await using var exists = new SqlCommand("SELECT DB_ID(@name)", master);
		exists.Parameters.AddWithValue("@name", created);
		Assert.True(await exists.ExecuteScalarAsync() is null or DBNull);
	}
}
