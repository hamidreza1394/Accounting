namespace Faranou.Licensing.Contracts;

/// <summary>
/// Outer envelope: payload JSON + metadata + signature (Base64 IEEE P1363 for ECDSA).
/// Private keys never appear here or in the customer app (D-041, OD-07).
/// </summary>
public sealed record LicenseEnvelope(
	int FormatVersion,
	string Kid,
	string Algorithm,
	string PayloadJson,
	string SignatureBase64);
