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
/// (<c>Provisioning:TrustedAcknowledgementKeys:&lt;keyId&gt;</c> = Base64 SubjectPublicKeyInfo of an ECDSA P-256 key).
/// The configuration holds public key material only; the private key stays in the protected signing provider.
/// </summary>
public sealed class ConfigurationCustomerProvisioningAcknowledgementTrustStore(IConfiguration configuration)
    : ICustomerProvisioningAcknowledgementTrustStore
{
    public const string SectionPath = "Provisioning:TrustedAcknowledgementKeys";

    public bool TryGetPublicKey(string? keyId, out byte[] subjectPublicKeyInfo) =>
        ConfigurationTrustedPublicKeys.TryRead(configuration, SectionPath, keyId, out subjectPublicKeyInfo);
}
