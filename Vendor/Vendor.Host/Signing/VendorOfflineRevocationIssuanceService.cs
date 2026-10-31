using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Applies an approved license revocation and persists its signed, full-state offline snapshot
/// inside the caller's single Vendor database transaction.
/// </summary>
public sealed class VendorOfflineRevocationIssuanceService(
    VendorDbContext db,
    VendorOfflineRevocationEnvelopeFactory envelopeFactory,
    IVendorAuditWriter audit,
    TimeProvider timeProvider)
{
    public Task<VendorOfflineRevocationIssuanceResult> EnsureInitialSnapshotAsync(
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken) =>
        CreateSnapshotAsync(actorUserId, correlationId, onlyIfMissing: true, cancellationToken);

    public Task<VendorOfflineRevocationIssuanceResult> RefreshCurrentSnapshotAsync(
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken) =>
        CreateSnapshotAsync(actorUserId, correlationId, onlyIfMissing: false, cancellationToken);

    private async Task<VendorOfflineRevocationIssuanceResult> CreateSnapshotAsync(
        Guid actorUserId,
        string correlationId,
        bool onlyIfMissing,
        CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Revocation snapshot creation must run inside a Vendor database transaction.");
        if (actorUserId == Guid.Empty)
            return VendorOfflineRevocationIssuanceResult.Conflict("revocation_snapshot_actor_required");

        if (!await AcquireSequenceLockAsync(cancellationToken).ConfigureAwait(false))
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_sequence_lock_unavailable");

        var currentSequence = await db.OfflineRevocationSnapshots
            .Select(item => (long?)item.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
        if (onlyIfMissing && currentSequence > 0)
            return VendorOfflineRevocationIssuanceResult.Success(currentSequence);
        if (currentSequence == long.MaxValue)
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_sequence_exhausted");

        var revokedLicenseIds = await db.Licenses.AsNoTracking()
            .Where(item => item.Status == "Revoked")
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (revokedLicenseIds.Count > OfflineRevocationListValidator.MaxRevokedLicenseIds)
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_snapshot_limit_exceeded");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var payload = new OfflineRevocationList(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            currentSequence + 1,
            now,
            now.AddDays(30),
            revokedLicenseIds
                .Select(id => id.ToString("D"))
                .Order(StringComparer.Ordinal)
                .ToArray());
        var creation = await envelopeFactory.CreateAsync(payload, currentSequence, cancellationToken)
            .ConfigureAwait(false);
        if (!creation.Succeeded || creation.Envelope is null)
        {
            return creation.StatusCode == StatusCodes.Status503ServiceUnavailable
                ? VendorOfflineRevocationIssuanceResult.Unavailable(
                    creation.ErrorCode ?? "offline_revocation_signing_unavailable")
                : VendorOfflineRevocationIssuanceResult.Conflict(
                    creation.ErrorCode ?? "offline_revocation_snapshot_invalid");
        }

        var envelope = creation.Envelope;
        var payloadBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);
        var snapshot = new VendorOfflineRevocationSnapshot
        {
            Id = Guid.NewGuid(),
            Sequence = payload.Sequence,
            KeyId = envelope.KeyId,
            Algorithm = envelope.Algorithm,
            EnvelopeJson = JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web),
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant(),
            IssuedAtUtc = payload.IssuedAtUtc,
            ExpiresAtUtc = payload.ExpiresAtUtc,
            CreatedAtUtc = now
        };
        db.OfflineRevocationSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(new VendorAuditWriteRequest(
            actorUserId,
            onlyIfMissing
                ? "vendor.revocation.snapshot.initialized"
                : "vendor.revocation.snapshot.refreshed",
            nameof(VendorOfflineRevocationSnapshot),
            snapshot.Id.ToString("D"),
            correlationId), cancellationToken);

        return VendorOfflineRevocationIssuanceResult.Success(payload.Sequence);
    }

    public async Task<VendorOfflineRevocationIssuanceResult> RevokeApprovedAsync(
        VendorApprovalRequest approval,
        Guid reviewerUserId,
        byte[] expectedApprovalVersion,
        string? reviewComment,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(expectedApprovalVersion);

        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("License revocation must run inside the approval transaction.");

        if (approval.ResourceType != "license" || approval.TargetStatus != "Revoked" ||
            approval.Status != "Pending" || approval.RequestedByUserId == reviewerUserId ||
            approval.ResourceId == Guid.Empty || expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return VendorOfflineRevocationIssuanceResult.Conflict("stale_approval_version");

        var license = await db.Licenses.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId,
            cancellationToken);
        if (license is null)
            return VendorOfflineRevocationIssuanceResult.NotFound("license_not_found");

        if (!license.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) ||
            license.Status != "Issued")
            return VendorOfflineRevocationIssuanceResult.Conflict("approval_resource_stale");

        if (!await AcquireSequenceLockAsync(cancellationToken).ConfigureAwait(false))
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_sequence_lock_unavailable");

        var currentSequence = await db.OfflineRevocationSnapshots
            .Select(item => (long?)item.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
        if (currentSequence == long.MaxValue)
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_sequence_exhausted");

        var revokedLicenseIds = await db.Licenses.AsNoTracking()
            .Where(item => item.Status == "Revoked")
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        revokedLicenseIds.Add(license.Id);

        if (revokedLicenseIds.Count > OfflineRevocationListValidator.MaxRevokedLicenseIds)
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_snapshot_limit_exceeded");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var payload = new OfflineRevocationList(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            currentSequence + 1,
            now,
            now.AddDays(30),
            revokedLicenseIds
                .Select(id => id.ToString("D"))
                .Order(StringComparer.Ordinal)
                .ToArray());

        var creation = await envelopeFactory.CreateAsync(payload, currentSequence, cancellationToken)
            .ConfigureAwait(false);
        if (!creation.Succeeded || creation.Envelope is null)
        {
            return creation.StatusCode == StatusCodes.Status503ServiceUnavailable
                ? VendorOfflineRevocationIssuanceResult.Unavailable(
                    creation.ErrorCode ?? "offline_revocation_signing_unavailable")
                : VendorOfflineRevocationIssuanceResult.Conflict(
                    creation.ErrorCode ?? "offline_revocation_snapshot_invalid");
        }

        var envelope = creation.Envelope;
        var payloadBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var envelopeJson = JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web);

        db.Entry(license).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        license.Status = "Revoked";

        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();

        var snapshot = new VendorOfflineRevocationSnapshot
        {
            Id = Guid.NewGuid(),
            Sequence = payload.Sequence,
            KeyId = envelope.KeyId,
            Algorithm = envelope.Algorithm,
            EnvelopeJson = envelopeJson,
            PayloadSha256 = payloadHash,
            IssuedAtUtc = payload.IssuedAtUtc,
            ExpiresAtUtc = payload.ExpiresAtUtc,
            CreatedAtUtc = now
        };
        db.OfflineRevocationSnapshots.Add(snapshot);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorOfflineRevocationIssuanceResult.Conflict("stale_approval_or_resource_version");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_sequence_already_issued");
        }

        // All three audit records share the approval transaction with license status and snapshot.
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId,
            "vendor.approval.approved",
            nameof(VendorApprovalRequest),
            approval.Id.ToString("D"),
            correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId,
            "vendor.license.revoked",
            nameof(VendorLicense),
            license.Id.ToString("D"),
            correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId,
            "vendor.revocation.snapshot.issued",
            nameof(VendorOfflineRevocationSnapshot),
            snapshot.Id.ToString("D"),
            correlationId), cancellationToken);

        return VendorOfflineRevocationIssuanceResult.Success(payload.Sequence);
    }

    private async Task<bool> AcquireSequenceLockAsync(CancellationToken cancellationToken)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Revocation sequence locking requires an active transaction.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = 20;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'Faranou.Vendor.OfflineRevocation.Sequence',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 15000;
            SELECT @result;
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null &&
               int.TryParse(System.Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture), out var code) &&
               code >= 0;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed record VendorOfflineRevocationIssuanceResult(
    bool Succeeded,
    int StatusCode,
    long? Sequence,
    string? ErrorCode)
{
    public static VendorOfflineRevocationIssuanceResult Success(long sequence) =>
        new(true, StatusCodes.Status200OK, sequence, null);

    public static VendorOfflineRevocationIssuanceResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, null, code);

    public static VendorOfflineRevocationIssuanceResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, null, code);

    public static VendorOfflineRevocationIssuanceResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, null, code);
}
