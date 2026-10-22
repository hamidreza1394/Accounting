using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain;
using Infrastructure.CorporateGroups;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// A holding with two subsidiaries and an unrelated company, and users with different
/// permissions. Works on any provider (in-memory or SQL Server).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>Admin</c>: role with every group permission in Holding, SubA and SubB; viewer in Other.</item>
/// <item><c>HoldingOnly</c>: every group permission in Holding, viewer (no permission) in SubA and SubB.</item>
/// <item><c>Viewer</c>: only <c>corporate-groups.view</c> in Holding.</item>
/// <item><c>Outsider</c>: every group permission in Other only.</item>
/// </list>
/// </remarks>
internal sealed class CorporateGroupScenario
{
	public static readonly string[] AllPermissionCodes =
	[
		CorporateGroupPermissions.View,
		CorporateGroupPermissions.Manage,
		CorporateGroupPermissions.ManageMembers,
		CorporateGroupPermissions.ManageOwnership,
		CorporateGroupPermissions.ViewAggregateReport,
		CorporateGroupPermissions.RunConsolidatedReport,
		CorporateGroupPermissions.ApproveAdjustments,
		CorporateGroupPermissions.ExportGroupReport,
	];

	public static readonly DateOnly YearStart = new(2026, 3, 21);
	public static readonly DateOnly YearEnd = new(2027, 3, 20);
	public static readonly DateOnly QuarterEnd = new(2026, 6, 20);

	private CorporateGroupScenario(Data.DatabaseContext database)
	{
		Database = database;
		Service = new CorporateGroupService(database, new Infrastructure.Tenancy.CompanyAccessService(database), TimeProvider.System);
	}

	public Data.DatabaseContext Database { get; }

	public CorporateGroupService Service { get; }

	public Guid Admin { get; private set; }

	public Guid HoldingOnly { get; private set; }

	public Guid Viewer { get; private set; }

	public Guid Outsider { get; private set; }

	public Guid InactiveAdmin { get; private set; }

	public Guid Holding { get; private set; }

	public Guid SubA { get; private set; }

	public Guid SubB { get; private set; }

	public Guid Other { get; private set; }

	public Dictionary<Guid, Guid> YearPeriod { get; } = [];

	public Dictionary<Guid, Guid> AdminRole { get; } = [];

	public static async Task<CorporateGroupScenario> CreateAsync(Data.DatabaseContext database)
	{
		var scenario = new CorporateGroupScenario(database);
		await scenario.SeedAsync();
		return scenario;
	}

	/// <summary>Creates a group owned by Holding with Holding, SubA and SubB as members.</summary>
	public async Task<Guid> CreateHoldingGroupAsync(string? code = null)
	{
		var group = await Service.CreateGroupAsync(Admin, Holding, code ?? Unique("grp"), "گروه آزمایشی");
		Assert.True(group.Succeeded, group.ErrorCode);

		foreach (var (company, role) in new[]
		{
			(Holding, CorporateGroupMemberRole.Parent),
			(SubA, CorporateGroupMemberRole.Subsidiary),
			(SubB, CorporateGroupMemberRole.Subsidiary),
		})
		{
			var member = await Service.AddMemberAsync(Admin, group.Value, company, role, YearStart, null);
			Assert.True(member.Succeeded, member.ErrorCode);
		}

		return group.Value;
	}

	public static string Unique(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];

