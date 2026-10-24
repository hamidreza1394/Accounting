namespace Faranou.Licensing.Contracts;

public interface ILicenseSignatureVerifier
{
	string Algorithm { get; }

	bool TryVerify(LicenseEnvelope envelope, ReadOnlySpan<byte> publicKeySpki, out string? errorCode);
}
