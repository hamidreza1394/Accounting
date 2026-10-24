namespace Faranou.Licensing.Contracts;

/// <summary>
/// A public verification key bound to a kid. Private keys never appear here.
/// </summary>
public sealed class TrustedPublicKey
{
	public TrustedPublicKey(string kid, string algorithm, byte[] publicKeySpki, bool retired = false)
	{
		if (string.IsNullOrWhiteSpace(kid) || kid.Length > LicenseInputLimits.MaxKidChars)
			throw new ArgumentException("kid is invalid.", nameof(kid));
		if (string.IsNullOrWhiteSpace(algorithm) || algorithm.Length > LicenseInputLimits.MaxAlgorithmChars)
			throw new ArgumentException("algorithm is invalid.", nameof(algorithm));
		ArgumentNullException.ThrowIfNull(publicKeySpki);
		if (publicKeySpki.Length is 0 or > LicenseInputLimits.MaxPublicKeyBytes)
			throw new ArgumentException("public key length is invalid.", nameof(publicKeySpki));

		Kid = kid;
		Algorithm = algorithm;
		PublicKeySpki = publicKeySpki;
		Retired = retired;
	}

	public string Kid { get; }
	public string Algorithm { get; }
	public byte[] PublicKeySpki { get; }
	public bool Retired { get; }
}
