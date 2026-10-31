using System.Data;
using System.Text;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Authorized, auditable, no-cache export of the latest signed offline-revocation snapshot.
/// The endpoint only returns a previously committed Vendor artifact; it never signs on export.
/// </summary>
public static class VendorOfflineRevocationEndpoints
{
    public static IEndpointRouteBuilder MapVendorOfflineRevocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vendor/offline-revocations");

        group.MapPost("/latest/export", async (
            HttpContext context,
            IAntiforgery antiforgery,
            VendorDbContext db,
            UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var snapshot = await db.SignedRevocationSnapshots.AsNoTracking()
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefaultAsync(ct);
            if (snapshot is null)
                return Results.NotFound(new { error = "offline_revocation_snapshot_not_found" });

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.offline-revocation.snapshot-exported",
                nameof(Faranou.Vendor.Persistence.Domain.VendorSignedRevocationSnapshot),
                snapshot.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            context.Response.Headers.ContentDisposition = "attachment; filename=\"offline-revocation.json\"";
            return Results.Text(snapshot.EnvelopeJson, "application/json", Encoding.UTF8);
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesRead));

        return endpoints;
    }
}
