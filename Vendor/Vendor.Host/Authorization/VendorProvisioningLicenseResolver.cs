using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Host.Signing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Resolves one Vendor license to the latest (or a previously bound) verified signed revision.
/// Client-supplied subsystem codes are only a requested subset; the signed envelope is authoritative.
/// </summary>
public sealed class VendorProvisioningLicenseResolver(
    VendorDbContext db,
    LicenseSigningTrustStore trustStore,
    TimeProvider timeProvider,
    VendorProvisioningCompanyReferenceGate? companyReferenceGate = null)
{
    private readonly VendorProvisioningCompanyReferenceGate _companyReferenceGate =
        companyReferenceGate ?? VendorProvisioningCompanyReferenceGate.Disabled;

    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public Task<VendorProvisioningLicenseBindingResult> ResolveLatestAsync(
        Guid licenseId,
        VendorCustomer customer,
        VendorInstallation installation,
        IReadOnlyList<string>? requestedSubsystems,
        CancellationToken cancellationToken) =>
        ResolveAsync(licenseId, customer, installation, requestedSubsystems, null, null, true, cancellationToken);

    /// <summary>
    /// <paramref name="requestedCompanyReference"/> is only a selector for a company-scoped (v2) license; the
    /// returned reference is read from the verified signed payload. It is rejected for legacy v1 licenses.
    /// </summary>
    public Task<VendorProvisioningLicenseBindingResult> ResolveLatestAsync(
        Guid licenseId,
        VendorCustomer customer,
        VendorInstallation installation,
        IReadOnlyList<string>? requestedSubsystems,
        string? requestedCompanyReference,
        CancellationToken cancellationToken) =>
        ResolveAsync(
            licenseId, customer, installation, requestedSubsystems, requestedCompanyReference, null, true,
            cancellationToken);

    public Task<VendorProvisioningLicenseBindingResult> ResolveSequenceAsync(
        Guid licenseId,
        int sequence,
        VendorCustomer customer,
        VendorInstallation installation,
        IReadOnlyList<string>? requestedSubsystems,
        CancellationToken cancellationToken) =>
        ResolveAsync(licenseId, customer, installation, requestedSubsystems, null, sequence, false, cancellationToken);

    public Task<VendorProvisioningLicenseBindingResult> ResolveSequenceAsync(
        Guid licenseId,
        int sequence,
        VendorCustomer customer,
        VendorInstallation installation,
        IReadOnlyList<string>? requestedSubsystems,
        string? requestedCompanyReference,
        CancellationToken cancellationToken) =>
        ResolveAsync(
            licenseId, customer, installation, requestedSubsystems, requestedCompanyReference, sequence, false,
            cancellationToken);

    private async Task<VendorProvisioningLicenseBindingResult> ResolveAsync(
        Guid licenseId,
        VendorCustomer customer,
        VendorInstallation installation,
        IReadOnlyList<string>? requestedSubsystems,
        string? requestedCompanyReference,
        int? expectedSequence,
        bool requireLatest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(installation);

        if (licenseId == Guid.Empty || customer.Id == Guid.Empty || installation.Id == Guid.Empty)
            return VendorProvisioningLicenseBindingResult.NotFound("provisioning_license_not_found");

        // Never trust target status values captured before the current transaction began.
        var currentCustomer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == customer.Id, cancellationToken);
        var currentInstallation = await db.Installations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == installation.Id, cancellationToken);
        if (currentCustomer is null || currentInstallation is null ||
            currentInstallation.CustomerId != currentCustomer.Id)
            return VendorProvisioningLicenseBindingResult.NotFound("provisioning_license_not_found");

        if (!string.Equals(currentCustomer.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(currentInstallation.Status, "Active", StringComparison.Ordinal))
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_inactive");

        var license = await db.Licenses.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == licenseId, cancellationToken);
        if (license is null || license.CustomerId != customer.Id ||
            license.InstallationId is Guid boundInstallation && boundInstallation != currentInstallation.Id)
            return VendorProvisioningLicenseBindingResult.NotFound("provisioning_license_not_found");

        if (license.Status != "Issued")
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_inactive");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (license.ValidFromUtc > now ||
            license.LicenseKind == LicensePayload.KindPerpetual && license.ValidUntilUtc is not null ||
            license.LicenseKind == LicensePayload.KindTerm &&
                (license.ValidUntilUtc is null || license.ValidUntilUtc <= now))
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_outside_validity");

        IQueryable<VendorSignedLicenseEnvelope> envelopes = db.SignedLicenseEnvelopes.AsNoTracking()
            .Where(item => item.LicenseId == licenseId);
        if (expectedSequence is int sequence)
            envelopes = envelopes.Where(item => item.Sequence == sequence);
        else
            envelopes = envelopes.OrderByDescending(item => item.Sequence);

        var storedEnvelope = await envelopes.FirstOrDefaultAsync(cancellationToken);
        if (storedEnvelope is null || storedEnvelope.Sequence < 1)
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_envelope_missing");

        if (requireLatest && storedEnvelope.Sequence != license.LastIssuedSequence)
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_sequence_stale");

        LicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(storedEnvelope.EnvelopeJson, EnvelopeJsonOptions);
        }
        catch (JsonException)
        {
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_envelope_invalid");
        }

        if (envelope is null ||
            envelope.FormatVersion != LicenseAlgorithms.CurrentFormatVersion ||
            !string.Equals(envelope.Kid, storedEnvelope.KeyId, StringComparison.Ordinal) ||
            !string.Equals(envelope.Algorithm, storedEnvelope.Algorithm, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(envelope.PayloadJson) ||
            !string.Equals(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant(),
                storedEnvelope.PayloadSha256,
                StringComparison.Ordinal))
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_envelope_invalid");

        if (!trustStore.TryGetTrustedPublicKey(envelope.Kid, out var publicKey))
            return VendorProvisioningLicenseBindingResult.Unavailable("provisioning_license_signing_key_untrusted");

        var validator = new LicenseEnvelopeValidator(
            new StaticTrustedKeyResolver([
                new TrustedPublicKey(envelope.Kid, envelope.Algorithm, publicKey.ToArray())
            ]),
            new EcdsaP256Sha256Verifier(),
            new LicenseEvaluator(timeProvider));
        var validation = validator.Validate(envelope, currentInstallation.Id.ToString("D"));
        if (!validation.Success || validation.Payload is null ||
            validation.Status is LicenseStatus.Grace or LicenseStatus.Restricted or LicenseStatus.Invalid)
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_signature_or_payload_invalid");

        var payload = validation.Payload;
        if (!Guid.TryParseExact(payload.LicenseId, "D", out var payloadLicenseId) ||
            payloadLicenseId != license.Id ||
            payload.Sequence != storedEnvelope.Sequence ||
            !string.Equals(payload.InstallationId, currentInstallation.Id.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(payload.ProductCode, license.ProductCode, StringComparison.Ordinal) ||
            !string.Equals(payload.LicenseKind, license.LicenseKind, StringComparison.Ordinal) ||
            payload.GraceDays != license.GraceDays ||
            payload.NotBeforeUtc?.ToUniversalTime() != license.ValidFromUtc.ToUniversalTime() ||
            payload.ExpiresAtUtc?.ToUniversalTime() != license.ValidUntilUtc?.ToUniversalTime() ||
            payload.Limits.MaxCompanies != license.MaxCompanies)
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_binding_mismatch");

        // Payload v2 stores modules per opaque CompanyReference and intentionally has no
        // installation-wide subsystem list. Schema v4 has no trusted company-reference binding, so
        // company entitlements are never flattened. Schema v5 binds exactly one CompanyReference taken
        // from this signed payload, and only while the (default-off) gate is open.
        if (payload.FormatVersion == LicensePayload.CurrentFormatVersion)
        {
            if (!_companyReferenceGate.IsEnabled)
                return VendorProvisioningLicenseBindingResult.Conflict(
                    "provisioning_company_reference_mapping_required");

            var selection = VendorProvisioningCompanyReferenceRules.Select(
                payload, requestedCompanyReference, requestedSubsystems);
            return selection.Succeeded
                ? VendorProvisioningLicenseBindingResult.Success(
                    license.Id.ToString("D"), storedEnvelope.Sequence, selection.Subsystems!,
                    selection.Entitlements!, selection.CompanyReference)
                : new VendorProvisioningLicenseBindingResult(false, selection.StatusCode, selection.ErrorCode);
        }

        // A company selector is meaningless for a legacy installation-wide license.
        if (requestedCompanyReference is not null)
            return VendorProvisioningLicenseBindingResult.BadRequest("provisioning_company_reference_not_applicable");

        var entitlements = payload.Limits.Subsystems
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (entitlements.Length == 0)
            return VendorProvisioningLicenseBindingResult.Conflict("provisioning_license_has_no_subsystems");
        string[] selected;
        if (requestedSubsystems is null)
        {
            selected = entitlements;
        }
        else
        {
            if (requestedSubsystems.Count is < 1 or > 4 ||
                requestedSubsystems.Any(code => string.IsNullOrWhiteSpace(code) ||
                    !LicenseSubsystemCodes.IsAllowed(code)) ||
                requestedSubsystems.Distinct(StringComparer.Ordinal).Count() != requestedSubsystems.Count)
                return VendorProvisioningLicenseBindingResult.BadRequest("invalid_provisioning_subsystems");

            selected = requestedSubsystems.OrderBy(code => code, StringComparer.Ordinal).ToArray();
            // Entitlement is decided by the pure engine against the signed payload: a requested
            // code outside the signed installation-wide allowlist is denied (fail-closed).
            if (selected.Any(code => !EntitlementPolicyEngine.EvaluateLegacySubsystem(payload, code).Allowed))
                return VendorProvisioningLicenseBindingResult.Conflict("provisioning_subsystem_not_entitled");
        }

        return VendorProvisioningLicenseBindingResult.Success(
            license.Id.ToString("D"), storedEnvelope.Sequence, selected, entitlements);
    }
}

public sealed record VendorProvisioningLicenseBindingResult(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? LicenseId = null,
    int Sequence = 0,
    IReadOnlyList<string>? Subsystems = null,
    IReadOnlyList<string>? Entitlements = null,
    string? CompanyReference = null)
{
    /// <param name="companyReference">
    /// Null for legacy (schema v4) bindings; otherwise the reference read from the signed v2 payload (schema v5).
    /// </param>
    public static VendorProvisioningLicenseBindingResult Success(
        string licenseId, int sequence, IReadOnlyList<string> subsystems, IReadOnlyList<string> entitlements,
        string? companyReference = null) =>
        new(true, StatusCodes.Status200OK, null, licenseId, sequence, subsystems, entitlements, companyReference);

    public static VendorProvisioningLicenseBindingResult BadRequest(string code) =>
        new(false, StatusCodes.Status400BadRequest, code);

    public static VendorProvisioningLicenseBindingResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code);

    public static VendorProvisioningLicenseBindingResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);

    public static VendorProvisioningLicenseBindingResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, code);
}
