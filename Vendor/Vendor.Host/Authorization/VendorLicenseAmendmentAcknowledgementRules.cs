using System.Security.Cryptography;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Pure delivery/acknowledgement rules for explicit license amendment requests (#393, S-B owner
/// decision of 2026-11-24). Delivery records that the exact issued envelope bytes were exported
/// to the customer with a per-delivery nonce; <c>Applied</c> is set only by a valid signed
/// customer acknowledgement, never by delivery itself. Only the first acknowledgement is
/// accepted; exact signed retries are idempotent and a different signed digest conflicts.
/// The rules never sign, persist, export or change payment state.
/// </summary>
public static class VendorLicenseAmendmentAcknowledgementRules
{
    /// <summary>First delivery stamps the row and moves Issued → Delivered.</summary>
    public static bool CanDeliver(string status) =>
        string.Equals(status, Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusIssued,
            StringComparison.Ordinal);

    /// <summary>
    /// Re-exporting the SAME envelope bytes and nonce is always safe (the customer import of an
    /// identical envelope is idempotent); it never re-stamps delivery metadata or re-generates
    /// the nonce.
    /// </summary>
    public static bool CanReExport(string status) =>
        string.Equals(status, Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusDelivered,
            StringComparison.Ordinal) ||
        string.Equals(status, Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusApplied,
            StringComparison.Ordinal);

    /// <summary>Only a delivered amendment (with delivery metadata) may receive an acknowledgement.</summary>
    public static bool CanAcceptAcknowledgement(
        string status,
        DateTimeOffset? deliveredAtUtc,
        string? deliveredNonce,
        string? deliveredPayloadSha256) =>
        string.Equals(status, Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusDelivered,
            StringComparison.Ordinal) &&
        deliveredAtUtc is not null &&
        !string.IsNullOrWhiteSpace(deliveredNonce) &&
        !string.IsNullOrWhiteSpace(deliveredPayloadSha256);

    /// <summary>64 lowercase hex characters (RandomNumberGenerator.GetHexString(32)).</summary>
    public static bool IsDeliveryNonceShape(string? nonce) =>
        nonce is { Length: 64 } && nonce.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>
    /// Maps a validated customer acknowledgement outcome to the terminal amendment state.
    /// AlreadyApplied is deliberately idempotent (the amendment is in force); a customer
    /// Rejected outcome is a terminal rejection with a stable failure code. Transient delivery
    /// problems are never represented here — delivery failure is not customer rejection.
    /// </summary>
    public static bool TryResolveOutcome(
        string? result,
        string? errorCode,
        out string status,
        out string? failureCode)
    {
        status = string.Empty;
        failureCode = null;

        switch (result)
        {
            case "Applied":
            case "AlreadyApplied":
                if (errorCode is not null)
                {
                    return false;
                }
                status = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusApplied;
                return true;
            case "Rejected":
                // Persist only stable, non-sensitive outcome codes emitted by the customer
                // consumer. Never store free-form exception text, connection details or PII.
                if (!IsAllowedRejectionCode(errorCode))
                {
                    return false;
                }
                status = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusRejected;
                failureCode = "amendment_ack_rejected";
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Rejection codes a trusted customer consumer can legitimately emit while refusing to
    /// apply a license amendment (stable import policy outcomes, never raw exception text).
    /// Unknown codes fail closed: the acknowledgement is rejected and no state advances.
    /// </summary>
    private static bool IsAllowedRejectionCode(string? errorCode) =>
        errorCode is "license_validation_failed"
            or "license_status_policy_unapproved"
            or "license_id_change_requires_reactivation"
            or "license_sequence_replay_or_downgrade"
            or "license_import_conflict"
            or "license_import_concurrency_conflict";

    /// <summary>Generates the per-delivery nonce (256 bits of entropy, 64 lowercase hex chars).</summary>
    public static string NewDeliveryNonce() => RandomNumberGenerator.GetHexString(64, lowercase: true);
}
