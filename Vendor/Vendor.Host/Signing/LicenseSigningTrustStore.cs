using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Configuration;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Public verification keys for envelopes previously issued by the Vendor license signer.
/// Private keys remain exclusively in the external signing provider.
/// </summary>
public sealed class LicenseSigningTrustStore
{
    private readonly IReadOnlyDictionary<string, TrustedKey> _keys;

    public LicenseSigningTrustStore(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var keys = new Dictionary<string, TrustedKey>(StringComparer.Ordinal);
        foreach (var section in configuration.GetSection("Licensing:TrustedSigningKeys").GetChildren())
        {
            var keyId = section.Key;
            var encodedValue = section["PublicKeySubjectPublicKeyInfoBase64"];
            var revoked = bool.TryParse(section["Revoked"], out var isRevoked) && isRevoked;

            if (!IsValidKeyId(keyId) || string.IsNullOrWhiteSpace(encodedValue) || keys.ContainsKey(keyId))
                throw new InvalidOperationException("License signing trust configuration is invalid.");

            byte[] encoded;
            try
            {
                encoded = Convert.FromBase64String(encodedValue);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("License signing trust public key encoding is invalid.", exception);
            }

            try
            {
                using var key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(encoded, out var bytesRead);
                if (bytesRead != encoded.Length || key.KeySize != 256 ||
                    !string.Equals(key.ExportParameters(false).Curve.Oid.Value,
                        ECCurve.NamedCurves.nistP256.Oid.Value, StringComparison.Ordinal))
                    throw new InvalidOperationException("License signing trust keys must be ECDSA P-256 public keys.");
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException("License signing trust public key is invalid.", exception);
            }

            keys.Add(keyId, new TrustedKey(encoded, revoked));
        }

        _keys = keys;
    }

    public bool TryGetTrustedPublicKey(string? keyId, out ReadOnlyMemory<byte> publicKey)
    {
        publicKey = ReadOnlyMemory<byte>.Empty;
        if (!IsValidKeyId(keyId) || !_keys.TryGetValue(keyId!, out var key) || key.Revoked)
            return false;

        publicKey = key.SubjectPublicKeyInfo;
        return true;
    }

    private static bool IsValidKeyId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private sealed record TrustedKey(byte[] SubjectPublicKeyInfo, bool Revoked);
}
