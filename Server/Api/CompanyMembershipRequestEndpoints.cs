using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Owns the pending-to-approved membership workflow. Pending requests are deliberately
/// separate from UserCompanyMembership, which is the only operational membership source.
/// </summary>
public static class CompanyMembershipRequestEndpoints
{
    public const string ApprovalPermissionCode = "users.membership.approve";

    public static IEndpointRouteBuilder MapCompanyMembershipRequestEndpoints(this IEndpointRouteBuilder api)
    {
        var companyUsers = api.MapGroup("/me/membership-requests")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        companyUsers.MapGet("/destinations", GetDestinationsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        companyUsers.MapGet("", GetMyRequestsAsync);
        companyUsers.MapPost("", RequestMembershipAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        var destinationApprovers = api.MapGroup("/admin/companies/{companyId:guid}/membership-requests")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        destinationApprovers.MapGet("/pending", GetPendingRequestsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        destinationApprovers.MapGet("/assignable-roles", GetAssignableRolesAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        destinationApprovers.MapGet("/approvers", GetApproversAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        destinationApprovers.MapGet("", GetRequestHistoryAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        destinationApprovers.MapPost("/{requestId:guid}/approve", ApproveAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        destinationApprovers.MapPost("/{requestId:guid}/reject", RejectAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        return api;
    }

    public static async Task<IResult> RequestMembershipAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] CreateCompanyMembershipRequestRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();
        SetNoStore(httpContext);
        if (request is null || request.DestinationCompanyId == Guid.Empty)
            return TypedResults.BadRequest(new { error = "شرکت مقصد معتبر لازم است." });
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        var requesterIsActive = await database.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId.Value && user.IsActive, cancellationToken);
        if (!requesterIsActive)
            return TypedResults.NotFound();

        // CustomerBoundary is a fail-closed isolation boundary, not a permission. A requester
        // must already have an active membership in at least one provisioned boundary; a
        // caller cannot bypass the discovery endpoint by posting an arbitrary company ID.
        var accessibleCompanies = await companyAccess.GetAccessibleCompaniesAsync(
            userId.Value, cancellationToken);
        var accessibleCompanyIds = accessibleCompanies
            .Select(company => company.CompanyId)
            .ToArray();
        var requesterBoundaries = database.Companies.AsNoTracking()
            .Where(company => accessibleCompanyIds.Contains(company.Id))
            .Select(company => company.CustomerBoundaryId)
            .Where(boundaryId => boundaryId.HasValue);

        var destinationIsAllowed = await database.Companies.AsNoTracking()
            .AnyAsync(company =>
                company.Id == request.DestinationCompanyId &&
                company.IsActive &&
                company.CustomerBoundaryId.HasValue &&
                requesterBoundaries.Contains(company.CustomerBoundaryId),
                cancellationToken);
        if (!destinationIsAllowed)
            return TypedResults.NotFound();

        var alreadyActive = await database.UserCompanyMemberships.AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId.Value &&
                membership.TenantId == request.DestinationCompanyId &&
                membership.IsActive &&
                membership.Role.IsActive, cancellationToken);
        if (alreadyActive)
            return TypedResults.Conflict(new { error = "کاربر در شرکت مقصد عضویت فعال دارد." });

        var pendingExists = await database.CompanyMembershipRequests.AsNoTracking()
            .AnyAsync(item =>
                item.UserId == userId.Value &&
                item.DestinationTenantId == request.DestinationCompanyId &&
                item.Status == Domain.CompanyMembershipRequestStatuses.Pending,
                cancellationToken);
        if (pendingExists)
            return TypedResults.Conflict(new { error = "برای این شرکت یک درخواست عضویت در انتظار تعیین تکلیف است." });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var membershipRequest = new Domain.CompanyMembershipRequest
        {
            UserId = userId.Value,
            DestinationTenantId = request.DestinationCompanyId,
            Status = Domain.CompanyMembershipRequestStatuses.Pending,
            RequestedAtUtc = now,
        };
        database.CompanyMembershipRequests.Add(membershipRequest);
        database.SecurityAuditEvents.Add(CreateAudit(
            userId.Value,
            request.DestinationCompanyId,
            "CompanyMembershipRequested",
            membershipRequest.Id,
            now,
            httpContext,
            new
            {
                membershipRequest.DestinationTenantId,
                membershipRequest.Status,
            }));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // The filtered unique index is the final guard against simultaneous duplicate requests.
            return TypedResults.Conflict(new { error = "برای این شرکت یک درخواست عضویت در انتظار تعیین تکلیف است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Json(
            new CompanyMembershipRequestCreatedResponse(membershipRequest.Id, membershipRequest.Status, now),
            statusCode: StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetMyRequestsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        string? status = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        if (!TryNormalizeStatus(status, out var normalizedStatus))
            return TypedResults.BadRequest(new { error = "وضعیت درخواست معتبر نیست." });

        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.CompanyMembershipRequests.AsNoTracking()
            .Where(item => item.UserId == userId.Value);
        if (normalizedStatus is not null)
            query = query.Where(item => item.Status == normalizedStatus);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.RequestedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(item => new
            {
                item.Id,
                item.DestinationTenantId,
                DestinationCompanyName = item.DestinationCompany.Name,
                item.Status,
                item.RequestedAtUtc,
                item.DecidedAtUtc,
                item.DecisionReason,
                ApprovedRoleCode = item.ApprovedRole == null ? null : item.ApprovedRole.Code,
                ApprovedRoleName = item.ApprovedRole == null ? null : item.ApprovedRole.Name,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(item => new CompanyMembershipRequestItemResponse(
            item.Id,
            item.DestinationTenantId,
            item.DestinationCompanyName,
            item.Status,
            AsUtc(item.RequestedAtUtc),
            AsUtc(item.DecidedAtUtc),
            item.DecisionReason,
            item.ApprovedRoleCode,
            item.ApprovedRoleName)).ToArray();

        return TypedResults.Ok(new CompanyMembershipRequestListResponse(
            items, safePage, safePageSize, totalCount));
    }

    /// <summary>
    /// Exact-name discovery inside the requester's already-provisioned customer boundary.
    /// Requiring a non-empty exact term prevents bulk enumeration of company names.
    /// </summary>
    public static async Task<IResult> GetDestinationsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        string? search,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        var exactName = search?.Trim();
        if (string.IsNullOrWhiteSpace(exactName) || exactName.Length is < 3 or > 120)
            return TypedResults.BadRequest(new { error = "برای جستجوی شرکت، نام دقیق با حداقل ۳ و حداکثر ۱۲۰ نویسه لازم است." });

        var requesterIsActive = await database.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId.Value && user.IsActive, cancellationToken);
        if (!requesterIsActive)
            return TypedResults.NotFound();

        var accessibleCompanies = await companyAccess.GetAccessibleCompaniesAsync(
            userId.Value, cancellationToken);
        var accessibleCompanyIds = accessibleCompanies
            .Select(company => company.CompanyId)
            .ToArray();
        var requesterBoundaries = database.Companies.AsNoTracking()
            .Where(company => accessibleCompanyIds.Contains(company.Id))
            .Select(company => company.CustomerBoundaryId)
            .Where(boundaryId => boundaryId.HasValue);

        var query = database.Companies.AsNoTracking()
            .Where(company =>
                company.IsActive &&
                company.CustomerBoundaryId.HasValue &&
                requesterBoundaries.Contains(company.CustomerBoundaryId))
            .Where(company => company.Name == exactName)
            .Where(company => !database.UserCompanyMemberships.Any(membership =>
                membership.UserId == userId.Value &&
                membership.TenantId == company.Id &&
                membership.IsActive &&
                membership.Role.IsActive))
            .Where(company => !database.CompanyMembershipRequests.Any(request =>
                request.UserId == userId.Value &&
                request.DestinationTenantId == company.Id &&
                request.Status == Domain.CompanyMembershipRequestStatuses.Pending));

        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(company => company.Name)
            .ThenBy(company => company.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(company => new MembershipDestinationResponse(company.Id, company.Name))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new MembershipDestinationListResponse(
            items, safePage, safePageSize, totalCount));
    }

    public static async Task<IResult> GetAssignableRolesAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        if (!await companyAccess.HasPermissionAsync(
                userId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        var approverPermissionIds = await database.UserCompanyMemberships.AsNoTracking()
            .Where(membership =>
                membership.UserId == userId.Value &&
                membership.TenantId == companyId &&
                membership.IsActive &&
                membership.Company.IsActive &&
                membership.Role.TenantId == companyId &&
                membership.Role.IsActive)
            .SelectMany(membership => membership.Role.RolePermissions.Select(rolePermission => rolePermission.PermissionId))
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = database.CompanyRoles.AsNoTracking()
            .Where(role => role.TenantId == companyId && role.IsActive && !role.IsSystemRole)
            .Where(role => !role.RolePermissions.Any(rolePermission =>
                !approverPermissionIds.Contains(rolePermission.PermissionId)));

        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(role => role.Name)
            .ThenBy(role => role.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(role => new AssignableCompanyRoleResponse(role.Id, role.Code, role.Name, role.IsSystemRole))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new AssignableCompanyRoleListResponse(
            items, safePage, safePageSize, totalCount));
    }

    public static async Task<IResult> GetApproversAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        if (!await companyAccess.HasPermissionAsync(
                userId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        var query =
            from membership in database.UserCompanyMemberships.AsNoTracking()
            join user in database.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.TenantId == companyId &&
                membership.IsActive &&
                membership.Company.IsActive &&
                membership.Role.TenantId == companyId &&
                membership.Role.IsActive &&
                membership.Role.RolePermissions.Any(rolePermission =>
                    rolePermission.Permission.Code == ApprovalPermissionCode) &&
                user.IsActive
            select new
            {
                UserId = user.Id,
                user.Username,
                user.FirstName,
                user.LastName,
            };

        var distinctQuery = query.Distinct();
        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await distinctQuery.CountAsync(cancellationToken);
        var rows = await distinctQuery
            .OrderBy(item => item.LastName)
            .ThenBy(item => item.FirstName)
            .ThenBy(item => item.UserId)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item =>
        {
            var displayName = $"{item.FirstName} {item.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = item.Username ?? "کاربر";
            return new MembershipApproverResponse(item.UserId, displayName, item.Username);
        }).ToArray();

        return TypedResults.Ok(new MembershipApproverListResponse(
            items, safePage, safePageSize, totalCount));
    }

    public static async Task<IResult> GetRequestHistoryAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        string? status = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        if (!await companyAccess.HasPermissionAsync(
                userId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();
        if (!TryNormalizeStatus(status, out var normalizedStatus))
            return TypedResults.BadRequest(new { error = "وضعیت درخواست معتبر نیست." });

        var query = database.CompanyMembershipRequests.AsNoTracking()
            .Where(item => item.DestinationTenantId == companyId);
        if (normalizedStatus is not null)
            query = query.Where(item => item.Status == normalizedStatus);

        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await (
            from item in query
            join requester in database.Users.AsNoTracking()
                on item.UserId equals requester.Id into requesterRows
            from requester in requesterRows.DefaultIfEmpty()
            join decider in database.Users.AsNoTracking()
                on item.DecidedByUserId equals decider.Id into deciderRows
            from decider in deciderRows.DefaultIfEmpty()
            orderby item.RequestedAtUtc descending, item.Id descending
            select new
            {
                item.Id,
                item.Status,
                item.RequestedAtUtc,
                item.DecidedAtUtc,
                item.DecisionReason,
                RequesterUsername = requester == null ? null : requester.Username,
                RequesterFirstName = requester == null ? null : requester.FirstName,
                RequesterLastName = requester == null ? null : requester.LastName,
                DeciderFirstName = decider == null ? null : decider.FirstName,
                DeciderLastName = decider == null ? null : decider.LastName,
                DeciderUsername = decider == null ? null : decider.Username,
                ApprovedRoleName = item.ApprovedRole == null ? null : item.ApprovedRole.Name,
            })
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item =>
        {
            var requesterDisplayName = $"{item.RequesterFirstName} {item.RequesterLastName}".Trim();
            if (string.IsNullOrWhiteSpace(requesterDisplayName))
                requesterDisplayName = item.RequesterUsername ?? "کاربر ناموجود";
            var deciderDisplayName = $"{item.DeciderFirstName} {item.DeciderLastName}".Trim();
            if (string.IsNullOrWhiteSpace(deciderDisplayName))
                deciderDisplayName = item.DeciderUsername;
            return new CompanyMembershipRequestHistoryItemResponse(
                item.Id,
                requesterDisplayName,
                item.RequesterUsername,
                item.Status,
                AsUtc(item.RequestedAtUtc),
                AsUtc(item.DecidedAtUtc),
                string.IsNullOrWhiteSpace(deciderDisplayName) ? null : deciderDisplayName,
                item.ApprovedRoleName,
                item.DecisionReason);
        }).ToArray();

        return TypedResults.Ok(new CompanyMembershipRequestHistoryListResponse(
            items, safePage, safePageSize, totalCount));
    }

    public static async Task<IResult> GetPendingRequestsAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        if (!await companyAccess.HasPermissionAsync(
                userId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.CompanyMembershipRequests.AsNoTracking()
            .Where(item =>
                item.DestinationTenantId == companyId &&
                item.Status == Domain.CompanyMembershipRequestStatuses.Pending);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await (
            from item in query
            join user in database.Users.AsNoTracking()
                on item.UserId equals user.Id into userRows
            from user in userRows.DefaultIfEmpty()
            select new
            {
                item.Id,
                item.UserId,
                item.RequestedAtUtc,
                Username = user == null ? null : user.Username,
                FirstName = user == null ? null : user.FirstName,
                LastName = user == null ? null : user.LastName,
                IsRequesterActive = user != null && user.IsActive,
            })
            .OrderBy(item => item.RequestedAtUtc)
            .ThenBy(item => item.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item =>
        {
            var displayName = $"{item.FirstName} {item.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = item.Username ?? item.UserId.ToString("N");
            return new CompanyMembershipPendingItemResponse(
                item.Id,
                item.UserId,
                displayName,
                item.Username,
                AsUtc(item.RequestedAtUtc),
                item.IsRequesterActive);
        }).ToArray();

        SetNoStore(httpContext);
        return TypedResults.Ok(new CompanyMembershipPendingListResponse(
            items, safePage, safePageSize, totalCount));
    }

    public static async Task<IResult> ApproveAsync(
        Guid companyId,
        Guid requestId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] ApproveCompanyMembershipRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var approverId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (approverId is null)
            return TypedResults.Unauthorized();
        SetNoStore(httpContext);
        if (request is null || request.RoleId == Guid.Empty)
            return TypedResults.BadRequest(new { error = "گروه دسترسی مقصد معتبر نیست." });
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await companyAccess.HasPermissionAsync(
                approverId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        if (!await companyAccess.HasPermissionAsync(
                approverId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        var membershipRequest = await database.CompanyMembershipRequests
            .FirstOrDefaultAsync(item =>
                item.Id == requestId &&
                item.DestinationTenantId == companyId &&
                item.Status == Domain.CompanyMembershipRequestStatuses.Pending,
                cancellationToken);
        if (membershipRequest is null)
            return TypedResults.NotFound();
        if (membershipRequest.UserId == approverId.Value)
            return TypedResults.Json(
                new { error = "درخواست‌کننده نمی‌تواند درخواست عضویت خودش را تأیید کند." },
                statusCode: StatusCodes.Status403Forbidden);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var requesterIsActive = await database.Users.AsNoTracking()
            .AnyAsync(user => user.Id == membershipRequest.UserId && user.IsActive, cancellationToken);
        var destinationRole = await database.CompanyRoles
            .Include(role => role.RolePermissions)
            .FirstOrDefaultAsync(role =>
                role.Id == request.RoleId &&
                role.TenantId == companyId &&
                role.IsActive,
                cancellationToken);

        if (!requesterIsActive || destinationRole is null)
            return TypedResults.Conflict(new { error = "کاربر یا گروه دسترسی مقصد دیگر شرایط تأیید را ندارد." });

        // A company approver may delegate only permissions they currently hold in this
        // destination company. System roles are installation/domain templates and cannot
        // be assigned through the inter-company approval workflow.
        // Track the exact membership and role rows that establish the approver's delegation
        // ceiling. RowVersion guards below make a concurrent revocation/change fail closed
        // instead of approving from a stale permission snapshot.
        var approverMemberships = await database.UserCompanyMemberships
            .Include(membership => membership.Role)
                .ThenInclude(role => role.RolePermissions)
            .Where(membership =>
                membership.UserId == approverId.Value &&
                membership.TenantId == companyId &&
                membership.IsActive &&
                membership.Role.TenantId == companyId &&
                membership.Role.IsActive)
            .ToListAsync(cancellationToken);

        var approverPermissionIds = approverMemberships
            .SelectMany(membership => membership.Role.RolePermissions)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToHashSet();
        var destinationPermissionIds = destinationRole.RolePermissions
            .Select(rolePermission => rolePermission.PermissionId)
            .ToArray();

        if (destinationRole.IsSystemRole ||
            destinationPermissionIds.Any(permissionId => !approverPermissionIds.Contains(permissionId)))
        {
            return TypedResults.Json(
                new { error = "شما مجاز به واگذاری این گروه دسترسی نیستید." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        // Force no-op updates on every row used to calculate the delegation ceiling. The
        // membership RowVersion protects active membership/role assignment; the role RowVersion
        // protects its active flag and permission links (UpdateRoleAsync updates the role row
        // in the same transaction as those links). The destination role is guarded as well.
        foreach (var membership in approverMemberships)
        {
            database.Entry(membership).Property(current => current.RoleId).IsModified = true;
            database.Entry(membership.Role).Property(current => current.IsActive).IsModified = true;
        }
        database.Entry(destinationRole).Property(current => current.IsActive).IsModified = true;

        var existingMembership = await database.UserCompanyMemberships
            .Include(membership => membership.Role)
            .FirstOrDefaultAsync(membership =>
                membership.UserId == membershipRequest.UserId &&
                membership.TenantId == companyId,
                cancellationToken);
        if (existingMembership is { IsActive: true } && existingMembership.Role.IsActive)
            return TypedResults.Conflict(new { error = "کاربر در شرکت مقصد از قبل عضویت فعال دارد." });

        if (existingMembership is null)
        {
            database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = membershipRequest.UserId,
                TenantId = companyId,
                RoleId = destinationRole.Id,
                Role = destinationRole,
                IsActive = true,
                CreatedAtUtc = now,
            });
        }
        else
        {
            existingMembership.RoleId = destinationRole.Id;
            existingMembership.Role = destinationRole;
            existingMembership.IsActive = true;
        }

        membershipRequest.Status = Domain.CompanyMembershipRequestStatuses.Approved;
        membershipRequest.ApprovedRoleId = destinationRole.Id;
        membershipRequest.DecidedByUserId = approverId.Value;
        membershipRequest.DecidedAtUtc = now;
        database.SecurityAuditEvents.Add(CreateAudit(
            approverId.Value, companyId, "CompanyMembershipApproved",
            membershipRequest.Id, now, httpContext,
            new { membershipRequest.UserId, membershipRequest.DestinationTenantId, RoleId = destinationRole.Id }));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(new { error = "درخواست، مجوزها یا عضویت تأییدکننده هم‌زمان تغییر کرده‌اند؛ اطلاعات را تازه‌سازی کنید." });
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return TypedResults.Conflict(new { error = "عضویت کاربر در شرکت مقصد هم‌زمان تغییر کرده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    public static async Task<IResult> RejectAsync(
        Guid companyId,
        Guid requestId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] RejectCompanyMembershipRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var approverId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (approverId is null)
            return TypedResults.Unauthorized();
        SetNoStore(httpContext);
        if (request?.Reason is { Length: > 500 })
            return TypedResults.BadRequest(new { error = "توضیح رد درخواست بیش از حد مجاز است." });
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await companyAccess.HasPermissionAsync(
                approverId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        if (!await companyAccess.HasPermissionAsync(
                approverId.Value, companyId, ApprovalPermissionCode, cancellationToken))
            return TypedResults.NotFound();

        var membershipRequest = await database.CompanyMembershipRequests.FirstOrDefaultAsync(item =>
            item.Id == requestId &&
            item.DestinationTenantId == companyId &&
            item.Status == Domain.CompanyMembershipRequestStatuses.Pending,
            cancellationToken);
        if (membershipRequest is null)
            return TypedResults.NotFound();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var suppliedReason = request?.Reason;
        var reason = string.IsNullOrWhiteSpace(suppliedReason) ? null : suppliedReason.Trim();
        membershipRequest.Status = Domain.CompanyMembershipRequestStatuses.Rejected;
        membershipRequest.DecidedByUserId = approverId.Value;
        membershipRequest.DecidedAtUtc = now;
        membershipRequest.DecisionReason = reason;
        database.SecurityAuditEvents.Add(CreateAudit(
            approverId.Value, companyId, "CompanyMembershipRejected",
            membershipRequest.Id, now, httpContext,
            new { membershipRequest.UserId, membershipRequest.DestinationTenantId, ReasonProvided = reason is not null }));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(new { error = "درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    private static bool TryNormalizeStatus(string? status, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(status))
            return true;

        normalized = status.Trim() switch
        {
            Domain.CompanyMembershipRequestStatuses.Pending => Domain.CompanyMembershipRequestStatuses.Pending,
            Domain.CompanyMembershipRequestStatuses.Approved => Domain.CompanyMembershipRequestStatuses.Approved,
            Domain.CompanyMembershipRequestStatuses.Rejected => Domain.CompanyMembershipRequestStatuses.Rejected,
            _ => null,
        };
        return normalized is not null;
    }

    private static DateTime AsUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? AsUtc(value.Value) : null;

    private static Domain.SecurityAuditEvent CreateAudit(
        Guid actorId,
        Guid tenantId,
        string eventType,
        Guid requestId,
        DateTime occurredAtUtc,
        HttpContext httpContext,
        object details) => new()
    {
        UserId = actorId,
        TenantId = tenantId,
        EventType = eventType,
        EntityType = "CompanyMembershipRequest",
        EntityId = requestId.ToString(),
        OccurredAtUtc = occurredAtUtc,
        CorrelationId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier,
        IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
        DetailsJson = JsonSerializer.Serialize(details),
    };

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is SqlException sqlException && sqlException.Number is 2601 or 2627)
                return true;
        }
        return false;
    }

    private static void SetNoStore(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
    }
}

public sealed record MembershipDestinationResponse(Guid CompanyId, string Name);
public sealed record MembershipDestinationListResponse(
    IReadOnlyList<MembershipDestinationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
public sealed record AssignableCompanyRoleResponse(Guid Id, string Code, string Name, bool IsSystemRole);
public sealed record AssignableCompanyRoleListResponse(
    IReadOnlyList<AssignableCompanyRoleResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
public sealed record MembershipApproverResponse(Guid UserId, string DisplayName, string? Username);
public sealed record MembershipApproverListResponse(
    IReadOnlyList<MembershipApproverResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
public sealed record CompanyMembershipRequestHistoryItemResponse(
    Guid Id,
    string RequesterDisplayName,
    string? Username,
    string Status,
    DateTime RequestedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecidedByDisplayName,
    string? ApprovedRoleName,
    string? DecisionReason);
public sealed record CompanyMembershipRequestHistoryListResponse(
    IReadOnlyList<CompanyMembershipRequestHistoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
public sealed record CreateCompanyMembershipRequestRequest(Guid DestinationCompanyId);
public sealed record ApproveCompanyMembershipRequest(Guid RoleId);
public sealed record RejectCompanyMembershipRequest(string? Reason);
public sealed record CompanyMembershipRequestCreatedResponse(Guid Id, string Status, DateTime RequestedAtUtc);
public sealed record CompanyMembershipRequestItemResponse(
    Guid Id,
    Guid DestinationCompanyId,
    string DestinationCompanyName,
    string Status,
    DateTime RequestedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecisionReason,
    string? ApprovedRoleCode,
    string? ApprovedRoleName);
public sealed record CompanyMembershipRequestListResponse(
    IReadOnlyList<CompanyMembershipRequestItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
public sealed record CompanyMembershipPendingItemResponse(
    Guid Id,
    Guid UserId,
    string RequesterDisplayName,
    string? Username,
    DateTime RequestedAtUtc,
    bool IsRequesterActive);
public sealed record CompanyMembershipPendingListResponse(
    IReadOnlyList<CompanyMembershipPendingItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
