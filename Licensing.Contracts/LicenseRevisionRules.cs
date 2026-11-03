namespace Faranou.Licensing.Contracts;

/// <summary>
/// Pure successor rules for an already signature-verified payload. Callers must validate both
/// source envelopes first. This helper does not authenticate signatures or persist replay state.
/// </summary>
public static class LicenseRevisionRules
{
    public static bool TryValidateSuccessor(
        LicensePayload current,
        LicensePayload candidate,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidate);
        errorCode = null;

        if (StructuralValidation.ValidatePayload(current).Count > 0)
        {
            errorCode = "current_payload_invalid";
            return false;
        }

        if (StructuralValidation.ValidatePayload(candidate).Count > 0)
        {
            errorCode = "candidate_payload_invalid";
            return false;
        }

        if (!string.Equals(current.LicenseId, candidate.LicenseId, StringComparison.Ordinal))
        {
            errorCode = "license_id_changed";
            return false;
        }

        if (!string.Equals(current.InstallationId, candidate.InstallationId, StringComparison.Ordinal))
        {
            errorCode = "installation_id_changed";
            return false;
        }

        if (!string.Equals(current.ProductCode, candidate.ProductCode, StringComparison.Ordinal))
        {
            errorCode = "product_code_changed";
            return false;
        }

        if (candidate.FormatVersion < current.FormatVersion)
        {
            errorCode = "payload_format_downgrade";
            return false;
        }

        if (candidate.Sequence <= current.Sequence)
        {
            errorCode = "sequence_not_increasing";
            return false;
        }

        return true;
    }
}
