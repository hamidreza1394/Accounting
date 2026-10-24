namespace Faranou.Licensing.Contracts;

/// <summary>
/// Evaluation outcome for an installed license envelope (D-026 machine states).
/// Independent of operational RBAC permissions (D-025).
/// </summary>
public enum LicenseStatus
{
	Missing = 0,
	Valid = 1,
	ExpiringSoon = 2,
	Grace = 3,
	Restricted = 4,
	Invalid = 5
}
