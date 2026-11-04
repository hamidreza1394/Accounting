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
/// The phase-4 operational workflows on top of the D-072 structure: the unit subtree with
/// its assignment counts, the members of a unit, the holders of a position, the allow-listed
/// change history of an employee and the effective-dating controls (no future-dated starts,
/// no start before the recorded hire date).
/// </summary>
public sealed class HrWorkflowsEndpointTests
{
    [Fact]
    public async Task UnitSubtree_ReturnsDescendantsWithTheirAssignmentCounts()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(
            HrOrganizationalStructureEndpoints.CreateUnitsPermission,
            HrOrganizationalStructureEndpoints.ListUnitsPermission,
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var root = await fixture.AddCreatedUnit(manager.Id, "ROOT", "دفتر مرکزی");
        var sales = await fixture.AddCreatedUnit(manager.Id, "SALES", "فروش", root);
        var overseas = await fixture.AddCreatedUnit(manager.Id, "OVERSEAS", "بازرگانی", sales);
        var other = await fixture.AddCreatedUnit(manager.Id, "OTHER", "غیرمرتبط");
        var employee = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var another = await fixture.AddCreatedEmployee(manager.Id, "EMP-2", "رضا", "احمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, employee, unitId: sales);
        await fixture.StartAssignment(manager.Id, another, unitId: overseas);

        var result = await fixture.GetUnitSubtree(manager.Id, root);

        var ok = Assert.IsType<Ok<HrUnitSubtreeNode>>(result);
        Assert.Equal(root, ok.Value!.Id);
        Assert.Equal(0, ok.Value.ActiveAssignmentCount);
        var salesNode = Assert.Single(ok.Value.Children);
        Assert.Equal(sales, salesNode.Id);
        Assert.Equal(1, salesNode.ActiveAssignmentCount);
        var overseasNode = Assert.Single(salesNode.Children);
        Assert.Equal(overseas, overseasNode.Id);
        Assert.Equal(1, overseasNode.ActiveAssignmentCount);
        Assert.All(ok.Value.Children, node => Assert.NotEqual(other, node.Id));
    }

    [Fact]
    public async Task UnitSubtree_UnknownOrForeignUnit_Is404WithoutDisclosure()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(HrOrganizationalStructureEndpoints.ListUnitsPermission);
        await fixture.Database.SaveChangesAsync();

        Assert.Equal(StatusCodes.Status404NotFound, (await fixture.GetUnitSubtree(manager.Id, Guid.NewGuid()) as IStatusCodeHttpResult)?.StatusCode);

        var foreignCompany = fixture.AddCompany("شرکت دوم");
        var foreignUnit = fixture.AddUnitRecord(foreignCompany.Id, "FOREIGN", "واحد بیرونی");
        await fixture.Database.SaveChangesAsync();
        Assert.Equal(StatusCodes.Status404NotFound, (await fixture.GetUnitSubtree(manager.Id, foreignUnit.Id) as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task UnitMembers_ListsOnlyTheActiveAssigneesOfThatUnit()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(
            HrOrganizationalStructureEndpoints.CreateUnitsPermission,
            HrOrganizationalStructureEndpoints.ListUnitsPermission,
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var unitA = await fixture.AddCreatedUnit(manager.Id, "UNIT-A", "فروش");
        var unitB = await fixture.AddCreatedUnit(manager.Id, "UNIT-B", "عملیات");
        var assigned = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var elsewhere = await fixture.AddCreatedEmployee(manager.Id, "EMP-2", "رضا", "احمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, assigned, unitId: unitA);
        await fixture.StartAssignment(manager.Id, elsewhere, unitId: unitB);

        var result = await fixture.ListUnitEmployees(manager.Id, unitA);
        var ok = Assert.IsType<Ok<HrUnitEmployeeListResponse>>(result);
        Assert.Equal(1, ok.Value!.TotalCount);
        Assert.Equal(assigned, ok.Value.Items.Single().EmployeeId);

        // Ending the assignment removes the employee from the live unit members.
        var assignmentId = await fixture.Database.CompanyEmployeeAssignments
            .Where(current => current.EmployeeId == assigned)
            .Select(current => current.Id)
            .SingleAsync();
        await fixture.EndAssignment(manager.Id, assignmentId);
        var afterEnd = Assert.IsType<Ok<HrUnitEmployeeListResponse>>(await fixture.ListUnitEmployees(manager.Id, unitA));
        Assert.Equal(0, afterEnd.Value!.TotalCount);
    }

    [Fact]
    public async Task PositionHolders_ListsEveryActiveHolderAcrossUnits()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(
            HrOrganizationalStructureEndpoints.CreatePositionsPermission,
            HrOrganizationalStructureEndpoints.ListPositionsPermission,
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.CreateUnitsPermission,
            HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var position = await fixture.AddCreatedPosition(manager.Id, "POS-1", "حسابدار");
        var unitA = await fixture.AddCreatedUnit(manager.Id, "UNIT-A", "فروش");
        var unitB = await fixture.AddCreatedUnit(manager.Id, "UNIT-B", "عملیات");
        var first = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var second = await fixture.AddCreatedEmployee(manager.Id, "EMP-2", "رضا", "احمدی");
        await fixture.Database.SaveChangesAsync();
        await fixture.StartAssignment(manager.Id, first, unitId: unitA, positionId: position);
        await fixture.StartAssignment(manager.Id, second, unitId: unitB, positionId: position);

        var result = await fixture.ListPositionEmployees(manager.Id, position);
        var ok = Assert.IsType<Ok<HrPositionEmployeeListResponse>>(result);
        Assert.Equal(2, ok.Value!.TotalCount);
        Assert.Contains(ok.Value.Items, item => item.EmployeeId == first && item.OrganizationalUnitId == unitA);
        Assert.Contains(ok.Value.Items, item => item.EmployeeId == second && item.OrganizationalUnitId == unitB);
    }

    [Fact]
    public async Task EmployeeHistory_ServesTheAllowListedAuditTrail()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission,
            HrOrganizationalStructureEndpoints.UpdateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageAssignmentsPermission,
            HrOrganizationalStructureEndpoints.ManageEmployeeUserLinkPermission);
        var employeeId = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        var memberUser = await fixture.AddMember(fixture.AddRole("CLERK"), "member-user");
        await fixture.LinkEmployeeUser(manager.Id, employeeId, memberUser.Id);
        await fixture.DeactivateEmployee(manager.Id, employeeId);

        var result = await fixture.GetEmployeeHistory(manager.Id, employeeId);
        var ok = Assert.IsType<Ok<HrEmployeeHistoryResponse>>(result);
        var eventTypes = ok.Value!.Items.Select(item => item.EventType).ToArray();
        Assert.Contains("HrEmployeeCreated", eventTypes);
        Assert.Contains("HrEmployeeUserLinked", eventTypes);
        Assert.Contains("HrEmployeeTerminated", eventTypes);
        Assert.DoesNotContain("UserCompanyRoleAssigned", eventTypes);
        // The allow-listed projection carries no request metadata or payload JSON.
        var fields = typeof(HrEmployeeHistoryItem).GetProperties().Select(property => property.Name).Order().ToArray();
        Assert.Equal(["ActorUserId", "CorrelationId", "EntityId", "EntityType", "EventType", "OccurredAtUtc"], fields);
    }

    [Fact]
    public async Task AssignmentStart_InTheFuture_IsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var employee = await fixture.AddCreatedEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.StartAssignmentAt(manager.Id, employee, DateTime.UtcNow.AddDays(7));
        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(0, await fixture.Database.CompanyEmployeeAssignments.CountAsync());
    }

    [Fact]
    public async Task AssignmentStart_BeforeTheRecordedHireDate_IsRefused()
    {
        await using var fixture = new Fixture();
        var manager = await fixture.AddHrManager(
            HrOrganizationalStructureEndpoints.CreateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ManageAssignmentsPermission);
        var hiredOnUtc = DateTime.UtcNow.AddDays(-30);
        var createResult = await fixture.CreateEmployee(manager.Id, "EMP-1", "علی", "محمدی");
        Assert.IsType<Created<HrEmployeeCreatedResponse>>(createResult);
        var employeeId = (await fixture.Database.CompanyEmployees.SingleAsync()).Id;
        var employee = await fixture.Database.CompanyEmployees.SingleAsync(current => current.Id == employeeId);
        employee.HiredOnUtc = DateTime.UtcNow.AddDays(-10);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.StartAssignmentAt(manager.Id, employeeId, DateTime.UtcNow.AddDays(-20));
        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(0, await fixture.Database.CompanyEmployeeAssignments.CountAsync());

        // Starting after the hire date works.
        var valid = await fixture.StartAssignmentAt(manager.Id, employeeId, DateTime.UtcNow.AddDays(-5));
        Assert.IsType<Created<HrAssignmentCreatedResponse>>(valid);
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
                Username = $"{username}{Guid.NewGuid():N}"[..20],
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

        /// <summary>A company manager of the fixture company holding the given hr.* codes.</summary>
        public async Task<Domain.User> AddHrManager(params string[] permissionCodes)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = Company.Id,
                Company = Company,
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
            var user = await AddMember(role, "hr-manager");
            await Database.SaveChangesAsync();
            return user;
        }

        public Domain.CompanyOrganizationalUnit AddUnitRecord(Guid tenantId, string code, string name)
        {
            var unit = new Domain.CompanyOrganizationalUnit
            {
                TenantId = tenantId,
                Code = code,
                Name = name,
                IsActive = true,
            };
            Database.CompanyOrganizationalUnits.Add(unit);
            return unit;
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
            if (result is not Created<HrPositionCreatedResponse> created)
                throw new Xunit.Sdk.XunitException($"position create failed: {Describe(result)}");
            await Database.SaveChangesAsync();
            return created.Value!.Id;
        }

        public Task<IResult> GetUnitSubtree(Guid actorId, Guid unitId) =>
            HrOrganizationalStructureEndpoints.GetUnitSubtreeAsync(
                Company.Id, unitId, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess, default);

        public Task<IResult> ListUnitEmployees(Guid actorId, Guid unitId) =>
            HrOrganizationalStructureEndpoints.ListUnitEmployeesAsync(
                Company.Id, unitId, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                null, null, default);

        public Task<IResult> ListPositionEmployees(Guid actorId, Guid positionId) =>
            HrOrganizationalStructureEndpoints.ListPositionEmployeesAsync(
                Company.Id, positionId, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                null, null, default);

        public Task<IResult> GetEmployeeHistory(Guid actorId, Guid employeeId) =>
            HrOrganizationalStructureEndpoints.GetEmployeeHistoryAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId), Database, CompanyAccess,
                null, null, default);

        public Task<IResult> StartAssignment(
            Guid actorId, Guid employeeId, Guid? unitId = null, Guid? positionId = null, Guid? managerEmployeeId = null) =>
            StartAssignmentAt(actorId, employeeId, DateTime.UtcNow, unitId, positionId, managerEmployeeId);

        public Task<IResult> StartAssignmentAt(
            Guid actorId, Guid employeeId, DateTime startOnUtc, Guid? unitId = null, Guid? positionId = null, Guid? managerEmployeeId = null) =>
            HrOrganizationalStructureEndpoints.StartAssignmentAsync(
                Company.Id, new DefaultHttpContext(), Principal(actorId),
                new HrStartAssignmentRequest(employeeId, unitId, positionId, managerEmployeeId,
                    startOnUtc, EndActive: false),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> EndAssignment(Guid actorId, Guid assignmentId) =>
            HrOrganizationalStructureEndpoints.EndAssignmentAsync(
                Company.Id, assignmentId, new DefaultHttpContext(), Principal(actorId),
                new HrEndAssignmentRequest(null),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> LinkEmployeeUser(Guid actorId, Guid employeeId, Guid userId) =>
            HrOrganizationalStructureEndpoints.LinkEmployeeUserAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new HrLinkEmployeeUserRequest(userId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> DeactivateEmployee(Guid actorId, Guid employeeId) =>
            HrOrganizationalStructureEndpoints.DeactivateEmployeeAsync(
                Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public static string Describe(IResult result)
        {
            var statusCode = (result as IStatusCodeHttpResult)?.StatusCode;
            var valueProperty = result.GetType().GetProperty("Value");
            var value = valueProperty?.GetValue(result);
            return $"status={statusCode} body={System.Text.Json.JsonSerializer.Serialize(value)}";
        }

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
