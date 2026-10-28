namespace Faranou.Licensing.Contracts;

/// <summary>
/// Signed notice that a specific issued license sequence was revoked in the Vendor control plane.
/// Customer-side processing is a separate integration; this contract does not imply instant offline revocation.
/// </summary>
public sealed record LicenseRevocationPayload(
    int FormatVersion,
    string LicenseId,
    string InstallationId,
    string ProductCode,
    int Sequence,
    DateTimeOffset RevokedAtUtc);
