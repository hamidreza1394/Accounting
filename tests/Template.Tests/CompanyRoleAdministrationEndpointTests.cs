using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class CompanyRoleAdministrationEndpointTests
{
    [Fact]
    public async Task CreateRole_UsesOnlyRegisteredPermissionCodesAndWritesAudit()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        fixture.AddPermission("trade.form.party.view");
        fixture.AddPermission("trade.form.party.create");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "TRADE_OPERATOR", "کارشناس بازرگانی", "trade.operator", null);

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.CreateRoleAsync(
            fixture.CompanyId, new DefaultHttpContext(), fixture.Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var role = await fixture.Database.CompanyRoles.Include(x => x.RolePermissions)
            .SingleAsync(x => x.TenantId == fixture.CompanyId);
        Assert.Equal("TRADE_OPERATOR", role.Code);
        Assert.Equal(2, role.RolePermissions.Count);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyRoleCreated" && audit.TenantId == fixture.CompanyId);
    }

    [Fact]
    public async Task CreateRole_RejectsUnknownPermissionCodes()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        fixture.AddPermission("trade.form.party.view");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "CUSTOM", "نقش سفارشی", null, ["accounting.document.approve"]);

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.CreateRoleAsync(
            fixture.CompanyId, new DefaultHttpContext(), fixture.Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.CompanyRoles.AnyAsync());
    }


    [Fact]
    public async Task UpdateRole_RejectsRoleIdOwnedByAnotherCompany()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
        fixture.Database.Companies.Add(otherCompany);
        var foreignRole = new Domain.CompanyRole
        {
            TenantId = otherCompany.Id,
            Company = otherCompany,
            Code = "FOREIGN",
            Name = "نقش شرکت دیگر",
            IsActive = true,
        };
        fixture.Database.CompanyRoles.Add(foreignRole);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.UpdateRoleAsync(
            fixture.CompanyId, foreignRole.Id, new DefaultHttpContext(), fixture.Principal(admin.Id),
            new Server.Api.UpdateCompanyRoleRequest("تغییر غیرمجاز", true, Array.Empty<string>()),
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal("نقش شرکت دیگر", (await fixture.Database.CompanyRoles.SingleAsync(x => x.Id == foreignRole.Id)).Name);
    }

    [Fact]
    public async Task AssignMembership_RejectsRoleFromAnotherCompany()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var user = fixture.AddUser("employee");
        fixture.AddCompany();
        var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
        fixture.Database.Companies.Add(otherCompany);
        var foreignRole = new Domain.CompanyRole
        {
            TenantId = otherCompany.Id,
            Company = otherCompany,
            Code = "FOREIGN",
            Name = "نقش شرکت دیگر",
            IsActive = true,
        };
        fixture.Database.CompanyRoles.Add(foreignRole);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            fixture.CompanyId, foreignRole.Id, user.Id, new DefaultHttpContext(),
            fixture.Principal(admin.Id), new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync());
    }


    [Fact]
    public async Task AssignMembership_DoesNotCreateANewMembershipBeforeDestinationApproval()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var user = fixture.AddUser("employee");
        fixture.AddCompany();
        var role = new Domain.CompanyRole
        {
            TenantId = fixture.CompanyId,
            Code = "ACCOUNTANT",
            Name = "حسابدار",
            IsActive = true,
        };
        fixture.Database.CompanyRoles.Add(role);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            fixture.CompanyId, role.Id, user.Id, new DefaultHttpContext(),
            fixture.Principal(admin.Id), new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync());
    }

    [Fact]
    public async Task AssignMembership_DoesNotReactivateAnInactiveMembership()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var user = fixture.AddUser("employee");
        fixture.AddCompany();
        var role = new Domain.CompanyRole
        {
            TenantId = fixture.CompanyId,
            Code = "ACCOUNTANT",
            Name = "حسابدار",
            IsActive = true,
        };
        fixture.Database.CompanyRoles.Add(role);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = user.Id,
            TenantId = fixture.CompanyId,
            Role = role,
            RoleId = role.Id,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            fixture.CompanyId, role.Id, user.Id, new DefaultHttpContext(),
            fixture.Principal(admin.Id), new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False((await fixture.Database.UserCompanyMemberships.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task GetRoles_RejectsAnAuthenticatedNonInstallationAdministrator()
    {
        await using var fixture = new Fixture();
        var user = fixture.AddUser("company-manager");
        fixture.AddCompany();
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.GetRolesAsync(
            fixture.CompanyId,
            new DefaultHttpContext(),
            fixture.Principal(user.Id),
            fixture.Database,
            default);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task GetRoleMembers_ReturnsOnlyActiveMembersOfThatRoleWithMinimalFields()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var accountant = fixture.AddRole("ACCOUNTANT");
        var viewer = fixture.AddRole("VIEWER");
        var inCompany = fixture.AddMember(accountant, "ali");
        fixture.AddMember(accountant, "inactive-member", membershipActive: false);
        fixture.AddMember(viewer, "other-role-member");
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
            fixture.CompanyId, accountant.Id, null, null, new DefaultHttpContext(),
            fixture.Principal(admin.Id), fixture.Database, default);

        var ok = Assert.IsType<Ok<Server.Api.CompanyRoleMemberListResponse>>(result);
        var member = Assert.Single(ok.Value!.Items);
        Assert.Equal(inCompany.Id, member.UserId);
        Assert.Equal(1, ok.Value.TotalCount);
        // The projection type itself carries no e-mail, phone or password hash.
        var fields = typeof(Server.Api.CompanyRoleMemberResponse).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain(fields, name => name.Contains("Email") || name.Contains("Phone") || name.Contains("Password"));
    }

    [Fact]
    public async Task GetRoleMembers_PagesAndClampsThePageSize()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var role = fixture.AddRole("ACCOUNTANT");
        for (var i = 0; i < 3; i++) fixture.AddMember(role, $"member-{i}");
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
            fixture.CompanyId, role.Id, 2, 2, new DefaultHttpContext(),
            fixture.Principal(admin.Id), fixture.Database, default);
        var page = Assert.IsType<Ok<Server.Api.CompanyRoleMemberListResponse>>(result).Value!;
        Assert.Equal(3, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(2, page.Page);

        var huge = await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
            fixture.CompanyId, role.Id, 0, 100000, new DefaultHttpContext(),
            fixture.Principal(admin.Id), fixture.Database, default);
        var clamped = Assert.IsType<Ok<Server.Api.CompanyRoleMemberListResponse>>(huge).Value!;
        Assert.Equal(1, clamped.Page);
        Assert.Equal(100, clamped.PageSize);
    }

    [Fact]
    public async Task GetRoleMembers_RejectsRoleOfAnotherCompanyAndNonAdministrators()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var manager = fixture.AddUser("company-manager");
        fixture.AddCompany();
        var foreignRole = fixture.AddRoleInOtherCompany("FOREIGN");
        fixture.AddMember(foreignRole, "foreign-member");
        await fixture.Database.SaveChangesAsync();

        var foreign = await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
            fixture.CompanyId, foreignRole.Id, null, null, new DefaultHttpContext(),
            fixture.Principal(admin.Id), fixture.Database, default);
        Assert.Equal(StatusCodes.Status404NotFound, (foreign as IStatusCodeHttpResult)?.StatusCode);

        var forbidden = await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
            fixture.CompanyId, foreignRole.Id, null, null, new DefaultHttpContext(),
            fixture.Principal(manager.Id), fixture.Database, default);
        Assert.Equal(StatusCodes.Status403Forbidden, (forbidden as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task TransferMembers_MovesActiveMembersAndWritesOneAuditEvent()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var target = fixture.AddRole("NEW");
        var first = fixture.AddMember(source, "first");
        var second = fixture.AddMember(source, "second");
        var inactiveUser = fixture.AddMember(source, "left-company", membershipActive: false);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Transfer(admin.Id, source.Id, target.Id);

        var ok = Assert.IsType<Ok<Server.Api.TransferCompanyRoleMembersResponse>>(result);
        Assert.Equal(2, ok.Value!.MovedCount);
        await using var reader = fixture.NewContext();
        var memberships = await reader.UserCompanyMemberships.ToListAsync();
        Assert.Equal(target.Id, memberships.Single(m => m.UserId == first.Id).RoleId);
        Assert.Equal(target.Id, memberships.Single(m => m.UserId == second.Id).RoleId);
        var untouched = memberships.Single(m => m.UserId == inactiveUser.Id);
        Assert.Equal(source.Id, untouched.RoleId);
        Assert.False(untouched.IsActive);
        var audits = await reader.SecurityAuditEvents
            .Where(e => e.EventType == "UserCompanyRoleAssigned").ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit =>
        {
            Assert.Equal(fixture.CompanyId, audit.TenantId);
            Assert.Equal(admin.Id, audit.UserId);
            Assert.Contains("\"bulkTransfer\":true", audit.DetailsJson);
        });
        Assert.Contains(audits, audit => audit.EntityId == $"{first.Id}:{fixture.CompanyId}");
    }

    [Fact]
    public async Task TransferMembers_ThenTheEmptiedRoleCanBeDeactivated()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var target = fixture.AddRole("NEW");
        fixture.AddMember(source, "member");
        await fixture.Database.SaveChangesAsync();

        var blocked = await Server.Api.CompanyRoleAdministrationEndpoints.UpdateRoleAsync(
            fixture.CompanyId, source.Id, new DefaultHttpContext(), fixture.Principal(admin.Id),
            new Server.Api.UpdateCompanyRoleRequest(source.Name, false, Array.Empty<string>()),
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (blocked as IStatusCodeHttpResult)?.StatusCode);

        await fixture.Transfer(admin.Id, source.Id, target.Id);

        var allowed = await Server.Api.CompanyRoleAdministrationEndpoints.UpdateRoleAsync(
            fixture.CompanyId, source.Id, new DefaultHttpContext(), fixture.Principal(admin.Id),
            new Server.Api.UpdateCompanyRoleRequest(source.Name, false, Array.Empty<string>()),
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (allowed as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task TransferMembers_RejectsATargetRoleOfAnotherCompanyWithoutMovingAnyone()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var foreignTarget = fixture.AddRoleInOtherCompany("FOREIGN");
        fixture.AddMember(source, "member");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Transfer(admin.Id, source.Id, foreignTarget.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(source.Id, (await fixture.Database.UserCompanyMemberships.SingleAsync()).RoleId);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task TransferMembers_RejectsInactiveTargetSameRoleAndEmptyTarget()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var inactiveTarget = fixture.AddRole("DISABLED", active: false);
        fixture.AddMember(source, "member");
        await fixture.Database.SaveChangesAsync();

        Assert.Equal(StatusCodes.Status409Conflict,
            ((await fixture.Transfer(admin.Id, source.Id, inactiveTarget.Id)) as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest,
            ((await fixture.Transfer(admin.Id, source.Id, source.Id)) as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest,
            ((await fixture.Transfer(admin.Id, source.Id, Guid.Empty)) as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(source.Id, (await fixture.Database.UserCompanyMemberships.SingleAsync()).RoleId);
    }

    [Fact]
    public async Task TransferMembers_RejectsNonAdministratorsAndDoesNotMoveAnyone()
    {
        await using var fixture = new Fixture();
        var manager = fixture.AddUser("company-manager");
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var target = fixture.AddRole("NEW");
        fixture.AddMember(source, "member");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Transfer(manager.Id, source.Id, target.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(source.Id, (await fixture.Database.UserCompanyMemberships.SingleAsync()).RoleId);
    }

    [Fact]
    public async Task TransferMembers_RefusesMoreThanTheBatchLimitAndMovesNobody()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var target = fixture.AddRole("NEW");
        for (var i = 0; i <= Server.Api.CompanyRoleAdministrationEndpoints.MaxTransferBatchSize; i++)
            fixture.AddMember(source, $"bulk-{i}");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Transfer(admin.Id, source.Id, target.Id);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.All(await fixture.Database.UserCompanyMemberships.ToListAsync(), m => Assert.Equal(source.Id, m.RoleId));
    }

    [Fact]
    public async Task TransferMembers_RejectsMissingBodyUnknownCompanyAndForeignSourceRole()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var target = fixture.AddRole("NEW");
        var foreignSource = fixture.AddRoleInOtherCompany("FOREIGN");
        fixture.AddMember(foreignSource, "foreign-member");
        await fixture.Database.SaveChangesAsync();

        var noBody = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
            fixture.CompanyId, target.Id, new DefaultHttpContext(), fixture.Principal(admin.Id), null,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status400BadRequest, (noBody as IStatusCodeHttpResult)?.StatusCode);

        var unknownCompany = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
            Guid.NewGuid(), target.Id, new DefaultHttpContext(), fixture.Principal(admin.Id),
            new Server.Api.TransferCompanyRoleMembersRequest(target.Id),
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (unknownCompany as IStatusCodeHttpResult)?.StatusCode);

        var foreign = await fixture.Transfer(admin.Id, foreignSource.Id, target.Id);
        Assert.Equal(StatusCodes.Status404NotFound, (foreign as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal(foreignSource.Id, (await reader.UserCompanyMemberships.SingleAsync()).RoleId);
    }

    [Fact]
    public async Task TransferMembers_RejectsAnInvalidAntiforgeryTokenWithoutMovingAnyone()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var target = fixture.AddRole("NEW");
        fixture.AddMember(source, "member");
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
            fixture.CompanyId, source.Id, new DefaultHttpContext(), fixture.Principal(admin.Id),
            new Server.Api.TransferCompanyRoleMembersRequest(target.Id),
            new DenyAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal(source.Id, (await reader.UserCompanyMemberships.SingleAsync()).RoleId);
    }

    [Fact]
    public async Task TransferMembers_WithNoActiveMembersReturnsZeroAndWritesNoAudit()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var source = fixture.AddRole("OLD");
        var target = fixture.AddRole("NEW");
        fixture.AddMember(source, "left", membershipActive: false);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Transfer(admin.Id, source.Id, target.Id);

        Assert.Equal(0, Assert.IsType<Ok<Server.Api.TransferCompanyRoleMembersResponse>>(result).Value!.MovedCount);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task TransferMembers_RefusesToMoveFromOrIntoASystemRole()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        var system = fixture.AddRole("company-admin");
        system.IsSystemRole = true;
        var normal = fixture.AddRole("NORMAL");
        fixture.AddMember(system, "boss");
        fixture.AddMember(normal, "clerk");
        await fixture.Database.SaveChangesAsync();

        Assert.Equal(StatusCodes.Status409Conflict,
            ((await fixture.Transfer(admin.Id, system.Id, normal.Id)) as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict,
            ((await fixture.Transfer(admin.Id, normal.Id, system.Id)) as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal(1, await reader.UserCompanyMemberships.CountAsync(m => m.RoleId == system.Id));
        Assert.Equal(1, await reader.UserCompanyMemberships.CountAsync(m => m.RoleId == normal.Id));
        Assert.False(await reader.SecurityAuditEvents.AnyAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = NewContext();
        }

        private readonly string _databaseName = Guid.NewGuid().ToString();

        /// <summary>A fresh context on the same store, so assertions read what was really saved.</summary>
        public Data.DatabaseContext NewContext() => new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(_databaseName).Options);

        public Data.DatabaseContext Database { get; }
        public Guid CompanyId { get; private set; }

        public Domain.User AddAdministrator()
        {
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            Database.Roles.Add(role);
            var user = AddUser("admin");
            user.Role = role;
            return user;
        }

        public Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = true,
                Password = new Infrastructure.Security.PasswordHashService().Hash("Admin-Password-Phrase-1406!"),
            };
            Database.Users.Add(user);
            return user;
        }

        public void AddCompany()
        {
            var company = new Domain.Company { Name = "شرکت نمونه", IsActive = true };
            Database.Companies.Add(company);
            CompanyId = company.Id;
        }

        public void AddPermission(string code)
        {
            Database.PermissionDefinitions.Add(new Domain.PermissionDefinition
            {
                Code = code,
                Name = code,
                Description = "test permission",
            });
        }

        public Domain.CompanyRole AddRole(string code, bool active = true)
        {
            var role = new Domain.CompanyRole { TenantId = CompanyId, Code = code, Name = code, IsActive = active };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public Domain.CompanyRole AddRoleInOtherCompany(string code)
        {
            var other = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
            Database.Companies.Add(other);
            var role = new Domain.CompanyRole { TenantId = other.Id, Company = other, Code = code, Name = code, IsActive = true };
            Database.CompanyRoles.Add(role);
            return role;
        }

        // Light user (no password hashing) so tests can create hundreds of members cheaply.
        public Domain.User AddMember(Domain.CompanyRole role, string username, bool membershipActive = true)
        {
            var user = new Domain.User($"{username}@example.test") { Username = username, IsActive = true, Password = "x" };
            Database.Users.Add(user);
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = role.TenantId,
                Role = role,
                RoleId = role.Id,
                IsActive = membershipActive,
                CreatedAtUtc = DateTime.UtcNow,
            });
            return user;
        }

        public Task<IResult> Transfer(Guid actorId, Guid sourceRoleId, Guid targetRoleId) =>
            Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
                CompanyId, sourceRoleId, new DefaultHttpContext(), Principal(actorId),
                new Server.Api.TransferCompanyRoleMembersRequest(targetRoleId),
                new AllowAntiforgery(), Database, TimeProvider.System, default);

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

    private sealed class DenyAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(false);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
