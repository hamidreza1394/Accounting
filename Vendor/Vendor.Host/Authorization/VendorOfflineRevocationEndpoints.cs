using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Authenticated bootstrap, refresh and export endpoints for signed offline revocation snapshots.</summary>
public static class VendorOfflineRevocationEndpoints
{
    public static IEndpointRouteBuilder MapVendorOfflineRevocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var revocations = endpoints.MapGroup("/api/vendor/revocations");

        revocations.MapPost("/initialize", async (
            HttpContext context,
            IAntiforgery antiforgery,
            VendorDbContext db,
            VendorOfflineRevocationIssuanceService issuance,
            UserManager<VendorAdminUser> users,
            CancellationToken cancellationToken) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            var result = await issuance.EnsureInitialSnapshotAsync(
                actor.Id, CorrelationId(), cancellationToken);
            if (!result.Succeeded)
                return Results.Json(new { error = result.ErrorCode }, statusCode: result.StatusCode);

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new { sequence = result.Sequence });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesIssue));

        revocations.MapPost("/refresh", async (
            HttpContext context,
            IAntiforgery antiforgery,
            VendorDbContext db,
            VendorOfflineRevocationIssuanceService issuance,
            UserManager<VendorAdminUser> users,
            CancellationToken cancellationToken) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            var result = await issuance.RefreshCurrentSnapshotAsync(
                actor.Id, CorrelationId(), cancellationToken);
            if (!result.Succeeded)
                return Results.Json(new { error = result.ErrorCode }, statusCode: result.StatusCode);

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new { sequence = result.Sequence });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesIssue));

        revocations.MapGet("/current", async (
            HttpContext context,
            VendorDbContext db,
            IVendorAuditWriter audit,
            VendorOfflineRevocationEnvelopeFactory envelopeFactory,
            UserManager<VendorAdminUser> users,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var snapshot = await db.OfflineRevocationSnapshots.AsNoTracking()
                .OrderByDescending(item => item.Sequence)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (snapshot is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Json(
                    new { error = "offline_revocation_snapshot_not_initialized" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            OfflineRevocationListEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(
                    snapshot.EnvelopeJson, JsonSerializerOptions.Web);
            }
            catch (JsonException)
            {
                envelope = null;
            }

            var now = timeProvider.GetUtcNow();
            var integrityValid = false;
            if (envelope is not null &&
                !string.IsNullOrEmpty(envelope.PayloadJson) &&
                string.Equals(envelope.KeyId, snapshot.KeyId, StringComparison.Ordinal) &&
                string.Equals(envelope.Algorithm, snapshot.Algorithm, StringComparison.Ordinal))
            {
                try
                {
                    var payloadBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);
                    var storedDigest = Convert.FromHexString(snapshot.PayloadSha256);
                    var actualDigest = SHA256.HashData(payloadBytes);
                    integrityValid = CryptographicOperations.FixedTimeEquals(storedDigest, actualDigest) &&
                        envelopeFactory.TryVerifyEnvelope(
                            envelope, now, snapshot.Sequence - 1, out var payload, out _) &&
                        payload is not null &&
                        payload.Sequence == snapshot.Sequence &&
                        payload.IssuedAtUtc == snapshot.IssuedAtUtc &&
                        payload.ExpiresAtUtc == snapshot.ExpiresAtUtc;
                }
                catch (FormatException)
                {
                    integrityValid = false;
                }
            }

            if (!integrityValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Json(
                    new { error = "offline_revocation_snapshot_integrity_failed" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.revocation.snapshot.exported",
                nameof(VendorOfflineRevocationSnapshot),
                snapshot.Id.ToString("D"),
                CorrelationId()), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            context.Response.Headers["Cache-Control"] = "no-store, no-cache";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            return Results.Content(snapshot.EnvelopeJson, "application/json; charset=utf-8");
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesIssue));

        return endpoints;
    }

    private static string CorrelationId() =>
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}
