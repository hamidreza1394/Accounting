using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Provisioning;

public interface ICustomerProvisioningTrustStore
{
    bool TryGetPublicKey(string? keyId, out byte[] subjectPublicKeyInfo);
}

/// <summary>
/// Resolves key identifiers only against customer-side deployment configuration.
/// The package's KeyId is an untrusted lookup hint and never supplies key material.
/// </summary>
public sealed class ConfigurationCustomerProvisioningTrustStore(IConfiguration configuration)
    : ICustomerProvisioningTrustStore
{
    private const string SectionPath = "Provisioning:TrustedVendorKeys";

    public bool TryGetPublicKey(string? keyId, out byte[] subjectPublicKeyInfo)
    {
        subjectPublicKeyInfo = Array.Empty<byte>();
        if (!IsValidKeyId(keyId))
            return false;

        var configuredValue = configuration.GetSection(SectionPath)
            .GetChildren()
            .FirstOrDefault(entry => string.Equals(entry.Key, keyId, StringComparison.Ordinal))
            ?.Value;
        if (string.IsNullOrWhiteSpace(configuredValue))
            return false;

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configuredValue);
        }
        catch (FormatException)
        {
            return false;
        }

        if (key.Length is 0 or > 1024)
            return false;

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(key, out var bytesRead);
            if (bytesRead != key.Length || ecdsa.KeySize != 256)
                return false;
        }
        catch (CryptographicException)
        {
            return false;
        }

        subjectPublicKeyInfo = key;
        return true;
    }

    private static bool IsValidKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId) &&
        keyId.Length <= 128 &&
        keyId.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
