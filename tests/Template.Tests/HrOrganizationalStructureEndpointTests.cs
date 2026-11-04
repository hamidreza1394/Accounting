using System.Security.Claims;
using Domain;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Server.Api;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The company-scoped HR endpoints (S2, D-072): dual-mode authorization, employee CRUD
/// with the optional user link, the unit tree with cycle prevention, the position guard
/// and the append-only assignment history. The database side of the constraints is
/// proven on SQL Server by <c>Integration.HrOrganizationalStructureSqlServerTests</c>.
/// </summary>
public sealed class HrOrganizationalStructureEndpointTests
{
    [Fact]
    public async Task Employee_MemberWithCreatePermission_IsCreatedWithoutAnyUserAccount()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.CreateEmployee(manager.Id, "EMP-1", "علی", "محمدی");

        var created = Assert.IsType<Created<HrEmployeeCreatedResponse>>(result);
        var employee = await fixture.Database.CompanyEmployees.SingleAsync();
        Assert.Equal(created.Value!.Id, employee.Id);
        Assert.Equal("EMP-1", employee.EmployeeNumber);
        Assert.Null(employee.UserCompanyMembershipId);
        Assert.Equal(Domain.CompanyEmployeeEmploymentStatus.Active, employee.EmploymentStatus);
        Assert.True(await fixture.Database.SecurityAuditEvents
            .AnyAsync(securityEvent => securityEvent.EventType == "HrEmployeeCreated" && securityEvent.TenantId == fixture.Company.Id));
    }

    [Fact]
    public async Task Employee_DuplicateNumber_IsRefusedInsideTheCompany()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        await fixture.CreateEmployee(manager.Id, "EMP-1", "علی", "محمدی");

        var result = await fixture.CreateEmployee(manager.Id, "EMP-1", "رضا", "احمدی");

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Employee_SameNumberInAnotherCompany_IsAllowed()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        var otherCompany = fixture.AddCompany("شرکت دوم");
        var otherManager = await fixture.AddMemberWithPermission(otherCompany, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        await fixture.CreateEmployee(manager.Id, "EMP-1", "علی", "محمدی");

        var result = await fixture.CreateEmployee(otherManager.Id, "EMP-1", "رضا", "احمدی", otherCompany.Id);

        Assert.IsType<Created<HrEmployeeCreatedResponse>>(result);
        Assert.Equal(2, await fixture.Database.CompanyEmployees.CountAsync());
    }

    [Fact]
    public async Task Employee_MemberWithoutPermission_IsForbidden()
    {
        await using var fixture = new Fixture();
        var member = await fixture.AddMember(fixture.AddRole("CLERK"), "plain");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.ListEmployees(member.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Employee_NonMemberOfTheCompany_GetsTheUnknownCompany404()
    {
        await using var fixture = new Fixture();
        var otherCompany = fixture.AddCompany("شرکت بیرون");
        var outsider = await fixture.AddMemberWithPermission(otherCompany, HrOrganizationalStructureEndpoints.ListEmployeesPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.ListEmployees(outsider.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Employee_InstallationAdministrator_IsAcceptedWithoutAMembership()
    {
        await using var fixture = new Fixture();
        var admin = await fixture.AddInstallationAdministrator();
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.ListEmployees(admin.Id);

        Assert.IsType<Ok<HrEmployeeListResponse>>(result);
    }

    [Fact]
    public async Task Employee_UserLink_RequiresAnActiveMembershipOfTheSameCompany()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageEmployeeUserLinkPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var memberUser = await fixture.AddMember(fixture.AddRole("CLERK"), "member-user");
        await fixture.Database.SaveChangesAsync();

        var linked = await fixture.LinkEmployeeUser(manager.Id, employeeId, memberUser.Id);
        Assert.Equal(StatusCodes.Status204NoContent, (linked as IStatusCodeHttpResult)?.StatusCode);

        // A user without a membership in this company cannot be anchored.
        var outsiderUser = fixture.AddUser("no-membership");
        await fixture.Database.SaveChangesAsync();
        var refused = await fixture.LinkEmployeeUser(manager.Id, employeeId, outsiderUser.Id);
        Assert.Equal(StatusCodes.Status409Conflict, (refused as IStatusCodeHttpResult)?.StatusCode);

        // The link never touches memberships or roles (D-064/D-072).
        Assert.False(await fixture.Database.SecurityAuditEvents
            .AnyAsync(securityEvent => securityEvent.EventType == "UserCompanyRoleAssigned"));
    }

    [Fact]
    public async Task Employee_UserLink_OneMembershipAnchorsOnlyOneEmployee()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageEmployeeUserLinkPermission);
        var firstId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var secondId = await fixture.AddCreatedEmployee(manager.Id, "EMP-2", "رضا", "احمدی");
        var memberUser = await fixture.AddMember(fixture.AddRole("CLERK"), "member-user");
        await fixture.Database.SaveChangesAsync();

        Assert.Equal(StatusCodes.Status204NoContent, (await fixture.LinkEmployeeUser(manager.Id, firstId, memberUser.Id) as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, (await fixture.LinkEmployeeUser(manager.Id, secondId, memberUser.Id) as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Employee_Deactivate_EndsTheActiveAssignmentAndKeepsTheHistory()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.UpdateEmployeesPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var unitId = await fixture.AddCreatedUnit(manager.Id, "UNIT-1", "دفتر مرکزی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employeeId, unitId);

        var result = await fixture.DeactivateEmployee(manager.Id, employeeId);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        var stored = await fixture.Database.CompanyEmployees.SingleAsync(current => current.Id == employeeId);
        Assert.Equal(Domain.CompanyEmployeeEmploymentStatus.Terminated, stored.EmploymentStatus);
        Assert.NotNull(stored.TerminatedOnUtc);
        var assignment = await fixture.Database.CompanyEmployeeAssignments.SingleAsync();
        Assert.Equal(Domain.CompanyEmployeeAssignmentStatus.Ended, assignment.Status);
        Assert.NotNull(assignment.EndOnUtc);
    }

    [Fact]
    public async Task Employee_UpdateWithRowVersion_IsEnforced()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();

        var missing = await fixture.UpdateEmployee(manager.Id, employeeId, rowVersion: null);
        Assert.Equal(StatusCodes.Status400BadRequest, (missing as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Unit_MoveUnderOwnDescendant_IsRefusedAndPersistsNothing()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        var childId = await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", rootId);
        await fixture.Database.SaveChangesAsync();

        // Moving the root under its own child must be refused (409) and must not persist.
        var cycled = await fixture.UpdateUnit(manager.Id, rootId, "دفتر مرکزی", childId, isActive: true, rowVersion: "AAAAAAAAB9I=");
        Assert.Equal(StatusCodes.Status409Conflict, (cycled as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.CompanyOrganizationalUnits.SingleAsync(current => current.Id == rootId)).ParentUnitId);
    }

    [Fact]
    public async Task Unit_MoveUnderAnotherRoot_IsApplied()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        var siblingId = await fixture.AddCreatedUnit(manager.Id, "OPS", "عملیات");
        await fixture.Database.SaveChangesAsync();

        // The InMemory provider stores an empty rowversion, so the test sends the base64
        // of the empty token; on SQL Server a stale or empty token fails closed with 409.
        var moved = await fixture.UpdateUnit(manager.Id, rootId, "دفتر مرکزی", siblingId, isActive: true, rowVersion: string.Empty);
        Assert.Equal(StatusCodes.Status204NoContent, (moved as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(siblingId, (await fixture.Database.CompanyOrganizationalUnits.SingleAsync(current => current.Id == rootId)).ParentUnitId);
    }

    [Fact]
    public async Task Unit_Deactivate_IsBlockedByActiveChildrenAndAssignments()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        var childId = await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", rootId);
        await fixture.Database.SaveChangesAsync();

        Assert.Equal(StatusCodes.Status409Conflict, (await fixture.DeactivateUnit(manager.Id, rootId) as IStatusCodeHttpResult)?.StatusCode);

        // Without children the unit deactivates while it has no active assignments.
        Assert.Equal(StatusCodes.Status204NoContent, (await fixture.DeactivateUnit(manager.Id, childId) as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Unit_TreeView_BuildsTheHierarchyOfActiveUnits()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ListUnitsPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", rootId);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.ListUnits(manager.Id, view: "tree");
        var ok = Assert.IsType<Ok<HrUnitTreeResponse>>(result);
        var rootNode = Assert.Single(ok.Value!.Roots);
        Assert.Equal(rootId, rootNode.Id);
        Assert.Single(rootNode.Children);
    }

    [Fact]
    public async Task Position_Deactivate_IsBlockedByAnActiveAssignment()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreatePositionsPermission, HrOrganizationalStructureEndpoints.UpdatePositionsPermission, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        var assignmentManager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var positionId = await fixture.AddCreatedPosition(manager.Id, "POS-1", "حسابدار");
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(assignmentManager.Id, employeeId, positionId: positionId);

        Assert.Equal(StatusCodes.Status409Conflict, (await fixture.DeactivatePosition(manager.Id, positionId) as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Assignment_StartingASecondActiveOne_IsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var unitId = await fixture.AddCreatedUnit(manager.Id, "UNIT-1", "دفتر مرکزی");
        await fixture.Database.SaveChangesAsync();

        var first = await fixture.StartAssignment(manager.Id, employeeId, unitId);
        Assert.IsType<Created<HrAssignmentCreatedResponse>>(first);

        var second = await fixture.StartAssignment(manager.Id, employeeId, unitId);
        Assert.Equal(StatusCodes.Status409Conflict, (second as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Assignment_SelfManagementAndCrossCompanyManager_AreRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var outsider = fixture.AddCreatedEmployeeRecord(fixture.Company.Id, "EMP-OTHER", "بیرونی", "کاربر");
        await fixture.Database.SaveChangesAsync();

        var self = await fixture.StartAssignment(manager.Id, employeeId, managerEmployeeId: employeeId);
        Assert.Equal(StatusCodes.Status409Conflict, (self as IStatusCodeHttpResult)?.StatusCode);

        var foreignCompany = fixture.AddCompany("شرکت دوم");
        var foreignManager = fixture.AddCreatedEmployeeRecord(foreignCompany.Id, "EMP-X", "بیرونی", "مدیر");
        await fixture.Database.SaveChangesAsync();

        var crossCompany = await fixture.StartAssignment(manager.Id, employeeId, managerEmployeeId: foreignManager.Id);
        Assert.Equal(StatusCodes.Status409Conflict, (crossCompany as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Assignment_ManagerCycle_IsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var a = await fixture.AddCreatedEmployee(manager.Id, "EMP-A", "الف", "یک");
        var b = await fixture.AddCreatedEmployee(manager.Id, "EMP-B", "ب", "دو");
        var c = await fixture.AddCreatedEmployee(manager.Id, "EMP-C", "پ", "سه");
        await fixture.Database.SaveChangesAsync();

        Assert.IsType<Created<HrAssignmentCreatedResponse>>(await fixture.StartAssignment(manager.Id, a, managerEmployeeId: null));
        Assert.IsType<Created<HrAssignmentCreatedResponse>>(await fixture.StartAssignment(manager.Id, b, managerEmployeeId: a));
        Assert.IsType<Created<HrAssignmentCreatedResponse>>(await fixture.StartAssignment(manager.Id, c, managerEmployeeId: b));

        // Making A the manager of the candidate chain root would close the cycle A -> B -> C -> A.
        var cycled = await fixture.ChangeAssignment(manager.Id, a, managerEmployeeId: c);
        Assert.Equal(StatusCodes.Status409Conflict, (cycled as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Assignment_Change_EndsTheCurrentAndStartsTheNewOne()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var unitAId = await fixture.AddCreatedUnit(manager.Id, "UNIT-A", "فروش");
        var unitBId = await fixture.AddCreatedUnit(manager.Id, "UNIT-B", "عملیات");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employeeId, unitAId);

        var changed = await fixture.ChangeAssignment(manager.Id, employeeId, unitId: unitBId);

        Assert.IsType<Ok<HrAssignmentCreatedResponse>>(changed);
        var assignments = await fixture.Database.CompanyEmployeeAssignments
            .OrderBy(assignment => assignment.StartOnUtc)
            .ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.Equal(Domain.CompanyEmployeeAssignmentStatus.Ended, assignments[0].Status);
        Assert.Equal(unitAId, assignments[0].OrganizationalUnitId);
        Assert.Equal(Domain.CompanyEmployeeAssignmentStatus.Active, assignments[1].Status);
        Assert.Equal(unitBId, assignments[1].OrganizationalUnitId);
    }

    [Fact]
    public async Task Assignment_HistoryRequiresAtLeastOneFilter()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ViewEmployeesPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.ListAssignments(manager.Id, employeeId: null);
        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Assignment_EndsTheActiveOneAndKeepsHistoryImmutable()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var unitId = await fixture.AddCreatedUnit(manager.Id, "UNIT-1", "دفتر مرکزی");
        await fixture.Database.SaveChangesAsync();
        var created = Assert.IsType<Created<HrAssignmentCreatedResponse>>(
            await fixture.StartAssignment(manager.Id, employeeId, unitId));

        var ended = await fixture.EndAssignment(manager.Id, created.Value!.Id);
        Assert.Equal(StatusCodes.Status204NoContent, (ended as IStatusCodeHttpResult)?.StatusCode);

        var again = await fixture.EndAssignment(manager.Id, created.Value.Id);
        Assert.Equal(StatusCodes.Status409Conflict, (again as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = NewContext();
            Company = AddCompany("شرکت نمونه");
        }

        private readonly string _databaseName = Guid.NewGuid().ToString();

        public Data.DatabaseContext NewContext() => new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(_databaseName).Options);

        public Data.DatabaseContext Database { get; }
        public Domain.Company Company { get; }

        public Infrastructure.Tenancy.CompanyAccessService CompanyAccess => new(Database);

        public Domain.Company AddCompany(string name)
        {
            var company = new Domain.Company { Name = name, IsActive = true };
            Database.Companies.Add(company);
            return company;
        }

        public Domain.CompanyRole AddRole(string code)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = Company.Id,
                Company = Company,
                Code = code,
                Name = code,
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}-{Guid.NewGuid():N}@example.test")
            {
                Username = username,
                FirstName = username,
                IsActive = true,
                Password = "x",
            };
            Database.Users.Add(user);
            return user;
        }

        public async Task<Domain.User> AddMember(Domain.CompanyRole role, string username)
        {
            var user = AddUser(username);
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = role.TenantId,
                Role = role,
                RoleId = role.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await Database.SaveChangesAsync();
            return user;
        }

        public async Task<Domain.User> AddMemberWithPermission(Domain.Company company, params string[] permissionCodes)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = company.Id,
                Company = company,
                Code = "HR-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "منابع انسانی",
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            foreach (var permissionCode in permissionCodes)
            {
                var permission = new Domain.PermissionDefinition
                {
                    Code = permissionCode,
                    Name = permissionCode,
                    Description = "test permission",
                };
                Database.PermissionDefinitions.Add(permission);
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                {
                    Role = role,
                    PermissionId = permission.Id,
                });
            }
            var user = await AddMember(role, $"hr-manager-{Guid.NewGuid():N}"[..24]);
            await Database.SaveChangesAsync();
            return user;
        }

        public async Task<Domain.User> AddInstallationAdministrator()
        {
            var adminRole = new Domain.Role(Constants.Role.Admin)
            {
                IsActive = true,
            };
            Database.Roles.Add(adminRole);
            var user = new Domain.User($"admin-{Guid.NewGuid():N}@example.test")
            {
                Username = "admin",
                FirstName = "مدیر",
                IsActive = true,
                Password = "x",
                RoleId = adminRole.Id,
                Role = adminRole,
            };
            Database.Users.Add(user);
            await Database.SaveChangesAsync();
            return user;
        }

        public async Task<IResult> CreateEmployee(Guid actorId, string number, string firstName, string lastName, Guid? companyId = null)
        {
            return await HrOrganizationalStructureEndpoints.CreateEmployeeAsync(
                companyId ?? Company.Id, new DefaultHttpContext(), Principal(actorId),
                new HrCreateEmployeeRequest(number, firstName, lastName, null),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);
        }

        public async Task<Guid> AddCreatedEmployee(Guid actorId, string number, string firstName, string lastName)
        {
            var result = await CreateEmployee(actorId, number, firstName, lastName);
            if (result is not Created<HrEmployeeCreatedResponse> created)
                throw new Xunit.Sdk.XunitException($"employee create failed: {Describe(result)}");
            return created.Value!.Id;
        }

        public Domain.CompanyEmployee AddCreatedEmployeeRecord(Guid tenantId, string number, string firstName, string lastName)
        {
            var employee = new Domain.CompanyEmployee
            {
                TenantId = tenantId,
                EmployeeNumber = number,
                FirstName = firstName,
                LastName = lastName,
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            };
            Database.CompanyEmployees.Add(employee);
            return employee;
        }

        public static string Describe(IResult result)
        {
            var statusCode = (result as IStatusCodeHttpResult)?.StatusCode;
            var valueProperty = result.GetType().GetProperty("Value");
            var value = valueProperty?.GetValue(result);
            return $"status={statusCode} body={System.Text.Json.JsonSerializer.Serialize(value)}";
        }

        public async Task<Guid> AddCreatedUnit(Guid actorId, string code, string name, Guid? parentId = null)
        {
            var result = await HrOrganizationalStructureEndpoints.CreateUnitAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId),
                new HrCreateUnitRequest(code, name, null, parentId, null, null),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);
            if (result is not Created<HrUnitCreatedResponse> created)
                throw new Xunit.Sdk.XunitException($"unit create failed: {Describe(result)}");
            await Database.SaveChangesAsync();
            return created.Value!.Id;
        }

        public async Task<Guid> AddCreatedPosition(Guid actorId, string code, string name)
        {
            var result = await HrOrganizationalStructureEndpoints.CreatePositionAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId),
                new HrCreatePositionRequest(code, name, null, null),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);
            var created = Assert.IsType<Created<HrPositionCreatedResponse>>(result);
            await Database.SaveChangesAsync();
            return created.Value!.Id;
        }

        public Task<IResult> ListEmployees(Guid actorId, string? search = null) =>
            HrOrganizationalStructureEndpoints.ListEmployeesAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                search, null, null, null, null, default);

        public Task<IResult> UpdateEmployee(Guid actorId, Guid employeeId, string? rowVersion) =>
            HrOrganizationalStructureEndpoints.UpdateEmployeeAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new HrUpdateEmployeeRequest("علی", "محمدی", Domain.CompanyEmployeeEmploymentStatus.Active, null, rowVersion),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> DeactivateEmployee(Guid actorId, Guid employeeId) =>
            HrOrganizationalStructureEndpoints.DeactivateEmployeeAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> LinkEmployeeUser(Guid actorId, Guid employeeId, Guid userId) =>
            HrOrganizationalStructureEndpoints.LinkEmployeeUserAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new HrLinkEmployeeUserRequest(userId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> ListUnits(Guid actorId, string? view = null) =>
            HrOrganizationalStructureEndpoints.ListUnitsAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                view, null, null, null, default);

        public Task<IResult> UpdateUnit(Guid actorId, Guid unitId, string name, Guid? parentId, bool isActive, string? rowVersion) =>
            HrOrganizationalStructureEndpoints.UpdateUnitAsync(
                Company.Id, unitId, new DefaultHttpContext(), Principal(actorId),
                new HrUpdateUnitRequest(name, null, parentId, isActive, null, null, rowVersion),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> DeactivateUnit(Guid actorId, Guid unitId) =>
            HrOrganizationalStructureEndpoints.DeactivateUnitAsync(
                Company.Id, unitId, new DefaultHttpContext(), Principal(actorId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> DeactivatePosition(Guid actorId, Guid positionId) =>
            HrOrganizationalStructureEndpoints.DeactivatePositionAsync(
                Company.Id, positionId, new DefaultHttpContext(), Principal(actorId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> StartAssignment(
            Guid actorId, Guid employeeId, Guid? unitId = null, Guid? positionId = null, Guid? managerEmployeeId = null) =>
            HrOrganizationalStructureEndpoints.StartAssignmentAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId),
                new HrStartAssignmentRequest(employeeId, unitId, positionId, managerEmployeeId,
                    DateTime.UtcNow, EndActive: false),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> ChangeAssignment(
            Guid actorId, Guid employeeId, Guid? unitId = null, Guid? positionId = null, Guid? managerEmployeeId = null) =>
            HrOrganizationalStructureEndpoints.ChangeAssignmentAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new HrChangeAssignmentRequest(unitId, positionId, managerEmployeeId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> EndAssignment(Guid actorId, Guid assignmentId) =>
            HrOrganizationalStructureEndpoints.EndAssignmentAsync(
                Company.Id, assignmentId, new DefaultHttpContext(), Principal(actorId),
                new HrEndAssignmentRequest(null),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> ListAssignments(Guid actorId, Guid? employeeId) =>
            HrOrganizationalStructureEndpoints.ListAssignmentsAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                employeeId, null, null, null, null, default);

        public ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
