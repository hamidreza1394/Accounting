namespace Faranou.Licensing.Contracts;

/// <summary>
/// Signed revocation notice. It is deliberately distinct from LicenseEnvelope so a revocation
/// can never be mistaken for a usable license by the existing license payload validator.
/// </summary>
public sealed record LicenseRevocationEnvelope(
    int FormatVersion,
    string Kid,
    string Algorithm,
    string PayloadJson,
    string SignatureBase64);
