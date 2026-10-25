namespace Faranou.Licensing.Contracts;

/// <summary>
/// Commercial limits carried in the signed payload.
/// D-045 Accepted: v1 carries maxCompanies + the fixed subsystem-code allowlist.
/// </summary>
public sealed record LicenseLimits(
	int? MaxCompanies,
	IReadOnlyList<string> Subsystems)
{
	public static LicenseLimits Empty { get; } = new(null, Array.Empty<string>());
}
