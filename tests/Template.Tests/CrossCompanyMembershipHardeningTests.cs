using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// D-078 × D-065 inter-company hardening: a manager may provision their OWN company
/// freely, but the membership of any other company stays behind the destination-approved
/// request workflow — corporate group membership and ownership relationships grant
/// nothing, self-approval stays forbidden, and a decision never leaks an active
/// membership before the destination approves it.
/// </summary>
public sealed class CrossCompanyMembershipHardeningTests
{
    [Fact]
    public async Task SameCompanyProvisioning_NeverCreatesAPendingRequest_EvenForTheSameActor()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerAsync();

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), fixture.Principal(manager.Id), fixture.NewCreateRequest("in.company"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());
        var membership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(item => item.UserId != manager.Id);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task ManagerOfA_CannotProvisionIntoB_AndNothingIsCreatedThere()
    {
        await using var fixture = new Fixture();
        var companyB = new Domain.Company { Name = "شرکت ب", IsActive = true };
        fixture.Database.Add(companyB);
        var (companyA, managerA) = await fixture.SeedCompanyManagerAsync();

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            companyB.Id, new DefaultHttpContext(), fixture.Principal(managerA.Id), fixture.NewCreateRequest("sneaky"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.Users.AnyAsync(user => user.Username == "sneaky"));
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(item => item.TenantId == companyB.Id));
        // No pending request was smuggled in either: provisioning never falls back to the
        // request workflow — that path belongs to the user and the destination approver.
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());
    }

    [Fact]
    public async Task HoldingRelationship_GrantsNoApprovalRight_OverTheSubsidiary()
    {
        await using var fixture = new Fixture();
        var companyB = new Domain.Company { Name = "شرکت زیرمجموعه", IsActive = true };
        // A owns B through an ownership relationship AND a corporate group membership.
        var (companyA, managerA) = await fixture.SeedCompanyManagerAsync(withMembershipApprove: true);
        companyB.CustomerBoundaryId = companyA.CustomerBoundaryId;
        fixture.Database.Add(companyB);
        var group = new Domain.CorporateGroup
        {
            Code = "G1",
            Name = "گروه آزمون",
            OwnerCompanyId = companyA.Id,
            OwnerCompany = companyA,
            IsActive = true,
            CreatedByUserId = managerA.Id,
        };
        fixture.Database.Add(group);
        fixture.Database.CorporateGroupMembers.Add(new Domain.CorporateGroupMember
        {
            CorporateGroupId = group.Id,
            CorporateGroup = group,
            CompanyId = companyB.Id,
            Company = companyB,
            MemberRole = Domain.CorporateGroupMemberRole.Subsidiary,
            ValidFrom = new DateOnly(2026, 1, 1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = managerA.Id,
        });
        fixture.Database.OwnershipRelationships.Add(new Domain.OwnershipRelationship
        {
            ParentCompanyId = companyA.Id,
            ParentCompany = companyA,
            SubsidiaryCompanyId = companyB.Id,
            SubsidiaryCompany = companyB,
            OwnershipPercentage = 100m,
            VotingPercentage = 100m,
            ValidFrom = new DateOnly(2026, 1, 1),
        });
        await fixture.Database.SaveChangesAsync();

        // A member of A requests membership in the subsidiary B through the real,
        // self-service workflow (D-065).
        var requester = fixture.NewUser("wants.in.b", active: true);
        fixture.Database.Add(requester);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = requester.Id, TenantId = companyA.Id, Company = companyA,
            RoleId = fixture.MemberRoleId(companyA), Role = null, IsActive = true,
        });
        await fixture.Database.SaveChangesAsync();
        var requested = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(companyB.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);
        var requestId = CreatedRequestId(requested);

        // The manager of the PARENT company A cannot decide the subsidiary's queue: the
        // holding relationship is not an approval permission in B (D-065/D-078).
        var denied = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            companyB.Id, requestId, new DefaultHttpContext(), fixture.Principal(managerA.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.NewRoleId()),
            new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (denied as IStatusCodeHttpResult)?.StatusCode);

        var rejectDenied = await Server.Api.CompanyMembershipRequestEndpoints.RejectAsync(
            companyB.Id, requestId, new DefaultHttpContext(), fixture.Principal(managerA.Id),
            new Server.Api.RejectCompanyMembershipRequest("parent says no"),
            new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (rejectDenied as IStatusCodeHttpResult)?.StatusCode);

        // Nothing changed: the request is still pending and no membership exists in B.
        var stillPending = await fixture.Database.CompanyMembershipRequests.SingleAsync();
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending, stillPending.Status);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(item => item.TenantId == companyB.Id));
    }

    [Fact]
    public async Task IndependentApprover_InTheDestinationCompany_CanDecide_TheParentCannot()
    {
        await using var fixture = new Fixture();
        var companyB = new Domain.Company { Name = "شرکت مقصد", IsActive = true };
        var (companyA, managerA) = await fixture.SeedCompanyManagerAsync(withMembershipApprove: true);
        companyB.CustomerBoundaryId = companyA.CustomerBoundaryId;
        fixture.Database.Add(companyB);
        await fixture.Database.SaveChangesAsync();

        // The approver holds users.membership.approve ONLY in B (an independent, explicit
        // grant — not derived from any group or ownership relationship).
        var approver = await fixture.SeedApproverOfCompanyAsync(companyB);
        var requester = fixture.NewUser("joins.b", active: true);
        fixture.Database.Add(requester);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = requester.Id, TenantId = companyA.Id, Company = companyA,
            RoleId = fixture.MemberRoleId(companyA), Role = null, IsActive = true,
        });
        await fixture.Database.SaveChangesAsync();

        var requested = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(companyB.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);
        var requestId = CreatedRequestId(requested);

        var role = fixture.AddRoleTo(companyB, "CLERK", "منشی");
        await fixture.Database.SaveChangesAsync();

        // The parent's manager — although holding the approval permission in A and the
        // user being "theirs" — cannot decide B's request…
        var parentDenied = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            companyB.Id, requestId, new DefaultHttpContext(), fixture.Principal(managerA.Id),
            new Server.Api.ApproveCompanyMembershipRequest(role.Id),
            new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (parentDenied as IStatusCodeHttpResult)?.StatusCode);

        // …while the destination approver with the independent grant can.
        var approved = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            companyB.Id, requestId, new DefaultHttpContext(), fixture.Principal(approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(role.Id),
            new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (approved as IStatusCodeHttpResult)?.StatusCode);

        var membership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(item => item.UserId == requester.Id && item.TenantId == companyB.Id);
        Assert.True(membership.IsActive);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Approved,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task SelfApproval_StaysForbidden_InsideTheDestinationCompany()
    {
        await using var fixture = new Fixture();
        var (company, approver) = await fixture.SeedCompanyManagerAsync(withMembershipApprove: true);
        var role = fixture.AddRoleTo(company, "CLERK", "منشی");
        await fixture.Database.SaveChangesAsync();

        // The approver requests membership of their own company through the workflow and
        // then tries to approve the request themselves (403, CHECK constraint too).
        var request = new Domain.CompanyMembershipRequest
        {
            UserId = approver.Id,
            DestinationTenantId = company.Id,
            DestinationCompany = company,
            Status = Domain.CompanyMembershipRequestStatuses.Pending,
            RequestedAtUtc = DateTime.UtcNow,
        };
        fixture.Database.Add(request);
        await fixture.Database.SaveChangesAsync();

        var denied = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            company.Id, request.Id, new DefaultHttpContext(), fixture.Principal(approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(role.Id),
            new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var stillPending = await fixture.Database.CompanyMembershipRequests.SingleAsync();
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending, stillPending.Status);
    }

    [Fact]
    public async Task DuplicatePendingRequests_AreRejected_AndNeverBecomeMemberships()
    {
        await using var fixture = new Fixture();
        var destination = new Domain.Company { Name = "شرکت مقصد", IsActive = true };
        fixture.Database.Add(destination);
        var (companyA, managerA) = await fixture.SeedCompanyManagerAsync();
        var requester = fixture.NewUser("applicant", active: true);
        fixture.Database.Add(requester);
        // The requester is an active member of A, inside the shared customer boundary.
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = requester.Id, TenantId = companyA.Id, Company = companyA,
            RoleId = fixture.MemberRoleId(companyA), Role = null, IsActive = true,
        });
        // A shared customer boundary so the destination is discoverable/requestable.
        destination.CustomerBoundaryId = companyA.CustomerBoundaryId;
        await fixture.Database.SaveChangesAsync();

        var first = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(destination.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status201Created, (first as IStatusCodeHttpResult)?.StatusCode);

        var second = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(destination.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (second as IStatusCodeHttpResult)?.StatusCode);

        Assert.Equal(1, await fixture.Database.CompanyMembershipRequests.CountAsync());
        // The pending request has produced no operational membership anywhere.
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(item =>
            item.UserId == requester.Id && item.TenantId == destination.Id));
    }

    [Fact]
    public async Task Rejection_CreatesNoMembership_KeepsHistoryAndAudit_AndAllowsReapply()
    {
        await using var fixture = new Fixture();
        var (destination, approver) = await fixture.SeedCompanyManagerAsync(withMembershipApprove: true);

        // The requester is an active member of a source company inside the same customer
        // boundary, so the real self-service request endpoint applies.
        var source = new Domain.Company { Name = "شرکت مبدأ", IsActive = true, CustomerBoundaryId = destination.CustomerBoundaryId };
        var requester = fixture.NewUser("rejected.user", active: true);
        fixture.Database.AddRange(source, requester);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = requester.Id, TenantId = source.Id, Company = source,
            RoleId = fixture.MemberRoleId(source), Role = null, IsActive = true,
        });
        await fixture.Database.SaveChangesAsync();

        var requested = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(destination.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);
        var requestId = CreatedRequestId(requested);

        var rejected = await Server.Api.CompanyMembershipRequestEndpoints.RejectAsync(
            destination.Id, requestId, new DefaultHttpContext(), fixture.Principal(approver.Id),
            new Server.Api.RejectCompanyMembershipRequest("موقتاً امکان‌پذیر نیست"),
            new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (rejected as IStatusCodeHttpResult)?.StatusCode);

        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(item =>
            item.UserId == requester.Id && item.TenantId == destination.Id));
        var decided = await fixture.Database.CompanyMembershipRequests.SingleAsync();
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Rejected, decided.Status);
        Assert.Equal("موقتاً امکان‌پذیر نیست", decided.DecisionReason);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            item => item.EventType == "CompanyMembershipRejected" && item.EntityId == requestId.ToString());

        // A new request is possible after a rejection; the history row stays untouched.
        var reapply = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(destination.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status201Created, (reapply as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(2, await fixture.Database.CompanyMembershipRequests.CountAsync());
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(item =>
            item.UserId == requester.Id && item.TenantId == destination.Id));
    }

    private static Guid CreatedRequestId(IResult result)
    {
        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var value = result.GetType().GetProperty("Value")?.GetValue(result);
        return Assert.IsType<Server.Api.CompanyMembershipRequestCreatedResponse>(value!).Id;
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
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

        private Domain.Company? Company { get; set; }

        private Domain.User? ManagerUser { get; set; }

        private Domain.CompanyRole? ManagerRole { get; set; }

        public Guid? ManagerRoleId => ManagerRole?.Id;

        /// <summary>A dedicated non-privileged member role of the seeded company (client
        /// id, usable before saving) so requesters never inherit the manager's grants.</summary>
        public Guid MemberRoleId(Domain.Company company)
        {
            var role = AddRoleTo(company, "MEMBER", "عضو");
            return role.Id;
        }

        public Domain.User NewUser(string username, bool active) => new($"{username}@example.test")
        {
            Username = username,
            IsActive = active,
            Password = "private-password",
        };

        public ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("AccountKind", "User"),
        ], "test"));

        public Server.Api.CreateUserRequest NewCreateRequest(string username) => new(
            username, "کاربر", "شرکت", $"{username}@example.test", null,
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true);

        public Guid NewRoleId() => Guid.NewGuid();

        public Domain.CompanyRole AddRoleTo(Domain.Company company, string code, string name)
        {
            var role = new Domain.CompanyRole
            { Company = company, TenantId = company.Id, Code = code, Name = name, IsActive = true };
            Database.Add(role);
            return role;
        }

        public async Task<(Domain.Company Company, Domain.User Manager)> SeedCompanyManagerAsync(
            bool withMembershipApprove = false)
        {
            Company = new Domain.Company
            {
                Name = "شرکت مبدأ " + Guid.NewGuid().ToString("N")[..6],
                IsActive = true,
                CustomerBoundaryId = Guid.NewGuid(),
            };
            ManagerRole = new Domain.CompanyRole
            { Company = Company, TenantId = Company.Id, Code = "MANAGER", Name = "مدیر شرکت", IsActive = true };
            ManagerUser = NewUser("manager", active: true);
            Database.AddRange(Company, ManagerRole, ManagerUser);
            var createPermission = new Domain.PermissionDefinition
            { Code = Server.Api.CompanyUserProvisioningEndpoints.CreateUserPermissionCode, Name = "ایجاد کاربر برای شرکت" };
            Database.Add(createPermission);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            { Role = ManagerRole, RoleId = ManagerRole.Id, Permission = createPermission, PermissionId = createPermission.Id });
            if (withMembershipApprove)
            {
                var approvePermission = new Domain.PermissionDefinition
                { Code = "users.membership.approve", Name = "تأیید درخواست عضویت در شرکت" };
                Database.Add(approvePermission);
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                { Role = ManagerRole, RoleId = ManagerRole.Id, Permission = approvePermission, PermissionId = approvePermission.Id });
            }
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = ManagerUser.Id, TenantId = Company.Id, Company = Company,
                RoleId = ManagerRole.Id, Role = ManagerRole, IsActive = true,
            });
            await Database.SaveChangesAsync();
            return (Company, ManagerUser);
        }

        /// <summary>Seeds one user who holds <c>users.membership.approve</c> in the given
        /// destination company only — an independent, explicit grant. The permission code is
        /// unique, so the definition is shared with any other actor that holds it.</summary>
        public async Task<Domain.User> SeedApproverOfCompanyAsync(Domain.Company company)
        {
            var role = new Domain.CompanyRole
            { Company = company, TenantId = company.Id, Code = "APPROVER", Name = "تأییدکننده", IsActive = true };
            var approver = NewUser("approver", active: true);
            var permission = await GetOrCreatePermissionAsync("users.membership.approve");
            Database.AddRange(role, approver);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            { Role = role, RoleId = role.Id, Permission = permission, PermissionId = permission.Id });
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = approver.Id, TenantId = company.Id, Company = company,
                RoleId = role.Id, Role = role, IsActive = true,
            });
            await Database.SaveChangesAsync();
            return approver;
        }

        private async Task<Domain.PermissionDefinition> GetOrCreatePermissionAsync(string code)
        {
            var permission = Database.PermissionDefinitions.Local
                .FirstOrDefault(candidate => candidate.Code == code)
                ?? await Database.PermissionDefinitions.FirstOrDefaultAsync(candidate => candidate.Code == code);
            if (permission is null)
            {
                permission = new Domain.PermissionDefinition { Code = code, Name = code };
                Database.Add(permission);
                await Database.SaveChangesAsync();
            }
            return permission;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
