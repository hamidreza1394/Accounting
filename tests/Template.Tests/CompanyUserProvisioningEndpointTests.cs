using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// D-078: company-scoped user provisioning. A company manager holding
/// <c>users.create</c> creates a user for (or reuses an account in) their own company and
/// the same-company membership is active immediately — no pending request, no destination
/// approval, and no role, permission or fiscal grant. Cross-company provisioning is
/// impossible: the actor needs the permission in the very company of the route.
/// </summary>
public sealed class CompanyUserProvisioningEndpointTests
{
    private const string CreateUserPermission = "users.create";

    [Fact]
    public async Task Provision_CreatesUserAndActiveRolelessMembership_WithAudits()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync(withUsersList: false);

        var request = new Server.Api.CreateUserRequest(
            "new.member", "کاربر", "تازه", "new.member@example.test", null,
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true);

        var context = new DefaultHttpContext();
        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, context, Principal(manager.Id), request, new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);

        var created = Assert.IsType<Created<Server.Api.CompanyUserProvisionedResponse>>(result);
        Assert.True(created.Value!.UserCreated);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());

        var user = await fixture.Database.Users.Include(u => u.Role)
            .SingleAsync(candidate => candidate.Username == "new.member");
        Assert.Equal(created.Value.UserId, user.Id);
        Assert.Null(user.RoleId); // no legacy global role either
        Assert.NotEqual("A-Long-Unique-Phrase-1406!", user.Password);
        Assert.Equal(Infrastructure.Security.PasswordVerificationOutcome.Success,
            new Infrastructure.Security.PasswordHashService().Verify(user.Password, "A-Long-Unique-Phrase-1406!"));

        var membership = await fixture.Database.UserCompanyMemberships
            .Include(membership => membership.Role)
            .SingleAsync(candidate => candidate.UserId == user.Id && candidate.TenantId == company.Id);
        Assert.True(membership.IsActive);
        Assert.Null(membership.RoleId);
        Assert.Null(membership.Role);

        // Same-company provisioning never creates a pending request (D-078 §2).
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());

        var audits = await fixture.Database.SecurityAuditEvents.ToListAsync();
        Assert.Contains(audits, audit => audit.EventType == "UserCreated" && audit.EntityId == user.Id.ToString());
        var membershipAudit = Assert.Single(audits, audit => audit.EventType == "UserMembershipCreated");
        Assert.Equal(user.Id.ToString(), membershipAudit.EntityId);
        Assert.Equal(company.Id, membershipAudit.TenantId);
        Assert.DoesNotContain("A-Long-Unique-Phrase-1406!", membershipAudit.DetailsJson);
        Assert.DoesNotContain(user.Password, membershipAudit.DetailsJson);
    }

    [Fact]
    public async Task Provision_RequiresUsersCreate_InThatCompany()
    {
        await using var fixture = new Fixture();
        var companyA = new Domain.Company { Name = "شرکت الف", IsActive = true };
        var companyB = new Domain.Company { Name = "شرکت ب", IsActive = true };
        var otherPermission = new Domain.PermissionDefinition { Code = "users.list", Name = "مشاهده کاربران شرکت" };
        var roleWithoutCreate = new Domain.CompanyRole
        { Company = companyA, TenantId = companyA.Id, Code = "READER", Name = "خواننده", IsActive = true };
        var manager = fixture.NewUser("manager", active: true);

        fixture.Database.AddRange(companyA, companyB, otherPermission, roleWithoutCreate, manager);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = manager.Id, TenantId = companyA.Id, Company = companyA,
            RoleId = roleWithoutCreate.Id, Role = roleWithoutCreate, IsActive = true,
        });
        await fixture.Database.SaveChangesAsync();

        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);
        var request = fixture.NewCreateRequest("anyone");

        // A member without users.create is an explicit 403.
        var denied = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            companyA.Id, new DefaultHttpContext(), Principal(manager.Id), request, new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Security.PasswordHashService(), access,
            TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        // A company the actor has no membership in is the same 404 as an unknown company —
        // a manager of A cannot create users in B and learns nothing about B.
        var foreign = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            companyB.Id, new DefaultHttpContext(), Principal(manager.Id), request, new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Security.PasswordHashService(), access,
            TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (foreign as IStatusCodeHttpResult)?.StatusCode);

        Assert.False(await fixture.Database.Users.AnyAsync(user => user.Username == "anyone"));
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(membership => membership.TenantId == companyB.Id));
    }

    [Fact]
    public async Task Provision_InstallationAdministratorCanBootstrapFirstMember()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddInstallationAdmin();
        var company = new Domain.Company { Name = "شرکت تازه", IsActive = true };
        fixture.Database.Add(company);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.NewCreateRequest("first.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);

        var created = Assert.IsType<Created<Server.Api.CompanyUserProvisionedResponse>>(result);
        Assert.True(created.Value.UserCreated);
        var membership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(candidate => candidate.TenantId == company.Id);
        Assert.True(membership.IsActive);
        Assert.Null(membership.RoleId);

        // An inactive company stays invisible (same 404 as an unknown company).
        company.IsActive = false;
        await fixture.Database.SaveChangesAsync();
        var inactive = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.NewCreateRequest("second.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (inactive as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.Users.AnyAsync(user => user.Username == "second.member"));
    }

    [Fact]
    public async Task Provision_ReusesExistingAccountWithoutTouchingCredentialsOrProfile()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync();
        var hasher = new Infrastructure.Security.PasswordHashService();
        var existing = fixture.NewUser("known.member", active: true);
        existing.FirstName = "قبلی";
        existing.LastName = "ثبت‌شده";
        existing.EmailAddress = "known.member@example.test";
        existing.CellPhoneNumber = "09120000001";
        existing.Password = hasher.Hash("Original-Secret-Phrase-1406!");
        fixture.Database.Add(existing);
        await fixture.Database.SaveChangesAsync();
        var originalHash = existing.Password;
        var originalInsert = existing.InsertDateTime;

        // A completely different profile in the request: everything must be ignored.
        var request = new Server.Api.CreateUserRequest(
            "known.member", "نام", "جدید", "other.address@example.test", "09120000009",
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", false);

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), request, new AllowAntiforgery(),
            fixture.Database, hasher, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);

        var created = Assert.IsType<Created<Server.Api.CompanyUserProvisionedResponse>>(result);
        Assert.False(created.Value.UserCreated);
        Assert.Equal(existing.Id, created.Value.UserId);
        Assert.Equal(1, await fixture.Database.Users.CountAsync(user => user.Username == "known.member"));

        var reloaded = await fixture.Database.Users.SingleAsync(user => user.Id == existing.Id);
        Assert.Equal(originalHash, reloaded.Password); // password never changed
        Assert.Equal("قبلی", reloaded.FirstName);
        Assert.Equal("ثبت‌شده", reloaded.LastName);
        Assert.Equal("other.address@example.test" == reloaded.EmailAddress ? "unexpected" : "known.member@example.test", reloaded.EmailAddress);
        Assert.Equal("09120000001", reloaded.CellPhoneNumber);
        Assert.True(reloaded.IsActive);
        Assert.Equal(originalInsert, reloaded.InsertDateTime);

        var membership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(candidate => candidate.UserId == existing.Id && candidate.TenantId == company.Id);
        Assert.True(membership.IsActive);
        Assert.Null(membership.RoleId);
    }

    [Fact]
    public async Task Provision_ConflictsOnActiveOrInactiveMembershipAndInactiveAccounts()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync();
        var activeMember = fixture.NewUser("active.member", active: true);
        var inactiveMember = fixture.NewUser("inactive.member", active: true);
        var disabledAccount = fixture.NewUser("disabled.member", active: false);
        var someRole = new Domain.CompanyRole
        { Company = company, TenantId = company.Id, Code = "CLERK", Name = "منشی", IsActive = true };
        fixture.Database.AddRange(activeMember, inactiveMember, disabledAccount, someRole);
        fixture.Database.UserCompanyMemberships.AddRange(
            new Domain.UserCompanyMembership
            { UserId = activeMember.Id, TenantId = company.Id, Company = company, RoleId = someRole.Id, Role = someRole, IsActive = true },
            new Domain.UserCompanyMembership
            { UserId = inactiveMember.Id, TenantId = company.Id, Company = company, RoleId = someRole.Id, Role = someRole, IsActive = false });
        await fixture.Database.SaveChangesAsync();
        var membershipCount = await fixture.Database.UserCompanyMemberships.CountAsync();

        var active = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("active.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (active as IStatusCodeHttpResult)?.StatusCode);
        Assert.Contains("از قبل عضویت فعال دارد", ReadBody(active));

        var inactive = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("inactive.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (inactive as IStatusCodeHttpResult)?.StatusCode);
        Assert.Contains("درخواست و تأیید شرکت", ReadBody(inactive));

        var disabled = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("disabled.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (disabled as IStatusCodeHttpResult)?.StatusCode);

        Assert.Equal(membershipCount, await fixture.Database.UserCompanyMemberships.CountAsync());
    }

    [Fact]
    public async Task Provision_RejectsDuplicateEmailOrPhoneOfAnotherAccount()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync();
        var other = fixture.NewUser("other.user", active: true);
        other.EmailAddress = "taken@example.test";
        other.CellPhoneNumber = "09120000002";
        other.Password = "private-password";
        fixture.Database.Add(other);
        await fixture.Database.SaveChangesAsync();
        var userCount = await fixture.Database.Users.CountAsync();

        var emailConflict = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("fresh.one", email: "taken@example.test"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (emailConflict as IStatusCodeHttpResult)?.StatusCode);

        var phoneConflict = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("fresh.two", phone: "09120000002"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (phoneConflict as IStatusCodeHttpResult)?.StatusCode);

        Assert.Equal(userCount, await fixture.Database.Users.CountAsync());
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(membership => membership.UserId != manager.Id));
    }

    [Fact]
    public async Task Provision_IndependentGrantInAnotherCompany_IsAllowedWithoutAnyHoldingRole()
    {
        await using var fixture = new Fixture();
        var companyA = new Domain.Company { Name = "شرکت الف", IsActive = true };
        var companyB = new Domain.Company { Name = "شرکت ب", IsActive = true };
        var createPermission = new Domain.PermissionDefinition { Code = CreateUserPermission, Name = "ایجاد کاربر برای شرکت" };
        var roleA = new Domain.CompanyRole { Company = companyA, TenantId = companyA.Id, Code = "MANAGER_A", Name = "مدیر الف", IsActive = true };
        var roleB = new Domain.CompanyRole { Company = companyB, TenantId = companyB.Id, Code = "MANAGER_B", Name = "مدیر ب", IsActive = true };
        var manager = fixture.NewUser("dual.manager", active: true);
        fixture.Database.AddRange(companyA, companyB, createPermission, roleA, roleB, manager);
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        { Role = roleA, RoleId = roleA.Id, Permission = createPermission, PermissionId = createPermission.Id });
        fixture.Database.UserCompanyMemberships.AddRange(
            new Domain.UserCompanyMembership
            { UserId = manager.Id, TenantId = companyA.Id, Company = companyA, RoleId = roleA.Id, Role = roleA, IsActive = true },
            new Domain.UserCompanyMembership
            { UserId = manager.Id, TenantId = companyB.Id, Company = companyB, RoleId = roleB.Id, Role = roleB, IsActive = true });
        await fixture.Database.SaveChangesAsync();

        // The same permission code is granted independently inside company B; no corporate
        // group or ownership relationship exists — only the membership carries the grant.
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        { Role = roleB, RoleId = roleB.Id, Permission = createPermission, PermissionId = createPermission.Id });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            companyB.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("member.of.b"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var membership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(candidate => candidate.TenantId == companyB.Id && candidate.UserId != manager.Id);
        Assert.True(membership.IsActive);
        Assert.Null(membership.RoleId);
    }

    [Fact]
    public async Task Provision_RolelessMembership_GrantsNoAccessButStaysVisibleInDirectory()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync();
        var listPermission = new Domain.PermissionDefinition { Code = "users.list", Name = "مشاهده کاربران شرکت" };
        fixture.Database.Add(listPermission);
        await fixture.Database.SaveChangesAsync();
        var managerMembership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(candidate => candidate.UserId == manager.Id && candidate.TenantId == company.Id);
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            RoleId = managerMembership.RoleId!.Value,
            Permission = listPermission,
            PermissionId = listPermission.Id,
        });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("no.role.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        var created = Assert.IsType<Created<Server.Api.CompanyUserProvisionedResponse>>(result);
        var memberId = created.Value.UserId;

        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);

        // Fail-closed: the membership alone is not an operational access of any kind.
        Assert.False(await access.HasPermissionAsync(memberId, company.Id, "users.list"));
        Assert.False(await access.HasPermissionAsync(memberId, company.Id, CreateUserPermission));
        Assert.Empty(await access.GetGrantedPermissionCodesAsync(memberId, company.Id));
        Assert.Empty(await access.GetAccessibleCompaniesAsync(memberId));

        // …but the member stays visible in the company directory with empty role fields.
        var directory = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.Database, access);
        var ok = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(directory);
        var item = Assert.Single(ok.Value!.Items, candidate => candidate.UserId == memberId);
        Assert.True(item.MembershipIsActive);
        Assert.Null(item.RoleId);
        Assert.Null(item.RoleCode);
        Assert.Null(item.RoleName);
        Assert.False(item.RoleIsActive);
    }

    [Fact]
    public async Task Provision_SharesTheAdministratorValidationAndSecurityControls()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync();
        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);

        // The shared validation is exactly the administrator's policy.
        var mismatch = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id),
            new Server.Api.CreateUserRequest("someone", "نام", "خانوادگی", "someone@example.test", null,
                "A-Long-Unique-Phrase-1406!", "A-Different-Long-Phrase-1406!", true),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status400BadRequest, (mismatch as IStatusCodeHttpResult)?.StatusCode);

        var badCharset = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id),
            new Server.Api.CreateUserRequest("ali;drop", "نام", "خانوادگی", "someone2@example.test", null,
                "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status400BadRequest, (badCharset as IStatusCodeHttpResult)?.StatusCode);

        // Anti-forgery is enforced on the company path too.
        var forged = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.NewCreateRequest("someone3"),
            new DenyAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status400BadRequest, (forged as IStatusCodeHttpResult)?.StatusCode);

        // Anonymous callers are 401.
        var anonymous = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), new ClaimsPrincipal(), fixture.NewCreateRequest("someone4"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status401Unauthorized, (anonymous as IStatusCodeHttpResult)?.StatusCode);

        Assert.False(await fixture.Database.Users.AnyAsync(user =>
            (user.Username == "someone" || user.Username == "someone2" ||
             user.Username == "someone3" || user.Username == "someone4" ||
             user.Username == "ali;drop")));
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    private static string? ReadBody(IResult result)
    {
        var value = result.GetType().GetProperty("Value")?.GetValue(result);
        return value?.ToString();
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

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        }

        public Data.DatabaseContext Database { get; }

        public Domain.User NewUser(string username, bool active) => new($"{username}@example.test")
        {
            Username = username,
            IsActive = active,
            Password = "private-password",
        };

        public Domain.User AddInstallationAdmin()
        {
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User("admin@example.test")
            {
                Username = "admin",
                IsActive = true,
                Role = role,
                Password = "private-password",
            };
            Database.AddRange(role, admin);
            return admin;
        }

        public Server.Api.CreateUserRequest NewCreateRequest(
            string username, string? email = null, string? phone = null) => new(
            username, "کاربر", "شرکت", email ?? $"{username}@example.test", phone,
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true);

        /// <summary>Seeds one active company whose manager holds <c>users.create</c> (and
        /// optionally <c>users.list</c>) through an active role.</summary>
        public async Task<(Domain.Company Company, Domain.User Manager)> SeedCompanyManagerAsync(bool withUsersList = true)
        {
            var company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            var createPermission = new Domain.PermissionDefinition { Code = CreateUserPermission, Name = "ایجاد کاربر برای شرکت" };
            var listPermission = new Domain.PermissionDefinition { Code = "users.list", Name = "مشاهده کاربران شرکت" };
            var role = new Domain.CompanyRole
            { Company = company, TenantId = company.Id, Code = "MANAGER", Name = "مدیر شرکت", IsActive = true };
            var manager = NewUser("manager", active: true);
            Database.AddRange(company, createPermission, role, manager);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            { Role = role, RoleId = role.Id, Permission = createPermission, PermissionId = createPermission.Id });
            if (withUsersList)
            {
                Database.Add(listPermission);
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                { Role = role, RoleId = role.Id, Permission = listPermission, PermissionId = listPermission.Id });
            }
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = manager.Id, TenantId = company.Id, Company = company,
                RoleId = role.Id, Role = role, IsActive = true,
            });
            await Database.SaveChangesAsync();
            return (company, manager);
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
