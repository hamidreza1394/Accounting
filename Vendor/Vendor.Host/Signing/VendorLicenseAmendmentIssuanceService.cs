using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Host.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Issues an approved license amendment inside the caller's Vendor database transaction. The
/// candidate payload is always rebuilt by the official amendment builder from the latest
/// signature-verified envelope (never from stored drafts or purchase metadata), signed by the
/// existing fail-closed license signer and persisted as the next sequence of the same license
/// and LicenseId. Payment state is never read here: issuance follows the approval, not the
/// payment (D-108), and nothing in this service records, verifies or allocates payments.
/// </summary>
public sealed class VendorLicenseAmendmentIssuanceService(
    VendorDbContext db,
    VendorLicenseEnvelopeFactory envelopeFactory,
    LicenseSigningTrustStore trustStore,
    IVendorAuditWriter audit,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VendorLicenseAmendmentIssuanceResult> IssueApprovedAsync(
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
            throw new InvalidOperationException("License amendment issuance must run inside the approval transaction.");

        if (approval.ResourceType != "licenseAmendment" || approval.TargetStatus != "Issued" ||
            approval.Status != "Pending" ||
            approval.ResourceId == Guid.Empty || expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return VendorLicenseAmendmentIssuanceResult.Conflict("stale_approval_version");

        var request = await db.LicenseAmendmentRequests.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (request is null)
            return VendorLicenseAmendmentIssuanceResult.NotFound("amendment_request_not_found");
        if (request.Status != VendorLicenseAmendmentRequest.StatusPendingApproval ||
            !request.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion))
            return VendorLicenseAmendmentIssuanceResult.Conflict("approval_resource_stale");

        var license = await db.Licenses.SingleOrDefaultAsync(
            item => item.Id == request.LicenseId, cancellationToken);
        if (license is null)
            return VendorLicenseAmendmentIssuanceResult.NotFound("license_not_found");
        if (license.CustomerId != request.CustomerId)
            return VendorLicenseAmendmentIssuanceResult.Conflict("license_installation_mismatch");

        var customer = await db.Customers.SingleOrDefaultAsync(
            item => item.Id == request.CustomerId, cancellationToken);
        var installation = license.InstallationId is { } installationId
            ? await db.Installations.SingleOrDefaultAsync(item => item.Id == installationId, cancellationToken)
            : null;
        if (customer is null || installation is null)
            return VendorLicenseAmendmentIssuanceResult.Conflict("license_installation_mismatch");

        var now = timeProvider.GetUtcNow().ToUniversalTime();

        // The current payload comes only from the latest stored envelope after signature
        // verification against the deployment trust store (D-056); stored drafts are ignored.
        var currentPayloadResult = await TryVerifyLatestPayloadAsync(license, installation, cancellationToken);
        if (currentPayloadResult.Error is not null || currentPayloadResult.Payload is null)
            return new VendorLicenseAmendmentIssuanceResult(false, currentPayloadResult.StatusCode, currentPayloadResult.Error);

        var currentPayload = currentPayloadResult.Payload;
        LicensePayload? candidate;
        string? buildError;
        switch (request.Kind)
        {
            case VendorLicenseAmendmentRequest.KindMaxCompanies:
                candidate = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
                    currentPayload, license, installation, customer, request.RequestedMaxCompanies!.Value, now,
                    out var maxCompaniesCandidate, out var maxCompaniesError) ? maxCompaniesCandidate : null;
                buildError = maxCompaniesError;
                break;
            case VendorLicenseAmendmentRequest.KindSupportUpdates:
                candidate = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
                    currentPayload, license, installation, customer, request.RequestedSupportUntilUtc,
                    request.RequestedUpdatesUntilUtc, now, out var extensionCandidate, out var extensionError) ? extensionCandidate : null;
                buildError = extensionError;
                break;
            default:
                candidate = null;
                buildError = "invalid_amendment_kind";
                break;
        }
        if (candidate is null || buildError is not null)
            return VendorLicenseAmendmentIssuanceResult.Conflict(buildError ?? "amendment_payload_invalid");

        var creation = await envelopeFactory.CreateAsync(candidate, cancellationToken);
        if (!creation.Succeeded || creation.Envelope is null)
        {
            if (creation.ErrorCode is
                "signing_provider_unavailable" or "signing_failed" or "signing_provider_failed" or
                "signing_result_invalid" or "signed_envelope_invalid")
                return VendorLicenseAmendmentIssuanceResult.Unavailable("license_amendment_issuance_unavailable");

            return VendorLicenseAmendmentIssuanceResult.Conflict(creation.ErrorCode ?? "amendment_payload_invalid");
        }

        var envelope = creation.Envelope;
        var payloadBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var envelopeJson = JsonSerializer.Serialize(envelope);

        // The approval pins the amendment REQUEST row version (captured after PendingApproval);
        // the license row keeps its own optimistic-concurrency version: a concurrent issuance of
        // the same license still fails SaveChanges (stale) or the unique (LicenseId, Sequence).
        db.Entry(request).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        request.Status = VendorLicenseAmendmentRequest.StatusIssued;
        request.UpdatedAtUtc = now;
        request.ApprovalRequestId = approval.Id;
        license.LastIssuedSequence = candidate.Sequence;
        // Keep the license row in lockstep with the signed payload: the delivery resolver and
        // every future amendment builder compare license.MaxCompanies against the payload's
        // value, and a stale column would reject the very amendment just issued. Support and
        // update periods exist only in the signed payload, so there is nothing to mirror.
        if (request.Kind == VendorLicenseAmendmentRequest.KindMaxCompanies)
            license.MaxCompanies = candidate.Limits.MaxCompanies;

        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();

        db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
        {
            Id = Guid.NewGuid(),
            LicenseId = license.Id,
            Sequence = candidate.Sequence,
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
            return VendorLicenseAmendmentIssuanceResult.Conflict("stale_approval_or_resource_version");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return VendorLicenseAmendmentIssuanceResult.Conflict("license_sequence_already_issued");
        }

        // VendorAuditWriter participates in the current transaction: a failing audit append rolls
        // back the envelope, sequence, request status and approval decision together (fail-closed).
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.approved", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.amendment.issued", nameof(VendorLicenseAmendmentRequest),
            request.Id.ToString("D"), correlationId), cancellationToken);

        return VendorLicenseAmendmentIssuanceResult.Success(candidate.Sequence);
    }

    /// <summary>
    /// Applies an Approved=Rejected decision through the production decision path: marks the
    /// amendment request Rejected with a stable failure code and the approval Rejected in the
    /// caller's transaction, with RowVersion pinning and two independent audit events. Never
    /// signs or persists an envelope, and never touches payment state (D-108).
    /// </summary>
    public async Task<VendorLicenseAmendmentRejectionResult> RejectApprovedAsync(
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
            throw new InvalidOperationException("License amendment rejection must run inside the approval transaction.");

        if (approval.ResourceType != "licenseAmendment" ||
            approval.Status != "Pending" ||
            approval.ResourceId == Guid.Empty || expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return VendorLicenseAmendmentRejectionResult.Conflict("stale_approval_version");

        var request = await db.LicenseAmendmentRequests.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (request is null)
            return VendorLicenseAmendmentRejectionResult.NotFound("amendment_request_not_found");
        if (request.Status != VendorLicenseAmendmentRequest.StatusPendingApproval ||
            !request.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion))
            return VendorLicenseAmendmentRejectionResult.Conflict("approval_resource_stale");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        db.Entry(request).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        request.Status = VendorLicenseAmendmentRequest.StatusRejected;
        request.FailureCode = "amendment_rejected";
        request.UpdatedAtUtc = now;

        approval.Status = "Rejected";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();
        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorLicenseAmendmentRejectionResult.Conflict("stale_approval_or_resource_version");
        }

        // VendorAuditWriter participates in the current transaction: a failing audit append rolls
        // the request status and approval decision back together (fail-closed).
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.rejected", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.amendment.rejected", nameof(VendorLicenseAmendmentRequest),
            request.Id.ToString("D"), correlationId), cancellationToken);

        return VendorLicenseAmendmentRejectionResult.Success();
    }

    /// <summary>
    /// Read-only review support: rebuilds the candidate from the latest signature-verified
    /// envelope and returns its summary without persisting, auditing or advancing any state.
    /// </summary>
    public async Task<VendorLicenseAmendmentPreviewResult> PreviewAsync(
        VendorLicenseAmendmentRequest request, CancellationToken cancellationToken)
    {
        var license = await db.Licenses.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.LicenseId, cancellationToken);
        if (license is null)
            return VendorLicenseAmendmentPreviewResult.Conflict("license_not_found");
        if (license.CustomerId != request.CustomerId)
            return VendorLicenseAmendmentPreviewResult.Conflict("license_installation_mismatch");

        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken);
        var installation = license.InstallationId is { } installationId
            ? await db.Installations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == installationId, cancellationToken)
            : null;
        if (customer is null || installation is null)
            return VendorLicenseAmendmentPreviewResult.Conflict("license_installation_mismatch");

        var currentPayloadResult = await TryVerifyLatestPayloadAsync(license, installation, cancellationToken);
        if (currentPayloadResult.Error is not null || currentPayloadResult.Payload is null)
            return new VendorLicenseAmendmentPreviewResult(false, currentPayloadResult.StatusCode, currentPayloadResult.Error, null);

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        LicensePayload? candidate;
        string? buildError;
        switch (request.Kind)
        {
            case VendorLicenseAmendmentRequest.KindMaxCompanies:
                candidate = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
                    currentPayloadResult.Payload, license, installation, customer, request.RequestedMaxCompanies!.Value, now,
                    out var maxCompaniesCandidate, out var maxCompaniesError) ? maxCompaniesCandidate : null;
                buildError = maxCompaniesError;
                break;
            case VendorLicenseAmendmentRequest.KindSupportUpdates:
                candidate = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
                    currentPayloadResult.Payload, license, installation, customer, request.RequestedSupportUntilUtc,
                    request.RequestedUpdatesUntilUtc, now, out var extensionCandidate, out var extensionError) ? extensionCandidate : null;
                buildError = extensionError;
                break;
            default:
                candidate = null;
                buildError = "invalid_amendment_kind";
                break;
        }
        if (candidate is null || buildError is not null)
            return VendorLicenseAmendmentPreviewResult.Conflict(buildError ?? "amendment_payload_invalid");

        return VendorLicenseAmendmentPreviewResult.Success(candidate);
    }

    private async Task<(LicensePayload? Payload, int StatusCode, string? Error)> TryVerifyLatestPayloadAsync(
        VendorLicense license, VendorInstallation installation, CancellationToken cancellationToken)
    {
        var storedEnvelope = await db.SignedLicenseEnvelopes.AsNoTracking()
            .Where(item => item.LicenseId == license.Id)
            .OrderByDescending(item => item.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (storedEnvelope is null || storedEnvelope.Sequence != license.LastIssuedSequence)
            return (null, StatusCodes.Status409Conflict, "amendment_envelope_missing");

        LicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(storedEnvelope.EnvelopeJson, EnvelopeJsonOptions);
        }
        catch (JsonException)
        {
            return (null, StatusCodes.Status409Conflict, "amendment_envelope_invalid");
        }

        if (envelope is null ||
            envelope.FormatVersion != LicenseAlgorithms.CurrentFormatVersion ||
            !string.Equals(envelope.Kid, storedEnvelope.KeyId, StringComparison.Ordinal) ||
            !string.Equals(envelope.Algorithm, storedEnvelope.Algorithm, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(envelope.PayloadJson) ||
            !string.Equals(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant(),
                storedEnvelope.PayloadSha256, StringComparison.Ordinal))
            return (null, StatusCodes.Status409Conflict, "amendment_envelope_invalid");

        if (!trustStore.TryGetTrustedPublicKey(envelope.Kid, out var publicKey))
            return (null, StatusCodes.Status503ServiceUnavailable, "amendment_signing_key_untrusted");

        var validator = new LicenseEnvelopeValidator(
            new StaticTrustedKeyResolver([
                new TrustedPublicKey(envelope.Kid, envelope.Algorithm, publicKey.ToArray())
            ]),
            new EcdsaP256Sha256Verifier(),
            new LicenseEvaluator(timeProvider));
        var validation = validator.Validate(envelope, installation.Id.ToString("D"));
        if (!validation.Success || validation.Payload is null ||
            validation.Status is LicenseStatus.Grace or LicenseStatus.Restricted or LicenseStatus.Invalid)
            return (null, StatusCodes.Status409Conflict, "amendment_signature_or_payload_invalid");

        var payload = validation.Payload;
        if (payload.Sequence != storedEnvelope.Sequence ||
            !string.Equals(payload.LicenseId, license.Id.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(payload.InstallationId, installation.Id.ToString("D"), StringComparison.Ordinal))
            return (null, StatusCodes.Status409Conflict, "amendment_envelope_binding_invalid");

        return (payload, StatusCodes.Status200OK, null);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed record VendorLicenseAmendmentRejectionResult(bool Succeeded, int StatusCode, string? ErrorCode)
{
    public static VendorLicenseAmendmentRejectionResult Success() =>
        new(true, StatusCodes.Status200OK, null);
    public static VendorLicenseAmendmentRejectionResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);
    public static VendorLicenseAmendmentRejectionResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code);
}

public sealed record VendorLicenseAmendmentIssuanceResult(bool Succeeded, int StatusCode, string? ErrorCode)
{
    public static VendorLicenseAmendmentIssuanceResult Success(int sequence) =>
        new(true, StatusCodes.Status200OK, null);
    public static VendorLicenseAmendmentIssuanceResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);
    public static VendorLicenseAmendmentIssuanceResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code);
    public static VendorLicenseAmendmentIssuanceResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, code);
}

public sealed record VendorLicenseAmendmentPreviewResult(
    bool Succeeded, int StatusCode, string? ErrorCode, VendorLicenseAmendmentPreviewSummary? Candidate)
{
    public static VendorLicenseAmendmentPreviewResult Success(LicensePayload candidate) =>
        new(true, StatusCodes.Status200OK, null, new VendorLicenseAmendmentPreviewSummary(
            candidate.Sequence, candidate.Limits.MaxCompanies, candidate.SupportUntilUtc,
            candidate.UpdatesUntilUtc, candidate.IssuedAtUtc));
    public static VendorLicenseAmendmentPreviewResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code, null);
}

public sealed record VendorLicenseAmendmentPreviewSummary(
    int Sequence, int? MaxCompanies, DateTimeOffset? SupportUntilUtc,
    DateTimeOffset? UpdatesUntilUtc, DateTimeOffset IssuedAtUtc);
