using System.Diagnostics;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Authenticated export endpoint for the latest signed full-state revocation snapshot.</summary>
public static class VendorOfflineRevocationEndpoints
{
    public static IEndpointRouteBuilder MapVendorOfflineRevocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var revocations = endpoints.MapGroup("/api/vendor/revocations");

        revocations.MapGet("/current", async (
            HttpContext context,
            VendorDbContext db,
            IVendorAuditWriter audit,
            CancellationToken cancellationToken) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var snapshot = await db.OfflineRevocationSnapshots.AsNoTracking()
                .OrderByDescending(item => item.Sequence)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (snapshot is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.NotFound(new { error = "offline_revocation_snapshot_not_found" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                null,
                "vendor.revocation.snapshot.exported",
                nameof(VendorOfflineRevocationSnapshot),
                snapshot.Id.ToString("D"),
                CorrelationId()), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            context.Response.Headers.CacheControl = "no-store, no-cache";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            return Results.Content(snapshot.EnvelopeJson, "application/json; charset=utf-8");
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesIssue));

        return endpoints;
    }

    private static string CorrelationId() =>
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}
