namespace Faranou.Vendor.Host.Licensing;

/// <summary>
/// Signs exact canonical payload bytes using a deployment-managed key. Implementations must not
/// export private key material and must use a remote KMS/HSM or an explicitly approved signer.
/// </summary>
public interface IVendorLicenseSigner
{
    Task<VendorLicenseSignature> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken);
}

public sealed record VendorLicenseSignature(string Kid, string Algorithm, string SignatureBase64);

/// <summary>Raised when no approved signing provider is configured. Callers must fail closed.</summary>
public sealed class VendorLicenseSignerUnavailableException : Exception
{
    public VendorLicenseSignerUnavailableException()
        : base("A trusted Vendor license signing provider is not configured.")
    {
    }
}

/// <summary>
/// Safe default for environments without an approved KMS/HSM adapter. It never creates or falls
/// back to a software private key, so issuance and revocation cannot silently succeed.
/// </summary>
public sealed class UnconfiguredVendorLicenseSigner : IVendorLicenseSigner
{
    public Task<VendorLicenseSignature> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<VendorLicenseSignature>(new VendorLicenseSignerUnavailableException());
    }
}
