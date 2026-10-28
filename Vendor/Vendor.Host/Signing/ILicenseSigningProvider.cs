using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Provider-neutral signing boundary for Vendor license issuance.
/// Implementations must keep private key material inside the configured KMS/HSM or signing service.
/// The input is the exact UTF-8 byte sequence of the canonical payload; implementations must not
/// deserialize and reserialize it before signing.
/// </summary>
public interface ILicenseSigningProvider
{
    /// <summary>
    /// Signs the exact canonical payload bytes with the configured production signing key.
    /// A missing/unavailable credential must be reported as an unavailable result; implementations
    /// must never fall back to a local software key.
    /// </summary>
    ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a signing-provider call. Successful results are constrained to the algorithm and
/// fixed-width signature format accepted by Licensing.Contracts v1.
/// </summary>
public sealed record LicenseSigningResult
{
    private LicenseSigningResult(
        bool succeeded,
        string? keyId,
        string? algorithm,
        byte[] signature,
        string? errorCode)
    {
        Succeeded = succeeded;
        KeyId = keyId;
        Algorithm = algorithm;
        Signature = signature;
        ErrorCode = errorCode;
    }

    public bool Succeeded { get; }
    public string? KeyId { get; }
    public string? Algorithm { get; }
    public ReadOnlyMemory<byte> Signature { get; }
    public string? ErrorCode { get; }

    public static LicenseSigningResult Success(
        string keyId,
        string algorithm,
        ReadOnlySpan<byte> signature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (keyId.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(keyId));
        if (!LicenseAlgorithms.IsAllowed(algorithm))
            throw new ArgumentException("The signing algorithm is not allowed by the license contract.", nameof(algorithm));
        if (signature.Length != 64)
            throw new ArgumentException("The license contract requires a 64-byte ECDSA P-256 signature.", nameof(signature));

        return new LicenseSigningResult(true, keyId, algorithm, signature.ToArray(), null);
    }

    public static LicenseSigningResult Unavailable() =>
        new(false, null, null, [], "signing_provider_unavailable");

    public static LicenseSigningResult Failed() =>
        new(false, null, null, [], "signing_failed");
}
