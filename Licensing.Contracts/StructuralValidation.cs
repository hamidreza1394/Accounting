using System.Text.RegularExpressions;

namespace Faranou.Licensing.Contracts;

public static class StructuralValidation
{
    private static readonly Regex IdPattern = new(
        @"^[A-Za-z0-9._-]{8," + LicenseInputLimits.MaxIdChars + "}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CodePattern = new(
        @"^[A-Za-z0-9._-]{1," + LicenseInputLimits.MaxCodeChars + "}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex KidPattern = new(
        @"^[A-Za-z0-9._-]{1," + LicenseInputLimits.MaxKidChars + "}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> ValidateActivationRequest(ActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        if (request.FormatVersion < 1)
            errors.Add("formatVersion must be >= 1");
        if (request.InstallationId is null || !IdPattern.IsMatch(request.InstallationId))
            errors.Add("installationId is invalid");
        if (request.ProductCode is null || !CodePattern.IsMatch(request.ProductCode))
            errors.Add("productCode is invalid");
        if (string.IsNullOrWhiteSpace(request.ProductVersion) || request.ProductVersion.Length > LicenseInputLimits.MaxCodeChars)
            errors.Add("productVersion is invalid");
        if (request.CreatedAtUtc == default)
            errors.Add("createdAtUtc is required");
        if (request.CustomerHint is { Length: > ActivationRequest.MaxCustomerHintLength })
            errors.Add("customerHint exceeds max length");
        return errors;
    }

    public static IReadOnlyList<string> ValidatePayload(LicensePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var errors = new List<string>();
        if (!LicensePayload.IsSupportedFormatVersion(payload.FormatVersion))
            errors.Add($"unsupported payload formatVersion: {payload.FormatVersion}");
        if (payload.LicenseId is null || !IdPattern.IsMatch(payload.LicenseId))
            errors.Add("licenseId is invalid");
        if (payload.InstallationId is null || !IdPattern.IsMatch(payload.InstallationId))
            errors.Add("installationId is invalid");
        if (payload.ProductCode is null || !CodePattern.IsMatch(payload.ProductCode))
            errors.Add("productCode is invalid");
        if (payload.LicenseKind is not (LicensePayload.KindPerpetual or LicensePayload.KindTerm))
            errors.Add("licenseKind must be perpetual or term");
        if (payload.GraceDays < 0 || payload.GraceDays > LicensePayload.MaxGraceDaysCeiling)
            errors.Add("graceDays out of range");
        if (payload.Sequence < 1)
            errors.Add("sequence must be >= 1");

        if (payload.LicenseKind == LicensePayload.KindTerm)
        {
            if (payload.ExpiresAtUtc is null)
                errors.Add("term license requires expiresAtUtc");
        }
        else if (payload.LicenseKind == LicensePayload.KindPerpetual)
        {
            if (payload.ExpiresAtUtc is not null)
                errors.Add("perpetual license must not set expiresAtUtc");
        }

        if (payload.NotBeforeUtc is { } nbf && payload.ExpiresAtUtc is { } exp && nbf > exp)
            errors.Add("notBeforeUtc must not be after expiresAtUtc");

        if (payload.FormatVersion == LicensePayload.LegacyFormatVersion &&
            (payload.SupportUntilUtc is not null || payload.UpdatesUntilUtc is not null))
            errors.Add("support/update dates require payload v2");

        if (payload.Limits is null)
        {
            errors.Add("limits is required");
        }
        else
        {
            if (payload.Limits.MaxCompanies is < 1)
                errors.Add("maxCompanies must be >= 1 when set");

            var subsystems = payload.Limits.Subsystems;
            if (subsystems is null)
            {
                errors.Add("subsystems is required");
            }
            else if (subsystems.Count > LicenseInputLimits.MaxSubsystemCount)
            {
                errors.Add("too many subsystems");
            }
            else
            {
                if (payload.FormatVersion == LicensePayload.CurrentFormatVersion && subsystems.Count > 0)
                    errors.Add("payload v2 must not contain installation-wide subsystems");

                var seenSubsystems = new HashSet<string>(StringComparer.Ordinal);
                foreach (var subsystem in subsystems)
                {
                    if (subsystem is null || !CodePattern.IsMatch(subsystem) ||
                        subsystem.Length > LicenseInputLimits.MaxSubsystemCodeChars)
                    {
                        errors.Add("invalid subsystem code");
                        continue;
                    }

                    if (payload.FormatVersion == LicensePayload.LegacyFormatVersion &&
                        !LicenseSubsystemCodes.IsAllowed(subsystem))
                        errors.Add("unsupported subsystem code");
                    if (!seenSubsystems.Add(subsystem))
                        errors.Add("duplicate subsystem code");
                }
            }
        }

        if (payload.FormatVersion == LicensePayload.LegacyFormatVersion)
        {
            if (payload.CompanyEntitlements is not null)
                errors.Add("company entitlements require payload v2");
        }
        else if (payload.FormatVersion == LicensePayload.CurrentFormatVersion)
        {
            ValidateCompanyEntitlements(payload.CompanyEntitlements, payload.Limits?.MaxCompanies, errors);
        }

        return errors;
    }

