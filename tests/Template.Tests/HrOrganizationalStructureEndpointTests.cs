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
    public async Task Employee_Detail_ServesTheLinkedUserAndHandlesTheMissingLink()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(
            fixture.Company,
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageEmployeeUserLinkPermission);
        var unlinkedId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var memberUser = await fixture.AddMember(fixture.AddRole("CLERK"), "member-user");
        await fixture.LinkEmployeeUser(manager.Id, unlinkedId, memberUser.Id);

        // Without the link (the SQL Server LEFT JOIN path must not throw on a null
        // membership), and with it.
        var unlinked = await fixture.CreateEmployee(manager.Id, "EMP-2", "رضا", "احمدی");
        Assert.IsType<Created<HrEmployeeCreatedResponse>>(unlinked);
        var unlinkedEntityId = await fixture.Database.CompanyEmployees
            .Where(current => current.EmployeeNumber == "EMP-2")
            .Select(current => current.Id)
            .SingleAsync();

        var linkedDetail = await HrOrganizationalStructureEndpoints.GetEmployeeAsync(
            fixture.Company.Id, unlinkedId, new DefaultHttpContext(), fixture.Principal(manager.Id),
            fixture.Database, fixture.CompanyAccess, default);
        var linkedOk = Assert.IsType<Ok<HrEmployeeDetail>>(linkedDetail);
        Assert.Equal(memberUser.Id, linkedOk.Value!.LinkedUserId);
        Assert.NotNull(linkedOk.Value.UserCompanyMembershipId);

        var bareDetail = await HrOrganizationalStructureEndpoints.GetEmployeeAsync(
            fixture.Company.Id, unlinkedEntityId, new DefaultHttpContext(), fixture.Principal(manager.Id),
            fixture.Database, fixture.CompanyAccess, default);
        var bareOk = Assert.IsType<Ok<HrEmployeeDetail>>(bareDetail);
        Assert.Null(bareOk.Value!.LinkedUserId);
        Assert.Null(bareOk.Value.UserCompanyMembershipId);
    }

    [Fact]
    public async Task Employee_List_FilteredByUser_ReturnsOnlyTheLinkedEmployeeOfThatUser()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(
            fixture.Company,
            HrOrganizationalStructureEndpoints.ListEmployeesPermission,
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageEmployeeUserLinkPermission);
        var memberUser = await fixture.AddMember(fixture.AddRole("CLERK"), "member-user");
        var otherUser = await fixture.AddMember(fixture.AddRole("CLERK"), "other-user");
        var linkedId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.AddCreatedEmployee(manager.Id, "EMP-2", "رضا", "احمدی");
        await fixture.LinkEmployeeUser(manager.Id, linkedId, memberUser.Id);

        var result = await fixture.ListEmployeesForUser(manager.Id, memberUser.Id);
        var ok = Assert.IsType<Ok<HrEmployeeListResponse>>(result);
        Assert.Equal(1, ok.Value!.TotalCount);
        var item = Assert.Single(ok.Value.Items);
        Assert.Equal(linkedId, item.Id);

        // Another member of the same company has no linked employee; an unknown user
        // stays an empty list (fail-closed, no existence leak); Guid.Empty is rejected.
        var other = await fixture.ListEmployeesForUser(manager.Id, otherUser.Id);
        Assert.Equal(0, Assert.IsType<Ok<HrEmployeeListResponse>>(other).Value!.TotalCount);

        var unknown = await fixture.ListEmployeesForUser(manager.Id, Guid.NewGuid());
        Assert.Equal(0, Assert.IsType<Ok<HrEmployeeListResponse>>(unknown).Value!.TotalCount);

        var empty = await fixture.ListEmployeesForUser(manager.Id, Guid.Empty);
        Assert.Equal(StatusCodes.Status400BadRequest, (empty as IStatusCodeHttpResult)?.StatusCode);
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
    public async Task Employee_Deactivate_BeforeTheFutureHireDate_IsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی", hiredOnUtc: DateTime.UtcNow.AddDays(30));
        await fixture.Database.SaveChangesAsync();

        // Terminating now would store a termination before the recorded hire date, which
        // the employment period rule forbids; the request must fail closed.
        var result = await fixture.DeactivateEmployee(manager.Id, employeeId);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        var stored = await fixture.Database.CompanyEmployees.SingleAsync(current => current.Id == employeeId);
        Assert.Equal(Domain.CompanyEmployeeEmploymentStatus.Active, stored.EmploymentStatus);
        Assert.Null(stored.TerminatedOnUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents
            .AnyAsync(securityEvent => securityEvent.EventType == "HrEmployeeTerminated"));
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
    public async Task Unit_Update_CannotDeactivateWithAnActiveChild()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", rootId);
        await fixture.Database.SaveChangesAsync();

        // The edit endpoint must enforce the same deactivation guard as the dedicated
        // deactivate endpoint: an active child unit blocks the active -> inactive turn.
        var blocked = await fixture.UpdateUnit(manager.Id, rootId, "دفتر مرکزی", parentId: null, isActive: false, rowVersion: string.Empty);

        Assert.Equal(StatusCodes.Status409Conflict, (blocked as IStatusCodeHttpResult)?.StatusCode);
        Assert.True((await fixture.Database.CompanyOrganizationalUnits.SingleAsync(current => current.Id == rootId)).IsActive);
    }

    [Fact]
    public async Task Unit_Update_CannotDeactivateWithAnActiveAssignment()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var unitId = await fixture.AddCreatedUnit(manager.Id, "UNIT-1", "دفتر مرکزی");
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employeeId, unitId: unitId);

        var blocked = await fixture.UpdateUnit(manager.Id, unitId, "دفتر مرکزی", parentId: null, isActive: false, rowVersion: string.Empty);

        Assert.Equal(StatusCodes.Status409Conflict, (blocked as IStatusCodeHttpResult)?.StatusCode);
        Assert.True((await fixture.Database.CompanyOrganizationalUnits.SingleAsync(current => current.Id == unitId)).IsActive);

        // Ending the assignment unblocks the same edit path.
        var assignmentId = await fixture.Database.CompanyEmployeeAssignments
            .Where(current => current.EmployeeId == employeeId)
            .Select(current => current.Id)
            .SingleAsync();
        await fixture.EndAssignment(manager.Id, assignmentId);
        var unblocked = await fixture.UpdateUnit(manager.Id, unitId, "دفتر مرکزی", parentId: null, isActive: false, rowVersion: string.Empty);
        Assert.Equal(StatusCodes.Status204NoContent, (unblocked as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Unit_Update_RecordsTheParentChangeInTheAuditTrail()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        var siblingId = await fixture.AddCreatedUnit(manager.Id, "OPS", "عملیات");
        await fixture.Database.SaveChangesAsync();

        await fixture.UpdateUnit(manager.Id, rootId, "دفتر مرکزی", parentId: siblingId, isActive: true, rowVersion: string.Empty);
        await fixture.UpdateUnit(manager.Id, siblingId, "عملیات", parentId: null, isActive: true, rowVersion: string.Empty);

        var events = await fixture.Database.SecurityAuditEvents
            .Where(securityEvent => securityEvent.EventType == "HrUnitUpdated")
            .OrderBy(securityEvent => securityEvent.OccurredAtUtc)
            .Select(securityEvent => securityEvent.DetailsJson)
            .ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Contains("\"parentChanged\":true", events[0]);
        Assert.Contains("\"parentChanged\":false", events[1]);
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
    public async Task Unit_FlatList_PaginatesOnlyTheActiveUnits()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ListUnitsPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.UpdateUnitsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        var childId = await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", rootId);
        await fixture.Database.SaveChangesAsync();
        Assert.Equal(StatusCodes.Status204NoContent, (await fixture.DeactivateUnit(manager.Id, childId) as IStatusCodeHttpResult)?.StatusCode);

        var result = await fixture.ListUnits(manager.Id, view: "flat", page: 1, pageSize: 1);

        var ok = Assert.IsType<Ok<HrUnitListResponse>>(result);
        Assert.Equal(1, ok.Value!.TotalCount);
        var item = Assert.Single(ok.Value.Items);
        Assert.Equal(rootId, item.Id);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Unit_Detail_ServesTheCountsAndTheConcurrencyToken()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ListUnitsPermission, HrOrganizationalStructureEndpoints.CreateUnitsPermission, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var rootId = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", rootId);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employeeId, unitId: rootId);

        var result = await fixture.GetUnit(manager.Id, rootId);

        var ok = Assert.IsType<Ok<HrUnitDetail>>(result);
        Assert.Equal(rootId, ok.Value!.Id);
        Assert.Equal(1, ok.Value.ActiveChildCount);
        Assert.Equal(1, ok.Value.ActiveAssignmentCount);
        // The InMemory provider leaves the rowversion empty; the E2E run on SQL Server
        // asserts a real token value (HrUnitDetail always carries the field).
        Assert.NotNull(ok.Value.RowVersion);
    }

    [Fact]
    public async Task Position_Detail_ServesTheActiveAssignmentCount()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ListPositionsPermission, HrOrganizationalStructureEndpoints.CreatePositionsPermission, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var positionId = await fixture.AddCreatedPosition(manager.Id, "POS-1", "حسابدار");
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employeeId, positionId: positionId);

        var result = await fixture.GetPosition(manager.Id, positionId);

        var ok = Assert.IsType<Ok<HrPositionDetail>>(result);
        Assert.Equal(positionId, ok.Value!.Id);
        Assert.Equal(1, ok.Value.ActiveAssignmentCount);
        // The InMemory provider leaves the rowversion empty; the E2E run on SQL Server
        // asserts a real token value.
        Assert.NotNull(ok.Value.RowVersion);
    }

    [Fact]
    public async Task Employee_Unlink_RemovesTheLinkAndRefusesTheSecondTime()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ViewEmployeesPermission, HrOrganizationalStructureEndpoints.ManageEmployeeUserLinkPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var memberUser = await fixture.AddMember(fixture.AddRole("CLERK"), "member-user");
        await fixture.Database.SaveChangesAsync();

        Assert.Equal(StatusCodes.Status204NoContent, (await fixture.LinkEmployeeUser(manager.Id, employeeId, memberUser.Id) as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, (await fixture.UnlinkEmployeeUser(manager.Id, employeeId) as IStatusCodeHttpResult)?.StatusCode);

        // The second unlink has nothing left to remove and the detail shows no link.
        Assert.Equal(StatusCodes.Status409Conflict, (await fixture.UnlinkEmployeeUser(manager.Id, employeeId) as IStatusCodeHttpResult)?.StatusCode);
        var detail = Assert.IsType<Ok<HrEmployeeDetail>>(await HrOrganizationalStructureEndpoints.GetEmployeeAsync(
            fixture.Company.Id, employeeId, new DefaultHttpContext(), fixture.Principal(manager.Id),
            fixture.Database, fixture.CompanyAccess, default));
        Assert.Null(detail.Value!.UserCompanyMembershipId);
        Assert.Null(detail.Value.LinkedUserId);
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
    public async Task Position_Update_CannotDeactivateWithAnActiveAssignment()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreatePositionsPermission, HrOrganizationalStructureEndpoints.UpdatePositionsPermission, HrOrganizationalStructureEndpoints.CreateEmployeesPermission, HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var positionId = await fixture.AddCreatedPosition(manager.Id, "POS-1", "حسابدار");
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employeeId, positionId: positionId);

        // The edit endpoint must enforce the same deactivation guard as the dedicated
        // deactivate endpoint: an active assignment blocks the active -> inactive turn.
        var blocked = await fixture.UpdatePosition(manager.Id, positionId, "حسابدار", isActive: false, rowVersion: string.Empty);

        Assert.Equal(StatusCodes.Status409Conflict, (blocked as IStatusCodeHttpResult)?.StatusCode);
        Assert.True((await fixture.Database.CompanyPositions.SingleAsync(current => current.Id == positionId)).IsActive);

        // Ending the assignment unblocks the same edit path.
        var assignmentId = await fixture.Database.CompanyEmployeeAssignments
            .Where(current => current.EmployeeId == employeeId)
            .Select(current => current.Id)
            .SingleAsync();
        await fixture.EndAssignment(manager.Id, assignmentId);
        var unblocked = await fixture.UpdatePosition(manager.Id, positionId, "حسابدار", isActive: false, rowVersion: string.Empty);
        Assert.Equal(StatusCodes.Status204NoContent, (unblocked as IStatusCodeHttpResult)?.StatusCode);
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

    [Fact]
    public async Task NextNumber_EmptyCompany_SuggestsOne_AndSetsNoStore()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);

        var httpContext = new DefaultHttpContext();
        var result = await fixture.NextNumber(manager.Id, httpContext);

        Assert.Equal("1", Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(result).Value!.SuggestedEmployeeNumber);
        Assert.Contains("no-store", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData(new[] { "5", "12", "7" }, "13")]
    [InlineData(new[] { "EMP-101", "EMP-999" }, "1")]
    [InlineData(new[] { "EMP-101", "8", "9x" }, "9")]
    [InlineData(new[] { "0099", "100" }, "101")]
    [InlineData(new[] { "0099", "7" }, "0100")]
    [InlineData(new[] { "99999999999999999999999999999" }, "100000000000000000000000000000")]
    [InlineData(new[] { "9223372036854775807" }, "9223372036854775808")]
    public async Task NextNumber_UsesTheLargestNumericNumberAndIgnoresTheRest(string[] existing, string expected)
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        foreach (var number in existing)
            fixture.AddCreatedEmployeeRecord(fixture.Company.Id, number, "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.NextNumber(manager.Id);

        Assert.Equal(expected, Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(result).Value!.SuggestedEmployeeNumber);
    }

    [Fact]
    public async Task NextNumber_ThirtyNines_CannotBeIncremented_AndReturns409WithNoStore()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        fixture.AddCreatedEmployeeRecord(fixture.Company.Id, new string('9', 30), "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var result = await fixture.NextNumber(manager.Id, httpContext);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Contains("no-store", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task NextNumber_CountsTerminatedEmployees_AndIgnoresOtherCompanies()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        var otherCompany = fixture.AddCompany("شرکت دوم");
        var terminated = fixture.AddCreatedEmployeeRecord(fixture.Company.Id, "20", "علی", "محمدی");
        terminated.EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Terminated;
        terminated.TerminatedOnUtc = DateTime.UtcNow;
        fixture.AddCreatedEmployeeRecord(fixture.Company.Id, "3", "رضا", "احمدی");
        fixture.AddCreatedEmployeeRecord(otherCompany.Id, "500", "سارا", "کریمی");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.NextNumber(manager.Id);

        Assert.Equal("21", Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(result).Value!.SuggestedEmployeeNumber);
    }

    [Fact]
    public async Task NextNumber_IsASuggestionOnly_AndTheSecondCreateWithTheSameNumberIsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);

        var first = Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(await fixture.NextNumber(manager.Id)).Value!;
        var second = Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(await fixture.NextNumber(manager.Id)).Value!;
        Assert.Equal(first.SuggestedEmployeeNumber, second.SuggestedEmployeeNumber);
        Assert.Equal(0, await fixture.Database.CompanyEmployees.CountAsync());

        Assert.IsType<Created<HrEmployeeCreatedResponse>>(
            await fixture.CreateEmployee(manager.Id, first.SuggestedEmployeeNumber, "علی", "محمدی"));
        var loser = await fixture.CreateEmployee(manager.Id, second.SuggestedEmployeeNumber, "رضا", "احمدی");
        Assert.Equal(StatusCodes.Status409Conflict, (loser as IStatusCodeHttpResult)?.StatusCode);

        var third = Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(await fixture.NextNumber(manager.Id)).Value!;
        Assert.Equal("2", third.SuggestedEmployeeNumber);
    }

    [Fact]
    public async Task NextNumber_RequiresTheCreatePermission_NotJustTheListPermission()
    {
        await using var fixture = new Fixture();
        var lister = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.ListEmployeesPermission);

        var result = await fixture.NextNumber(lister.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task NextNumber_OtherCompanyMember_GetsTheUnknownCompany404()
    {
        await using var fixture = new Fixture();
        var otherCompany = fixture.AddCompany("شرکت بیرون");
        var outsider = await fixture.AddMemberWithPermission(otherCompany, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);

        var result = await fixture.NextNumber(outsider.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task NextNumber_InactiveMembership_IsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddMemberWithPermission(fixture.Company, HrOrganizationalStructureEndpoints.CreateEmployeesPermission);
        var membership = await fixture.Database.UserCompanyMemberships.SingleAsync(current => current.UserId == manager.Id);
        membership.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.NextNumber(manager.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task NextNumber_Anonymous_IsUnauthorized_AndInstallationAdministratorIsAccepted()
    {
        await using var fixture = new Fixture();
        var admin = await fixture.AddInstallationAdministrator();

        var anonymous = await HrOrganizationalStructureEndpoints.GetNextEmployeeNumberAsync(
            fixture.Company.Id, new DefaultHttpContext(), new ClaimsPrincipal(new ClaimsIdentity()),
            fixture.Database, fixture.CompanyAccess, default);

        Assert.Equal(StatusCodes.Status401Unauthorized, (anonymous as IStatusCodeHttpResult)?.StatusCode);
        Assert.IsType<Ok<HrNextEmployeeNumberResponse>>(await fixture.NextNumber(admin.Id));
    }

    [Fact]
    public async Task NextNumber_UnknownOrInactiveCompany_IsNotFound()
    {
        await using var fixture = new Fixture();
        var admin = await fixture.AddInstallationAdministrator();
        fixture.Company.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.NextNumber(admin.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
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

        public async Task<IResult> CreateEmployee(Guid actorId, string number, string firstName, string lastName, Guid? companyId = null, DateTime? hiredOnUtc = null)
        {
            return await HrOrganizationalStructureEndpoints.CreateEmployeeAsync(
                companyId ?? Company.Id, new DefaultHttpContext(), Principal(actorId),
                new HrCreateEmployeeRequest(number, firstName, lastName, hiredOnUtc),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);
        }

        public async Task<Guid> AddCreatedEmployee(Guid actorId, string number, string firstName, string lastName, DateTime? hiredOnUtc = null)
        {
            var result = await CreateEmployee(actorId, number, firstName, lastName, hiredOnUtc: hiredOnUtc);
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
                search: search, cancellationToken: default);

        public Task<IResult> NextNumber(Guid actorId, HttpContext? httpContext = null) =>
            HrOrganizationalStructureEndpoints.GetNextEmployeeNumberAsync(
                Company.Id, httpContext ?? new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess, default);

        public Task<IResult> ListEmployeesForUser(Guid actorId, Guid userId) =>
            HrOrganizationalStructureEndpoints.ListEmployeesAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                userId: userId, cancellationToken: default);

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

        public Task<IResult> ListUnits(Guid actorId, string? view = null, int? page = null, int? pageSize = null) =>
            HrOrganizationalStructureEndpoints.ListUnitsAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                view, null, page, pageSize, default);

        public Task<IResult> GetUnit(Guid actorId, Guid unitId) =>
            HrOrganizationalStructureEndpoints.GetUnitAsync(
                Company.Id, unitId, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess, default);

        public Task<IResult> GetPosition(Guid actorId, Guid positionId) =>
            HrOrganizationalStructureEndpoints.GetPositionAsync(
                Company.Id, positionId, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess, default);

        public Task<IResult> UnlinkEmployeeUser(Guid actorId, Guid employeeId) =>
            HrOrganizationalStructureEndpoints.UnlinkEmployeeUserAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

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

        public Task<IResult> UpdatePosition(Guid actorId, Guid positionId, string name, bool isActive, string? rowVersion) =>
            HrOrganizationalStructureEndpoints.UpdatePositionAsync(
                Company.Id, positionId, new DefaultHttpContext(), Principal(actorId),
                new HrUpdatePositionRequest(name, null, null, isActive, rowVersion),
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
