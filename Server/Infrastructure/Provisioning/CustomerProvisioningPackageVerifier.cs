using System;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Options;

namespace Infrastructure.Provisioning;

/// <summary>
/// Verifies a Vendor package before any customer database mutation. Installation identity
/// comes from deployment configuration; neither customer nor installation is trusted from
/// the package merely because the package has a valid signature.
/// </summary>
public sealed class CustomerProvisioningPackageVerifier(
    ICustomerProvisioningTrustStore trustStore,
    IOptions<CustomerProvisioningSettings> options,
    TimeProvider timeProvider)
{
    public bool TryVerify(
        CustomerProvisioningPackage? package,
        out CustomerProvisioningPayload? payload,
        out string errorCode)
    {
        payload = null;
        errorCode = "provisioning_consumer_not_configured";

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CustomerReference) ||
            string.IsNullOrWhiteSpace(settings.InstallationId))
            return false;

        if (package is null ||
            !trustStore.TryGetPublicKey(package.KeyId, out var trustedPublicKey))
        {
            errorCode = "provisioning_trusted_key_unknown";
            return false;
        }

        if (!CustomerProvisioningPackageValidator.TryValidateAndVerify(
                package,
                trustedPublicKey,
                timeProvider.GetUtcNow(),
                out payload,
                out errorCode))
            return false;

        if (!string.Equals(payload!.CustomerReference, settings.CustomerReference, StringComparison.Ordinal))
        {
            payload = null;
            errorCode = "provisioning_customer_mismatch";
            return false;
        }

        if (!string.Equals(payload.InstallationId, settings.InstallationId, StringComparison.Ordinal))
        {
            payload = null;
            errorCode = "provisioning_installation_mismatch";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }
}