    private static void ValidateCompanyEntitlements(
        IReadOnlyList<CompanyLicenseEntitlement?>? companies,
        int? maxCompanies,
        List<string> errors)
    {
        if (companies is null)
        {
            errors.Add("companyEntitlements is required in payload v2");
            return;
        }

        if (companies.Count > LicenseInputLimits.MaxCompanyEntitlementCount)
            errors.Add("too many company entitlements");
        if (maxCompanies is { } companyLimit && companies.Count > companyLimit)
            errors.Add("company entitlements exceed maxCompanies");

        var seenCompanies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var company in companies)
        {
            if (company is null)
            {
                errors.Add("company entitlement is null");
                continue;
            }

            if (company.CompanyReference is null || !IdPattern.IsMatch(company.CompanyReference))
                errors.Add("company reference is invalid");
            else if (!seenCompanies.Add(company.CompanyReference))
                errors.Add("duplicate company reference");

            var seenModules = new HashSet<string>(StringComparer.Ordinal);
            if (company.Modules is null)
            {
                errors.Add("company modules are required");
            }
            else if (company.Modules.Count > LicenseInputLimits.MaxSubsystemCount)
            {
                errors.Add("too many company modules");
            }
            else
            {
                foreach (var module in company.Modules)
                {
                    if (module is null || !CodePattern.IsMatch(module) ||
                        module.Length > LicenseInputLimits.MaxSubsystemCodeChars)
                    {
                        errors.Add("invalid company module code");
                        continue;
                    }

                    if (!LicenseSubsystemCodes.IsAllowedCompanyModule(module))
                        errors.Add("unsupported company module code");
                    if (!seenModules.Add(module))
                        errors.Add("duplicate company module code");
                }
            }

            if (company.MaxUsers < 1 || company.MaxUsers > LicenseInputLimits.MaxUsersPerCompany)
                errors.Add("company maxUsers is out of range");

            var seenFeatures = new HashSet<string>(StringComparer.Ordinal);
            if (company.Features is null)
            {
                errors.Add("company features are required");
            }
            else if (company.Features.Count > LicenseInputLimits.MaxFeaturesPerCompany)
            {
                errors.Add("too many company features");
            }
            else
            {
                foreach (var feature in company.Features)
                {
                    if (feature is null)
                    {
                        errors.Add("company feature is null");
                        continue;
                    }

                    if (feature.Code is null || !CodePattern.IsMatch(feature.Code))
                    {
                        errors.Add("feature code is invalid");
                        continue;
                    }

                    if (!seenFeatures.Add(feature.Code))
                        errors.Add("duplicate feature code");

                    if (!LicenseFeatureCatalog.TryGetDefinition(feature.Code, out var definition) ||
                        definition is null)
                    {
                        errors.Add("unsupported feature code");
                        continue;
                    }

                    if (!string.Equals(feature.Type, definition.ValueType, StringComparison.Ordinal))
                        errors.Add("feature value type does not match catalog");

                    if (!seenModules.Contains(definition.RequiredModule))
                        errors.Add("feature module is not entitled");

                    var hasEnabled = feature.Enabled.HasValue;
                    var hasMaxCount = feature.MaxCount.HasValue;
                    var hasEnumValue = feature.EnumValue is not null;
                    switch (feature.Type)
                    {
                        case LicenseFeatureValueTypes.Enabled:
                            if (!hasEnabled || hasMaxCount || hasEnumValue)
                                errors.Add("enabled feature value is invalid");
                            break;
                        case LicenseFeatureValueTypes.MaxCount:
                            if (!hasMaxCount || hasEnabled || hasEnumValue ||
                                feature.MaxCount is < 0 or > LicenseInputLimits.MaxFeatureCount)
                                errors.Add("max_count feature value is invalid");
                            break;
                        case LicenseFeatureValueTypes.Enum:
                            if (hasEnabled || hasMaxCount || !IsValidEnumValue(feature.EnumValue))
                                errors.Add("enum feature value is invalid");
                            break;
                        default:
                            errors.Add("unsupported feature value type");
                            break;
                    }
                }
            }
        }
    }

    private static bool IsValidEnumValue(string? value)
        => value is not null &&
           value.Length <= LicenseInputLimits.MaxFeatureEnumValueChars &&
           CodePattern.IsMatch(value);

    public static IReadOnlyList<string> ValidateEnvelopeStructure(LicenseEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var errors = new List<string>();
        if (envelope.FormatVersion != LicenseAlgorithms.CurrentFormatVersion)
            errors.Add($"unsupported envelope formatVersion: {envelope.FormatVersion}");
        if (envelope.Kid is null || !KidPattern.IsMatch(envelope.Kid))
            errors.Add("kid is invalid");
        if (string.IsNullOrWhiteSpace(envelope.Algorithm) || envelope.Algorithm.Length > LicenseInputLimits.MaxAlgorithmChars)
            errors.Add("algorithm is invalid");
        else if (!LicenseAlgorithms.IsAllowed(envelope.Algorithm))
            errors.Add("algorithm not allowed");
        if (string.IsNullOrEmpty(envelope.PayloadJson))
            errors.Add("payloadJson is required");
        else if (envelope.PayloadJson.Length > LicenseInputLimits.MaxPayloadJsonChars)
            errors.Add("payloadJson too large");
        if (string.IsNullOrEmpty(envelope.SignatureBase64))
            errors.Add("signatureBase64 is required");
        else if (envelope.SignatureBase64.Length > LicenseInputLimits.MaxSignatureBase64Chars)
            errors.Add("signatureBase64 too large");
        else
        {
            try
            {
                _ = Convert.FromBase64String(envelope.SignatureBase64);
            }
            catch
            {
                errors.Add("signatureBase64 is not valid Base64");
            }
        }
        return errors;
    }
}
