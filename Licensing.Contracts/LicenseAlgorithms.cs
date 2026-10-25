namespace Faranou.Licensing.Contracts;

/// <summary>
/// Fixed v1 algorithm identifier accepted by D-045; only this allowlisted value may verify.
/// </summary>
public static class LicenseAlgorithms
{
	public const string EcdsaP256Sha256 = "ECDSA-P256-SHA256";
	public const int CurrentFormatVersion = 1;

	/// <summary>Explicit allowlist — unknown algorithms are rejected.</summary>
	public static bool IsAllowed(string? algorithm)
		=> string.Equals(algorithm, EcdsaP256Sha256, StringComparison.Ordinal);
}