	private async Task SeedAsync()
	{
		var permissionIds = new Dictionary<string, Guid>();
		foreach (var code in AllPermissionCodes)
		{
			var existing = await Database.PermissionDefinitions.FirstOrDefaultAsync(permission => permission.Code == code);
			if (existing is null)
			{
				existing = new PermissionDefinition { Code = code, Name = code };
				Database.PermissionDefinitions.Add(existing);
			}

			permissionIds[code] = existing.Id;
		}

		Holding = AddCompany("هلدینگ");
		SubA = AddCompany("فرعی الف");
		SubB = AddCompany("فرعی ب");
		Other = AddCompany("شرکت مستقل");

		var all = AllPermissionCodes;
		var adminRoles = new Dictionary<Guid, Guid>();
		var viewerRoles = new Dictionary<Guid, Guid>();
		var viewOnlyRoles = new Dictionary<Guid, Guid>();

		foreach (var company in new[] { Holding, SubA, SubB, Other })
		{
			adminRoles[company] = AddRole(company, "group-admin", all.Select(code => permissionIds[code]));
			viewerRoles[company] = AddRole(company, "viewer", []);
			viewOnlyRoles[company] = AddRole(company, "group-viewer", [permissionIds[CorporateGroupPermissions.View]]);
			YearPeriod[company] = AddPeriod(company, YearStart, YearEnd, FiscalPeriodStatus.Open);
			AdminRole[company] = adminRoles[company];
		}

		Admin = AddUser(isActive: true);
		AddMembership(Admin, Holding, adminRoles[Holding]);
		AddMembership(Admin, SubA, adminRoles[SubA]);
		AddMembership(Admin, SubB, adminRoles[SubB]);
		AddMembership(Admin, Other, viewerRoles[Other]);

		HoldingOnly = AddUser(isActive: true);
		AddMembership(HoldingOnly, Holding, adminRoles[Holding]);
		AddMembership(HoldingOnly, SubA, viewerRoles[SubA]);
		AddMembership(HoldingOnly, SubB, viewerRoles[SubB]);

		Viewer = AddUser(isActive: true);
		AddMembership(Viewer, Holding, viewOnlyRoles[Holding]);

		Outsider = AddUser(isActive: true);
		AddMembership(Outsider, Other, adminRoles[Other]);

		InactiveAdmin = AddUser(isActive: false);
		AddMembership(InactiveAdmin, Holding, adminRoles[Holding]);

		await Database.SaveChangesAsync();
		Database.ChangeTracker.Clear();
	}

	public Guid AddPeriod(Guid company, DateOnly start, DateOnly end, FiscalPeriodStatus status)
	{
		var period = new FiscalPeriod { TenantId = company, Title = Unique("دوره"), StartDate = start, EndDate = end, Status = status };
		Database.FiscalPeriods.Add(period);
		return period.Id;
	}

	private Guid AddCompany(string name)
	{
		var company = new Company { Name = name + " " + Unique("c"), IsActive = true };
		Database.Companies.Add(company);
		return company.Id;
	}

	private Guid AddRole(Guid company, string code, IEnumerable<Guid> permissions)
	{
		var role = new CompanyRole { TenantId = company, Code = code, Name = code, IsActive = true };
		Database.CompanyRoles.Add(role);
		foreach (var permission in permissions)
		{
			Database.CompanyRolePermissions.Add(new CompanyRolePermission { RoleId = role.Id, PermissionId = permission });
		}

		return role.Id;
	}

	private Guid AddUser(bool isActive)
	{
		var username = Unique("u");
		var user = new User(emailAddress: $"{username}@example.test")
		{
			Username = username,
			Password = "not-a-real-hash",
			IsActive = isActive,
		};
		Database.Users.Add(user);
		return user.Id;
	}

	private void AddMembership(Guid user, Guid company, Guid role)
	{
		Database.UserCompanyMemberships.Add(new UserCompanyMembership { UserId = user, TenantId = company, RoleId = role, IsActive = true });
	}
}

/// <summary>
/// The use cases and the access rules of <see cref="CorporateGroupService" /> (D-033, D-034)
/// on the in-memory provider. <see cref="CorporateGroupSqlServerTests" /> repeats the
/// critical ones on SQL Server.
/// </summary>
public sealed class CorporateGroupServiceTests : IDisposable
{
	private readonly Data.DatabaseContext database;

