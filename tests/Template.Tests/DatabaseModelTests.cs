using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Verifies the EF Core model of the Taraz Ava database: the tables, the columns,
/// the keys, the indexes and the foreign keys. The tests build the model in
/// memory, they do not need a running SQL Server instance.
/// </summary>
public sealed class DatabaseModelTests
{
	private static DatabaseContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<DatabaseContext>()
			.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TarazAva_ModelTests;Trusted_Connection=True;")
			.Options;

		return new DatabaseContext(options);
	}

	private static IModel Model
	{
		get
		{
			using var context = CreateContext();

			return context.Model;
		}
	}

	[Fact]
	public void Model_ContainsEveryTableOfTheDomain()
	{
		var tables = Model.GetEntityTypes()
			.Select(entityType => entityType.GetTableName())
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();

		Assert.Equal(
			new[]
			{
				"ApplicationHandlers",
				"Companies",
				"CompanyRolePermissions",
				"CompanyRoles",
				"FiscalPeriods",
				"LoginLogs",
				"MenuItems",
				"PageCategories",
				"Pages",
				"Permissions",
				"PermissionDefinitions",
				"PostCategory",
				"Roles",
				"SecurityAuditEvents",
				"UserCompanyMemberships",
				"Users",
			},
			tables);
	}

	[Fact]
	public void EveryEntityTypeHasAGuidPrimaryKey()
	{
		foreach (var entityType in Model.GetEntityTypes())
		{
			var primaryKey = entityType.FindPrimaryKey();

			Assert.NotNull(primaryKey);
			Assert.Equal("Id", Assert.Single(primaryKey!.Properties).Name);
			Assert.Equal(typeof(Guid), primaryKey.Properties[0].ClrType);
		}
	}

	[Fact]
	public void CompanyRole_HasTheAlternateKeyOfTheTenant()
	{
		var companyRole = Model.FindEntityType("Domain.CompanyRole");

		Assert.NotNull(companyRole);

		var alternateKey = Assert.Single(
			companyRole!.GetKeys().Where(key => !key.IsPrimaryKey()));

		Assert.Equal(
			new[] { "Id", "TenantId" },
			alternateKey.Properties.Select(property => property.Name).ToArray());
	}

	[Fact]
	public void UserCompanyMembership_UsesTheCompositeForeignKeyOfTheCompanyRole()
	{
		var membership = Model.FindEntityType("Domain.UserCompanyMembership");

		Assert.NotNull(membership);

		var foreignKey = Assert.Single(
			membership!.GetForeignKeys()
				.Where(key => key.PrincipalEntityType.ClrType == typeof(Domain.CompanyRole)));

		Assert.Equal(
			new[] { "RoleId", "TenantId" },
			foreignKey.Properties.Select(property => property.Name).ToArray());
		Assert.Equal(
			new[] { "Id", "TenantId" },
			foreignKey.PrincipalKey.Properties.Select(property => property.Name).ToArray());
	}

	[Fact]
	public void MoneyIsNeverStoredAsAFloatingPointNumber()
	{
		foreach (var entityType in Model.GetEntityTypes())
		{
			foreach (var property in entityType.GetProperties())
			{
				var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

				Assert.False(
					type == typeof(float) || type == typeof(double),
					$"The property '{entityType.Name}.{property.Name}' uses the floating point type '{type.Name}'.");
			}
		}
	}

	[Fact]
	public void EveryForeignKeyHasADeleteBehavior()
	{
		foreach (var entityType in Model.GetEntityTypes())
		{
			foreach (var foreignKey in entityType.GetForeignKeys())
			{
				Assert.True(
					Enum.IsDefined(foreignKey.DeleteBehavior),
					$"The foreign key '{entityType.Name}' has no valid delete behavior.");
			}
		}
	}

	[Fact]
	public void TheUniqueIndexesOfTheUsersAreFiltered()
	{
		var user = Model.FindEntityType("Domain.User");

		Assert.NotNull(user);

		var index = Assert.Single(
			user!.GetIndexes().Where(candidate => candidate.Properties[0].Name == "Username"));

		Assert.True(index.IsUnique);
		Assert.Equal("[Username] IS NOT NULL", index.GetFilter());
	}

	[Fact]
	public void TheDateOnlyColumnsUseTheDateStoreType()
	{
		var fiscalPeriod = Model.FindEntityType("Domain.FiscalPeriod");

		Assert.NotNull(fiscalPeriod);

		var startDate = fiscalPeriod!.FindProperty("StartDate");

		Assert.NotNull(startDate);
		Assert.Equal("date", startDate!.GetColumnType());
	}

	[Fact]
	public void TheMigrationsOfThePersistenceAssemblyAreDiscovered()
	{
		using var context = CreateContext();

		var migrations = context.Database.GetMigrations().ToArray();

		Assert.Contains("20251017120000_InitialCreate", migrations);
	}
}
