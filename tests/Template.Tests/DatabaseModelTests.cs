using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Verifies the EF Core model of the Farano database: the tables, the columns,
/// the keys, the indexes and the foreign keys. The tests build the model in
/// memory, they do not need a running SQL Server instance.
/// </summary>
public sealed class DatabaseModelTests
{
	private static DatabaseContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<DatabaseContext>()
			.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Farano_ModelTests;Trusted_Connection=True;")
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
				"ConsolidationProfiles",
				"ConsolidationRunCompanies",
				"ConsolidationRuns",
				"CorporateGroupMembers",
				"CorporateGroups",
				"CustomerOfflineRevocationStates",
				"FiscalPeriods",
				"InstallationIdentities",
				"InstalledLicenses",
				"LoginLogs",
				"MenuItems",
				"OwnershipRelationships",
				"PageCategories",
				"Pages",
				"PermissionDefinitions",
				"Permissions",
				"PostCategory",
				"ProvisioningRequestClaims",
				"Roles",
				"SecurityAuditEvents",
				"UserCompanyMemberships",
				"Users",
			},
			tables);
	}

	[Fact]
	public void CustomerOfflineRevocationState_IsSingletonAndUsesRowVersionConcurrency()
	{
		var state = Model.FindEntityType(typeof(Domain.CustomerOfflineRevocationState));
		Assert.NotNull(state);
		Assert.Equal("CustomerOfflineRevocationStates", state!.GetTableName());
		Assert.Equal("Id", Assert.Single(state.FindPrimaryKey()!.Properties).Name);

		var rowVersion = state.FindProperty(nameof(Domain.CustomerOfflineRevocationState.RowVersion));
		Assert.NotNull(rowVersion);
		Assert.Equal("rowversion", rowVersion!.GetColumnType(), ignoreCase: true);
		Assert.True(rowVersion.IsConcurrencyToken);
		Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
		Assert.Null(state.FindProperty("InstallationId"));
	}

	[Fact]
	public void InstallationIdentities_KeepVersionedPublicIdentityAndAssuranceWithConcurrency()
	{
		var identity = Model.FindEntityType(typeof(Domain.InstallationIdentity));
		Assert.NotNull(identity);
		Assert.Equal("InstallationIdentities", identity!.GetTableName());
		Assert.Equal("Id", Assert.Single(identity.FindPrimaryKey()!.Properties).Name);

		var installationAlternateKey = Assert.Single(
			identity.GetKeys().Where(key => !key.IsPrimaryKey()));
		Assert.Equal(new[] { "Id", "InstallationId" },
			installationAlternateKey.Properties.Select(property => property.Name).ToArray());

		var installationAlternateKey = Assert.Single(
			identity.GetKeys().Where(key => !key.IsPrimaryKey()));
		Assert.Equal(new[] { "Id", "InstallationId" },
			installationAlternateKey.Properties.Select(property => property.Name).ToArray());

		var versionIndex = Assert.Single(identity.GetIndexes(), index =>
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "InstallationId", "KeyVersion" }));
		Assert.True(versionIndex.IsUnique);

		var keyIdIndex = Assert.Single(identity.GetIndexes(), index =>
			index.Properties.Select(property => property.Name).SequenceEqual(new[] { "KeyId" }));
		Assert.True(keyIdIndex.IsUnique);

		var currentIndex = Assert.Single(identity.GetIndexes(), index =>
			index.IsUnique &&
			index.Properties.Select(property => property.Name).SequenceEqual(new[] { "InstallationId" }) &&
			index.GetFilter()!.Contains("Current", StringComparison.Ordinal));
		Assert.NotNull(currentIndex);

		var rowVersion = identity.FindProperty(nameof(Domain.InstallationIdentity.RowVersion));
		Assert.NotNull(rowVersion);
		Assert.Equal("rowversion", rowVersion!.GetColumnType(), ignoreCase: true);
		Assert.True(rowVersion.IsConcurrencyToken);
		Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);

		Assert.DoesNotContain(identity.GetProperties(), property =>
			property.Name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
		Assert.Equal(24, identity.FindProperty(nameof(Domain.InstallationIdentity.AssuranceLevel))!.GetMaxLength());
	}

	[Fact]
	public void InstalledLicenses_PreserveSignedEnvelopeHistoryAndEnforceSingleCurrentLicense()
	{
		var license = Model.FindEntityType(typeof(Domain.InstalledLicense));
		Assert.NotNull(license);
		Assert.Equal("InstalledLicenses", license!.GetTableName());
		Assert.Equal("Id", Assert.Single(license.FindPrimaryKey()!.Properties).Name);

		var identityForeignKey = Assert.Single(license.GetForeignKeys(), key =>
			key.PrincipalEntityType.ClrType == typeof(Domain.InstallationIdentity));
		Assert.Equal(new[] { "InstallationIdentityId", "InstallationId" },
			identityForeignKey.Properties.Select(property => property.Name).ToArray());
		Assert.Equal(new[] { "Id", "InstallationId" },
			identityForeignKey.PrincipalKey.Properties.Select(property => property.Name).ToArray());
		Assert.Equal(DeleteBehavior.Restrict, identityForeignKey.DeleteBehavior);

		foreach (var columns in new[]
		{
			new[] { "InstallationId", "Sequence" },
			new[] { "LicenseId", "Sequence" },
		})
		{
			var index = Assert.Single(license.GetIndexes(), candidate =>
				candidate.Properties.Select(property => property.Name).SequenceEqual(columns));
			Assert.True(index.IsUnique);
		}

		var currentIndex = Assert.Single(license.GetIndexes(), index =>
			index.IsUnique &&
			index.Properties.Select(property => property.Name).SequenceEqual(new[] { "InstallationId" }) &&
			index.GetFilter()!.Contains("Current", StringComparison.Ordinal));
		Assert.NotNull(currentIndex);

		foreach (var propertyName in new[] { "EnvelopeJson", "PayloadJson", "SignatureBase64", "PayloadDigest" })
		{
			var property = license.FindProperty(propertyName);
			Assert.NotNull(property);
			Assert.False(property!.IsNullable);
		}

		var rowVersion = license.FindProperty(nameof(Domain.InstalledLicense.RowVersion));
		Assert.NotNull(rowVersion);
		Assert.Equal("rowversion", rowVersion!.GetColumnType(), ignoreCase: true);
		Assert.True(rowVersion.IsConcurrencyToken);
		Assert.DoesNotContain(license.GetProperties(), property =>
			property.Name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void EveryEntityTypeHasAGuidPrimaryKey()
	{
		foreach (var entityType in Model.GetEntityTypes())
		{
			var primaryKey = entityType.FindPrimaryKey();

			Assert.NotNull(primaryKey);

			if (entityType.ClrType == typeof(Domain.CompanyRolePermission))
			{
				// The permission of a role is identified by the pair (role, permission).
				Assert.Equal(
					new[] { "RoleId", "PermissionId" },
					primaryKey!.Properties.Select(property => property.Name).ToArray());

				continue;
			}

			if (entityType.ClrType == typeof(Domain.ConsolidationRunCompany))
			{
				// A company is in the scope of a run at most once (D-033).
				Assert.Equal(
					new[] { "ConsolidationRunId", "CompanyId" },
					primaryKey!.Properties.Select(property => property.Name).ToArray());

				continue;
			}

			Assert.Equal("Id", Assert.Single(primaryKey!.Properties).Name);
			Assert.Equal(typeof(Guid), primaryKey.Properties[0].ClrType);
		}
	}

	[Fact]
	public void TheFiscalPeriodOfARunCompany_MustBelongToTheSameCompany()
	{
		// (FiscalPeriodId, CompanyId) references FiscalPeriods (Id, TenantId), so the
		// database rejects the fiscal period of another company (D-033).
		var runCompany = Model.FindEntityType("Domain.ConsolidationRunCompany");
		Assert.NotNull(runCompany);

		var foreignKey = Assert.Single(
			runCompany!.GetForeignKeys(),
			candidate => candidate.PrincipalEntityType.ClrType == typeof(Domain.FiscalPeriod));

		Assert.Equal(new[] { "FiscalPeriodId", "CompanyId" }, foreignKey.Properties.Select(property => property.Name).ToArray());
		Assert.Equal(new[] { "Id", "TenantId" }, foreignKey.PrincipalKey.Properties.Select(property => property.Name).ToArray());

		var run = Model.FindEntityType("Domain.ConsolidationRun");
		var profileKey = Assert.Single(
			run!.GetForeignKeys(),
			candidate => candidate.PrincipalEntityType.ClrType == typeof(Domain.ConsolidationProfile));
		Assert.Equal(new[] { "ConsolidationProfileId", "CorporateGroupId" }, profileKey.Properties.Select(property => property.Name).ToArray());
	}

	[Fact]
	public void TheCorporateGroupModel_NeverDeletesInCascade()
	{
		var types = new[]
		{
			typeof(Domain.CorporateGroup), typeof(Domain.CorporateGroupMember), typeof(Domain.OwnershipRelationship),
			typeof(Domain.ConsolidationProfile), typeof(Domain.ConsolidationRun), typeof(Domain.ConsolidationRunCompany),
		};

		foreach (var type in types)
		{
			foreach (var foreignKey in Model.FindEntityType(type)!.GetForeignKeys())
			{
				Assert.Equal(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
			}
		}

		var percentage = Model.FindEntityType(typeof(Domain.OwnershipRelationship))!.FindProperty(nameof(Domain.OwnershipRelationship.OwnershipPercentage));
		Assert.Equal("decimal(9,6)", percentage!.GetColumnType());
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
	public void ProvisioningClaims_EnforceReplayUniquenessAndCompanyBoundaries()
	{
		var claim = Model.FindEntityType(typeof(Domain.ProvisioningRequestClaim));
		Assert.NotNull(claim);

		foreach (var propertyName in new[] { "RequestId", "NonceHash", "EnrollmentTokenHash" })
		{
			var index = Assert.Single(claim!.GetIndexes(), candidate =>
				candidate.Properties.Select(property => property.Name).SequenceEqual(new[] { propertyName }));
			Assert.True(index.IsUnique, $"Provisioning claim index '{propertyName}' must be unique.");
		}

		Assert.DoesNotContain(claim!.GetProperties(), property =>
			property.Name is "EnrollmentToken" or "Password" or "PasswordHash");

		var periodForeignKey = Assert.Single(claim.GetForeignKeys(),
			candidate => candidate.PrincipalEntityType.ClrType == typeof(Domain.FiscalPeriod));
		Assert.Equal(new[] { "FiscalPeriodId", "CompanyId" },
			periodForeignKey.Properties.Select(property => property.Name).ToArray());
		Assert.Equal(new[] { "Id", "TenantId" },
			periodForeignKey.PrincipalKey.Properties.Select(property => property.Name).ToArray());

		var parentForeignKey = Assert.Single(claim.GetForeignKeys(),
			candidate => candidate.PrincipalEntityType.ClrType == typeof(Domain.Company));
		Assert.Equal(new[] { "ParentCompanyId" },
			parentForeignKey.Properties.Select(property => property.Name).ToArray());
		Assert.Equal(DeleteBehavior.Restrict, parentForeignKey.DeleteBehavior);
	}

	[Fact]
	public void ProvisioningClaims_RowVersionIsConfiguredAsSqlServerConcurrencyToken()
	{
		var claim = Model.FindEntityType(typeof(Domain.ProvisioningRequestClaim));
		Assert.NotNull(claim);

		var rowVersion = claim!.FindProperty(nameof(Domain.ProvisioningRequestClaim.RowVersion));
		Assert.NotNull(rowVersion);
		Assert.Equal(typeof(byte[]), rowVersion!.ClrType);
		Assert.Equal("rowversion", rowVersion.GetColumnType(), ignoreCase: true);
		Assert.True(rowVersion.IsConcurrencyToken);
		Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
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
		Assert.Equal("date", startDate!.GetColumnType(), ignoreCase: true);
	}

	[Fact]
	public void TheMigrationsOfThePersistenceAssemblyAreDiscovered()
	{
		using var context = CreateContext();

		var migrations = context.Database.GetMigrations().ToArray();

		Assert.Contains("20251017120000_InitialCreate", migrations);
		Assert.Contains("20261017090000_AlignSchemaWithModel", migrations);
		Assert.Contains("20261029161402_AddCustomerProvisioningClaims", migrations);
		Assert.Contains("20261031120000_AddCustomerOfflineRevocationState", migrations);
		Assert.Contains("20261031130000_AddCustomerLicensePersistence", migrations);
	}

	[Fact]
	public void TheModelSnapshotDescribesTheCurrentModel()
	{
		// This is the check that "dotnet ef migrations has-pending-model-changes" runs. A
		// snapshot that differs from the model makes every "Database.MigrateAsync" throw
		// "PendingModelChangesWarning", which is what the database setup page reported.
		// The comparison only builds the two models, it never opens a connection.
		using var context = CreateContext();

		Assert.False(context.Database.HasPendingModelChanges());
	}
}
