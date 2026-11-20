using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Microsoft.AspNetCore.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Explicit operator-initiated license amendment requests tied to purchased sales order lines
/// (D-108/D-109). Starting a request only records intent: the candidate payload is rebuilt by the
/// official builder from the latest signature-verified envelope, reviewed as a separate approval
/// and issued by the existing signing pipeline. Payment verification never starts or issues an
/// amendment, and company-scoped kinds stay Blocked until CompanyReference mapping (#392) is
/// operational.
/// </summary>
public static class VendorLicenseAmendmentEndpoints
{
    public static IEndpointRouteBuilder MapVendorLicenseAmendmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vendor/license-amendment-requests");

        group.MapPost("/preview", async (
            CreateVendorLicenseAmendmentRequest body, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, VendorDbContext db,
            VendorLicenseAmendmentIssuanceService issuance, TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            var access = await authorization.AuthorizeAsync(
                context.User,
                VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesManage));
            if (!access.Succeeded) return Results.Forbid();

            var (request, error) = await BuildTransientRequestAsync(
                body, db, timeProvider, requireFreshCheck: false, ct);
            if (error is not null || request is null)
                return Results.Conflict(new { error });

            var periodError = await ValidateRequestedPeriodsAsync(
                body, request, issuance, ct);
            if (periodError is not null)
                return Results.Conflict(new { error = periodError });

            var preview = await issuance.PreviewAsync(request, ct);
            if (!preview.Succeeded || preview.Candidate is null)
                return Results.Json(new { error = preview.ErrorCode }, statusCode: preview.StatusCode);

            return Results.Ok(new
            {
                sequence = preview.Candidate.Sequence,
                maxCompanies = preview.Candidate.MaxCompanies,
                supportUntilUtc = preview.Candidate.SupportUntilUtc,
                updatesUntilUtc = preview.Candidate.UpdatesUntilUtc,
                issuedAtUtc = preview.Candidate.IssuedAtUtc
            });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesManage));

        group.MapPost("/", async (
            CreateVendorLicenseAmendmentRequest body, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, VendorDbContext db,
            VendorLicenseAmendmentIssuanceService issuance,
            UserManager<VendorAdminUser> users, IVendorAuditWriter audit,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            var access = await authorization.AuthorizeAsync(
                context.User,
                VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesManage));
            if (!access.Succeeded) return Results.Forbid();

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            var (transient, error) = await BuildTransientRequestAsync(
                body, db, timeProvider, requireFreshCheck: true, ct);
            if (error is not null || transient is null)
                return Results.Conflict(new { error });

            // supportUpdates requests are validated against the latest SIGNATURE-VERIFIED
            // envelope before anything is stored: an invalid or not-yet-extendable period fails
            // here with an actionable code instead of surfacing later at issuance time.
            var periodError = await ValidateRequestedPeriodsAsync(
                body, transient, issuance, ct);
            if (periodError is not null)
                return Results.Conflict(new { error = periodError });

            var request = new VendorLicenseAmendmentRequest
            {
                Id = Guid.NewGuid(),
                OrderId = transient.OrderId,
                OrderLineId = transient.OrderLineId,
                CustomerId = transient.CustomerId,
                LicenseId = transient.LicenseId,
                Kind = transient.Kind,
                RequestedMaxCompanies = transient.RequestedMaxCompanies,
                RequestedSupportUntilUtc = transient.RequestedSupportUntilUtc,
                RequestedUpdatesUntilUtc = transient.RequestedUpdatesUntilUtc,
                Status = VendorLicenseAmendmentRequest.StatusRequested,
                RequestedByUserId = actor.Id,
                CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
                UpdatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime()
            };
            db.LicenseAmendmentRequests.Add(request);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                // The only unique index on inserts is the active-request filter for this line:
                // raced with another operator starting the same purchased line's amendment.
                return Results.Conflict(new { error = "amendment_request_already_active" });
            }

            // The audit event shares the request's transaction: a failing audit append rolls the
            // request row back instead of leaving an unaudited purchase-fulfillment record.
            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.amendment.requested", nameof(VendorLicenseAmendmentRequest),
                request.Id.ToString("D"), CorrelationId(context)), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/vendor/license-amendment-requests/{request.Id:D}", ToResponse(request));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesManage));

        group.MapGet("/", async (
            int? page, int? pageSize, Guid? orderId, Guid? customerId, VendorDbContext db,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            var paging = VendorPageRequest.Normalize(page, pageSize);
            IQueryable<VendorLicenseAmendmentRequest> query = db.LicenseAmendmentRequests.AsNoTracking();
            if (orderId is { } orderFilter) query = query.Where(item => item.OrderId == orderFilter);
            if (customerId is { } customerFilter) query = query.Where(item => item.CustomerId == customerFilter);
            query = query.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id);
            var totalCount = await query.CountAsync(ct);
            var items = await query.Skip(paging.Skip).Take(paging.PageSize)
                .Select(item => ToResponse(item))
                .ToListAsync(ct);
            return Results.Ok(new VendorPageResponse<VendorLicenseAmendmentResponse>(items, paging.Page, paging.PageSize, totalCount));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesRead));

        group.MapGet("/{id:guid}", async (
            Guid id, VendorDbContext db, CancellationToken ct) =>
        {
            var request = await db.LicenseAmendmentRequests.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => ToResponse(item))
                .SingleOrDefaultAsync(ct);
            return request is null ? Results.NotFound() : Results.Ok(request);
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesRead));

        group.MapPost("/{id:guid}/approval-request", async (
            Guid id, CreateVendorLicenseAmendmentApprovalRequest body, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, VendorDbContext db,
            UserManager<VendorAdminUser> users, IVendorAuditWriter audit,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(body.RowVersion, out var expectedRequestVersion))
                return Results.BadRequest(new { error = "invalid_amendment_approval_request" });

            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("licenseAmendment", id),
                VendorResourceCatalog.PolicyFor("licenseAmendment", VendorPermissionCatalog.LicensesIssue));
            if (!access.Succeeded) return Results.Forbid();

            var request = await db.LicenseAmendmentRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (request is null) return Results.NotFound();
            if (request.Status != VendorLicenseAmendmentRequest.StatusRequested)
                return Results.Conflict(new { error = "invalid_amendment_transition" });
            if (!request.RowVersion.AsSpan().SequenceEqual(expectedRequestVersion))
                return Results.Conflict(new { error = "stale_row_version" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var approval = new VendorApprovalRequest
            {
                Id = Guid.NewGuid(),
                ResourceType = "licenseAmendment",
                ResourceId = id,
                TargetStatus = "Issued",
                ExpectedResourceRowVersion = expectedRequestVersion,
                RequestedByUserId = actor.Id,
                RequestedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
                Status = "Pending"
            };
            db.ApprovalRequests.Add(approval);
            request.Status = VendorLicenseAmendmentRequest.StatusPendingApproval;
            request.ApprovalRequestId = approval.Id;
            request.UpdatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            try
            {
                await db.SaveChangesAsync(ct);
                // The approval must pin the request state it approved: PendingApproval, which is
                // the row version EF assigned by the save above (the operator-submitted version
                // belonged to Requested and is intentionally superseded here).
                approval.ExpectedResourceRowVersion = request.RowVersion.ToArray();
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent write raced the pre-transaction version check: fail closed.
                return Results.Conflict(new { error = "stale_row_version" });
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "approval_already_pending" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.amendment.approval-requested", nameof(VendorLicenseAmendmentRequest),
                id.ToString("D"), CorrelationId(context)), ct);
            await tx.CommitAsync(ct);
            return Results.Accepted($"/api/vendor/approvals/{approval.Id:D}", new { approval.Id });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesIssue));

        return endpoints;
    }

    /// <summary>
    /// Envelope-backed period validation for supportUpdates requests: shared by preview and
    /// creation so both paths fail identically before any state is stored. Other kinds skip
    /// this check — maxCompanies has its own rules, and no payment state is ever consulted.
    /// </summary>
    private static async Task<string?> ValidateRequestedPeriodsAsync(
        CreateVendorLicenseAmendmentRequest body, VendorLicenseAmendmentRequest transient,
        VendorLicenseAmendmentIssuanceService issuance, CancellationToken ct) =>
        transient.Kind == VendorLicenseAmendmentRules.KindSupportUpdates
            ? await issuance.ValidateRequestedPeriodsAsync(
                body.LicenseId, transient.CustomerId,
                body.RequestedSupportUntilUtc, body.RequestedUpdatesUntilUtc, ct)
            : null;

    /// <summary>
    /// Shared eligibility validation for preview and creation. Loads the purchased line, its order
    /// and the referenced license, then applies the pure rules. Payment state is intentionally not
    /// consulted (D-108 §1). No state is persisted; the returned entity is transient.
    /// </summary>
    private static async Task<(VendorLicenseAmendmentRequest? Request, string? Error)> BuildTransientRequestAsync(
        CreateVendorLicenseAmendmentRequest body, VendorDbContext db, TimeProvider timeProvider,
        bool requireFreshCheck, CancellationToken ct)
    {
        if (body.OrderLineId == Guid.Empty || body.LicenseId == Guid.Empty)
            return (null, "invalid_amendment_request");

        var line = await db.Set<VendorSalesOrderLine>().AsNoTracking()
            .Where(item => item.Id == body.OrderLineId)
            .Select(item => new { item.Id, item.OrderId, item.ProductCode, item.LicenseId })
            .SingleOrDefaultAsync(ct);
        if (line is null) return (null, "order_line_not_found");

        if (requireFreshCheck && await db.LicenseAmendmentRequests.AsNoTracking().AnyAsync(
                amendment => amendment.OrderLineId == line.Id &&
                    VendorLicenseAmendmentRules.BlockingStatuses.Contains(amendment.Status), ct))
            return (null, "amendment_request_already_active");

        var order = await db.SalesOrders.AsNoTracking()
            .Where(item => item.Id == line.OrderId)
            .Select(item => new { item.Id, item.CustomerId })
            .SingleOrDefaultAsync(ct);
        if (order is null) return (null, "order_line_not_found");

        var license = await db.Licenses.AsNoTracking()
            .Where(item => item.Id == body.LicenseId)
            .Select(item => new { item.Id, item.CustomerId, item.Status, item.MaxCompanies })
            .SingleOrDefaultAsync(ct);
        if (license is null) return (null, "license_not_found");
        if (license.CustomerId != order.CustomerId)
            return (null, "license_order_line_mismatch");

        var validationError = VendorLicenseAmendmentRules.ValidateCreate(
            body.Kind, line.ProductCode, line.LicenseId, body.LicenseId, license.Status,
            body.RequestedMaxCompanies, license.MaxCompanies,
            body.RequestedSupportUntilUtc, body.RequestedUpdatesUntilUtc,
            timeProvider.GetUtcNow().ToUniversalTime());
        if (validationError is not null) return (null, validationError);

        return (new VendorLicenseAmendmentRequest
        {
            Id = Guid.Empty,
            OrderId = order.Id,
            OrderLineId = line.Id,
            CustomerId = order.CustomerId,
            LicenseId = license.Id,
            Kind = body.Kind!,
            RequestedMaxCompanies = body.RequestedMaxCompanies,
            RequestedSupportUntilUtc = body.RequestedSupportUntilUtc,
            RequestedUpdatesUntilUtc = body.RequestedUpdatesUntilUtc
        }, null);
    }

    private static VendorLicenseAmendmentResponse ToResponse(VendorLicenseAmendmentRequest item) => new(
        item.Id, item.OrderId, item.OrderLineId, item.CustomerId, item.LicenseId,
        item.Kind, item.RequestedMaxCompanies, item.RequestedSupportUntilUtc,
        item.RequestedUpdatesUntilUtc, item.Status, item.ApprovalRequestId,
        item.FailureCode, item.CreatedAtUtc, item.UpdatedAtUtc,
        Convert.ToBase64String(item.RowVersion));

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    private static string CorrelationId(HttpContext context) =>
        System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}

/// <summary>Preview/create payload; no entitlement value is accepted as an arbitrary string.</summary>
public sealed record CreateVendorLicenseAmendmentRequest(
    Guid OrderLineId,
    Guid LicenseId,
    string? Kind,
    int? RequestedMaxCompanies,
    DateTimeOffset? RequestedSupportUntilUtc,
    DateTimeOffset? RequestedUpdatesUntilUtc);

public sealed record CreateVendorLicenseAmendmentApprovalRequest(string RowVersion);

public sealed record VendorLicenseAmendmentResponse(
    Guid Id, Guid OrderId, Guid OrderLineId, Guid CustomerId, Guid LicenseId,
    string Kind, int? RequestedMaxCompanies, DateTimeOffset? RequestedSupportUntilUtc,
    DateTimeOffset? RequestedUpdatesUntilUtc, string Status, Guid? ApprovalRequestId,
    string? FailureCode, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);
