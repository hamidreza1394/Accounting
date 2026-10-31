using System.Text;
using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Builds a canonical signed offline-revocation envelope and verifies the returned signature
/// against the dedicated revocation trust set before the caller can persist or distribute it.
/// </summary>
public sealed class VendorOfflineRevocationEnvelopeFactory(
    IOfflineRevocationSigningProvider signingProvider,
    IOfflineRevocationKeyResolver keyResolver,
    TimeProvider timeProvider)
{
    private readonly IOfflineRevocationSigningProvider _signingProvider =
        signingProvider ?? throw new ArgumentNullException(nameof(signingProvider));
    private readonly IOfflineRevocationKeyResolver _keyResolver =
        keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async ValueTask<OfflineRevocationEnvelopeCreationResult> CreateAsync(
        OfflineRevocationList payload,
        long minimumSequenceExclusive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var now = _timeProvider.GetUtcNow().ToUniversalTime();
        if (minimumSequenceExclusive < 0 ||
            payload.SchemaVersion != OfflineRevocationListValidator.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(payload.ListId) ||
            payload.ListId.Length > OfflineRevocationListValidator.MaxListIdLength ||
            payload.Sequence <= 0 ||
            payload.IssuedAtUtc == default ||
            payload.ExpiresAtUtc == default ||
            payload.IssuedAtUtc > now.AddMinutes(2) ||
            payload.ExpiresAtUtc <= now ||
            payload.ExpiresAtUtc <= payload.IssuedAtUtc ||
            payload.ExpiresAtUtc - payload.IssuedAtUtc > TimeSpan.FromDays(OfflineRevocationListValidator.MaxValidityDays) ||
            payload.RevokedLicenseIds is null ||
            payload.RevokedLicenseIds.Count > OfflineRevocationListValidator.MaxRevokedLicenseIds ||
            !AreLicenseIdsCanonical(payload.RevokedLicenseIds))
        {
            return OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_payload_invalid");
        }

        if (payload.Sequence <= minimumSequenceExclusive)
        {
            return OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_sequence_invalid");
        }

        string payloadJson;
        byte[] canonicalPayload;
        try
        {
            payloadJson = OfflineRevocationListValidator.Serialize(payload);
            canonicalPayload = Encoding.UTF8.GetBytes(payloadJson);
        }
        catch (ArgumentException)
        {
            return OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_payload_invalid");
        }

        if (payloadJson.Length > OfflineRevocationListValidator.MaxPayloadJsonChars)
        {
            return OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_payload_invalid");
        }

        LicenseSigningResult signing;
        try
        {
            signing = await _signingProvider.SignAsync(canonicalPayload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return OfflineRevocationEnvelopeCreationResult.Unavailable("offline_revocation_signing_unavailable");
        }

        if (!signing.Succeeded)
        {
            return signing.ErrorCode is "signing_provider_unavailable" or "signing_failed"
                ? OfflineRevocationEnvelopeCreationResult.Unavailable("offline_revocation_signing_unavailable")
                : OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_signing_failed");
        }

        if (string.IsNullOrWhiteSpace(signing.KeyId) ||
            signing.KeyId.Length > OfflineRevocationListValidator.MaxKeyIdLength ||
            !string.Equals(signing.Algorithm, LicenseAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal) ||
            signing.Signature.Length != 64)
        {
            return OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_signing_result_invalid");
        }

        if (!_keyResolver.TryResolve(signing.KeyId, out var trustedPublicKey) || trustedPublicKey.IsEmpty)
        {
            return OfflineRevocationEnvelopeCreationResult.Failed("offline_revocation_signing_key_untrusted");
        }

        var envelope = new OfflineRevocationListEnvelope(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            signing.KeyId,
            signing.Algorithm,
            payloadJson,
            Convert.ToBase64String(signing.Signature.Span));

        if (!OfflineRevocationListValidator.TryValidateAndVerify(
                envelope,
                trustedPublicKey.Span,
                now,
                minimumSequenceExclusive,
                out _,
                out var verificationError))
        {
            return OfflineRevocationEnvelopeCreationResult.Failed(verificationError);
        }

        return OfflineRevocationEnvelopeCreationResult.Success(envelope);
    }

    private static bool AreLicenseIdsCanonical(IReadOnlyList<string> ids)
    {
        string? previous = null;
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                id.Length > OfflineRevocationListValidator.MaxLicenseIdLength ||
                id.Any(char.IsControl) ||
                (previous is not null && string.Compare(previous, id, StringComparison.Ordinal) >= 0))
            {
                return false;
            }

            previous = id;
        }

        return true;
    }
}

public sealed record OfflineRevocationEnvelopeCreationResult(
    bool Succeeded,
    OfflineRevocationListEnvelope? Envelope,
    string? ErrorCode,
    int StatusCode)
{
    public static OfflineRevocationEnvelopeCreationResult Success(OfflineRevocationListEnvelope envelope) =>
        new(true, envelope, null, StatusCodes.Status200OK);

    public static OfflineRevocationEnvelopeCreationResult Failed(string errorCode) =>
        new(false, null, errorCode, StatusCodes.Status409Conflict);

    public static OfflineRevocationEnvelopeCreationResult Unavailable(string errorCode) =>
        new(false, null, errorCode, StatusCodes.Status503ServiceUnavailable);
}
