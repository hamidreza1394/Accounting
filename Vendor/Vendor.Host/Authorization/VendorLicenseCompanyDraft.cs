using System.Text.Json;
using System.Text.Json.Serialization;
using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Administrator input for one company in an unissued, company-scoped license.</summary>
/// <remarks>
/// <c>CompanyReference</c> is never client-authoritative: it is accepted only when it equals a reference the
/// server already stored in the same license's draft; a new company always receives a fresh server GUID-D.
/// </remarks>
public sealed record VendorCompanyDraftInput(
    string? CompanyReference,
    string? Descriptor,
    IReadOnlyList<string>? Modules,
    int? MaxUsers,
    IReadOnlyList<LicenseFeatureEntitlement?>? Features);

/// <summary>Stored form of one draft company. After issuance only reference and descriptor remain.</summary>
public sealed record VendorCompanyDraftEntry(
    string CompanyReference,
    string? Descriptor,
    IReadOnlyList<string>? Modules,
    int? MaxUsers,
    IReadOnlyList<LicenseFeatureEntitlement?>? Features);

public sealed record VendorCompanyDraft(IReadOnlyList<VendorCompanyDraftEntry> Companies);

/// <summary>
/// Validation, parsing and post-issuance reduction of <c>VendorLicense.CompanyEntitlementsDraftJson</c>.
/// The signed envelope, not this column, is the final source of truth after issuance. The descriptor is only
/// an operator label and must never be used as an ownership, Tenant or Company basis.
/// </summary>
public static class VendorLicenseCompanyDraft
{
    public const int MaxJsonBytes = 64 * 1024;
    public const int MaxDescriptorLength = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static bool TryNormalize(
        IReadOnlyList<VendorCompanyDraftInput>? inputs,
        string? existingDraftJson,
        int? maxCompanies,
        out string? json,
        out string? errorCode)
    {
        json = null;
        if (inputs is null || inputs.Count == 0)
        {
            errorCode = "company_list_empty";
            return false;
        }

        var known = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(existingDraftJson) && TryParse(existingDraftJson, out var existing))
            foreach (var company in existing!.Companies) known.Add(company.CompanyReference);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<VendorCompanyDraftEntry>(inputs.Count);
        foreach (var input in inputs)
        {
            if (input is null || !IsValidDescriptor(input.Descriptor))
            {
                errorCode = "company_descriptor_invalid";
                return false;
            }

            string reference;
            if (string.IsNullOrWhiteSpace(input.CompanyReference))
            {
                reference = Guid.NewGuid().ToString("D");
            }
            else
            {
                reference = input.CompanyReference.Trim();
                if (!known.Contains(reference))
                {
                    errorCode = "company_reference_unknown";
                    return false;
                }
            }

            if (!seen.Add(reference))
            {
                errorCode = "company_reference_duplicate";
                return false;
            }

            // Null entries are rejected (never silently dropped) so signed content equals the submitted content.
            if (input.Modules?.Any(m => m is null) == true || input.Features?.Any(f => f is null) == true)
            {
                errorCode = "company_entitlements_invalid";
                return false;
            }

            entries.Add(new VendorCompanyDraftEntry(
                reference, input.Descriptor!.Trim(),
                input.Modules?.Select(m => m.Trim()).Order(StringComparer.Ordinal).ToArray(),
                input.MaxUsers,
                input.Features?.OrderBy(f => f!.Code, StringComparer.Ordinal).ToArray()));
        }

        if (!TryToEntitlements(entries, maxCompanies, out _))
        {
            errorCode = "company_entitlements_invalid";
            return false;
        }

        var serialized = JsonSerializer.Serialize(new VendorCompanyDraft(entries), Json);
        if (System.Text.Encoding.UTF8.GetByteCount(serialized) > MaxJsonBytes)
        {
            errorCode = "company_draft_too_large";
            return false;
        }

