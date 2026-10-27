using System.Diagnostics;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Two-person review for customer and installation status changes.
/// A request is only a proposal; the persisted resource is changed inside the review transaction.
/// </summary>
public static class VendorApprovalEndpoints
{
    public static IEndpointRouteBuilder MapVendorApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var approvals = endpoints.MapGroup("/api/vendor/approvals");

        approvals.MapGet("/", async (
            int? page, int? pageSize, string? status, VendorDbContext db, CancellationToken ct) =>
        {
            if (status is not null && status is not ("Pending" or "Approved" or "Rejected" or "Stale"))
                return Results.BadRequest(new { error = "invalid_approval_status" });

            var paging = VendorPageRequest.Normalize(page, pageSize);
            IQueryable<VendorApprovalRequest> query = db.ApprovalRequests.AsNoTracking();
            if (status is not null) query = query.Where(item => item.Status == status);
            query = query.OrderByDescending(item => item.RequestedAtUtc).ThenBy(item => item.Id);
            var totalCount = await query.CountAsync(ct);
            var items = await query.Skip(paging.Skip).Take(paging.PageSize)
                .Select(item => new VendorApprovalResponse(
                    item.Id, item.ResourceType, item.ResourceId, item.TargetStatus,
                    item.RequestedByUserId, item.RequestedAtUtc, item.Status,
                    item.ReviewedByUserId, item.ReviewedAtUtc, item.ReviewComment,
                    Convert.ToBase64String(item.RowVersion)))
                .ToListAsync(ct);
            return Results.Ok(new VendorPageResponse<VendorApprovalResponse>(
                items, paging.Page, paging.PageSize, totalCount));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsRead));

        approvals.MapPost("/status-change", async (
            CreateVendorApprovalRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db,
            UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (request.ResourceId == Guid.Empty ||
                request.ResourceType is not ("customer" or "installation") ||
                !VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedResourceVersion))
                return Results.BadRequest(new { error = "invalid_approval_request" });

            var managePermission = PermissionFor(request.ResourceType);
            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor(request.ResourceType, request.ResourceId),
                VendorResourceCatalog.PolicyFor(request.ResourceType, managePermission));
            if (!access.Succeeded) return Results.Forbid();

            string? currentStatus;
            byte[] currentVersion;
            if (request.ResourceType == "customer")
            {
                var resource = await db.Customers.AsNoTracking()
                    .Where(item => item.Id == request.ResourceId)
                    .Select(item => new { item.Status, item.RowVersion })
                    .SingleOrDefaultAsync(ct);
                if (resource is null) return Results.NotFound();
                currentStatus = resource.Status;
                currentVersion = resource.RowVersion;
            }
            else
            {
                var resource = await db.Installations.AsNoTracking()
                    .Where(item => item.Id == request.ResourceId)
                    .Select(item => new { item.Status, item.RowVersion })
                    .SingleOrDefaultAsync(ct);
                if (resource is null) return Results.NotFound();
                currentStatus = resource.Status;
                currentVersion = resource.RowVersion;
            }

            if (!currentVersion.AsSpan().SequenceEqual(expectedResourceVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            if (!VendorBusinessMutationRules.IsOperationalStatusTransitionAllowed(currentStatus, request.TargetStatus))
                return Results.Conflict(new { error = "invalid_status_transition" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var approval = new VendorApprovalRequest
            {
                Id = Guid.NewGuid(),
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                TargetStatus = request.TargetStatus!,
                ExpectedResourceRowVersion = expectedResourceVersion,
                RequestedByUserId = actor.Id,
                RequestedAtUtc = DateTimeOffset.UtcNow,
                Status = "Pending"
            };
            db.ApprovalRequests.Add(approval);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "approval_already_pending" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.approval.requested", nameof(VendorApprovalRequest),
                approval.Id.ToString("D"), CorrelationId()), ct);
            await tx.CommitAsync(ct);

            return Results.Accepted($"/api/vendor/approvals/{approval.Id:D}",
                ToResponse(approval));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsRequest));

