using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Creates canonical, signed offline-revocation envelopes. Persistence, sequence allocation and
/// approval remain the responsibility of the transactional Vendor workflow.
/// </summary>
public sealed class VendorOfflineRevocationSnapshotFactory(
    IOfflineRevocationSigningProvider signingProvider,
    TimeProvider timeProvider)
{
    private readonly IOfflineRevocationSigningProvider _signingProvider =
        signingProvider ?? throw new ArgumentNullException(nameof(signingProvider));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async ValueTask<VendorOfflineRevocationSnapshotCreationResult> CreateAsync(
        long sequence,
        IReadOnlyCollection<string>? revokedLicenseIds,
        TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        if (sequence <= 0)
            return VendorOfflineRevocationSnapshotCreationResult.Invalid("offline_revocation_sequence_invalid");

        if (revokedLicenseIds is null ||
            revokedLicenseIds.Count > OfflineRevocationListValidator.MaxRevokedLicenseIds)
            return VendorOfflineRevocationSnapshotCreationResult.Invalid("offline_revocation_payload_invalid");

        if (validity <= TimeSpan.Zero ||
            validity > TimeSpan.FromDays(OfflineRevocationListValidator.MaxValidityDays))
            return VendorOfflineRevocationSnapshotCreationResult.Invalid("offline_revocation_validity_invalid");

        var canonicalIds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var licenseId in revokedLicenseIds)
        {
            if (string.IsNullOrWhiteSpace(licenseId) ||
                licenseId.Length > OfflineRevocationListValidator.MaxLicenseIdLength ||
                licenseId.Any(char.IsControl))
            {
                return VendorOfflineRevocationSnapshotCreationResult.Invalid(
                    "offline_revocation_license_id_invalid");
            }

            canonicalIds.Add(licenseId);
        }

        var issuedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        var expiresAtUtc = issuedAtUtc.Add(validity);
        var payload = new OfflineRevocationList(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            Guid.NewGuid().ToString("N"),
            sequence,
            issuedAtUtc,
            expiresAtUtc,
            canonicalIds.ToArray());
        var payloadJson = OfflineRevocationListValidator.Serialize(payload);
        if (payloadJson.Length > OfflineRevocationListValidator.MaxPayloadJsonChars)
        {
            return VendorOfflineRevocationSnapshotCreationResult.Invalid(
                "offline_revocation_payload_invalid");
        }

        LicenseSigningResult signing;
        try
        {
            signing = await _signingProvider.SignAsync(
                Encoding.UTF8.GetBytes(payloadJson), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException and not AccessViolationException)
        {
            return VendorOfflineRevocationSnapshotCreationResult.Unavailable(
                "offline_revocation_signing_unavailable");
        }

        if (!signing.Succeeded ||
            string.IsNullOrWhiteSpace(signing.KeyId) ||
            signing.KeyId.Length > OfflineRevocationListValidator.MaxKeyIdLength ||
            !string.Equals(signing.Algorithm, LicenseAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal) ||
            signing.Signature.Length != 64)
        {
            return VendorOfflineRevocationSnapshotCreationResult.Unavailable(
                "offline_revocation_signing_unavailable");
        }

        var envelope = new OfflineRevocationListEnvelope(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            signing.KeyId,
            signing.Algorithm!,
            payloadJson,
            Convert.ToBase64String(signing.Signature.Span));
        return VendorOfflineRevocationSnapshotCreationResult.Success(envelope);
    }
}

public sealed record VendorOfflineRevocationSnapshotCreationResult(
    bool Succeeded,
    OfflineRevocationListEnvelope? Envelope,
    string? ErrorCode)
{
    public static VendorOfflineRevocationSnapshotCreationResult Success(
        OfflineRevocationListEnvelope envelope) => new(true, envelope, null);

    public static VendorOfflineRevocationSnapshotCreationResult Invalid(string errorCode) =>
        new(false, null, errorCode);

    public static VendorOfflineRevocationSnapshotCreationResult Unavailable(string errorCode) =>
        new(false, null, errorCode);
}
