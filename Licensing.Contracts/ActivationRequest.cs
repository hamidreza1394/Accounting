namespace Faranou.Licensing.Contracts;

/// <summary>
/// Offline activation request produced by a customer installation (D-023).
/// Must not contain accounting data, secrets, or passwords.
/// Scope model follows OD-01 Proposed option D: technical license is per installation;
/// commercial account linkage lives only on the vendor side.
/// </summary>
public sealed record ActivationRequest(
	int FormatVersion,
	string InstallationId,
	string ProductCode,
	string ProductVersion,
	DateTimeOffset CreatedAtUtc,
	string? CustomerHint)
{
	public const int MaxCustomerHintLength = 128;
}
