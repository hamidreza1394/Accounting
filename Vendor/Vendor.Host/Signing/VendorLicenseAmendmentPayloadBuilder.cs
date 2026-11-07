using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence.Domain;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Builds an installation-level maxCompanies increase from an already signature-verified current
/// payload. This pure builder does not authenticate the source envelope, record a purchase,
/// request approval, sign, persist, or advance the durable sequence.
/// </summary>
public static class VendorLicenseAmendmentPayloadBuilder
{
    public static bool TryBuildMaxCompaniesIncrease(
        LicensePayload currentPayload,
        VendorLicense license,
        VendorInstallation installation,
        VendorCustomer customer,
        int newMaxCompanies,
        DateTimeOffset issuedAtUtc,
        out LicensePayload? amendmentPayload,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(currentPayload);
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(customer);

        amendmentPayload = null;
        errorCode = null;

        if (license.Id == Guid.Empty || installation.Id == Guid.Empty || customer.Id == Guid.Empty ||
            license.InstallationId != installation.Id ||
            license.CustomerId != customer.Id ||
            installation.CustomerId != customer.Id ||
            !string.Equals(currentPayload.LicenseId, license.Id.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(currentPayload.InstallationId, installation.Id.ToString("D"), StringComparison.Ordinal))
        {
            return Fail("license_installation_mismatch", out errorCode);
        }

        if (!string.Equals(customer.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(installation.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(license.Status, "Issued", StringComparison.Ordinal))
        {
            return Fail("license_not_amendable", out errorCode);
        }

        if (StructuralValidation.ValidatePayload(currentPayload).Count > 0)
            return Fail("current_license_payload_invalid", out errorCode);

        if (license.LastIssuedSequence != currentPayload.Sequence)
            return Fail("license_sequence_stale", out errorCode);

        if (!string.Equals(license.ProductCode, currentPayload.ProductCode, StringComparison.Ordinal) ||
            !string.Equals(license.LicenseKind, currentPayload.LicenseKind, StringComparison.Ordinal) ||
            license.GraceDays != currentPayload.GraceDays ||
            license.MaxCompanies != currentPayload.Limits.MaxCompanies ||
            license.ValidFromUtc.ToUniversalTime() != currentPayload.NotBeforeUtc?.ToUniversalTime() ||
            license.ValidUntilUtc?.ToUniversalTime() != currentPayload.ExpiresAtUtc?.ToUniversalTime())
        {
            return Fail("license_metadata_stale", out errorCode);
        }

        if (currentPayload.Limits.MaxCompanies is not int currentMaxCompanies)
            return Fail("current_company_limit_missing", out errorCode);

        if (newMaxCompanies <= currentMaxCompanies)
            return Fail("company_limit_not_increased", out errorCode);

        if (currentPayload.Sequence == int.MaxValue)
            return Fail("license_sequence_exhausted", out errorCode);

        if (issuedAtUtc == default ||
            issuedAtUtc.ToUniversalTime() < currentPayload.IssuedAtUtc.ToUniversalTime())
        {
            return Fail("amendment_issued_at_invalid", out errorCode);
        }

        var effectiveIssuedAt = issuedAtUtc.ToUniversalTime();
        if (currentPayload.NotBeforeUtc is { } notBefore &&
            notBefore.ToUniversalTime() > effectiveIssuedAt)
        {
            return Fail("license_not_yet_valid", out errorCode);
        }

        // Do not extend a term license after its entitlement has expired, even during any
        // historical grace period. Support/update expiry is intentionally independent.
        if (currentPayload.LicenseKind == LicensePayload.KindTerm &&
            (currentPayload.ExpiresAtUtc is not { } expiresAt ||
             expiresAt.ToUniversalTime() <= effectiveIssuedAt))
        {
            return Fail("license_outside_validity", out errorCode);
        }

        // The payload model exposes IReadOnlyList, but callers can still supply mutable
        // List/array instances. Snapshot every nested collection before returning a payload
        // that will later be canonicalized and signed.
        var candidate = currentPayload with
        {
            IssuedAtUtc = effectiveIssuedAt,
            Sequence = currentPayload.Sequence + 1,
            Limits = new LicenseLimits(
                newMaxCompanies,
                Array.AsReadOnly(currentPayload.Limits.Subsystems.ToArray())),
            CompanyEntitlements = CloneCompanyEntitlements(currentPayload.CompanyEntitlements)
        };

        if (StructuralValidation.ValidatePayload(candidate).Count > 0 ||
            !LicenseRevisionRules.TryValidateSuccessor(currentPayload, candidate, out _))
        {
            return Fail("amendment_payload_invalid", out errorCode);
        }

        amendmentPayload = candidate;
        return true;
    }


    /// <summary>
    /// Builds additive company-scoped module, feature and user-seat rights for an existing
    /// opaque CompanyReference in an already signature-verified v2 payload. It never creates
    /// a company reference, converts v1 to v2, authenticates the source envelope, records a
    /// purchase, requests approval, signs, persists or advances durable sequence state.
    /// </summary>
    public static bool TryBuildCompanyEntitlementIncrease(
        LicensePayload currentPayload,
        VendorLicense license,
        VendorInstallation installation,
        VendorCustomer customer,
        string companyReference,
        IReadOnlyList<string>? additionalModules,
        int? newMaxUsers,
        IReadOnlyList<LicenseFeatureEntitlement?>? featureIncreases,
        DateTimeOffset issuedAtUtc,
        out LicensePayload? amendmentPayload,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(currentPayload);
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(customer);

        amendmentPayload = null;
        errorCode = null;

        if (currentPayload.FormatVersion != LicensePayload.CurrentFormatVersion)
            return Fail("company_entitlements_require_payload_v2", out errorCode);

        if (license.Id == Guid.Empty || installation.Id == Guid.Empty || customer.Id == Guid.Empty ||
            license.InstallationId != installation.Id ||
            license.CustomerId != customer.Id ||
            installation.CustomerId != customer.Id ||
            !string.Equals(currentPayload.LicenseId, license.Id.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(currentPayload.InstallationId, installation.Id.ToString("D"), StringComparison.Ordinal))
        {
            return Fail("license_installation_mismatch", out errorCode);
        }

        if (!string.Equals(customer.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(installation.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(license.Status, "Issued", StringComparison.Ordinal))
        {
            return Fail("license_not_amendable", out errorCode);
        }

        if (StructuralValidation.ValidatePayload(currentPayload).Count > 0)
            return Fail("current_license_payload_invalid", out errorCode);

        if (license.LastIssuedSequence != currentPayload.Sequence)
            return Fail("license_sequence_stale", out errorCode);

        if (!string.Equals(license.ProductCode, currentPayload.ProductCode, StringComparison.Ordinal) ||
            !string.Equals(license.LicenseKind, currentPayload.LicenseKind, StringComparison.Ordinal) ||
            license.GraceDays != currentPayload.GraceDays ||
            license.MaxCompanies != currentPayload.Limits.MaxCompanies ||
            license.ValidFromUtc.ToUniversalTime() != currentPayload.NotBeforeUtc?.ToUniversalTime() ||
            license.ValidUntilUtc?.ToUniversalTime() != currentPayload.ExpiresAtUtc?.ToUniversalTime())
        {
            return Fail("license_metadata_stale", out errorCode);
        }

        if (currentPayload.Sequence == int.MaxValue)
            return Fail("license_sequence_exhausted", out errorCode);

        if (issuedAtUtc == default ||
            issuedAtUtc.ToUniversalTime() < currentPayload.IssuedAtUtc.ToUniversalTime())
        {
            return Fail("amendment_issued_at_invalid", out errorCode);
        }

        var effectiveIssuedAt = issuedAtUtc.ToUniversalTime();
        if (currentPayload.NotBeforeUtc is { } notBefore &&
            notBefore.ToUniversalTime() > effectiveIssuedAt)
        {
            return Fail("license_not_yet_valid", out errorCode);
        }

        if (currentPayload.LicenseKind == LicensePayload.KindTerm &&
            (currentPayload.ExpiresAtUtc is not { } expiresAt ||
             expiresAt.ToUniversalTime() <= effectiveIssuedAt))
        {
            return Fail("license_outside_validity", out errorCode);
        }

        if (string.IsNullOrWhiteSpace(companyReference))
            return Fail("company_reference_invalid", out errorCode);

        if (additionalModules is null || featureIncreases is null)
            return Fail("amendment_entitlement_request_invalid", out errorCode);

        if (additionalModules.Count > LicenseInputLimits.MaxSubsystemCount ||
            featureIncreases.Count > LicenseInputLimits.MaxFeaturesPerCompany)
        {
            return Fail("amendment_entitlement_request_too_large", out errorCode);
        }

        var companies = currentPayload.CompanyEntitlements!;
        var companyIndex = -1;
        for (var index = 0; index < companies.Count; index++)
        {
            if (string.Equals(companies[index]!.CompanyReference, companyReference, StringComparison.Ordinal))
            {
                companyIndex = index;
                break;
            }
        }

        if (companyIndex < 0)
            return Fail("company_not_entitled", out errorCode);

        var currentCompany = companies[companyIndex]!;
        var existingModules = currentCompany.Modules!;
        var moduleAdditions = additionalModules.ToArray();

        if (moduleAdditions.Any(code =>
                string.IsNullOrWhiteSpace(code) || !LicenseSubsystemCodes.IsAllowedCompanyModule(code)))
        {
            return Fail("company_module_invalid", out errorCode);
        }

        if (moduleAdditions.Distinct(StringComparer.Ordinal).Count() != moduleAdditions.Length)
            return Fail("company_module_duplicate", out errorCode);

        if (moduleAdditions.Any(code => existingModules.Contains(code, StringComparer.Ordinal)))
            return Fail("company_module_already_entitled", out errorCode);

        var resultingModules = existingModules
            .Concat(moduleAdditions)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        if (resultingModules.Length > LicenseInputLimits.MaxSubsystemCount)
            return Fail("company_module_limit_exceeded", out errorCode);

        var resultingMaxUsers = currentCompany.MaxUsers;
        if (newMaxUsers is int requestedMaxUsers)
        {
            if (requestedMaxUsers <= currentCompany.MaxUsers)
                return Fail("company_user_limit_not_increased", out errorCode);
            if (requestedMaxUsers > LicenseInputLimits.MaxUsersPerCompany)
                return Fail("company_user_limit_out_of_range", out errorCode);

            resultingMaxUsers = requestedMaxUsers;
        }

        var resultingFeatures = currentCompany.Features!
            .ToDictionary(feature => feature!.Code!, feature => feature!, StringComparer.Ordinal);
        var requestedFeatureCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var requestedFeature in featureIncreases)
        {
            if (requestedFeature is null || string.IsNullOrWhiteSpace(requestedFeature.Code))
                return Fail("company_feature_invalid", out errorCode);

            var code = requestedFeature.Code;
            if (!requestedFeatureCodes.Add(code))
                return Fail("company_feature_duplicate", out errorCode);

            if (!LicenseFeatureCatalog.TryGetDefinition(code, out var definition) || definition is null)
                return Fail("company_feature_unknown", out errorCode);

            if (!string.Equals(requestedFeature.Type, definition.ValueType, StringComparison.Ordinal))
                return Fail("company_feature_type_mismatch", out errorCode);

            if (!resultingModules.Contains(definition.RequiredModule, StringComparer.Ordinal))
                return Fail("company_feature_module_not_entitled", out errorCode);

            if (definition.ValueType == LicenseFeatureValueTypes.Enum)
                return Fail("company_feature_enum_not_supported", out errorCode);

            resultingFeatures.TryGetValue(code, out var existingFeature);

            if (definition.ValueType == LicenseFeatureValueTypes.Enabled)
            {
                if (requestedFeature.Enabled is not true ||
                    requestedFeature.MaxCount is not null ||
                    requestedFeature.EnumValue is not null)
                {
                    return Fail("company_feature_not_increased", out errorCode);
                }

                if (existingFeature is not null && existingFeature.Enabled is not false)
                    return Fail("company_feature_not_increased", out errorCode);
            }
            else if (definition.ValueType == LicenseFeatureValueTypes.MaxCount)
            {
                if (requestedFeature.MaxCount is not int requestedMaxCount ||
                    requestedMaxCount < 1 ||
                    requestedMaxCount > LicenseInputLimits.MaxFeatureCount ||
                    requestedFeature.Enabled is not null ||
                    requestedFeature.EnumValue is not null)
                {
                    return Fail("company_feature_invalid", out errorCode);
                }

                if (existingFeature is not null &&
                    (existingFeature.MaxCount is not int existingMaxCount ||
                     requestedMaxCount <= existingMaxCount))
                {
                    return Fail("company_feature_not_increased", out errorCode);
                }
            }
            else
            {
                return Fail("company_feature_type_not_supported", out errorCode);
            }

            resultingFeatures[code] = new LicenseFeatureEntitlement(
                requestedFeature.Code,
                requestedFeature.Type,
                requestedFeature.Enabled,
                requestedFeature.MaxCount,
                requestedFeature.EnumValue);
        }

        if (resultingFeatures.Count > LicenseInputLimits.MaxFeaturesPerCompany)
            return Fail("company_feature_limit_exceeded", out errorCode);

        if (moduleAdditions.Length == 0 && newMaxUsers is null && featureIncreases.Count == 0)
            return Fail("amendment_has_no_entitlement_increase", out errorCode);

        var updatedCompany = new CompanyLicenseEntitlement(
            currentCompany.CompanyReference,
            Array.AsReadOnly(resultingModules),
            resultingMaxUsers,
            Array.AsReadOnly(resultingFeatures.Values
                .OrderBy(feature => feature.Code, StringComparer.Ordinal)
                .ToArray()));

        var updatedCompanies = companies
            .Select((company, index) => index == companyIndex
                ? updatedCompany
                : CloneCompanyEntitlement(company!))
            .ToArray();

        var candidate = currentPayload with
        {
            IssuedAtUtc = effectiveIssuedAt,
            Sequence = currentPayload.Sequence + 1,
            Limits = new LicenseLimits(
                currentPayload.Limits.MaxCompanies,
                Array.AsReadOnly(currentPayload.Limits.Subsystems.ToArray())),
            CompanyEntitlements = Array.AsReadOnly(updatedCompanies)
        };

        if (StructuralValidation.ValidatePayload(candidate).Count > 0 ||
            !LicenseRevisionRules.TryValidateSuccessor(currentPayload, candidate, out _))
        {
            return Fail("amendment_payload_invalid", out errorCode);
        }

        amendmentPayload = candidate;
        return true;
    }

    /// <summary>
    /// Builds a v2 amendment that extends support and/or update periods already granted by the
    /// current signed payload. A missing current period is not interpreted as an implicit grant.
    /// This method does not authenticate, purchase, approve, sign, persist or advance durable state.
    /// </summary>
    public static bool TryBuildSupportAndUpdatePeriodExtension(
        LicensePayload currentPayload,
        VendorLicense license,
        VendorInstallation installation,
        VendorCustomer customer,
        DateTimeOffset? newSupportUntilUtc,
        DateTimeOffset? newUpdatesUntilUtc,
        DateTimeOffset issuedAtUtc,
        out LicensePayload? amendmentPayload,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(currentPayload);
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(customer);

        amendmentPayload = null;
        errorCode = null;

        if (currentPayload.FormatVersion != LicensePayload.CurrentFormatVersion)
            return Fail("support_update_periods_require_payload_v2", out errorCode);

        if (license.Id == Guid.Empty || installation.Id == Guid.Empty || customer.Id == Guid.Empty ||
            license.InstallationId != installation.Id ||
            license.CustomerId != customer.Id ||
            installation.CustomerId != customer.Id ||
            !string.Equals(currentPayload.LicenseId, license.Id.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(currentPayload.InstallationId, installation.Id.ToString("D"), StringComparison.Ordinal))
        {
            return Fail("license_installation_mismatch", out errorCode);
        }

        if (!string.Equals(customer.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(installation.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(license.Status, "Issued", StringComparison.Ordinal))
        {
            return Fail("license_not_amendable", out errorCode);
        }

        if (StructuralValidation.ValidatePayload(currentPayload).Count > 0)
            return Fail("current_license_payload_invalid", out errorCode);

        if (license.LastIssuedSequence != currentPayload.Sequence)
            return Fail("license_sequence_stale", out errorCode);

        if (!string.Equals(license.ProductCode, currentPayload.ProductCode, StringComparison.Ordinal) ||
            !string.Equals(license.LicenseKind, currentPayload.LicenseKind, StringComparison.Ordinal) ||
            license.GraceDays != currentPayload.GraceDays ||
            license.MaxCompanies != currentPayload.Limits.MaxCompanies ||
            license.ValidFromUtc.ToUniversalTime() != currentPayload.NotBeforeUtc?.ToUniversalTime() ||
            license.ValidUntilUtc?.ToUniversalTime() != currentPayload.ExpiresAtUtc?.ToUniversalTime())
        {
            return Fail("license_metadata_stale", out errorCode);
        }

        if (currentPayload.Sequence == int.MaxValue)
            return Fail("license_sequence_exhausted", out errorCode);

        if (issuedAtUtc == default ||
            issuedAtUtc.ToUniversalTime() < currentPayload.IssuedAtUtc.ToUniversalTime())
        {
            return Fail("amendment_issued_at_invalid", out errorCode);
        }

        var effectiveIssuedAt = issuedAtUtc.ToUniversalTime();
        if (currentPayload.NotBeforeUtc is { } notBefore &&
            notBefore.ToUniversalTime() > effectiveIssuedAt)
        {
            return Fail("license_not_yet_valid", out errorCode);
        }

        // A term license cannot be amended after its actual expiry; no grace period is implied.
        // Support/update dates remain independent from ExpiresAtUtc for perpetual licenses.
        if (currentPayload.LicenseKind == LicensePayload.KindTerm &&
            (currentPayload.ExpiresAtUtc is not { } expiresAt ||
             expiresAt.ToUniversalTime() <= effectiveIssuedAt))
        {
            return Fail("license_outside_validity", out errorCode);
        }

        if (newSupportUntilUtc is null && newUpdatesUntilUtc is null)
            return Fail("amendment_has_no_period_increase", out errorCode);

        if (!TryExtendPeriod(
                currentPayload.SupportUntilUtc, newSupportUntilUtc, effectiveIssuedAt,
                "support", out var supportUntilUtc, out errorCode))
            return false;

        if (!TryExtendPeriod(
                currentPayload.UpdatesUntilUtc, newUpdatesUntilUtc, effectiveIssuedAt,
                "updates", out var updatesUntilUtc, out errorCode))
            return false;

        // Snapshot nested collections before returning a payload that will later be canonicalized
        // and signed. Preserve all Company entitlements and all installation-level validity fields.
        var candidate = currentPayload with
        {
            IssuedAtUtc = effectiveIssuedAt,
            Sequence = currentPayload.Sequence + 1,
            Limits = new LicenseLimits(
                currentPayload.Limits.MaxCompanies,
                Array.AsReadOnly(currentPayload.Limits.Subsystems.ToArray())),
            CompanyEntitlements = CloneCompanyEntitlements(currentPayload.CompanyEntitlements),
            SupportUntilUtc = supportUntilUtc,
            UpdatesUntilUtc = updatesUntilUtc
        };

        if (StructuralValidation.ValidatePayload(candidate).Count > 0 ||
            !LicenseRevisionRules.TryValidateSuccessor(currentPayload, candidate, out _))
        {
            return Fail("amendment_payload_invalid", out errorCode);
        }

        amendmentPayload = candidate;
        return true;
    }

    private static bool TryExtendPeriod(
        DateTimeOffset? currentUntilUtc,
        DateTimeOffset? requestedUntilUtc,
        DateTimeOffset issuedAtUtc,
        string periodName,
        out DateTimeOffset? extendedUntilUtc,
        out string? errorCode)
    {
        extendedUntilUtc = currentUntilUtc;
        errorCode = null;

        if (requestedUntilUtc is null)
            return true;

        if (currentUntilUtc is not { } currentUntil)
            return Fail(periodName + "_period_not_previously_granted", out errorCode);

        var requestedUntil = requestedUntilUtc.Value.ToUniversalTime();
        if (requestedUntil <= currentUntil.ToUniversalTime())
            return Fail(periodName + "_period_not_increased", out errorCode);

        if (requestedUntil <= issuedAtUtc)
            return Fail(periodName + "_period_not_future", out errorCode);

        extendedUntilUtc = requestedUntil;
        return true;
    }

    private static IReadOnlyList<CompanyLicenseEntitlement?>? CloneCompanyEntitlements(
        IReadOnlyList<CompanyLicenseEntitlement?>? companies)
    {
        if (companies is null)
            return null;

        var snapshot = companies
            .Select(company => company is null ? null : CloneCompanyEntitlement(company))
            .ToArray();

        return Array.AsReadOnly(snapshot);
    }

    private static CompanyLicenseEntitlement CloneCompanyEntitlement(CompanyLicenseEntitlement company)
        => new(
            company.CompanyReference,
            company.Modules is null ? null : Array.AsReadOnly(company.Modules.ToArray()),
            company.MaxUsers,
            company.Features is null
                ? null
                : Array.AsReadOnly(company.Features
                    .Select(feature => feature is null
                        ? null
                        : new LicenseFeatureEntitlement(
                            feature.Code,
                            feature.Type,
                            feature.Enabled,
                            feature.MaxCount,
                            feature.EnumValue))
                    .ToArray()));

    private static bool Fail(string code, out string? errorCode)
    {
        errorCode = code;
        return false;
    }
}