	public CorporateGroupServiceTests()
	{
		database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options);
	}

	public void Dispose() => database.Dispose();

	private Task<CorporateGroupScenario> ScenarioAsync() => CorporateGroupScenario.CreateAsync(database);

	[Fact]
	public async Task HasPermission_NeedsAnActiveMembership_AndTheAssignedPermission()
	{
		var s = await ScenarioAsync();
		var access = new Infrastructure.Tenancy.CompanyAccessService(database);

		Assert.True(await access.HasPermissionAsync(s.Admin, s.SubA, CorporateGroupPermissions.ManageMembers));
		Assert.False(await access.HasPermissionAsync(s.Admin, s.Other, CorporateGroupPermissions.ManageMembers)); // viewer role
		Assert.False(await access.HasPermissionAsync(s.HoldingOnly, s.SubA, CorporateGroupPermissions.View));    // no permission in SubA
		Assert.False(await access.HasPermissionAsync(s.Outsider, s.Holding, CorporateGroupPermissions.View));    // not a member
		Assert.False(await access.HasPermissionAsync(s.InactiveAdmin, s.Holding, CorporateGroupPermissions.View)); // inactive user
		Assert.False(await access.HasPermissionAsync(s.Admin, s.Holding, "unknown.permission"));
		Assert.False(await access.HasPermissionAsync(s.Admin, Guid.Empty, CorporateGroupPermissions.View));
	}

	[Fact]
	public async Task HasPermission_IsWithdrawn_WhenTheMembershipCompanyOrRoleIsDeactivated()
	{
		var s = await ScenarioAsync();
		var access = new Infrastructure.Tenancy.CompanyAccessService(database);

		var membership = await database.UserCompanyMemberships.SingleAsync(m => m.UserId == s.Admin && m.TenantId == s.SubA);
		membership.IsActive = false;
		var company = await database.Companies.SingleAsync(c => c.Id == s.SubB);
		company.IsActive = false;
		var role = await database.CompanyRoles.SingleAsync(r => r.Id == s.AdminRole[s.Holding]);
		role.IsActive = false;
		await database.SaveChangesAsync();

		Assert.False(await access.HasPermissionAsync(s.Admin, s.SubA, CorporateGroupPermissions.View));
		Assert.False(await access.HasPermissionAsync(s.Admin, s.SubB, CorporateGroupPermissions.View));
		Assert.False(await access.HasPermissionAsync(s.Admin, s.Holding, CorporateGroupPermissions.View));
	}

	[Fact]
	public async Task CreatingAGroup_NeedsTheManagePermission_InTheOwnerCompany()
	{
		var s = await ScenarioAsync();

		var created = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");
		Assert.True(created.Succeeded);

		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.CreateGroupAsync(s.Viewer, s.Holding, CorporateGroupScenario.Unique("g"), "گروه")).Outcome);
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.CreateGroupAsync(s.Outsider, s.Holding, CorporateGroupScenario.Unique("g"), "گروه")).Outcome);
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.CreateGroupAsync(s.InactiveAdmin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه")).Outcome);
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.CreateGroupAsync(s.Admin, Guid.NewGuid(), CorporateGroupScenario.Unique("g"), "گروه")).Outcome);
	}

	[Fact]
	public async Task DeniedGroupCreation_DoesNotAuditTheUnverifiedOwnerCompanyAsTenant()
	{
		var s = await ScenarioAsync();

		var result = await s.Service.CreateGroupAsync(s.Outsider, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");

		Assert.Equal(CorporateGroupOutcome.NotFound, result.Outcome);
		var audit = await database.SecurityAuditEvents.SingleAsync(entry =>
			entry.UserId == s.Outsider &&
			entry.EventType == "CorporateGroupAccessDenied" &&
			entry.DetailsJson!.Contains("CorporateGroupCreate", StringComparison.Ordinal));
		Assert.Null(audit.TenantId);
		Assert.Null(audit.EntityId);
	}

	[Fact]
	public async Task AGroupCode_IsValidatedAndUnique()
	{
		var s = await ScenarioAsync();
		var code = CorporateGroupScenario.Unique("g");

		Assert.True((await s.Service.CreateGroupAsync(s.Admin, s.Holding, code, "گروه")).Succeeded);

		var duplicate = await s.Service.CreateGroupAsync(s.Admin, s.Holding, code, "گروه دیگر");
		Assert.Equal(CorporateGroupOutcome.Conflict, duplicate.Outcome);
		Assert.Equal(CorporateGroupService.ErrorDuplicateCode, duplicate.ErrorCode);

		Assert.Equal(CorporateGroupOutcome.Invalid, (await s.Service.CreateGroupAsync(s.Admin, s.Holding, "کد فارسی", "گروه")).Outcome);
		Assert.Equal(CorporateGroupOutcome.Invalid, (await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "  ")).Outcome);
	}

	[Fact]
	public async Task AGroupCode_IsUniquePerOwner_SoItCanNotProbeOtherCompanies()
	{
		var s = await ScenarioAsync();
		var code = CorporateGroupScenario.Unique("g");

		Assert.True((await s.Service.CreateGroupAsync(s.Outsider, s.Other, code, "گروه دیگر")).Succeeded);
		Assert.True((await s.Service.CreateGroupAsync(s.Admin, s.Holding, code, "گروه")).Succeeded);
	}

	[Fact]
	public async Task TheMemberAndTheSubsidiary_SeeTheChange_InTheirOwnAuditTrail()
	{
		var s = await ScenarioAsync();
		var group = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");
		var member = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);
		await s.Service.EndMembershipAsync(s.Admin, member.Value, CorporateGroupScenario.YearEnd);
		await s.Service.RecordOwnershipAsync(s.Admin, new(s.Holding, s.SubB, 60m, 60m, ControlBasis.Control, CorporateGroupScenario.YearStart, null));

		var subA = await database.SecurityAuditEvents.Where(audit => audit.TenantId == s.SubA).Select(audit => audit.EventType).ToListAsync();
		Assert.Contains("CorporateGroupMemberAdded", subA);
		Assert.Contains("CorporateGroupMemberEnded", subA);
		Assert.Contains("OwnershipRelationshipRecorded", await database.SecurityAuditEvents.Where(audit => audit.TenantId == s.SubB).Select(audit => audit.EventType).ToListAsync());
	}

	[Fact]
	public async Task EndingAMembership_WithoutThePermissionInTheMemberCompany_LooksLikeAddingOne()
	{
		var s = await ScenarioAsync();
		var group = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");
		var member = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);

		// HoldingOnly manages members in Holding but holds no permission in SubA.
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.EndMembershipAsync(s.HoldingOnly, member.Value, CorporateGroupScenario.YearEnd)).Outcome);
	}

	[Fact]
	public async Task AGroup_IsOnlyVisible_WithTheViewPermissionInTheOwnerCompany()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();

		Assert.Contains(await s.Service.GetVisibleGroupsAsync(s.Viewer), group => group.Id == groupId);
		Assert.DoesNotContain(await s.Service.GetVisibleGroupsAsync(s.Outsider), group => group.Id == groupId);
		Assert.DoesNotContain(await s.Service.GetVisibleGroupsAsync(s.InactiveAdmin), group => group.Id == groupId);
	}

	[Fact]
	public async Task AddingAMember_NeedsThePermission_InTheOwnerAndInTheMemberCompany()
	{
		var s = await ScenarioAsync();
		var group = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");

		// HoldingOnly manages members in Holding but has no permission in SubA.
		var pulledIn = await s.Service.AddMemberAsync(s.HoldingOnly, group.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.NotFound, pulledIn.Outcome);

		// Viewer sees the group but can not change its members.
		var viewer = await s.Service.AddMemberAsync(s.Viewer, group.Value, s.Holding, CorporateGroupMemberRole.Parent, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.AccessDenied, viewer.Outcome);

		// Outsider does not see the group at all.
		var outsider = await s.Service.AddMemberAsync(s.Outsider, group.Value, s.Other, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.NotFound, outsider.Outcome);

		// Admin may not add a company in which it holds no permission (Other: viewer role).
		var other = await s.Service.AddMemberAsync(s.Admin, group.Value, s.Other, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.NotFound, other.Outcome);

		Assert.Equal(0, await database.CorporateGroupMembers.CountAsync(member => member.CorporateGroupId == group.Value));
	}

	[Fact]
	public async Task ATamperedGroupIdentifier_LooksLikeAnUnknownGroup()
	{
		var s = await ScenarioAsync();

		var unknown = await s.Service.AddMemberAsync(s.Admin, Guid.NewGuid(), s.SubA, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.NotFound, unknown.Outcome);

		// A group of another owner the user can not see answers exactly the same.
		var foreignGroup = await s.Service.CreateGroupAsync(s.Outsider, s.Other, CorporateGroupScenario.Unique("g"), "گروه دیگر");
		var foreign = await s.Service.AddMemberAsync(s.Admin, foreignGroup.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.NotFound, foreign.Outcome);
		Assert.Null(foreign.ErrorCode);
	}

	[Fact]
	public async Task Memberships_CanNotOverlap_ButCanFollowEachOther()
	{
		var s = await ScenarioAsync();
		var group = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");
		var first = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, new DateOnly(2025, 3, 21), null);
		Assert.True(first.Succeeded);

		var overlapping = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Associate, CorporateGroupScenario.YearStart, null);
		Assert.Equal(CorporateGroupOutcome.Invalid, overlapping.Outcome);
		Assert.Equal(nameof(CorporateGroupRuleViolation.OverlappingMembership), overlapping.ErrorCode);

		Assert.True((await s.Service.EndMembershipAsync(s.Admin, first.Value, new DateOnly(2026, 3, 20))).Succeeded);
		Assert.Equal(CorporateGroupOutcome.Conflict, (await s.Service.EndMembershipAsync(s.Admin, first.Value, new DateOnly(2026, 3, 20))).Outcome);

		var next = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Associate, CorporateGroupScenario.YearStart, null);
		Assert.True(next.Succeeded, next.ErrorCode);

		var invalid = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubB, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearEnd, CorporateGroupScenario.YearStart);
		Assert.Equal(nameof(CorporateGroupRuleViolation.InvalidPeriod), invalid.ErrorCode);
	}

	[Fact]
	public async Task EndingAMembership_BeforeItStarts_IsRejected()
	{
		var s = await ScenarioAsync();
		var group = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");
		var member = await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, CorporateGroupScenario.YearStart, null);

		var ended = await s.Service.EndMembershipAsync(s.Admin, member.Value, CorporateGroupScenario.YearStart.AddDays(-1));
		Assert.Equal(CorporateGroupOutcome.Invalid, ended.Outcome);
		Assert.Equal(CorporateGroupOutcome.AccessDenied, (await s.Service.EndMembershipAsync(s.Viewer, member.Value, CorporateGroupScenario.YearEnd)).Outcome);
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.EndMembershipAsync(s.Outsider, member.Value, CorporateGroupScenario.YearEnd)).Outcome);
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.EndMembershipAsync(s.Admin, Guid.NewGuid(), CorporateGroupScenario.YearEnd)).Outcome);
	}

	[Fact]
	public async Task Ownership_NeedsThePermission_InTheParentAndInTheSubsidiary()
	{
		var s = await ScenarioAsync();
		var request = new OwnershipRequest(s.Holding, s.SubA, 80m, 80m, ControlBasis.Control, CorporateGroupScenario.YearStart, null);

		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.RecordOwnershipAsync(s.HoldingOnly, request)).Outcome);
		Assert.Equal(CorporateGroupOutcome.NotFound, (await s.Service.RecordOwnershipAsync(s.Outsider, request)).Outcome);
		Assert.True((await s.Service.RecordOwnershipAsync(s.Admin, request)).Succeeded);
	}

	[Fact]
	public async Task Ownership_RejectsCyclesTotalsAndASecondControllingParent()
	{
		var s = await ScenarioAsync();
		var start = CorporateGroupScenario.YearStart;

		Assert.True((await s.Service.RecordOwnershipAsync(s.Admin, new(s.Holding, s.SubA, 80m, 80m, ControlBasis.Control, start, null))).Succeeded);
		Assert.True((await s.Service.RecordOwnershipAsync(s.Admin, new(s.SubA, s.SubB, 60m, 60m, ControlBasis.Control, start, null))).Succeeded);

		var cycle = await s.Service.RecordOwnershipAsync(s.Admin, new(s.SubB, s.Holding, 51m, 51m, ControlBasis.Control, start, null));
		Assert.Equal(nameof(CorporateGroupRuleViolation.ControlCycle), cycle.ErrorCode);

		var crossHolding = await s.Service.RecordOwnershipAsync(s.Admin, new(s.SubB, s.Holding, 3m, 3m, ControlBasis.Investment, start, null));
		Assert.True(crossHolding.Succeeded, crossHolding.ErrorCode);

		var tooMuch = await s.Service.RecordOwnershipAsync(s.Admin, new(s.Holding, s.SubB, 50m, 10m, ControlBasis.Investment, start, null));
		Assert.Equal(nameof(CorporateGroupRuleViolation.OwnershipExceedsHundredPercent), tooMuch.ErrorCode);

		var secondControl = await s.Service.RecordOwnershipAsync(s.Admin, new(s.Holding, s.SubB, 10m, 10m, ControlBasis.Control, start, null));
		Assert.Equal(nameof(CorporateGroupRuleViolation.MultipleControllingParents), secondControl.ErrorCode);

		Assert.Equal(3, await database.OwnershipRelationships.CountAsync());
	}

	[Fact]
	public async Task AConsolidatedRun_NeedsAnActiveProfileOfItsOwnGroup()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();
		var otherGroupId = await s.CreateHoldingGroupAsync();
		var otherProfile = await s.Service.CreateProfileAsync(s.Admin, otherGroupId, "IFRS", AccountingFramework.Ifrs, "IRR");
		Assert.True(otherProfile.Succeeded);

		var missing = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Consolidated, null, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));
		Assert.Equal(CorporateGroupService.ErrorProfileRequired, missing.ErrorCode);

		var foreign = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Consolidated, otherProfile.Value, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));
		Assert.Equal(CorporateGroupOutcome.NotFound, foreign.Outcome);

		var profile = await s.Service.CreateProfileAsync(s.Admin, groupId, "استانداردهای ملی", AccountingFramework.IranianNationalStandards, "IRR");
		var run = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Consolidated, profile.Value, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));
		Assert.True(run.Succeeded, run.ErrorCode);
		Assert.Equal(ConsolidationRunStatus.ScopeResolved, run.Value!.Status);
	}

	[Fact]
	public async Task AProfile_IsValidated()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();

		Assert.Equal(CorporateGroupOutcome.Invalid, (await s.Service.CreateProfileAsync(s.Admin, groupId, "p", AccountingFramework.Ifrs, "irr")).Outcome);
		Assert.Equal(CorporateGroupOutcome.Invalid, (await s.Service.CreateProfileAsync(s.Admin, groupId, "p", (AccountingFramework)7, "IRR")).Outcome);
		Assert.True((await s.Service.CreateProfileAsync(s.Admin, groupId, "p", AccountingFramework.Ifrs, "USD")).Succeeded);
		Assert.Equal(CorporateGroupOutcome.Conflict, (await s.Service.CreateProfileAsync(s.Admin, groupId, "p", AccountingFramework.Ifrs, "USD")).Outcome);
		Assert.Equal(CorporateGroupOutcome.AccessDenied, (await s.Service.CreateProfileAsync(s.Viewer, groupId, "q", AccountingFramework.Ifrs, "USD")).Outcome);
	}

	[Fact]
	public async Task AnAggregateRun_RecordsEveryMember_WithItsCoveringFiscalPeriod()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();

		var run = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Aggregate, null, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));

		Assert.True(run.Succeeded, run.ErrorCode);
		Assert.Equal(ConsolidationRunStatus.ScopeResolved, run.Value!.Status);
		Assert.Equal(3, run.Value.Companies.Count);

		var stored = await database.ConsolidationRunCompanies.Where(company => company.ConsolidationRunId == run.Value.RunId).ToListAsync();
		Assert.Equal(3, stored.Count);
		Assert.All(stored, company => Assert.Equal(s.YearPeriod[company.CompanyId], company.FiscalPeriodId));
	}

	[Fact]
	public async Task ARun_IsDenied_WhenTheUserLacksThePermissionInOneCompanyOfTheScope()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();

		// HoldingOnly holds the report permission in Holding only.
		var run = await s.Service.StartReportRunAsync(s.HoldingOnly, new(groupId, GroupReportKind.Aggregate, null, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));

		Assert.Equal(CorporateGroupOutcome.AccessDenied, run.Outcome);
		Assert.Equal(CorporateGroupService.ErrorScopeAccessDenied, run.ErrorCode);
		Assert.Null(run.Value);
		Assert.Equal(0, await database.ConsolidationRuns.CountAsync(candidate => candidate.CorporateGroupId == groupId));

		var denied = await database.SecurityAuditEvents
			.Where(audit => audit.UserId == s.HoldingOnly && audit.EventType == "CorporateGroupAccessDenied")
			.ToListAsync();
		Assert.Single(denied);
		Assert.DoesNotContain(s.SubA.ToString(), denied[0].DetailsJson!, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ARevokedMembershipInASubsidiary_DeniesTheNextRun()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();
		var request = new GroupReportRunRequest(groupId, GroupReportKind.Aggregate, null, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd);
		Assert.True((await s.Service.StartReportRunAsync(s.Admin, request)).Succeeded);

		var membership = await database.UserCompanyMemberships.SingleAsync(m => m.UserId == s.Admin && m.TenantId == s.SubB);
		membership.IsActive = false;
		await database.SaveChangesAsync();

		Assert.Equal(CorporateGroupOutcome.AccessDenied, (await s.Service.StartReportRunAsync(s.Admin, request)).Outcome);
	}

	[Fact]
	public async Task ARun_WithoutACoveringFiscalPeriod_IsRecordedAsFailed_WithoutCompanies()
	{
		var s = await ScenarioAsync();
		var groupId = await s.CreateHoldingGroupAsync();

		// The reporting period crosses the end of the fiscal year of every member.
		var run = await s.Service.StartReportRunAsync(s.Admin, new(groupId, GroupReportKind.Aggregate, null, new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 30)));

		Assert.True(run.Succeeded);
		Assert.Equal(ConsolidationRunStatus.Failed, run.Value!.Status);
		Assert.Equal(CorporateGroupRules.FailureNoCoveringFiscalPeriod, run.Value.FailureCode);
		Assert.Empty(await database.ConsolidationRunCompanies.Where(company => company.ConsolidationRunId == run.Value.RunId).ToListAsync());
	}

	[Fact]
	public async Task ARun_ForAPeriodWithAPartialMembership_IsRecordedAsFailed()
	{
		var s = await ScenarioAsync();
		var group = await s.Service.CreateGroupAsync(s.Admin, s.Holding, CorporateGroupScenario.Unique("g"), "گروه");
		await s.Service.AddMemberAsync(s.Admin, group.Value, s.Holding, CorporateGroupMemberRole.Parent, CorporateGroupScenario.YearStart, null);
		await s.Service.AddMemberAsync(s.Admin, group.Value, s.SubA, CorporateGroupMemberRole.Subsidiary, new DateOnly(2026, 5, 1), null);

		var run = await s.Service.StartReportRunAsync(s.Admin, new(group.Value, GroupReportKind.Aggregate, null, CorporateGroupScenario.YearStart, CorporateGroupScenario.QuarterEnd));

		Assert.Equal(CorporateGroupRules.FailurePartialMembership, run.Value!.FailureCode);
	}

	[Fact]
	public async Task GroupChanges_AreAudited_WithoutFinancialData()
	{
		var s = await ScenarioAsync();
		await s.CreateHoldingGroupAsync();

		// In the owner company: the creation and the three memberships.
		var events = await database.SecurityAuditEvents
			.Where(audit => audit.UserId == s.Admin && audit.TenantId == s.Holding)
			.ToListAsync();
		Assert.Contains(events, audit => audit.EventType == "CorporateGroupCreated");
		Assert.Equal(3, events.Count(audit => audit.EventType == "CorporateGroupMemberAdded"));
		Assert.All(events, audit => Assert.DoesNotContain("Percentage", audit.DetailsJson ?? string.Empty, StringComparison.Ordinal));
	}
}