        approvals.MapPost("/{id:guid}/decision", async (
            Guid id, DecideVendorApprovalRequest request, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization,
            VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedApprovalVersion) ||
                request.Decision is not ("Approved" or "Rejected") ||
                request.Comment?.Length > 500)
                return Results.BadRequest(new { error = "invalid_approval_decision" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var approval = await db.ApprovalRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (approval is null) return Results.NotFound();
            if (!approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
                return Results.Conflict(new { error = "stale_approval_version" });
            if (approval.Status != "Pending")
                return Results.Conflict(new { error = "approval_already_decided" });
            if (approval.RequestedByUserId == actor.Id)
                return Results.Forbid();
            if (approval.ResourceType is not ("customer" or "installation"))
                return Results.Conflict(new { error = "unsupported_approval_resource" });

            var managePermission = PermissionFor(approval.ResourceType);
            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor(approval.ResourceType, approval.ResourceId),
                VendorResourceCatalog.PolicyFor(approval.ResourceType, managePermission));
            if (!access.Succeeded) return Results.Forbid();

            var now = DateTimeOffset.UtcNow;
            var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (request.Decision == "Approved")
            {
                var resourceIsCurrent = false;
                if (approval.ResourceType == "customer")
                {
                    var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == approval.ResourceId, ct);
                    if (customer is not null &&
                        customer.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) &&
                        VendorBusinessMutationRules.IsOperationalStatusTransitionAllowed(customer.Status, approval.TargetStatus))
                    {
                        db.Entry(customer).Property(item => item.RowVersion).OriginalValue = approval.ExpectedResourceRowVersion;
                        customer.Status = approval.TargetStatus;
                        customer.UpdatedAtUtc = now;
                        resourceIsCurrent = true;
                    }
                }
                else if (approval.ResourceType == "installation")
                {
                    var installation = await db.Installations.SingleOrDefaultAsync(item => item.Id == approval.ResourceId, ct);
                    if (installation is not null &&
                        installation.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) &&
                        VendorBusinessMutationRules.IsOperationalStatusTransitionAllowed(installation.Status, approval.TargetStatus))
                    {
                        db.Entry(installation).Property(item => item.RowVersion).OriginalValue = approval.ExpectedResourceRowVersion;
                        installation.Status = approval.TargetStatus;
                        resourceIsCurrent = true;
                    }
                }

                if (!resourceIsCurrent)
                {
                    approval.Status = "Stale";
                    approval.ReviewedByUserId = actor.Id;
                    approval.ReviewedAtUtc = now;
                    approval.ReviewComment = comment;
                    db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
                    await db.SaveChangesAsync(ct);
                    await audit.AppendAsync(new VendorAuditWriteRequest(
                        actor.Id, "vendor.approval.stale", nameof(VendorApprovalRequest),
                        approval.Id.ToString("D"), CorrelationId()), ct);
                    await tx.CommitAsync(ct);
                    return Results.Conflict(new { error = "approval_resource_stale" });
                }
            }

            approval.Status = request.Decision;
            approval.ReviewedByUserId = actor.Id;
            approval.ReviewedAtUtc = now;
            approval.ReviewComment = comment;
            db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = "stale_approval_or_resource_version" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                request.Decision == "Approved" ? "vendor.approval.approved" : "vendor.approval.rejected",
                nameof(VendorApprovalRequest), approval.Id.ToString("D"), CorrelationId()), ct);
            if (request.Decision == "Approved")
            {
                await audit.AppendAsync(new VendorAuditWriteRequest(
                    actor.Id,
                    approval.ResourceType == "customer"
                        ? "vendor.customer.status-changed"
                        : "vendor.installation.status-changed",
                    approval.ResourceType == "customer" ? nameof(VendorCustomer) : nameof(VendorInstallation),
                    approval.ResourceId.ToString("D"), CorrelationId()), ct);
            }
            await tx.CommitAsync(ct);
            return Results.Ok(ToResponse(approval));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsReview));

        return endpoints;
    }

    private static string PermissionFor(string resourceType) => resourceType switch
    {
        "customer" => VendorPermissionCatalog.CustomersManage,
        "installation" => VendorPermissionCatalog.InstallationsManage,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceType))
    };

    private static string CorrelationId() =>
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static VendorApprovalResponse ToResponse(VendorApprovalRequest item) =>
        new(item.Id, item.ResourceType, item.ResourceId, item.TargetStatus,
            item.RequestedByUserId, item.RequestedAtUtc, item.Status,
            item.ReviewedByUserId, item.ReviewedAtUtc, item.ReviewComment,
            Convert.ToBase64String(item.RowVersion));
}

public sealed record CreateVendorApprovalRequest(
    string? ResourceType, Guid ResourceId, string? TargetStatus, string? RowVersion);

public sealed record DecideVendorApprovalRequest(
    string? Decision, string? RowVersion, string? Comment);

public sealed record VendorApprovalResponse(
    Guid Id, string ResourceType, Guid ResourceId, string TargetStatus,
    Guid RequestedByUserId, DateTimeOffset RequestedAtUtc, string Status,
    Guid? ReviewedByUserId, DateTimeOffset? ReviewedAtUtc, string? ReviewComment,
    string RowVersion);