        json = serialized;
        errorCode = null;
        return true;
    }

    public static bool TryParse(string? json, out VendorCompanyDraft? draft)
    {
        draft = null;
        if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<VendorCompanyDraft>(json, Json);
            if (parsed?.Companies is null ||
                parsed.Companies.Any(c => c is null || !StructuralValidation.IsValidCompanyReference(c.CompanyReference)))
                return false;
            draft = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Converts a stored full draft into contract entitlements for signing, validating every entry through
    /// <see cref="StructuralValidation"/>. A reduced draft (no modules or MaxUsers) is not issuable.
    /// </summary>
    public static bool TryToEntitlements(
        IReadOnlyList<VendorCompanyDraftEntry> entries,
        int? maxCompanies,
        out IReadOnlyList<CompanyLicenseEntitlement?> entitlements)
    {
        var list = new List<CompanyLicenseEntitlement?>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Modules is null || entry.MaxUsers is null)
            {
                entitlements = [];
                return false;
            }

            list.Add(new CompanyLicenseEntitlement(
                entry.CompanyReference, entry.Modules, entry.MaxUsers.Value,
                entry.Features ?? Array.Empty<LicenseFeatureEntitlement?>()));
        }

        entitlements = list;
        var probe = new LicensePayload(
            LicensePayload.CurrentFormatVersion, Guid.Empty.ToString("D"), Guid.Empty.ToString("D"),
            "PROBE", LicensePayload.KindPerpetual, DateTimeOffset.UnixEpoch, null, null,
            LicensePayload.DefaultGraceDays, 1, new LicenseLimits(maxCompanies, Array.Empty<string>()))
        {
            CompanyEntitlements = list
        };
        return StructuralValidation.ValidatePayload(probe).Count == 0;
    }

    /// <summary>
    /// Installation-level subsystems shown in API responses: a company-scoped license has none (the legacy
    /// <c>SubsystemsJson</c> is kept untouched in storage but never presented as an entitlement).
    /// </summary>
    public static IReadOnlyList<string> EffectiveSubsystems(string? draftJson, string? subsystemsJson) =>
        draftJson is null ? VendorLicenseEntitlementRules.ParseSubsystems(subsystemsJson) : Array.Empty<string>();

    /// <summary>Response projection of the stored draft; null for legacy v1 licenses.</summary>
    public static IReadOnlyList<VendorCompanyDraftEntry>? ToResponse(string? json)
    {
        if (json is null) return null;
        if (!TryParse(json, out var draft))
            throw new InvalidOperationException("Stored company entitlement draft is invalid.");
        return draft!.Companies;
    }

    /// <summary>Drops everything but CompanyReference and Descriptor (same transaction as issuance).</summary>
    public static string Reduce(string draftJson)
    {
        if (!TryParse(draftJson, out var draft))
            throw new InvalidOperationException("Stored company entitlement draft is invalid.");
        var reduced = draft!.Companies
            .Select(c => new VendorCompanyDraftEntry(c.CompanyReference, c.Descriptor, null, null, null))
            .ToArray();
        return JsonSerializer.Serialize(new VendorCompanyDraft(reduced), Json);
    }

    /// <summary>Maps internal draft error codes to the stable public API codes consumed by the Vendor UI.</summary>
    public static string ToApiErrorCode(string internalCode) => internalCode switch
    {
        "company_draft_too_large" => "company_entitlements_too_large",
        "company_reference_unknown" or "company_reference_duplicate" => "company_reference_not_assignable",
        _ => "invalid_company_entitlements"
    };

    private static bool IsValidDescriptor(string? descriptor) =>
        !string.IsNullOrWhiteSpace(descriptor) &&
        descriptor.Trim().Length <= MaxDescriptorLength &&
        !descriptor.Any(IsDisallowedDescriptorChar);

    // Control characters and invisible/bidi format characters (U+202E, U+2066.., U+200B..) can make an operator
    // label look like another company; only ZWNJ (U+200C) is kept because Persian text needs it.
    private static bool IsDisallowedDescriptorChar(char c) =>
        char.IsControl(c) ||
        (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format && c != '\u200C');
}
