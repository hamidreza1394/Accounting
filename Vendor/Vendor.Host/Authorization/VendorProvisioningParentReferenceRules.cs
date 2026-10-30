using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Validates a parent provisioning request entirely inside the Vendor control plane.
/// It never connects to or queries a customer database.
/// </summary>
public static class VendorProvisioningParentReferenceRules
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public static async Task<bool> IsEligibleParentAsync(
        VendorDbContext db,
        string? parentCompanyReference,
        Guid customerId,
        Guid installationId,
        Guid currentRequestId,
        string customerReference,
        string targetInstallationCode,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(parentCompanyReference, "D", out var parentRequestId) ||
            parentRequestId == Guid.Empty ||
            !string.Equals(
                parentCompanyReference,
                parentRequestId.ToString("D"),
                StringComparison.OrdinalIgnoreCase) ||
            parentRequestId == currentRequestId)
        {
            return false;
        }

        var parent = await db.ProvisioningRequests.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.RequestId == parentRequestId.ToString("D"),
                cancellationToken);

        if (parent is null ||
            parent.CustomerId != customerId ||
            parent.InstallationId != installationId ||
            parent.Status != "Confirmed" ||
            parent.ConfirmedAtUtc is null ||
            parent.AcknowledgedAtUtc is null ||
            string.IsNullOrWhiteSpace(parent.AcknowledgementSha256) ||
            parent.AcknowledgementResult is not ("Applied" or "AlreadyApplied") ||
            string.IsNullOrWhiteSpace(parent.PayloadJson))
        {
            return false;
        }

        CustomerProvisioningPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CustomerProvisioningPayload>(
                parent.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        return payload is not null &&
            Guid.TryParseExact(payload.RequestId, "D", out var payloadRequestId) &&
            payloadRequestId == parentRequestId &&
            string.Equals(
                CustomerProvisioningCanonicalJson.Serialize(payload),
                parent.PayloadJson,
                StringComparison.Ordinal) &&
            payload.CompanyType is "parent" or "subsidiary" &&
            string.Equals(payload.CustomerReference, customerReference, StringComparison.Ordinal) &&
            string.Equals(payload.InstallationId, targetInstallationCode, StringComparison.Ordinal) &&
            string.Equals(parent.RequestId, parentRequestId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }
}
