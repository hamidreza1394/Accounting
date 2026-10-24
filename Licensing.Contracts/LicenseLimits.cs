namespace Faranou.Licensing.Contracts;

/// <summary>
/// Commercial limits carried in the signed payload.
/// OD-02 Proposed: v1 carries maxCompanies + subsystem codes only.
/// </summary>
public sealed record LicenseLimits(
	int? MaxCompanies,
	IReadOnlyList<string> Subsystems)
{
	public static LicenseLimits Empty { get; } = new(null, Array.Empty<string>());
}
