using System.Linq;
using System.Security.Cryptography;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Deployment-supplied public trust entry for Vendor provisioning package signatures.
/// The corresponding private key must remain in the approved external signing provider.
/// </summary>
public sealed class ProvisioningPackageSigningTrustedKeyOptions
{
    public string KeyId { get; set; } = string.Empty;

    public string PublicKeySubjectPublicKeyInfoBase64 { get; set; } = string.Empty;

    public bool Revoked { get; set; }
}

/// <summary>
/// Resolves Vendor package-signing public keys from independent deployment configuration.
/// Unknown, revoked or malformed key entries fail closed. No private key is loaded by this store.
/// </summary>
public sealed class ProvisioningPackageSigningTrustStore
{
    private readonly IReadOnlyDictionary<string, TrustedKey> _keys;

    public ProvisioningPackageSigningTrustStore(
        IEnumerable<ProvisioningPackageSigningTrustedKeyOptions> configuredKeys)
    {
        ArgumentNullException.ThrowIfNull(configuredKeys);

        var keys = new Dictionary<string, TrustedKey>(StringComparer.Ordinal);
        foreach (var configuredKey in configuredKeys)
        {
            if (configuredKey is null || !IsValidKeyId(configuredKey.KeyId))
            {
                throw new InvalidOperationException(
                    "Provisioning package signing trust configuration contains an invalid key ID.");
            }

            if (keys.ContainsKey(configuredKey.KeyId))
            {
                throw new InvalidOperationException(
                    "Provisioning package signing trust configuration contains duplicate key IDs.");
            }

            if (string.IsNullOrWhiteSpace(configuredKey.PublicKeySubjectPublicKeyInfoBase64))
            {
                throw new InvalidOperationException(
                    "Provisioning package signing trust configuration is missing public-key material.");
            }

            byte[] encoded;
            try
            {
                encoded = Convert.FromBase64String(configuredKey.PublicKeySubjectPublicKeyInfoBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "Provisioning package signing trust configuration contains invalid public-key encoding.",
                    exception);
            }

            if (encoded.Length is 0 or > 1024)
            {
                throw new InvalidOperationException(
                    "Provisioning package signing public key must be between 1 and 1024 bytes.");
            }

            try
            {
                using var publicKey = ECDsa.Create();
                publicKey.ImportSubjectPublicKeyInfo(encoded, out var bytesRead);
                if (bytesRead != encoded.Length || publicKey.KeySize != 256)
                {
                    throw new InvalidOperationException(
                        "Provisioning package signing keys must be valid ECDSA P-256 public keys.");
                }
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException(
                    "Provisioning package signing trust configuration contains an invalid ECDSA public key.",
                    exception);
            }

            keys.Add(configuredKey.KeyId, new TrustedKey(encoded, configuredKey.Revoked));
        }

        _keys = keys;
    }

    /// <summary>
    /// Returns only a configured, non-revoked public key. An empty trust set rejects all signers.
    /// </summary>
    public bool TryGetTrustedPublicKey(string? keyId, out ReadOnlyMemory<byte> subjectPublicKeyInfo)
    {
        subjectPublicKeyInfo = ReadOnlyMemory<byte>.Empty;
        if (!IsValidKeyId(keyId) || !_keys.TryGetValue(keyId!, out var key) || key.Revoked)
            return false;

        subjectPublicKeyInfo = key.SubjectPublicKeyInfo;
        return true;
    }

    private static bool IsValidKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId) &&
        keyId.Length <= 128 &&
        keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private sealed record TrustedKey(byte[] SubjectPublicKeyInfo, bool Revoked);
}
