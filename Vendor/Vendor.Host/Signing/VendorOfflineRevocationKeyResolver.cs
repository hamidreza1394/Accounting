using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Configuration;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Resolves only public keys configured for offline revocation. It never falls back to license,
/// provisioning-package or customer-acknowledgement trust stores.
/// </summary>
public sealed class VendorOfflineRevocationKeyResolver : IOfflineRevocationKeyResolver
{
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public VendorOfflineRevocationKeyResolver(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection("Licensing:OfflineRevocation:TrustedKeys").GetChildren())
        {
            if (string.IsNullOrWhiteSpace(entry.Key) ||
                entry.Key.Length > OfflineRevocationListValidator.MaxKeyIdLength ||
                entry.Key.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(entry.Value))
            {
                throw new InvalidOperationException("Offline revocation trust configuration is invalid.");
            }

            byte[] publicKey;
            try
            {
                publicKey = System.Convert.FromBase64String(entry.Value);
                using var parsed = ECDsa.Create();
                parsed.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);
                if (bytesRead != publicKey.Length || parsed.KeySize != 256)
                {
                    throw new InvalidOperationException("Offline revocation public key must be ECDSA P-256 SPKI.");
                }
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Offline revocation public key encoding is invalid.", exception);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException("Offline revocation public key is invalid.", exception);
            }

            if (!keys.TryAdd(entry.Key, publicKey))
            {
                throw new InvalidOperationException("Offline revocation key identifiers must be unique.");
            }
        }

        _keys = keys;
    }

    public bool TryResolve(string keyId, out ReadOnlyMemory<byte> trustedPublicKeySpki)
    {
        if (!string.IsNullOrWhiteSpace(keyId) && _keys.TryGetValue(keyId, out var key))
        {
            trustedPublicKeySpki = key;
            return true;
        }

        trustedPublicKeySpki = ReadOnlyMemory<byte>.Empty;
        return false;
    }
}
