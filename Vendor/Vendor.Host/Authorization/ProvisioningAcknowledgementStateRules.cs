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
                // Persist only stable, non-sensitive outcome codes emitted by the customer
                // consumer. Never store free-form exception text, connection details or PII.
                if (!IsAllowedRejectionCode(errorCode))
                    return false;
                status = "Failed";
                failureCode = errorCode;
                return true;
            default:
                return false;
        }
    }

    private static bool IsAllowedRejectionCode(string? errorCode) =>
        errorCode is "provisioning_parent_reference_invalid"
            or "provisioning_initial_admin_conflict";
}
