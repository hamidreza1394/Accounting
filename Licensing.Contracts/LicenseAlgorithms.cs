namespace Faranou.Licensing.Contracts;

/// <summary>
/// Stable algorithm identifiers written into the signed envelope.
/// OD-12 is still Proposed; ECDSA-P256-SHA256 is the provisional default for implementation
/// and tests only — not a product Accepted decision until the owner confirms.
/// </summary>
public static class LicenseAlgorithms
{
	/// <summary>ECDSA P-256 with SHA-256; signature encoded as IEEE P1363 (r||s, 64 bytes).</summary>
	public const string EcdsaP256Sha256 = "ECDSA-P256-SHA256";

	/// <summary>Current format version of the envelope JSON payload bytes that are signed.</summary>
	public const int CurrentFormatVersion = 1;
}
