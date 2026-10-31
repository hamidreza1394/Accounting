namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Stable Trade operation permission codes, assigned through the shared company-role
/// permission system. Fiscal-period selection/access is inherited from the Core work context.
/// </summary>
public static class TradePermissionCodes
{
    public const string CompanyReportView = "trade.reports.company.view";
    public const string MultiCompanyReportView = "trade.reports.multicompany.view";

    private const string FormPrefix = "trade.form.";

    private static readonly string[] WriteActionSuffixes =
    [
        ".create", ".update", ".approve", ".delete", ".submit", ".cancel", ".post",
        ".complete", ".release", ".close", ".reopen", ".issue", ".send", ".confirm",
        ".reject", ".receive", ".settle",
    ];

    public static string FormView(string formCode) => FormCode(formCode) + ".view";
    public static string FormCreate(string formCode) => FormCode(formCode) + ".create";
    public static string FormUpdate(string formCode) => FormCode(formCode) + ".update";
    public static string FormApprove(string formCode) => FormCode(formCode) + ".approve";
    public static string FormDelete(string formCode) => FormCode(formCode) + ".delete";
    public static string FormExport(string formCode) => FormCode(formCode) + ".export";

    /// <summary>
    /// Identifies permission codes that represent state-changing operations. The adapter
    /// rejects these codes when a caller incorrectly requests only read access.
    /// </summary>
    public static bool RequiresWriteIntent(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        return WriteActionSuffixes.Any(suffix =>
            permissionCode.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static string FormCode(string formCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formCode);

        if (formCode.Length > 64 || formCode.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '.' && character != '-' && character != '_'))
        {
            throw new ArgumentException("Invalid form code.", nameof(formCode));
        }

        return FormPrefix + formCode.ToLowerInvariant();
    }
}
