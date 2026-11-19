using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Applies a payment verification decision inside the caller's approval transaction (D-108 §2-5):
/// payment status, reviewer identity, approval decision and audit events commit together or not
/// at all. Payment verification never touches license issuance state, and vice versa.
/// </summary>
public sealed class VendorPaymentVerificationService(
    VendorDbContext db,
    IVendorAuditWriter audit,
    TimeProvider timeProvider)
{
    public async Task<VendorPaymentVerificationResult> DecideApprovedAsync(
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
            throw new InvalidOperationException("Payment verification must run inside the approval transaction.");

        if (approval.ResourceType != "salesOrderPayment" ||
            approval.Status != "Pending" ||
            approval.ResourceId == Guid.Empty || expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return VendorPaymentVerificationResult.Conflict("stale_approval_version");

        if (approval.TargetStatus is not (VendorPaymentRecord.StatusVerified or VendorPaymentRecord.StatusRejected))
            return VendorPaymentVerificationResult.Conflict("unsupported_payment_approval_target");

        var payment = await db.PaymentRecords.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (payment is null)
            return VendorPaymentVerificationResult.NotFound("payment_not_found");

        if (!payment.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) ||
            payment.Status != VendorPaymentRecord.StatusRecorded)
            return VendorPaymentVerificationResult.Conflict("approval_resource_stale");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        payment.Status = approval.TargetStatus;
        payment.VerifiedByUserId = reviewerUserId;
        payment.VerifiedAtUtc = now;
        if (approval.TargetStatus == VendorPaymentRecord.StatusRejected &&
            !string.IsNullOrWhiteSpace(reviewComment))
        {
            payment.RejectionReason = reviewComment.Trim();
        }

        db.Entry(payment).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment)
            ? null
            : reviewComment.Trim();
        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorPaymentVerificationResult.Conflict("stale_approval_or_payment_version");
        }

        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.approved", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId,
            approval.TargetStatus == VendorPaymentRecord.StatusVerified
                ? "vendor.payment.verified"
                : "vendor.payment.rejected",
            nameof(VendorPaymentRecord),
            payment.Id.ToString("D"), correlationId), cancellationToken);

        return VendorPaymentVerificationResult.Success();
    }
}

public sealed record VendorPaymentVerificationResult(bool Succeeded, int StatusCode, string? ErrorCode)
{
    public static VendorPaymentVerificationResult Success() => new(true, StatusCodes.Status200OK, null);
    public static VendorPaymentVerificationResult Conflict(string errorCode) => new(false, StatusCodes.Status409Conflict, errorCode);
    public static VendorPaymentVerificationResult NotFound(string errorCode) => new(false, StatusCodes.Status404NotFound, errorCode);
}
