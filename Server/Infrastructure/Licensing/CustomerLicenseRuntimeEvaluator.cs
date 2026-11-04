using System.Text.Json;
using Faranou.Licensing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Licensing;

/// <summary>
/// Stable result codes for customer runtime license evaluation. Callers must treat any
/// non-success code as fail-closed; Unavailable is never equivalent to NotRevoked.
/// </summary>
public sealed record CustomerLicenseRuntimeResult(
    bool IsUsable,
    LicenseStatus? Status,
    string? ErrorCode,
    string? LicenseId,
    int? Sequence)
{
    public static CustomerLicenseRuntimeResult Usable(LicenseStatus status, string licenseId, int sequence) =>
        new(true, status, null, licenseId, sequence);

    public static CustomerLicenseRuntimeResult Rejected(string errorCode, string? licenseId = null, int? sequence = null) =>
        new(false, LicenseStatus.Invalid, errorCode, licenseId, sequence);
}

/// <summary>
/// Single customer-runtime entry point that decides whether the currently installed license
/// is usable. Always re-validates the stored signed envelope through
/// <see cref="ICustomerLicenseEnvelopeValidator"/> so offline revocation, trust and TPM
/// failure modes cannot be bypassed by trusting the database row alone.
/// </summary>
public interface ICustomerLicenseRuntimeEvaluator
{
    Task<CustomerLicenseRuntimeResult> EvaluateCurrentAsync(
        string localInstallationId,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerLicenseRuntimeEvaluator : ICustomerLicenseRuntimeEvaluator
{
    private const int MaximumEnvelopeJsonLength = 300_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Data.DatabaseContext _database;
    private readonly ICustomerLicenseEnvelopeValidator _validator;
    private readonly ILogger<CustomerLicenseRuntimeEvaluator> _logger;

    public CustomerLicenseRuntimeEvaluator(
        Data.DatabaseContext database,
        ICustomerLicenseEnvelopeValidator validator,
        ILogger<CustomerLicenseRuntimeEvaluator> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CustomerLicenseRuntimeResult> EvaluateCurrentAsync(
        string localInstallationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localInstallationId) || localInstallationId.Length > 128)
            return CustomerLicenseRuntimeResult.Rejected("installation_id_invalid");

        Domain.InstalledLicense? current;
        try
        {
            current = await _database.InstalledLicenses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.InstallationId == localInstallationId &&
                            item.State == Domain.InstalledLicense.StateCurrent,
                    cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Customer license runtime evaluation could not read installed licenses.");
            return CustomerLicenseRuntimeResult.Rejected("license_state_unavailable");
        }

        if (current is null)
            return CustomerLicenseRuntimeResult.Rejected("license_missing");

        if (string.IsNullOrWhiteSpace(current.EnvelopeJson) ||
            current.EnvelopeJson.Length > MaximumEnvelopeJsonLength)
        {
            return CustomerLicenseRuntimeResult.Rejected("envelope_json_invalid", current.LicenseId, current.Sequence);
        }

        LicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(current.EnvelopeJson, JsonOptions);
        }
        catch (JsonException)
        {
            return CustomerLicenseRuntimeResult.Rejected("envelope_json_invalid", current.LicenseId, current.Sequence);
        }

        if (envelope is null)
            return CustomerLicenseRuntimeResult.Rejected("envelope_json_invalid", current.LicenseId, current.Sequence);

        LicenseValidationResult validation;
        try
        {
            validation = await _validator.ValidateAsync(envelope, localInstallationId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Customer license runtime validation failed closed.");
            return CustomerLicenseRuntimeResult.Rejected(
                "offline_revocation_unavailable", current.LicenseId, current.Sequence);
        }

        if (!validation.Success || validation.Payload is null)
        {
            var code = string.IsNullOrWhiteSpace(validation.ErrorCode)
                ? "license_validation_failed"
                : validation.ErrorCode;
            return CustomerLicenseRuntimeResult.Rejected(code, current.LicenseId, current.Sequence);
        }

        if (validation.Status is not (LicenseStatus.Valid or LicenseStatus.ExpiringSoon or LicenseStatus.Restricted))
        {
            return CustomerLicenseRuntimeResult.Rejected(
                "license_status_policy_unapproved", current.LicenseId, current.Sequence);
        }

        return CustomerLicenseRuntimeResult.Usable(
            validation.Status,
            validation.Payload.LicenseId,
            validation.Payload.Sequence);
    }
}
