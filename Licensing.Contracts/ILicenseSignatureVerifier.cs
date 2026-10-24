namespace Faranou.Licensing.Contracts;

/// <summary>
/// Customer-side verification only. Signing lives exclusively on the vendor host (OD-07).
/// </summary>
public interface ILicenseSignatureVerifier
{
	string Algorithm { get; }

	bool TryVerify(LicenseEnvelope envelope, ReadOnlySpan<byte> publicKeySpkiOrRaw, out string? error);
}
