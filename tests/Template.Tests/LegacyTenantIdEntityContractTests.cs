using Faranou.Modules.Trade.Infrastructure.Persistence;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Template.Tests;

/// <summary>
/// PR-3 (D-087): there is no Tenant entity yet, so every persisted <c>TenantId</c> still holds
/// <c>Company.Id</c> (Legacy meaning). This pins the complete list from the real EF models. A new entity
/// with a Tenant column fails here until the owner decides its meaning: adding it to the list is an explicit
/// "Legacy = Company.Id" statement, never an accident. A real Tenant needs the S3 migration, not this list.
/// </summary>
public sealed class LegacyTenantIdEntityContractTests
{
	// entity CLR name -> the column holding Company.Id in the Legacy state
	private static readonly string[] LegacyCoreEntities =
	[
		"CompanyEmployee.TenantId",
		"CompanyEmployeeAssignment.TenantId",
		"CompanyEmployeePhoto.TenantId",
		"CompanyMembershipRequest.DestinationTenantId",
		"CompanyOrganizationalUnit.TenantId",
		"CompanyPosition.TenantId",
		"CompanyRole.TenantId",
		"FiscalPeriod.TenantId",
		"SecurityAuditEvent.TenantId",
		"UserCompanyMembership.TenantId",
	];

	private static readonly string[] LegacyTradeEntities =
	[
		"Party.TenantId",
		"PartyBankAccount.TenantId",
		"PartyRoleAssignment.TenantId",
		"Product.TenantId",
		"ProductIdentifier.TenantId",
	];

	[Fact]
	public void TheCoreModel_HasExactlyTheDeclaredLegacyTenantColumns()
	{
		using var context = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
			.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Farano_LegacyTenantContract;Trusted_Connection=True;").Options);

		Assert.Equal(LegacyCoreEntities, TenantColumns(context.Model));
	}

	[Fact]
	public void TheTradeModel_HasExactlyTheDeclaredLegacyTenantColumns()
	{
		using var context = new TradeDbContext(new DbContextOptionsBuilder<TradeDbContext>()
			.UseSqlServer("Server=localhost;Database=Trade_LegacyTenantContract;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True").Options);

		Assert.Equal(LegacyTradeEntities, TenantColumns(context.Model));
	}

	[Fact]
	public void TheCompanyEntity_HasNoTenantColumn_SoCompanyIdIsTheOnlyLegacyOwner()
	{
		using var context = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
			.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Farano_LegacyTenantContract;Trusted_Connection=True;").Options);

		var company = context.Model.GetEntityTypes().Single(e => e.ClrType == typeof(Domain.Company));
		Assert.DoesNotContain(company.GetProperties(), p => p.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(context.Model.GetEntityTypes(), e => e.ClrType.Name.Equals("Tenant", StringComparison.OrdinalIgnoreCase));
	}

	private static string[] TenantColumns(IModel model) =>
		model.GetEntityTypes()
			.Where(e => !e.IsOwned() && e.BaseType is null)
			.SelectMany(e => e.GetProperties()
				.Where(p => p.Name.EndsWith("TenantId", StringComparison.Ordinal))
				.Select(p => $"{e.ClrType.Name}.{p.Name}"))
			.Order(StringComparer.Ordinal)
			.ToArray();
}
