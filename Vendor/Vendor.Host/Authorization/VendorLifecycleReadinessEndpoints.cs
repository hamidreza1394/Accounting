using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Read-only lifecycle readiness diagnostics (vendor mission stage 4). Every stage status comes
/// from real persisted rows through a bounded set of indexed queries for ONE customer; nothing
/// is mutated, no secrets or private keys are returned, and the CompanyReference gate (#392) is
/// reported Blocked while it is closed — never silently Ready.
/// </summary>
public static class VendorLifecycleReadinessEndpoints
{
    public static IEndpointRouteBuilder MapVendorLifecycleReadinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/vendor/customers/{customerId:guid}/readiness", async (
            Guid customerId, HttpContext context, IAuthorizationService authorization,
            VendorDbContext db, VendorProvisioningCompanyReferenceGate companyReferenceGate,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("customer", customerId),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersRead));
            if (!access.Succeeded) return Results.Forbid();

            var customer = await db.Customers.AsNoTracking()
                .Where(item => item.Id == customerId)
                .Select(item => new { item.Id, item.CustomerNumber, item.DisplayName, item.LegalName, item.Status })
                .SingleOrDefaultAsync(ct);
            if (customer is null) return Results.NotFound();

            // Read payment counts from the latest order only. Aggregating payments across all
            // orders while displaying the latest order number can falsely mark a new unpaid order
            // Ready because an older order happened to have a verified payment.
            var orderStats = await db.SalesOrders.AsNoTracking()
                .Where(item => item.CustomerId == customerId)
                .OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
                .Select(order => new
                {
                    order.OrderNumber,
                    order.CreatedAtUtc,
                    VerifiedPayments = order.Payments.Count(payment => payment.Status == VendorPaymentRecord.StatusVerified),
                    RecordedPayments = order.Payments.Count(payment => payment.Status == VendorPaymentRecord.StatusRecorded),
                    RejectedPayments = order.Payments.Count(payment => payment.Status == VendorPaymentRecord.StatusRejected)
                })
                .FirstOrDefaultAsync(ct);

            var licenseStats = await db.Licenses.AsNoTracking()
                .Where(item => item.CustomerId == customerId)
                .GroupBy(item => item.CustomerId)
                .Select(group => new
                {
                    Issued = group.Count(item => item.Status == "Issued"),
                    Draft = group.Count(item => item.Status == "Draft")
                })
                .SingleOrDefaultAsync(ct);

            var installation = await db.Installations.AsNoTracking()
                .Where(item => item.CustomerId == customerId)
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new { item.InstallationCode, item.Status, item.LastSeenAtUtc })
                .FirstOrDefaultAsync(ct);

            var provisioning = await db.ProvisioningRequests.AsNoTracking()
                .Where(item => item.CustomerId == customerId)
                .OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
                .Select(item => new { item.Status, item.FailureCode, item.AcknowledgementResult, item.UpdatedAtUtc, item.DeliveredAtUtc })
                .FirstOrDefaultAsync(ct);

            var pendingApprovals = await db.ApprovalRequests.AsNoTracking()
                .CountAsync(approval => approval.Status == "Pending" && (
                    (approval.ResourceType == "license" &&
                        db.Licenses.Any(item => item.Id == approval.ResourceId && item.CustomerId == customerId)) ||
                    (approval.ResourceType == "licenseAmendment" &&
                        db.LicenseAmendmentRequests.Any(item => item.Id == approval.ResourceId && item.CustomerId == customerId)) ||
                    (approval.ResourceType == "provisioning" &&
                        db.ProvisioningRequests.Any(item => item.Id == approval.ResourceId && item.CustomerId == customerId))), ct);

            var amendment = await db.LicenseAmendmentRequests.AsNoTracking()
                .Where(item => item.CustomerId == customerId)
                .OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.Id)
                .Select(item => new { item.Status, item.FailureCode, item.UpdatedAtUtc })
                .FirstOrDefaultAsync(ct);

            var snapshot = new VendorLifecycleReadinessRules.LifecycleSnapshot(
                customer.Status,
                orderStats is not null,
                orderStats?.LatestOrderNumber,
                orderStats?.LatestOrderAtUtc,
                orderStats?.VerifiedPayments ?? 0,
                orderStats?.RecordedPayments ?? 0,
                orderStats?.RejectedPayments ?? 0,
                licenseStats?.Issued ?? 0,
                licenseStats?.Draft ?? 0,
                installation is not null,
                installation?.InstallationCode,
                installation?.Status,
                installation?.LastSeenAtUtc,
                provisioning is not null,
                provisioning?.Status,
                provisioning?.FailureCode,
                provisioning?.AcknowledgementResult,
                provisioning?.UpdatedAtUtc,
                provisioning?.DeliveredAtUtc,
                pendingApprovals,
                amendment?.Status,
                amendment?.FailureCode,
                amendment?.UpdatedAtUtc,
                companyReferenceGate.IsEnabled);

            var response = new VendorLifecycleReadinessResponse(
                customer.Id, customer.CustomerNumber, customer.DisplayName ?? customer.LegalName,
                timeProvider.GetUtcNow().ToUniversalTime(),
                VendorLifecycleReadinessRules.Resolve(snapshot));
            return Results.Ok(response);
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersRead));

        return endpoints;
    }
}

public sealed record VendorLifecycleReadinessResponse(
    Guid CustomerId,
    string CustomerNumber,
    string DisplayName,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<VendorLifecycleReadinessRules.LifecycleStage> Stages);
