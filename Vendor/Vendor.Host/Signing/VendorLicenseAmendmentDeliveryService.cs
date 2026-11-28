using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Records the operator delivery (export) of an issued amendment envelope inside the caller's
/// Vendor database transaction. The delivered bytes are always the stored envelope of the
/// request's own <c>IssuedSequence</c> — re-verified against the recorded payload hash and key
/// metadata before anything is stamped. The first delivery generates a per-delivery nonce and
/// moves Issued → Delivered; re-exports return the SAME bytes and nonce without re-stamping.
/// Applied is never set here: only a valid signed customer acknowledgement can advance the state
/// past Delivered (S-B owner decision, #393).
/// </summary>
public sealed class VendorLicenseAmendmentDeliveryService(
    VendorDbContext db,
    IVendorAuditWriter audit,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VendorLicenseAmendmentDeliveryResult> DeliverAsync(
        VendorLicenseAmendmentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("A correlation id is required for amendment delivery auditing.", nameof(correlationId));

        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Amendment delivery must run inside the caller's transaction.");

        if (request.IssuedSequence is not int issuedSequence)
            return VendorLicenseAmendmentDeliveryResult.Conflict("amendment_envelope_missing");

        var envelopeRow = await db.SignedLicenseEnvelopes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.LicenseId == request.LicenseId && item.Sequence == issuedSequence,
                cancellationToken);
        var envelope = envelopeRow is null ? null : ParseStoredEnvelope(envelopeRow.EnvelopeJson);
        if (envelopeRow is null || envelope is null)
            return VendorLicenseAmendmentDeliveryResult.Conflict("amendment_envelope_missing");

        // Stored-envelope integrity: the payload hash and key metadata recorded at issuance must
        // still describe the stored bytes, exactly like the issuance verifier requires.
        if (!string.Equals(envelope.Kid, envelopeRow.KeyId, StringComparison.Ordinal) ||
            !string.Equals(envelope.Algorithm, envelopeRow.Algorithm, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(envelope.PayloadJson) ||
            !string.Equals(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant(),
                envelopeRow.PayloadSha256, StringComparison.Ordinal))
            return VendorLicenseAmendmentDeliveryResult.Conflict("amendment_envelope_invalid");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        string nonce;
        bool repeated;
        if (VendorLicenseAmendmentAcknowledgementRules.CanDeliver(request.Status))
        {
            nonce = VendorLicenseAmendmentAcknowledgementRules.NewDeliveryNonce();
            request.Status = VendorLicenseAmendmentRequest.StatusDelivered;
            request.DeliveredNonce = nonce;
            request.DeliveredPayloadSha256 = envelopeRow.PayloadSha256;
            request.DeliveredAtUtc = now;
            request.UpdatedAtUtc = now;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return VendorLicenseAmendmentDeliveryResult.Conflict("stale_row_version");
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actorUserId, "vendor.amendment.delivered", nameof(VendorLicenseAmendmentRequest),
                request.Id.ToString("D"), correlationId), cancellationToken);
            repeated = false;
        }
        else if (VendorLicenseAmendmentAcknowledgementRules.CanReExport(request.Status))
        {
            // Re-export of the SAME bytes and nonce: the customer import of an identical envelope
            // is idempotent, so delivery metadata is never re-stamped or re-generated here.
            if (!VendorLicenseAmendmentAcknowledgementRules.IsDeliveryNonceShape(request.DeliveredNonce) ||
                string.IsNullOrWhiteSpace(request.DeliveredPayloadSha256))
                return VendorLicenseAmendmentDeliveryResult.Conflict("amendment_delivery_metadata_invalid");

            nonce = request.DeliveredNonce;
            repeated = true;
            await audit.AppendAsync(new VendorAuditWriteRequest(
                actorUserId, "vendor.amendment.delivery-retried", nameof(VendorLicenseAmendmentRequest),
                request.Id.ToString("D"), correlationId), cancellationToken);
        }
        else
        {
            return VendorLicenseAmendmentDeliveryResult.Conflict("amendment_not_deliverable");
        }

        return VendorLicenseAmendmentDeliveryResult.Success(new VendorLicenseAmendmentDeliveryResponse(
            request.Id, request.Status, repeated, nonce, envelopeRow.PayloadSha256, issuedSequence,
            envelope.FormatVersion, envelope.Kid, envelope.Algorithm, envelope.PayloadJson,
            envelope.SignatureBase64));
    }

    private static LicenseEnvelope? ParseStoredEnvelope(string envelopeJson)
    {
        LicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(envelopeJson, EnvelopeJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        return envelope is not null &&
               !string.IsNullOrWhiteSpace(envelope.Kid) &&
               !string.IsNullOrWhiteSpace(envelope.Algorithm) &&
               !string.IsNullOrWhiteSpace(envelope.PayloadJson) &&
               !string.IsNullOrWhiteSpace(envelope.SignatureBase64)
            ? envelope
            : null;
    }
}

public sealed record VendorLicenseAmendmentDeliveryResult(bool Succeeded, int StatusCode, string? ErrorCode,
    VendorLicenseAmendmentDeliveryResponse? Delivery)
{
    public static VendorLicenseAmendmentDeliveryResult Success(VendorLicenseAmendmentDeliveryResponse delivery) =>
        new(true, StatusCodes.Status200OK, null, delivery);
    public static VendorLicenseAmendmentDeliveryResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code, null);
}

/// <summary>
/// The delivered amendment packet: the exact stored envelope bytes, the per-delivery nonce and
/// the delivered payload hash. Re-exports return identical values (idempotent re-delivery).
/// </summary>
public sealed record VendorLicenseAmendmentDeliveryResponse(
    Guid RequestId, string Status, bool Repeated, string Nonce, string PayloadSha256, int Sequence,
    int FormatVersion, string KeyId, string Algorithm, string PayloadJson, string SignatureBase64);
