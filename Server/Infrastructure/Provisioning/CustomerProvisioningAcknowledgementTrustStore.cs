using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Provisioning;

/// <summary>
/// Public keys the customer installation trusts for its own acknowledgement signatures.
/// A signature produced by the signing provider is verified against these keys before it is stored,
/// so a faulty, misconfigured or substituted provider can never persist an unverifiable acknowledgement.
/// </summary>
public interface ICustomerProvisioningAcknowledgementTrustStore
{
    bool TryGetPublicKey(string? keyId, out byte[] subjectPublicKeyInfo);
}

/// <summary>
/// Resolves acknowledgement key identifiers only against deployment configuration
/// (Provisioning:TrustedAcknowledgementKeys:&lt;keyId&gt; = Base64 SubjectPublicKeyInfo of an ECDSA P-256 key).
/// Revocation is explicit and independent from retaining the public key for historical audit.
/// </summary>
public sealed class ConfigurationCustomerProvisioningAcknowledgementTrustStore(IConfiguration configuration)
    : ICustomerProvisioningAcknowledgementTrustStore
{
    public const string SectionPath = "Provisioning:TrustedAcknowledgementKeys";
    public const string RevokedSectionPath = "Provisioning:RevokedAcknowledgementKeys";

    public bool TryGetPublicKey(string? keyId, out byte[] subjectPublicKeyInfo)
    {
        subjectPublicKeyInfo = Array.Empty<byte>();
        if (!IsValidKeyId(keyId))
            return false;

        if (bool.TryParse(configuration[RevokedSectionPath + ":" + keyId], out var revoked) && revoked)
            return false;

        return ConfigurationTrustedPublicKeys.TryRead(configuration, SectionPath, keyId, out subjectPublicKeyInfo);
    }

    private static bool IsValidKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId) &&
        keyId.Length <= 128 &&
        keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
