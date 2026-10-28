using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
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
/// Persists Vendor-side provisioning intent only. Until a production signer and customer-side
/// consumer are configured, requests remain AwaitingSigner and no company/admin is claimed created.
/// </summary>
public static class VendorProvisioningEndpoints
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public static IEndpointRouteBuilder MapVendorProvisioningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vendor/provisioning-requests");

        group.MapPost("/", async (
            CreateVendorProvisioningRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            IAuthorizationService authorization,
            VendorDbContext db,
            UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });

            var validationError = VendorProvisioningRequestRules.Validate(request);
            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            var customer = await db.Customers.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.CustomerId, ct);
            var installation = await db.Installations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.InstallationId, ct);
            if (customer is null || installation is null || installation.CustomerId != customer.Id)
                return Results.NotFound();

            var customerAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("customer", customer.Id),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersManage));
            var installationAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("installation", installation.Id, customer.Id),
                VendorResourceCatalog.PolicyFor("installation", VendorPermissionCatalog.InstallationsManage));
            if (!customerAccess.Succeeded || !installationAccess.Succeeded)
                return Results.Forbid();

            var requestId = request.RequestId.ToString("D");
            var existing = await db.ProvisioningRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == requestId, ct);
            if (existing is not null)
            {
                return IsSameRequest(existing, request, customer.CustomerNumber, installation.InstallationCode)
                    ? Results.Ok(ToResponse(existing))
                    : Results.Conflict(new { error = "provisioning_request_id_conflict" });
            }

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            var now = DateTimeOffset.UtcNow;
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var payload = new CustomerProvisioningPayload(
                CustomerProvisioningPackageValidator.CurrentFormatVersion,
                requestId,
                customer.CustomerNumber,
                installation.InstallationCode,
                request.CompanyName!.Trim(),
                request.CompanyType!.Trim(),
                request.InitialAdminUserName!.Trim(),
                request.Subsystems!.Order(StringComparer.Ordinal).ToArray(),
                now,
                now.AddHours(CustomerProvisioningPackageValidator.MaxPackageLifetimeHours),
                nonce);
            var payloadJson = CustomerProvisioningCanonicalJson.Serialize(payload);
            var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant();

            var item = new VendorProvisioningRequest
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                InstallationId = installation.Id,
                RequestId = requestId,
                Nonce = nonce,
                PayloadJson = payloadJson,
                PayloadSha256 = payloadHash,
                Status = "AwaitingSigner",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.ProvisioningRequests.Add(item);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                var concurrent = await db.ProvisioningRequests.AsNoTracking()
                    .SingleOrDefaultAsync(value => value.RequestId == requestId, ct);
                if (concurrent is not null &&
                    IsSameRequest(concurrent, request, customer.CustomerNumber, installation.InstallationCode))
                    return Results.Ok(ToResponse(concurrent));
                return Results.Conflict(new { error = "provisioning_request_id_conflict" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.provisioning.requested",
                nameof(VendorProvisioningRequest),
                item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);

            return Results.Created($"/api/vendor/provisioning-requests/{item.Id:D}", ToResponse(item));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersManage));

        group.MapPost("/{id:guid}/approval-request", async (
            Guid id, CreateVendorProvisioningApprovalRequest request, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, VendorDbContext db,
            UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedResourceVersion))
                return Results.BadRequest(new { error = "invalid_provisioning_approval_request" });

            var item = await db.ProvisioningRequests.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            if (item.Status != "AwaitingSigner")
                return Results.Conflict(new { error = "provisioning_request_not_awaiting_signer" });
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedResourceVersion))
                return Results.Conflict(new { error = "stale_row_version" });

            var customerAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("customer", item.CustomerId),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersManage));
            var installationAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("installation", item.InstallationId, item.CustomerId),
                VendorResourceCatalog.PolicyFor("installation", VendorPermissionCatalog.InstallationsManage));
            if (!customerAccess.Succeeded || !installationAccess.Succeeded)
                return Results.Forbid();

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var approval = new VendorApprovalRequest
            {
                Id = Guid.NewGuid(),
                ResourceType = "provisioning",
                ResourceId = item.Id,
                TargetStatus = "PendingDelivery",
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
                actor.Id, "vendor.provisioning.approval-requested", nameof(VendorProvisioningRequest),
                item.Id.ToString("D"), System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);

            return Results.Accepted($"/api/vendor/approvals/{approval.Id:D}", new
            {
                approval.Id, approval.ResourceType, approval.ResourceId, approval.TargetStatus,
                approval.RequestedAtUtc, approval.Status, RowVersion = Convert.ToBase64String(approval.RowVersion)
            });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsRequest));

        group.MapGet("/{id:guid}/package", async (
            Guid id, HttpContext context, IAuthorizationService authorization,
            VendorDbContext db, CancellationToken ct) =>
        {
            var item = await db.ProvisioningRequests.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            if (item.Status != "PendingDelivery" ||
                string.IsNullOrWhiteSpace(item.KeyId) ||
                string.IsNullOrWhiteSpace(item.Algorithm) ||
                string.IsNullOrWhiteSpace(item.SignatureBase64))
                return Results.Conflict(new { error = "provisioning_package_not_ready" });

            var customerAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("customer", item.CustomerId),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersRead));
            var installationAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("installation", item.InstallationId, item.CustomerId),
                VendorResourceCatalog.PolicyFor("installation", VendorPermissionCatalog.InstallationsRead));
            if (!customerAccess.Succeeded || !installationAccess.Succeeded)
                return Results.Forbid();

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            return Results.Ok(new CustomerProvisioningPackage(
                CustomerProvisioningPackageValidator.CurrentFormatVersion,
                item.KeyId, item.Algorithm, item.PayloadJson, item.SignatureBase64));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersRead));

        group.MapGet("/", async (
            Guid? customerId, int? page, int? pageSize,
            HttpContext context, IAuthorizationService authorization, VendorDbContext db, CancellationToken ct) =>
        {
            if (customerId is null || customerId == Guid.Empty)
                return Results.BadRequest(new { error = "customer_id_required" });

            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("customer", customerId.Value),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersRead));
            if (!access.Succeeded) return Results.Forbid();

            var pagination = VendorPageRequest.Normalize(page, pageSize);
            var query = db.ProvisioningRequests.AsNoTracking()
                .Where(item => item.CustomerId == customerId.Value)
                .OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id);
            var totalCount = await query.CountAsync(ct);
            var items = await query.Skip(pagination.Skip).Take(pagination.PageSize)
                .Select(item => new ProvisioningRequestResponse(
                    item.Id, item.CustomerId, item.InstallationId, item.RequestId, item.Status,
                    item.CreatedAtUtc, item.UpdatedAtUtc, item.ConfirmedAtUtc, item.FailureCode,
                    item.PayloadSha256, Convert.ToBase64String(item.RowVersion)))
                .ToListAsync(ct);
            return Results.Ok(new VendorPageResponse<ProvisioningRequestResponse>(
                items, pagination.Page, pagination.PageSize, totalCount));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersRead));

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IAuthorizationService authorization, VendorDbContext db, CancellationToken ct) =>
        {
            var item = await db.ProvisioningRequests.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();

            var customerAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("customer", item.CustomerId),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersRead));
            var installationAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("installation", item.InstallationId, item.CustomerId),
                VendorResourceCatalog.PolicyFor("installation", VendorPermissionCatalog.InstallationsRead));
            if (!customerAccess.Succeeded || !installationAccess.Succeeded)
                return Results.Forbid();

            return Results.Ok(ToResponse(item));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersRead));

        return endpoints;
    }

    private static bool IsSameRequest(
        VendorProvisioningRequest existing,
        CreateVendorProvisioningRequest request,
        string customerReference,
        string installationCode)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CustomerProvisioningPayload>(existing.PayloadJson, PayloadJsonOptions);
            return payload is not null &&
                   VendorProvisioningRequestRules.MatchesExisting(payload, request, customerReference, installationCode);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProvisioningRequestResponse ToResponse(VendorProvisioningRequest item) =>
        new(item.Id, item.CustomerId, item.InstallationId, item.RequestId, item.Status,
            item.CreatedAtUtc, item.UpdatedAtUtc, item.ConfirmedAtUtc, item.FailureCode,
            item.PayloadSha256, Convert.ToBase64String(item.RowVersion));

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
                return true;
        }

        return false;
    }
}

public sealed record ProvisioningRequestResponse(
    Guid Id,
    Guid CustomerId,
    Guid InstallationId,
    string RequestId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    string? FailureCode,
    string PayloadSha256,
    string RowVersion);

public sealed record CreateVendorProvisioningApprovalRequest(string? RowVersion);
