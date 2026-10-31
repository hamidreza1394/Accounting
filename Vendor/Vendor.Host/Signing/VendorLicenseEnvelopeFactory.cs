using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Creates a structurally valid signed envelope from a validated payload. This component deliberately
/// does not persist the envelope or mutate VendorLicense state; callers must do so transactionally
/// only after durable storage and approval semantics are implemented.
/// </summary>
public sealed class VendorLicenseEnvelopeFactory(ILicenseSigningProvider signingProvider)
{
    private readonly ILicenseSigningProvider _signingProvider =
        signingProvider ?? throw new ArgumentNullException(nameof(signingProvider));

    public async ValueTask<LicenseEnvelopeCreationResult> CreateAsync(
        LicensePayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var payloadErrors = StructuralValidation.ValidatePayload(payload);
        if (payloadErrors.Count > 0)
            return LicenseEnvelopeCreationResult.Failed("license_payload_invalid");

        string payloadJson;
        byte[] canonicalBytes;
        try
        {
            payloadJson = CanonicalJson.SerializePayload(payload);
            canonicalBytes = CanonicalJson.GetUtf8Bytes(payloadJson);
        }
        catch (ArgumentException)
        {
            return LicenseEnvelopeCreationResult.Failed("license_payload_invalid");
        }

        LicenseSigningResult signing;
        try
        {
            signing = await _signingProvider.SignAsync(canonicalBytes, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return LicenseEnvelopeCreationResult.Failed("signing_provider_failed");
        }

        if (!signing.Succeeded)
            return LicenseEnvelopeCreationResult.Failed(
                signing.ErrorCode is "signing_provider_unavailable" or "signing_failed"
                    ? signing.ErrorCode
                    : "signing_provider_failed");

        if (string.IsNullOrWhiteSpace(signing.KeyId) ||
            signing.KeyId.Length > 128 ||
            !LicenseAlgorithms.IsAllowed(signing.Algorithm) ||
            signing.Signature.Length != 64)
            return LicenseEnvelopeCreationResult.Failed("signing_result_invalid");

        var envelope = new LicenseEnvelope(
            FormatVersion: payload.FormatVersion,
            Kid: signing.KeyId,
            Algorithm: signing.Algorithm!,
            PayloadJson: payloadJson,
            SignatureBase64: Convert.ToBase64String(signing.Signature.Span));

        if (StructuralValidation.ValidateEnvelopeStructure(envelope).Count > 0)
            return LicenseEnvelopeCreationResult.Failed("signed_envelope_invalid");

        return LicenseEnvelopeCreationResult.Success(envelope);
    }
}

public sealed record LicenseEnvelopeCreationResult(
    bool Succeeded,
    LicenseEnvelope? Envelope,
    string? ErrorCode)
{
    public static LicenseEnvelopeCreationResult Success(LicenseEnvelope envelope) =>
        new(true, envelope, null);

    public static LicenseEnvelopeCreationResult Failed(string errorCode) =>
        new(false, null, errorCode);
}
