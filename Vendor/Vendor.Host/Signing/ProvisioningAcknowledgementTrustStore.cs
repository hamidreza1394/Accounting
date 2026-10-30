using System.Linq;
using System.Security.Cryptography;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Deployment-supplied trust entry for a customer-side provisioning acknowledgement key.
/// The public key is not accepted from an incoming acknowledgement envelope.
/// </summary>
public sealed class ProvisioningAcknowledgementTrustedKeyOptions
{
    public string KeyId { get; set; } = string.Empty;

    public string PublicKeySubjectPublicKeyInfoBase64 { get; set; } = string.Empty;

    public bool Revoked { get; set; }
}

/// <summary>
/// Resolves acknowledgement public keys from independently configured deployment trust.
/// Unknown and revoked key IDs fail closed. Duplicate IDs or malformed key material fail
/// construction so configuration mistakes cannot select an arbitrary trust anchor.
/// </summary>
public sealed class ProvisioningAcknowledgementTrustStore
{
    private readonly IReadOnlyDictionary<string, TrustedKey> _keys;

    public ProvisioningAcknowledgementTrustStore(
        IEnumerable<ProvisioningAcknowledgementTrustedKeyOptions> configuredKeys)
    {
        ArgumentNullException.ThrowIfNull(configuredKeys);

        var keys = new Dictionary<string, TrustedKey>(StringComparer.Ordinal);
        foreach (var configuredKey in configuredKeys)
        {
            if (configuredKey is null || !IsValidKeyId(configuredKey.KeyId))
            {
                throw new InvalidOperationException(
                    "Provisioning acknowledgement trust configuration contains an invalid key ID.");
            }

            if (keys.ContainsKey(configuredKey.KeyId))
            {
                throw new InvalidOperationException(
                    "Provisioning acknowledgement trust configuration contains duplicate key IDs.");
            }

            if (string.IsNullOrWhiteSpace(configuredKey.PublicKeySubjectPublicKeyInfoBase64))
            {
                throw new InvalidOperationException(
                    "Provisioning acknowledgement trust configuration is missing public-key material.");
            }

            byte[] encoded;
            try
            {
                encoded = Convert.FromBase64String(configuredKey.PublicKeySubjectPublicKeyInfoBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "Provisioning acknowledgement trust configuration contains invalid public-key encoding.",
                    exception);
            }

            if (encoded.Length is 0 or > 1024)
            {
                throw new InvalidOperationException(
                    "Provisioning acknowledgement public key must be between 1 and 1024 bytes.");
            }

            try
            {
                using var publicKey = ECDsa.Create();
                publicKey.ImportSubjectPublicKeyInfo(encoded, out var bytesRead);
                if (bytesRead != encoded.Length || publicKey.KeySize != 256)
                {
                    throw new InvalidOperationException(
                        "Provisioning acknowledgement keys must be valid ECDSA P-256 public keys.");
                }
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException(
                    "Provisioning acknowledgement trust configuration contains an invalid ECDSA public key.",
                    exception);
            }

            keys.Add(configuredKey.KeyId, new TrustedKey(encoded, configuredKey.Revoked));
        }

        _keys = keys;
    }

    /// <summary>
    /// Returns a configured, non-revoked SPKI key. An empty trust set intentionally rejects all
    /// key IDs, allowing deployments to start without enabling acknowledgement intake.
    /// </summary>
    public bool TryGetTrustedPublicKey(string? keyId, out ReadOnlyMemory<byte> subjectPublicKeyInfo)
    {
        subjectPublicKeyInfo = ReadOnlyMemory<byte>.Empty;
        if (!IsValidKeyId(keyId) ||
            !_keys.TryGetValue(keyId!, out var key) ||
            key.Revoked)
        {
            return false;
        }

        subjectPublicKeyInfo = key.SubjectPublicKeyInfo;
        return true;
    }

    private static bool IsValidKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId) &&
        keyId.Length <= 128 &&
        keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private sealed record TrustedKey(byte[] SubjectPublicKeyInfo, bool Revoked);
}
