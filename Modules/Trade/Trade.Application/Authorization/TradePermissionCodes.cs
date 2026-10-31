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

    public static string FormView(string formCode) => FormCode(formCode) + ".view";
    public static string FormCreate(string formCode) => FormCode(formCode) + ".create";
    public static string FormUpdate(string formCode) => FormCode(formCode) + ".update";
    public static string FormApprove(string formCode) => FormCode(formCode) + ".approve";
    public static string FormDelete(string formCode) => FormCode(formCode) + ".delete";
    public static string FormExport(string formCode) => FormCode(formCode) + ".export";

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
