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

        companyUsers.MapGet("", GetMyRequestsAsync);
        companyUsers.MapPost("", RequestMembershipAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        var destinationApprovers = api.MapGroup("/admin/companies/{companyId:guid}/membership-requests")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        destinationApprovers.MapGet("/pending", GetPendingRequestsAsync);
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

        var destinationIsActive = await database.Companies.AsNoTracking()
            .AnyAsync(company => company.Id == request.DestinationCompanyId && company.IsActive, cancellationToken);
        if (!destinationIsActive)
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
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        SetNoStore(httpContext);
        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.CompanyMembershipRequests.AsNoTracking()
            .Where(item => item.UserId == userId.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.RequestedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(item => new CompanyMembershipRequestItemResponse(
                item.Id,
                item.DestinationTenantId,
                item.DestinationCompany.Name,
                item.Status,
                item.RequestedAtUtc,
                item.DecidedAtUtc,
                item.DecisionReason,
                item.ApprovedRole == null ? null : item.ApprovedRole.Code,
                item.ApprovedRole == null ? null : item.ApprovedRole.Name))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        return TypedResults.Ok(new CompanyMembershipRequestListResponse(
            rows, safePage, safePageSize, totalCount));
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
                item.RequestedAtUtc,
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

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
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
        var destinationRole = await database.CompanyRoles.FirstOrDefaultAsync(role =>
            role.Id == request.RoleId &&
            role.TenantId == companyId &&
            role.IsActive,
            cancellationToken);

        if (!requesterIsActive || destinationRole is null)
            return TypedResults.Conflict(new { error = "کاربر یا گروه دسترسی مقصد دیگر شرایط تأیید را ندارد." });

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
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(new { error = "درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است." });
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

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
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
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(new { error = "درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

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
