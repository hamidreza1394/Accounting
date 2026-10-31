namespace Faranou.Licensing.Contracts;

/// <summary>
/// Canonical signed payload (UTF-8 JSON, deterministic property order).
/// Does not include the signature itself.
/// </summary>
public sealed record LicensePayload(
	int FormatVersion,
	string LicenseId,
	string InstallationId,
	string ProductCode,
	string LicenseKind,
	DateTimeOffset IssuedAtUtc,
	DateTimeOffset? NotBeforeUtc,
	DateTimeOffset? ExpiresAtUtc,
	int GraceDays,
	int Sequence,
	LicenseLimits Limits,
	string InstallationKeyFingerprint = "")
{
	public const string KindPerpetual = "perpetual";
	public const string KindTerm = "term";
	public const int MaxGraceDaysCeiling = 90;
	public const int DefaultGraceDays = 30;
}
