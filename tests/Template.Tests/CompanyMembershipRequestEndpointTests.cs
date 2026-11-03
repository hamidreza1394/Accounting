using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class CompanyMembershipRequestEndpointTests
{
    [Fact]
    public async Task RequestMembership_CreatesPendingRequestWithoutOperationalMembership()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var request = await fixture.Database.CompanyMembershipRequests.SingleAsync();
        Assert.Equal(fixture.Requester.Id, request.UserId);
        Assert.Equal(fixture.Destination.Id, request.DestinationTenantId);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending, request.Status);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == fixture.Requester.Id && membership.TenantId == fixture.Destination.Id));
        Assert.Null(await fixture.Access.FindAccessibleCompanyAsync(
            fixture.Requester.Id, fixture.Destination.Id));
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyMembershipRequested" && audit.UserId == fixture.Requester.Id);
    }

    [Fact]
    public async Task RequestMembership_RejectsDuplicatePendingAndExistingActiveMembership()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Database.CompanyMembershipRequests.Add(fixture.NewRequest());
        await fixture.Database.SaveChangesAsync();

        var duplicate = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (duplicate as IStatusCodeHttpResult)?.StatusCode);

        fixture.Database.CompanyMembershipRequests.RemoveRange(fixture.Database.CompanyMembershipRequests);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = fixture.Requester.Id,
            TenantId = fixture.Destination.Id,
            RoleId = fixture.MemberRole.Id,
            IsActive = true,
        });
        await fixture.Database.SaveChangesAsync();

        var alreadyMember = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (alreadyMember as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task RequestMembership_RequiresActiveAccountAndActiveDestination()
    {
        await using var fixture = await Fixture.CreateAsync();
        var inactiveUser = fixture.AddUser("inactive");
        inactiveUser.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var inactiveRequester = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(inactiveUser.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (inactiveRequester as IStatusCodeHttpResult)?.StatusCode);

        fixture.Destination.IsActive = false;
        await fixture.Database.SaveChangesAsync();
        var inactiveDestination = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (inactiveDestination as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task RequestMembership_RequiresAntiforgery()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new RejectAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Empty(await fixture.Database.CompanyMembershipRequests.ToListAsync());
    }

    [Fact]
    public async Task GetPendingRequests_RequiresDestinationPermissionAndReturnsOnlyMinimalRequesterFields()
    {
        await using var fixture = await Fixture.CreateAsync();
        var own = fixture.NewRequest();
        var other = fixture.NewRequest(fixture.OtherUser.Id);
        fixture.Database.CompanyMembershipRequests.AddRange(own, other);
        await fixture.Database.SaveChangesAsync();

        var allowed = await Server.Api.CompanyMembershipRequestEndpoints.GetPendingRequestsAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            fixture.Database, fixture.Access, page: 1, pageSize: 25, cancellationToken: default);
        var ok = Assert.IsType<Ok<Server.Api.CompanyMembershipPendingListResponse>>(allowed);
        Assert.Equal(2, ok.Value!.TotalCount);
        Assert.Equal(2, ok.Value.Items.Count);
        Assert.All(ok.Value.Items, item => Assert.True(item.IsRequesterActive));
        var fields = typeof(Server.Api.CompanyMembershipPendingItemResponse)
            .GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain(fields, name =>
            name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Phone", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Permission", StringComparison.OrdinalIgnoreCase));

        var denied = await Server.Api.CompanyMembershipRequestEndpoints.GetPendingRequestsAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.OtherUser.Id),
            fixture.Database, fixture.Access, cancellationToken: default);
        Assert.Equal(StatusCodes.Status404NotFound, (denied as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task ApproveMembership_RequiresDestinationPermissionAndCannotSelfApprove()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var unauthorized = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.OtherUser.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status404NotFound, (unauthorized as IStatusCodeHttpResult)?.StatusCode);

        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = fixture.Requester.Id,
            TenantId = fixture.Destination.Id,
            Role = fixture.ApproverRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await fixture.Database.SaveChangesAsync();

        var selfApproval = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status403Forbidden, (selfApproval as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task ApproveMembership_ActivatesMembershipWithDestinationRoleAndWritesAudit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        var membership = await fixture.Database.UserCompanyMemberships.SingleAsync(
            item => item.UserId == fixture.Requester.Id && item.TenantId == fixture.Destination.Id);
        Assert.True(membership.IsActive);
        Assert.Equal(fixture.MemberRole.Id, membership.RoleId);
        var saved = await fixture.Database.CompanyMembershipRequests.SingleAsync();
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Approved, saved.Status);
        Assert.Equal(fixture.MemberRole.Id, saved.ApprovedRoleId);
        Assert.Equal(fixture.Approver.Id, saved.DecidedByUserId);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyMembershipApproved" && audit.UserId == fixture.Approver.Id);
    }

    [Fact]
    public async Task ApproveMembership_AllowsRoleWhosePermissionsAreWithinApproversDelegationCeiling()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = fixture.MemberRole,
            Permission = fixture.DelegatedPermission,
        });
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Approved,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task ApproveMembership_RejectsRoleWithPermissionOutsideApproversDelegationCeilingWithoutMutation()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = fixture.MemberRole,
            Permission = fixture.ExtraPermission,
        });
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == fixture.Requester.Id && membership.TenantId == fixture.Destination.Id));
        Assert.DoesNotContain(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyMembershipApproved");
    }

    [Fact]
    public async Task ApproveMembership_RejectsSystemRoleEvenWhenItsPermissionsAreDelegable()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.MemberRole.IsSystemRole = true;
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = fixture.MemberRole,
            Permission = fixture.DelegatedPermission,
        });
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == fixture.Requester.Id && membership.TenantId == fixture.Destination.Id));
    }

    [Fact]
    public async Task ApproveMembership_DoesNotUsePermissionsGrantedInAnotherCompany()
    {
        await using var fixture = await Fixture.CreateAsync();
        var foreignCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
        var foreignRole = new Domain.CompanyRole
        {
            TenantId = foreignCompany.Id,
            Company = foreignCompany,
            Code = "FOREIGN_APPROVER",
            Name = "تأییدکننده شرکت دیگر",
            IsActive = true,
        };
        fixture.Database.Companies.Add(foreignCompany);
        fixture.Database.CompanyRoles.Add(foreignRole);
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = foreignRole,
            Permission = fixture.ExtraPermission,
        });
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = fixture.Approver.Id,
            TenantId = foreignCompany.Id,
            Role = foreignRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = fixture.MemberRole,
            Permission = fixture.ExtraPermission,
        });
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == fixture.Requester.Id && membership.TenantId == fixture.Destination.Id));
    }

    [Fact]
    public async Task ApproveMembership_RejectsInactiveOrForeignCompanyRole()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        var foreignCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
        var foreignRole = new Domain.CompanyRole
        {
            TenantId = foreignCompany.Id,
            Company = foreignCompany,
            Code = "FOREIGN",
            Name = "نقش شرکت دیگر",
            IsActive = true,
        };
        fixture.Database.Companies.Add(foreignCompany);
        fixture.Database.CompanyRoles.Add(foreignRole);
        await fixture.Database.SaveChangesAsync();

        var foreign = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(foreignRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (foreign as IStatusCodeHttpResult)?.StatusCode);

        fixture.MemberRole.IsActive = false;
        await fixture.Database.SaveChangesAsync();
        var inactive = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status409Conflict, (inactive as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == fixture.Requester.Id && membership.TenantId == fixture.Destination.Id));
    }

    [Fact]
    public async Task ApproveMembership_ReactivatesAnExistingMembershipWithAnInactiveRole()
    {
        await using var fixture = await Fixture.CreateAsync();
        var inactiveRole = new Domain.CompanyRole
        {
            TenantId = fixture.Destination.Id,
            Company = fixture.Destination,
            Code = "INACTIVE",
            Name = "نقش غیرفعال",
            IsActive = false,
        };
        fixture.Database.CompanyRoles.Add(inactiveRole);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = fixture.Requester.Id,
            TenantId = fixture.Destination.Id,
            Role = inactiveRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await fixture.Database.SaveChangesAsync();

        var created = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status201Created, (created as IStatusCodeHttpResult)?.StatusCode);
        var request = await fixture.Database.CompanyMembershipRequests.SingleAsync();

        var approved = await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.ApproveCompanyMembershipRequest(fixture.MemberRole.Id), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (approved as IStatusCodeHttpResult)?.StatusCode);

        var membership = await fixture.Database.UserCompanyMemberships.SingleAsync(
            item => item.UserId == fixture.Requester.Id && item.TenantId == fixture.Destination.Id);
        Assert.True(membership.IsActive);
        Assert.Equal(fixture.MemberRole.Id, membership.RoleId);
    }

    [Fact]
    public async Task RejectMembership_ClosesRequestWithoutCreatingMembership()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.NewRequest();
        fixture.Database.CompanyMembershipRequests.Add(request);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.RejectAsync(
            fixture.Destination.Id, request.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            new Server.Api.RejectCompanyMembershipRequest("عدم نیاز به عضویت"), new AllowAntiforgery(),
            fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        var saved = await fixture.Database.CompanyMembershipRequests.SingleAsync();
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Rejected, saved.Status);
        Assert.Equal("عدم نیاز به عضویت", saved.DecisionReason);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == fixture.Requester.Id && membership.TenantId == fixture.Destination.Id));
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyMembershipRejected");

        var newRequest = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(fixture.Destination.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status201Created, (newRequest as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task GetMyRequests_ReturnsOnlyTheAuthenticatedUsersRequests()
    {
        await using var fixture = await Fixture.CreateAsync();
        var own = fixture.NewRequest();
        var other = fixture.NewRequest(fixture.OtherUser.Id);
        fixture.Database.CompanyMembershipRequests.AddRange(own, other);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.GetMyRequestsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id), fixture.Database,
            page: 1, pageSize: 25, cancellationToken: default);

        var ok = Assert.IsType<Ok<Server.Api.CompanyMembershipRequestListResponse>>(result);
        Assert.Single(ok.Value!.Items);
        Assert.Equal(own.Id, ok.Value.Items[0].Id);
    }

    [Fact]
    public async Task GetDestinationsRequiresExactSearchAndNeverEnumeratesOutsideCustomerBoundary()
    {
        await using var fixture = await Fixture.CreateAsync();
        var foreignCompany = new Domain.Company
        {
            Name = fixture.Destination.Name,
            IsActive = true,
            CustomerBoundaryId = Guid.NewGuid(),
        };
        var pendingCompany = new Domain.Company
        {
            Name = "شرکت در انتظار",
            IsActive = true,
            CustomerBoundaryId = fixture.Destination.CustomerBoundaryId,
        };
        var existingMemberCompany = new Domain.Company
        {
            Name = "شرکت با عضویت فعال",
            IsActive = true,
            CustomerBoundaryId = fixture.Destination.CustomerBoundaryId,
        };
        var existingMemberRole = new Domain.CompanyRole
        {
            TenantId = existingMemberCompany.Id,
            Company = existingMemberCompany,
            Code = "EXISTING",
            Name = "عضو",
            IsActive = true,
        };
        fixture.Database.Companies.AddRange(foreignCompany, pendingCompany, existingMemberCompany);
        fixture.Database.CompanyRoles.Add(existingMemberRole);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = fixture.Requester.Id,
            TenantId = existingMemberCompany.Id,
            Role = existingMemberRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        var pending = fixture.NewRequest();
        pending.DestinationTenantId = pendingCompany.Id;
        pending.DestinationCompany = pendingCompany;
        fixture.Database.CompanyMembershipRequests.Add(pending);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.GetDestinationsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            fixture.Database, fixture.Access, fixture.Destination.Name, page: 1, pageSize: 25);

        var response = Assert.IsType<Ok<Server.Api.MembershipDestinationListResponse>>(result).Value!;
        var item = Assert.Single(response.Items);
        Assert.Equal(fixture.Destination.Id, item.CompanyId);
        Assert.Equal(1, response.TotalCount);

        var pendingSearch = await Server.Api.CompanyMembershipRequestEndpoints.GetDestinationsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            fixture.Database, fixture.Access, pendingCompany.Name);
        Assert.Empty(Assert.IsType<Ok<Server.Api.MembershipDestinationListResponse>>(pendingSearch).Value!.Items);

        var existingMembershipSearch = await Server.Api.CompanyMembershipRequestEndpoints.GetDestinationsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            fixture.Database, fixture.Access, existingMemberCompany.Name);
        Assert.Empty(Assert.IsType<Ok<Server.Api.MembershipDestinationListResponse>>(existingMembershipSearch).Value!.Items);

        var prefixSearch = await Server.Api.CompanyMembershipRequestEndpoints.GetDestinationsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            fixture.Database, fixture.Access, "شرکت");
        Assert.Empty(Assert.IsType<Ok<Server.Api.MembershipDestinationListResponse>>(prefixSearch).Value!.Items);

        var invalidSearch = await Server.Api.CompanyMembershipRequestEndpoints.GetDestinationsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            fixture.Database, fixture.Access, "شـ");
        Assert.Equal(StatusCodes.Status400BadRequest, (invalidSearch as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task RequestMembershipRejectsDirectPostOutsideTheRequestersCustomerBoundary()
    {
        await using var fixture = await Fixture.CreateAsync();
        var foreignCompany = new Domain.Company
        {
            Name = "شرکت خارج از مرز",
            IsActive = true,
            CustomerBoundaryId = Guid.NewGuid(),
        };
        fixture.Database.Companies.Add(foreignCompany);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.RequestMembershipAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            new Server.Api.CreateCompanyMembershipRequestRequest(foreignCompany.Id),
            new AllowAntiforgery(), fixture.Database, fixture.Access, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Empty(await fixture.Database.CompanyMembershipRequests.ToListAsync());
    }

    [Fact]
    public async Task GetAssignableRolesReturnsOnlyActiveNonSystemRolesWithinDelegationCeiling()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = fixture.MemberRole,
            Permission = fixture.DelegatedPermission,
        });
        var restrictedRole = new Domain.CompanyRole
        {
            TenantId = fixture.Destination.Id,
            Company = fixture.Destination,
            Code = "RESTRICTED",
            Name = "بالاتر از سقف",
            IsActive = true,
        };
        var systemRole = new Domain.CompanyRole
        {
            TenantId = fixture.Destination.Id,
            Company = fixture.Destination,
            Code = "SYSTEM",
            Name = "سیستمی",
            IsActive = true,
            IsSystemRole = true,
        };
        fixture.Database.CompanyRoles.AddRange(restrictedRole, systemRole);
        fixture.Database.CompanyRolePermissions.AddRange(
            new Domain.CompanyRolePermission { Role = restrictedRole, Permission = fixture.ExtraPermission },
            new Domain.CompanyRolePermission { Role = systemRole, Permission = fixture.DelegatedPermission });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.GetAssignableRolesAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            fixture.Database, fixture.Access);

        var response = Assert.IsType<Ok<Server.Api.AssignableCompanyRoleListResponse>>(result).Value!;
        var item = Assert.Single(response.Items);
        Assert.Equal(fixture.MemberRole.Id, item.Id);
        Assert.False(item.IsSystemRole);

        var denied = await Server.Api.CompanyMembershipRequestEndpoints.GetAssignableRolesAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.OtherUser.Id),
            fixture.Database, fixture.Access);
        Assert.Equal(StatusCodes.Status404NotFound, (denied as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task GetApproversReturnsOnlyActiveApproversInTheRequestedCompany()
    {
        await using var fixture = await Fixture.CreateAsync();
        var secondApprover = fixture.AddUser("second-approver");
        var secondRole = new Domain.CompanyRole
        {
            TenantId = fixture.Destination.Id,
            Company = fixture.Destination,
            Code = "SECOND_APPROVER",
            Name = "تأییدکننده دوم",
            IsActive = true,
        };
        fixture.Database.CompanyRoles.Add(secondRole);
        var approvalPermission = await fixture.Database.PermissionDefinitions.SingleAsync(
            item => item.Code == Server.Api.CompanyMembershipRequestEndpoints.ApprovalPermissionCode);
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = secondRole,
            Permission = approvalPermission,
        });
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = secondApprover.Id,
            TenantId = fixture.Destination.Id,
            Role = secondRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        var foreignCompany = new Domain.Company
        {
            Name = "شرکت دیگر",
            IsActive = true,
            CustomerBoundaryId = fixture.Destination.CustomerBoundaryId,
        };
        var foreignRole = new Domain.CompanyRole
        {
            TenantId = foreignCompany.Id,
            Company = foreignCompany,
            Code = "FOREIGN_APPROVER",
            Name = "تأییدکننده بیرونی",
            IsActive = true,
        };
        fixture.Database.Companies.Add(foreignCompany);
        fixture.Database.CompanyRoles.Add(foreignRole);
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = foreignRole,
            Permission = approvalPermission,
        });
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = fixture.OtherUser.Id,
            TenantId = foreignCompany.Id,
            Role = foreignRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.GetApproversAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            fixture.Database, fixture.Access);

        var response = Assert.IsType<Ok<Server.Api.MembershipApproverListResponse>>(result).Value!;
        Assert.Equal(2, response.TotalCount);
        Assert.Contains(response.Items, item => item.UserId == fixture.Approver.Id);
        Assert.Contains(response.Items, item => item.UserId == secondApprover.Id);
        Assert.DoesNotContain(response.Items, item => item.UserId == fixture.OtherUser.Id);
    }

    [Fact]
    public async Task RequestHistoryFiltersStatusAndSerializesUtcTimesExplicitly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var approvedAt = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Unspecified);
        var approved = fixture.NewRequest();
        approved.Status = Domain.CompanyMembershipRequestStatuses.Approved;
        approved.DecidedAtUtc = approvedAt;
        approved.DecidedByUserId = fixture.Approver.Id;
        approved.ApprovedRoleId = fixture.MemberRole.Id;
        approved.ApprovedRole = fixture.MemberRole;
        var rejected = fixture.NewRequest();
        rejected.Status = Domain.CompanyMembershipRequestStatuses.Rejected;
        rejected.DecidedAtUtc = approvedAt.AddMinutes(1);
        rejected.DecidedByUserId = fixture.Approver.Id;
        rejected.DecisionReason = "رد آزمون";
        fixture.Database.CompanyMembershipRequests.AddRange(approved, rejected);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyMembershipRequestEndpoints.GetRequestHistoryAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            fixture.Database, fixture.Access, status: "Approved");

        var response = Assert.IsType<Ok<Server.Api.CompanyMembershipRequestHistoryListResponse>>(result).Value!;
        var item = Assert.Single(response.Items);
        Assert.Equal(approved.Id, item.Id);
        Assert.Equal(DateTimeKind.Utc, item.RequestedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, item.DecidedAtUtc!.Value.Kind);
        Assert.Equal("approver", item.DecidedByDisplayName);

        var ownHistory = await Server.Api.CompanyMembershipRequestEndpoints.GetMyRequestsAsync(
            new DefaultHttpContext(), fixture.Principal(fixture.Requester.Id),
            fixture.Database, status: "Rejected");
        var ownResponse = Assert.IsType<Ok<Server.Api.CompanyMembershipRequestListResponse>>(ownHistory).Value!;
        Assert.Equal(1, ownResponse.TotalCount);
        Assert.All(ownResponse.Items, request => Assert.Equal(
            Domain.CompanyMembershipRequestStatuses.Rejected, request.Status));

        var invalid = await Server.Api.CompanyMembershipRequestEndpoints.GetRequestHistoryAsync(
            fixture.Destination.Id, new DefaultHttpContext(), fixture.Principal(fixture.Approver.Id),
            fixture.Database, fixture.Access, status: "Unknown");
        Assert.Equal(StatusCodes.Status400BadRequest, (invalid as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(Data.DatabaseContext database)
        {
            Database = database;
            Access = new Infrastructure.Tenancy.CompanyAccessService(database);
        }

        public Data.DatabaseContext Database { get; }
        public Infrastructure.Tenancy.CompanyAccessService Access { get; }
        public Domain.Company Destination { get; private set; } = null!;
        public Domain.Company Source { get; private set; } = null!;
        public Domain.User Requester { get; private set; } = null!;
        public Domain.User Approver { get; private set; } = null!;
        public Domain.User OtherUser { get; private set; } = null!;
        public Domain.CompanyRole ApproverRole { get; private set; } = null!;
        public Domain.CompanyRole MemberRole { get; private set; } = null!;
        public Domain.PermissionDefinition DelegatedPermission { get; private set; } = null!;
        public Domain.PermissionDefinition ExtraPermission { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var customerBoundaryId = Guid.NewGuid();
            var fixture = new Fixture(database)
            {
                Destination = new Domain.Company
                {
                    Name = "شرکت مقصد", IsActive = true, CustomerBoundaryId = customerBoundaryId,
                },
                Source = new Domain.Company
                {
                    Name = "شرکت مبدأ", IsActive = true, CustomerBoundaryId = customerBoundaryId,
                },
                Requester = new Domain.User("requester@example.test") { Username = "requester", IsActive = true },
                Approver = new Domain.User("approver@example.test") { Username = "approver", IsActive = true },
                OtherUser = new Domain.User("other@example.test") { Username = "other", IsActive = true },
            };
            fixture.ApproverRole = new Domain.CompanyRole
            {
                TenantId = fixture.Destination.Id, Company = fixture.Destination, Code = "APPROVER", Name = "تأییدکننده", IsActive = true,
            };
            fixture.MemberRole = new Domain.CompanyRole
            {
                TenantId = fixture.Destination.Id, Company = fixture.Destination, Code = "MEMBER", Name = "عضو", IsActive = true,
            };
            var sourceRole = new Domain.CompanyRole
            {
                TenantId = fixture.Source.Id, Company = fixture.Source, Code = "SOURCE_MEMBER", Name = "عضو مبدأ", IsActive = true,
            };
            var permission = new Domain.PermissionDefinition
            {
                Code = Server.Api.CompanyMembershipRequestEndpoints.ApprovalPermissionCode,
                Name = "تأیید عضویت",
            };
            fixture.DelegatedPermission = new Domain.PermissionDefinition
            {
                Code = "users.list",
                Name = "فهرست کاربران",
            };
            fixture.ExtraPermission = new Domain.PermissionDefinition
            {
                Code = "users.update",
                Name = "ویرایش کاربران",
            };
            database.Companies.AddRange(fixture.Destination, fixture.Source);
            database.Users.AddRange(fixture.Requester, fixture.Approver, fixture.OtherUser);
            database.CompanyRoles.AddRange(fixture.ApproverRole, fixture.MemberRole, sourceRole);
            database.PermissionDefinitions.AddRange(permission, fixture.DelegatedPermission, fixture.ExtraPermission);
            database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = fixture.Requester.Id,
                TenantId = fixture.Source.Id,
                RoleId = sourceRole.Id,
                Role = sourceRole,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
            database.CompanyRolePermissions.AddRange(
                new Domain.CompanyRolePermission
                {
                    Role = fixture.ApproverRole,
                    Permission = permission,
                },
                new Domain.CompanyRolePermission
                {
                    Role = fixture.ApproverRole,
                    Permission = fixture.DelegatedPermission,
                });
            database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = fixture.Approver.Id, TenantId = fixture.Destination.Id, Role = fixture.ApproverRole,
                IsActive = true, CreatedAtUtc = DateTime.UtcNow,
            });
            await database.SaveChangesAsync();
            return fixture;
        }

        public Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}@example.test") { Username = username, IsActive = true };
            Database.Users.Add(user);
            return user;
        }

        public Domain.CompanyMembershipRequest NewRequest(Guid? userId = null) => new()
        {
            UserId = userId ?? Requester.Id,
            DestinationTenantId = Destination.Id,
            DestinationCompany = Destination,
            Status = Domain.CompanyMembershipRequestStatuses.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8],
        };

        public ClaimsPrincipal Principal(Guid userId) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                    Infrastructure.Tenancy.CompanyAccessService.UserAccountKind),
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

    private sealed class RejectAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(false);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
