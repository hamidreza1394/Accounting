using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Licensing.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Performs license issuance inside the caller's Vendor database transaction. Signing is fail-closed:
/// this service never commits an approval or sequence unless the signed envelope and audit events persist.
/// </summary>
public sealed class VendorLicenseIssuanceService(
    VendorDbContext db,
    VendorLicenseEnvelopeFactory envelopeFactory,
    IVendorAuditWriter audit,
    TimeProvider timeProvider)
{
    public async Task<VendorLicenseIssuanceResult> IssueApprovedAsync(
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
            throw new InvalidOperationException("License issuance must run inside the approval transaction.");

        if (approval.ResourceType != "license" || approval.TargetStatus != "Issued" ||
            approval.Status != "Pending" || approval.RequestedByUserId == reviewerUserId ||
            approval.ResourceId == Guid.Empty || expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return VendorLicenseIssuanceResult.Conflict("stale_approval_version");

        var license = await db.Licenses.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (license is null)
            return VendorLicenseIssuanceResult.NotFound("license_not_found");

        if (!license.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) ||
            license.Status != "Draft")
            return VendorLicenseIssuanceResult.Conflict("approval_resource_stale");

        var installationId = license.InstallationId;
        if (installationId is null)
            return VendorLicenseIssuanceResult.Conflict("license_installation_mismatch");

        var installation = await db.Installations.SingleOrDefaultAsync(
            item => item.Id == installationId.Value, cancellationToken);
        if (installation is null)
            return VendorLicenseIssuanceResult.Conflict("license_installation_mismatch");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (!VendorLicensePayloadBuilder.TryBuild(license, installation, now, out var payload, out var payloadError) ||
            payload is null)
            return VendorLicenseIssuanceResult.Conflict(payloadError ?? "license_metadata_invalid");

        var creation = await envelopeFactory.CreateAsync(payload, cancellationToken);
        if (!creation.Succeeded || creation.Envelope is null)
        {
            var errorCode = creation.ErrorCode is
                "signing_provider_unavailable" or "signing_failed" or "signing_provider_failed"
                ? creation.ErrorCode
                : "license_signing_failed";
            return VendorLicenseIssuanceResult.Unavailable(errorCode);
        }

        var envelope = creation.Envelope;
        var payloadBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var envelopeJson = JsonSerializer.Serialize(envelope);

        db.Entry(license).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        license.LastIssuedSequence = payload.Sequence;
        license.Status = "Issued";

        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();

        db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
        {
            Id = Guid.NewGuid(),
            LicenseId = license.Id,
            Sequence = payload.Sequence,
            KeyId = envelope.Kid,
            Algorithm = envelope.Algorithm,
            EnvelopeJson = envelopeJson,
            PayloadSha256 = payloadHash,
            CreatedAtUtc = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorLicenseIssuanceResult.Conflict("stale_approval_or_resource_version");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return VendorLicenseIssuanceResult.Conflict("license_sequence_already_issued");
        }

        // VendorAuditWriter participates in the current transaction. If either audit append fails,
        // the caller's transaction is not committed and envelope, sequence, status and approval roll back.
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.approved", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.license.issued", nameof(VendorLicense),
            license.Id.ToString("D"), correlationId), cancellationToken);

        return VendorLicenseIssuanceResult.Success();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed record VendorLicenseIssuanceResult(bool Succeeded, int StatusCode, string? ErrorCode)
{
    public static VendorLicenseIssuanceResult Success() =>
        new(true, StatusCodes.Status200OK, null);
    public static VendorLicenseIssuanceResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);
    public static VendorLicenseIssuanceResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code);
    public static VendorLicenseIssuanceResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, code);
}
