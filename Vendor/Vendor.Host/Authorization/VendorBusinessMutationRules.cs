namespace Faranou.Vendor.Host.Authorization;

/// <summary>Input and lifecycle guards for Vendor business mutations.</summary>
public static class VendorBusinessMutationRules
{
    /// <summary>SQL Server rowversion is exactly eight bytes and is transported as base64.</summary>
    public static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var decoded = Convert.FromBase64String(value);
            if (decoded.Length != 8) return false;
            rowVersion = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool IsOperationalStatusTransitionAllowed(string? current, string? target) =>
        (string.Equals(current, "Active", StringComparison.Ordinal) &&
         string.Equals(target, "Suspended", StringComparison.Ordinal)) ||
        (string.Equals(current, "Suspended", StringComparison.Ordinal) &&
         string.Equals(target, "Active", StringComparison.Ordinal));

    public static bool IsDraft(string? status) =>
        string.Equals(status, "Draft", StringComparison.Ordinal);

    public static bool IsSupportedEnvironment(string? environment) =>
        environment is "Production" or "Staging" or "Development";
}
