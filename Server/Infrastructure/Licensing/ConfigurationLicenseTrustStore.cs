using Faranou.Licensing.Contracts;
using Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Licensing;

/// <summary>
/// Customer-side trust resolver for signed license envelopes. It reads only configured public
/// SPKI keys and keeps license-signing trust separate from revocation-list trust.
/// </summary>
public sealed class ConfigurationLicenseTrustStore(IConfiguration configuration) : ITrustedKeyResolver
{
    private const string TrustedKeysSection = "Licensing:TrustedLicenseKeys";
    private const string RetiredKeysSection = "Licensing:RetiredLicenseKeys";

    public bool TryResolve(string kid, out TrustedPublicKey? key)
    {
        key = null;
        if (!ConfigurationTrustedPublicKeys.TryRead(configuration, TrustedKeysSection, kid, out var publicKey))
        {
            return false;
        }

        var retiredValue = configuration[$"{RetiredKeysSection}:{kid}"];
        var retired = false;
        if (retiredValue is not null && !bool.TryParse(retiredValue, out retired))
        {
            return false;
        }

        key = new TrustedPublicKey(kid, LicenseAlgorithms.EcdsaP256Sha256, publicKey, retired);
        return true;
    }
}
