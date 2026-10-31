using System.Diagnostics;
using System.Text;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Authorized, audited, no-store export of the latest signed offline revocation snapshot.</summary>
public static class VendorOfflineRevocationEndpoints
{
    public static IEndpointRouteBuilder MapVendorOfflineRevocationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/vendor/offline-revocations/latest", async (
            HttpContext context,
            VendorDbContext db,
            UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";

            var actor = await users.GetUserAsync(context.User);
            if (actor is null)
                return Results.Forbid();

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var snapshot = await db.OfflineRevocationSnapshots.AsNoTracking()
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            if (snapshot is null || snapshot.ExpiresAtUtc <= now)
                return Results.Json(
                    new { error = "offline_revocation_snapshot_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.offline-revocation.snapshot-exported",
                nameof(VendorOfflineRevocationSnapshot),
                snapshot.ListId,
                Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            context.Response.Headers["Content-Disposition"] =
                "attachment; filename=\"offline-revocation-snapshot.json\"";
            return Results.Text(snapshot.EnvelopeJson, "application/json", Encoding.UTF8);
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesRead));

        return endpoints;
    }
}
