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
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Applies an approved license revocation only when a signed offline snapshot can be created and
/// persisted in the caller's transaction with the license status, approval and audit events.
/// </summary>
public sealed class VendorLicenseRevocationService(
    VendorDbContext db,
    VendorOfflineRevocationSequenceAllocator sequenceAllocator,
    VendorOfflineRevocationSnapshotFactory snapshotFactory,
    IOptions<VendorOfflineRevocationOptions> options,
    IVendorAuditWriter audit,
    TimeProvider timeProvider)
{
    public async Task<VendorLicenseRevocationResult> RevokeApprovedAsync(
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
            return VendorLicenseRevocationResult.Conflict("stale_approval_version");

        var license = await db.Licenses.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (license is null)
            return VendorLicenseRevocationResult.NotFound("license_not_found");

        if (!license.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) ||
            license.Status != "Issued" || license.LastIssuedSequence <= 0)
            return VendorLicenseRevocationResult.Conflict("approval_resource_stale");

        var hasIssuedEnvelope = await db.SignedLicenseEnvelopes.AnyAsync(
            item => item.LicenseId == license.Id && item.Sequence == license.LastIssuedSequence,
            cancellationToken);
        if (!hasIssuedEnvelope)
            return VendorLicenseRevocationResult.Conflict("license_signed_envelope_missing");

        var existingRevokedIds = await db.Licenses.AsNoTracking()
            .Where(item => item.Status == "Revoked")
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var revokedLicenseIds = existingRevokedIds
            .Append(license.Id)
            .Distinct()
            .Select(id => id.ToString("D"))
            .ToArray();

        var sequence = await sequenceAllocator.AllocateNextAsync(cancellationToken);
        var validity = TimeSpan.FromDays(options.Value.SnapshotValidityDays);
        var creation = await snapshotFactory.CreateAsync(
            sequence, revokedLicenseIds, validity, cancellationToken);
        if (!creation.Succeeded || creation.Envelope is null)
            return VendorLicenseRevocationResult.Unavailable("license_revocation_unavailable");

        string listId;
        try
        {
            using var payloadDocument = JsonDocument.Parse(creation.Envelope.PayloadJson);
            listId = payloadDocument.RootElement.GetProperty("listId").GetString()
                ?? throw new JsonException("Missing revocation list ID.");
            if (payloadDocument.RootElement.GetProperty("sequence").GetInt64() != sequence)
                return VendorLicenseRevocationResult.Conflict("offline_revocation_snapshot_sequence_mismatch");
        }
        catch (JsonException)
        {
            return VendorLicenseRevocationResult.Unavailable("license_revocation_unavailable");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var payloadBytes = Encoding.UTF8.GetBytes(creation.Envelope.PayloadJson);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

        db.Entry(license).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        license.Status = "Revoked";

        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();

        db.OfflineRevocationSnapshots.Add(new VendorOfflineRevocationSnapshot
        {
            Id = Guid.NewGuid(),
            Sequence = sequence,
            ListId = listId,
            KeyId = creation.Envelope.KeyId,
            Algorithm = creation.Envelope.Algorithm,
            EnvelopeJson = JsonSerializer.Serialize(creation.Envelope),
            PayloadSha256 = payloadHash,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(validity),
            CreatedAtUtc = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorLicenseRevocationResult.Conflict("stale_approval_or_resource_version");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return VendorLicenseRevocationResult.Conflict("offline_revocation_sequence_conflict");
        }

        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.approved", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.license.revoked", nameof(VendorLicense),
            license.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.offline-revocation.snapshot-created",
            nameof(VendorOfflineRevocationSnapshot), listId, correlationId), cancellationToken);

        return VendorLicenseRevocationResult.Success(sequence);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed record VendorLicenseRevocationResult(bool Succeeded, int StatusCode, string? ErrorCode, long? Sequence)
{
    public static VendorLicenseRevocationResult Success(long sequence) =>
        new(true, StatusCodes.Status200OK, null, sequence);
    public static VendorLicenseRevocationResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code, null);
    public static VendorLicenseRevocationResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code, null);
    public static VendorLicenseRevocationResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, code, null);
}
