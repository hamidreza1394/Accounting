namespace Faranou.Vendor.Host.Authorization;

/// <summary>Maps only validated acknowledgement outcomes to terminal Vendor states.</summary>
public static class ProvisioningAcknowledgementStateRules
{
    public static bool TryResolveOutcome(
        string? result,
        string? errorCode,
        out string status,
        out string? failureCode)
    {
        status = string.Empty;
        failureCode = null;

        switch (result)
        {
            case "Applied":
            case "AlreadyApplied":
                if (errorCode is not null)
                    return false;
                status = "Confirmed";
                return true;
            case "Rejected":
                if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 128 ||
                    errorCode.Any(char.IsControl))
                    return false;
                status = "Failed";
                failureCode = errorCode;
                return true;
            default:
                return false;
        }
    }
}
