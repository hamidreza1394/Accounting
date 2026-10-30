using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Signs a reviewed provisioning request and persists its delivery envelope atomically with the
/// two-person approval decision. The configured signer remains fail-closed when unavailable.
/// </summary>
public sealed class VendorProvisioningIssuanceService(
    VendorDbContext db,
    IProvisioningSigningProvider signingProvider,
    ProvisioningPackageSigningTrustStore signingTrustStore,
    IVendorAuditWriter audit,
    VendorProvisioningLicenseResolver licenseResolver,
    TimeProvider timeProvider,
    ILogger<VendorProvisioningIssuanceService> logger)
{
    public async Task<VendorProvisioningIssuanceResult> IssueApprovedAsync(
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
            throw new InvalidOperationException("Provisioning issuance must run inside the approval transaction.");

        if (approval.ResourceType != "provisioning" ||
            approval.TargetStatus != "PendingDelivery" ||
            approval.Status != "Pending" ||
            approval.RequestedByUserId == reviewerUserId ||
            approval.ResourceId == Guid.Empty ||
            expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return VendorProvisioningIssuanceResult.Conflict("stale_approval_version");

        var request = await db.ProvisioningRequests.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (request is null)
            return VendorProvisioningIssuanceResult.NotFound("provisioning_request_not_found");

        if (!request.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) ||
            request.Status != "AwaitingSigner")
            return VendorProvisioningIssuanceResult.Conflict("provisioning_request_stale");

        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken);
        var installation = await db.Installations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.InstallationId, cancellationToken);
        if (customer is null || installation is null || installation.CustomerId != customer.Id)
            return VendorProvisioningIssuanceResult.Conflict("provisioning_installation_mismatch");
        if (!string.Equals(customer.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(installation.Status, "Active", StringComparison.Ordinal))
            return VendorProvisioningIssuanceResult.Conflict("provisioning_target_inactive");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (!CustomerProvisioningPackageValidator.TryValidatePayload(
                request.PayloadJson, now, out var payload, out _) ||
            payload is null ||
            !string.Equals(payload.RequestId, request.RequestId, StringComparison.Ordinal) ||
            !string.Equals(payload.Nonce, request.Nonce, StringComparison.Ordinal) ||
            !string.Equals(payload.CustomerReference, customer.CustomerNumber, StringComparison.Ordinal) ||
            !string.Equals(payload.InstallationId, installation.InstallationCode, StringComparison.Ordinal))
            return VendorProvisioningIssuanceResult.Conflict("provisioning_payload_invalid");

        var payloadBytes = Encoding.UTF8.GetBytes(request.PayloadJson);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        if (!string.Equals(payloadHash, request.PayloadSha256, StringComparison.Ordinal))
            return VendorProvisioningIssuanceResult.Conflict("provisioning_payload_hash_mismatch");

        if (payload.SchemaVersion != CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion ||
            !Guid.TryParseExact(payload.LicenseId, "D", out var licenseId) ||
            payload.Sequence is not int licenseSequence)
            return VendorProvisioningIssuanceResult.Conflict("provisioning_license_binding_required");

        var licenseBinding = await licenseResolver.ResolveLatestAsync(
            licenseId, customer, installation, payload.Subsystems, cancellationToken);
        if (!licenseBinding.Succeeded)
            return new VendorProvisioningIssuanceResult(false, licenseBinding.StatusCode, licenseBinding.ErrorCode);
        if (!VendorProvisioningLicenseBindingRules.MatchesCurrentBinding(payload, licenseBinding))
            return VendorProvisioningIssuanceResult.Conflict("provisioning_license_sequence_stale");

        if (string.Equals(payload.CompanyType, "subsidiary", StringComparison.Ordinal) &&
            !await VendorProvisioningParentReferenceRules.IsEligibleParentAsync(
                db,
                payload.ParentCompanyReference,
                customer.Id,
                installation.Id,
                Guid.Parse(request.RequestId),
                customer.CustomerNumber,
                installation.InstallationCode,
                cancellationToken))
        {
            return VendorProvisioningIssuanceResult.Conflict("provisioning_parent_reference_invalid");
        }

        LicenseSigningResult signing;
        try
        {
            signing = await ProvisioningSigningExecution.SignWithTimeoutAsync(
                signingProvider,
                payloadBytes,
                TimeSpan.FromSeconds(15),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not log the provider exception, message, payload, key ID or signature.
            // The exception type is enough to diagnose the failure without exposing signer data.
            logger.LogError(
                "Provisioning signing provider failed with exception type {ExceptionType}.",
                exception.GetType().Name);
            return VendorProvisioningIssuanceResult.Unavailable("provisioning_signing_unavailable");
        }

        if (!signing.Succeeded)
        {
            logger.LogWarning(
                "Provisioning signing provider returned an unsuccessful result; issuance remains unavailable.");
            return VendorProvisioningIssuanceResult.Unavailable("provisioning_signing_unavailable");
        }

        if (string.IsNullOrWhiteSpace(signing.KeyId) ||
            signing.KeyId.Length > 128 ||
            !string.Equals(signing.Algorithm, LicenseAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal) ||
            signing.Signature.Length != 64)
        {
            logger.LogWarning(
                "Provisioning signing provider returned invalid signing metadata; issuance remains unavailable.");
            return VendorProvisioningIssuanceResult.Unavailable("provisioning_signing_unavailable");
        }

        if (!signingTrustStore.TryGetTrustedPublicKey(signing.KeyId, out var trustedPublicKey))
        {
            logger.LogWarning(
                "Provisioning signing provider returned a key ID outside the configured trust set; issuance remains unavailable.");
            return VendorProvisioningIssuanceResult.Unavailable("provisioning_signing_unavailable");
        }

        var signedPackage = new CustomerProvisioningPackage(
            CustomerProvisioningPackageValidator.CurrentFormatVersion,
            signing.KeyId,
            signing.Algorithm!,
            request.PayloadJson,
            Convert.ToBase64String(signing.Signature.Span));
        if (!CustomerProvisioningPackageValidator.TryValidateAndVerify(
                signedPackage,
                trustedPublicKey.Span,
                now,
                out _,
                out _))
        {
            logger.LogWarning(
                "Provisioning signing provider returned a signature that does not verify against the configured trust key; issuance remains unavailable.");
            return VendorProvisioningIssuanceResult.Unavailable("provisioning_signing_unavailable");
        }

        request.KeyId = signing.KeyId;
        request.Algorithm = signing.Algorithm;
        request.SignatureBase64 = Convert.ToBase64String(signing.Signature.Span);
        request.Status = "PendingDelivery";
        request.SignedAtUtc = now;
        request.UpdatedAtUtc = now;
        request.FailureCode = null;

        db.Entry(request).Property(item => item.RowVersion).OriginalValue = approval.ExpectedResourceRowVersion;
        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorProvisioningIssuanceResult.Conflict("stale_approval_or_provisioning_version");
        }

        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.approved", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.provisioning.signed", nameof(VendorProvisioningRequest),
            request.Id.ToString("D"), correlationId), cancellationToken);

        return VendorProvisioningIssuanceResult.Success();
    }
}

public sealed record VendorProvisioningIssuanceResult(bool Succeeded, int StatusCode, string? ErrorCode)
{
    public static VendorProvisioningIssuanceResult Success() =>
        new(true, StatusCodes.Status200OK, null);

    public static VendorProvisioningIssuanceResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);

    public static VendorProvisioningIssuanceResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code);

    public static VendorProvisioningIssuanceResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, code);
}
