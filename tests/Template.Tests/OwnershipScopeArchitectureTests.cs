using System.Reflection;
using System.Text.RegularExpressions;
using Infrastructure.Tenancy;
using Xunit;

namespace Template.Tests;

/// <summary>
/// PR-3 (D-087): the ownership contract keeps Tenant, Company and CustomerBoundary apart and is built
/// only from server-verified data. Behavioural checks run the real types; the structural guards scan the
/// real source tree (and are proven against negative fixtures, so a guard that can not fail is caught).
/// </summary>
public sealed class OwnershipScopeArchitectureTests
{
	private static readonly string RepoRoot = FindRepoRoot();

	private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private static CompanyAccess Access(Guid companyId) =>
		new(companyId, "Company", null, Guid.NewGuid(), "ADMIN", "Admin");

	private static WorkContext Context(Guid companyId) =>
		new(Access(companyId), new FiscalPeriodInfo(Guid.NewGuid(), "1405", new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20), Domain.FiscalPeriodStatus.Open));

	// ---- behaviour ----

	[Fact]
	public void LegacyOwnership_OfAVerifiedAccess_CarriesTheCompanyIdentifierInBothScopes()
	{
		var scope = OwnershipScope.FromLegacyCompany(Access(CompanyId));

		// the legacy state of D-087: tenant id = company id (documented, one place)
		Assert.Equal(CompanyId, scope.Tenant.Value);
		Assert.Equal(CompanyId, scope.Company.Value);
	}

	[Fact]
	public void TheWorkContext_ExposesTheOwnershipOfItsVerifiedCompany()
	{
		var context = Context(CompanyId);

		Assert.Equal(OwnershipScope.FromLegacyCompany(context.Company), context.Ownership);
		Assert.Equal(CompanyId, context.Ownership.Company.Value);
	}

	[Fact]
	public void AnEmptyCompanyIdentifier_NeverBecomesAnOwnership()
	{
		Assert.Throws<ArgumentException>(() => OwnershipScope.FromLegacyCompany(Access(Guid.Empty)));
		Assert.Throws<ArgumentNullException>(() => OwnershipScope.FromLegacyCompany((CompanyAccess)null!));
		Assert.Throws<ArgumentNullException>(() => OwnershipScope.FromLegacyCompany((WorkContext)null!));
	}

	[Fact]
	public void TenantAndCompanyScopes_AreDistinctTypesWithoutAnyConversion()
	{
		Assert.NotEqual(typeof(TenantScope), typeof(CompanyScope));

		foreach (var type in new[] { typeof(TenantScope), typeof(CompanyScope), typeof(OwnershipScope) })
		{
			var conversions = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
				.Where(method => method.Name is "op_Implicit" or "op_Explicit")
				.ToList();
			Assert.Empty(conversions);
		}

		// two scopes of the same value are still not interchangeable: they are not even comparable
		object tenant = OwnershipScope.FromLegacyCompany(Access(CompanyId)).Tenant;
		object company = OwnershipScope.FromLegacyCompany(Access(CompanyId)).Company;
		Assert.False(tenant.Equals(company));
	}

	[Fact]
	public void TheOwnershipContract_CanNotBeBuiltFromAnIdentifierOfAClient()
	{
		var publicFactories = typeof(OwnershipScope).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.ReturnType == typeof(OwnershipScope))
			.ToList();
		Assert.NotEmpty(publicFactories);

		// only server-verified records are accepted, never a Guid, a string or a request
		foreach (var factory in publicFactories)
		{
			var parameter = Assert.Single(factory.GetParameters());
			Assert.Contains(parameter.ParameterType, new[] { typeof(CompanyAccess), typeof(WorkContext) });
		}

		foreach (var type in new[] { typeof(TenantScope), typeof(CompanyScope), typeof(OwnershipScope) })
		{
			Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
		}
	}

	[Fact]
	public void TheOwnershipContract_HasNoCustomerBoundaryMember()
	{
		foreach (var type in new[] { typeof(TenantScope), typeof(CompanyScope), typeof(OwnershipScope), typeof(CompanyAccess) })
		{
			var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Select(member => member.Name);
			Assert.DoesNotContain(members, name => name.Contains("Boundary", StringComparison.OrdinalIgnoreCase));
		}
	}

	// ---- structural guards over the real source ----

	[Fact]
	public void NoSourceFile_UsesCustomerBoundaryIdAsTheTenantBoundary()
	{
		var offenders = Offenders(CustomerBoundaryAsTenant);

		Assert.True(offenders.Count == 0, "CustomerBoundaryId is not a tenant (D-038, D-087):\n" + string.Join("\n", offenders));
	}

	[Fact]
	public void TheGuard_ForCustomerBoundaryAsTenant_FlagsTheForbiddenForms()
	{
		foreach (var snippet in new[]
		{
			"var tenantId = company.CustomerBoundaryId;",
			"query.Where(x => x.TenantId == company.CustomerBoundaryId)",
			"entity.TenantId = boundaryId.CustomerBoundaryId.Value;",
			"new TenantScope(company.CustomerBoundaryId.Value)",
			"x.CustomerBoundaryId == tenantId",
		})
		{
			Assert.Matches(CustomerBoundaryAsTenant, snippet);
		}

		// the legitimate group-boundary comparison of D-038 is not flagged
		Assert.DoesNotMatch(CustomerBoundaryAsTenant, "member.Company.CustomerBoundaryId != group.OwnerCompany.CustomerBoundaryId");
	}

	[Fact]
	public void TheLegacyTenantEqualsCompanyEquation_ExistsOnlyInTheOwnershipScope()
	{
		var offenders = Offenders(TenantScopeConstruction, skipFile: "OwnershipScope.cs");

		Assert.True(offenders.Count == 0, "Tenant/Company scopes are created only by OwnershipScope:\n" + string.Join("\n", offenders));
		Assert.Matches(TenantScopeConstruction, "var t = new TenantScope(request.CompanyId);");
		Assert.Matches(TenantScopeConstruction, "var c = new CompanyScope(id)");
	}

	[Fact]
	public void CompanyAccess_And_WorkContext_AreCreatedOnlyByTheirVerifyingServices()
	{
		var access = Offenders(new Regex(@"new\s+CompanyAccess\s*\("), skipFile: "CompanyAccessService.cs");
		var context = Offenders(new Regex(@"new\s+WorkContext\s*\("), skipFile: "WorkContextService.cs");

		// the ownership contract is only as trustworthy as these two sources of it
		Assert.True(access.Count == 0, "CompanyAccess must come from CompanyAccessService:\n" + string.Join("\n", access));
		Assert.True(context.Count == 0, "WorkContext must come from WorkContextService:\n" + string.Join("\n", context));
	}

	// ---- helpers ----

	// TenantId/Tenant on one side of an assignment or comparison and CustomerBoundaryId on the other
	private static readonly Regex CustomerBoundaryAsTenant = new(
		@"(Tenant\w*\s*(==|!=|=|\()\s*[^;\r\n]*CustomerBoundary)|(CustomerBoundary\w*\s*(==|!=)\s*[^;\r\n]*Tenant)",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex TenantScopeConstruction = new(@"new\s+(TenantScope|CompanyScope)\s*\(", RegexOptions.Compiled);

	private static List<string> Offenders(Regex pattern, string? skipFile = null)
	{
		var result = new List<string>();
		foreach (var directory in new[] { "Server", "Domain", "Infrastructure", "Modules" })
		{
			var root = Path.Combine(RepoRoot, directory);
			if (!Directory.Exists(root))
			{
				continue;
			}

			foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(RepoRoot, file).Replace('\\', '/');
				if (relative.Contains("/obj/") || relative.Contains("/bin/") || relative.Contains("/Migrations/")
				    || (skipFile is not null && relative.EndsWith("/" + skipFile, StringComparison.Ordinal)))
				{
					continue;
				}

				var lineNumber = 0;
				foreach (var line in File.ReadLines(file))
				{
					lineNumber++;
					if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || line.TrimStart().StartsWith("///", StringComparison.Ordinal))
					{
						continue;
					}

					if (pattern.IsMatch(line))
					{
						result.Add($"{relative}:{lineNumber}: {line.Trim()}");
					}
				}
			}
		}

		return result;
	}

	private static string FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "Template.sln")) && File.Exists(Path.Combine(dir.FullName, "Server", "Server.csproj")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		throw new InvalidOperationException("Could not locate repository root (Template.sln + Server/Server.csproj).");
	}
}
