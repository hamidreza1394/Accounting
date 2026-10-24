namespace Faranou.Licensing.Contracts;

/// <summary>
/// Hard limits for untrusted license inputs (DoS and parse-cost control).
/// Sized for realistic offline activation envelopes, not arbitrary documents.
/// </summary>
public static class LicenseInputLimits
{
	public const int MaxPayloadJsonChars = 32_768;
	public const int MaxSignatureBase64Chars = 512;
	public const int MaxKidChars = 64;
	public const int MaxAlgorithmChars = 64;
	public const int MaxPublicKeyBytes = 256;
	public const int MaxSubsystemCount = 64;
	public const int MaxSubsystemCodeChars = 64;
	public const int MaxIdChars = 128;
	public const int MaxCodeChars = 64;
}
