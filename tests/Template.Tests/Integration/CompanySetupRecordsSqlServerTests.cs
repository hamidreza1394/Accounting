using System;
using System.Linq;
using System.Threading.Tasks;
using Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Validates the CompanySetupRecords migration (Issue #392): the schema, the binary-collated
/// unique reference, the one-to-one company binding, the delete restriction and the Up/Down
/// reversibility. Each test uses a uniquely named disposable database; it never touches the
/// shared test database.
/// </summary>
public sealed class CompanySetupRecordsSqlServerTests
{
	private const string PreviousMigration = "20261110130000_MakeCompanyMembershipRoleOptional";
	private const string SetupRecordsMigration = "20261116070805_AddCompanySetupRecords";

	private static bool SqlServerAvailable()
	{
		if (IntegrationDatabase.UsesSqlServer)
			return true;

		if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
			throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");

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
		var name = "FaranouTests_SetupRecords_" + Guid.NewGuid().ToString("N")[..12];
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

	private static Company NewCompany(string name) => new() { Name = name, IsActive = true };

	private static CompanySetupRecord NewRecord(Guid companyId, string reference, string installationId) => new()
	{
		CompanyId = companyId,
		CompanyReference = reference,
		InstallationId = installationId,
		CreatedAtUtc = DateTime.UtcNow,
	};

	[Fact]
	public async Task Migration_IsReversible_And_Enforces_Binary_Unique_Reference_And_Restricted_Company_Delete()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async name =>
		{
			Guid mappedCompanyId;
			await using (var before = CreateContext(name))
			{
				await before.GetService<IMigrator>().MigrateAsync(
					PreviousMigration, TestContext.Current.CancellationToken);

				before.Companies.Add(NewCompany("شرکت نگاشت‌نشده"));
				var mapped = NewCompany("شرکت نگاشت‌شده");
				before.Companies.Add(mapped);
				await before.SaveChangesAsync(TestContext.Current.CancellationToken);
				mappedCompanyId = mapped.Id;
			}

			await using (var context = CreateContext(name))
			{
				await context.GetService<IMigrator>().MigrateAsync(
					SetupRecordsMigration, TestContext.Current.CancellationToken);
				Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));

				var collation = await context.Database
					.SqlQueryRaw<string>(
						"SELECT [collation_name] AS [Value] FROM sys.columns " +
						"WHERE [object_id] = OBJECT_ID(N'dbo.CompanySetupRecords') AND [name] = N'CompanyReference'")
					.SingleAsync(TestContext.Current.CancellationToken);
				Assert.Equal("Latin1_General_BIN2", collation);

				context.CompanySetupRecords.Add(NewRecord(mappedCompanyId, "ACC-0001", "installation-a"));
				await context.SaveChangesAsync(TestContext.Current.CancellationToken);

				context.ChangeTracker.Clear();
				var referenceCompany = NewCompany("شرکت مرجع حروف‌متفاوت");
				context.Companies.Add(referenceCompany);
				await context.SaveChangesAsync(TestContext.Current.CancellationToken);

				// Binary collation: a case-distinct reference is a distinct identity.
				context.ChangeTracker.Clear();
				context.CompanySetupRecords.Add(NewRecord(
					referenceCompany.Id, "acc-0001", "installation-a"));
				await context.SaveChangesAsync(TestContext.Current.CancellationToken);

				// The exact same reference cannot map a second company.
				context.ChangeTracker.Clear();
				var duplicateCompany = NewCompany("شرکت مرجع تکراری");
				context.Companies.Add(duplicateCompany);
				await context.SaveChangesAsync(TestContext.Current.CancellationToken);
				context.ChangeTracker.Clear();
				context.CompanySetupRecords.Add(NewRecord(
					duplicateCompany.Id, "ACC-0001", "installation-a"));
				await Assert.ThrowsAsync<DbUpdateException>(() =>
					context.SaveChangesAsync(TestContext.Current.CancellationToken));

				// One setup record at most per company.
				context.ChangeTracker.Clear();
				context.CompanySetupRecords.Add(NewRecord(
					mappedCompanyId, "ACC-0002", "installation-a"));
				await Assert.ThrowsAsync<DbUpdateException>(() =>
					context.SaveChangesAsync(TestContext.Current.CancellationToken));

				// The company of a mapped record is delete-restricted.
				context.ChangeTracker.Clear();
				var mappedCompany = await context.Companies.SingleAsync(
					company => company.Id == mappedCompanyId, TestContext.Current.CancellationToken);
				context.Companies.Remove(mappedCompany);
				await Assert.ThrowsAsync<DbUpdateException>(() =>
					context.SaveChangesAsync(TestContext.Current.CancellationToken));
			}

			await using (var afterDown = CreateContext(name))
			{
				await afterDown.GetService<IMigrator>().MigrateAsync(
					PreviousMigration, TestContext.Current.CancellationToken);

				Assert.Equal(3, await afterDown.Companies.CountAsync(TestContext.Current.CancellationToken));
				var tables = await afterDown.Database.SqlQueryRaw<string>(
					"SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'CompanySetupRecords'")
					.ToListAsync(TestContext.Current.CancellationToken);
				Assert.Empty(tables);

				await afterDown.Database.MigrateAsync(TestContext.Current.CancellationToken);
				Assert.Empty(await afterDown.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
				Assert.Equal(3, await afterDown.Companies.CountAsync(TestContext.Current.CancellationToken));
				Assert.Empty(await afterDown.CompanySetupRecords.ToListAsync(TestContext.Current.CancellationToken));
			}
		});
	}
}
