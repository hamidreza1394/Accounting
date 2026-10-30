using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Provisioning;

public sealed record CustomerProvisioningAcknowledgementSigningResult(
    bool Success,
    string? KeyId,
    string? SignatureBase64,
    string? ErrorCode);

/// <summary>
/// Customer-side acknowledgement signer. Production implementations must delegate to an
/// independently configured protected signing provider; this interface never owns key material.
/// </summary>
public interface ICustomerProvisioningAcknowledgementSigningProvider
{
    Task<CustomerProvisioningAcknowledgementSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalAcknowledgementUtf8,
        CancellationToken cancellationToken);
}

/// <summary>Safe default: customer acknowledgements cannot be signed until a protected provider is configured.</summary>
public sealed class UnavailableCustomerProvisioningAcknowledgementSigningProvider
    : ICustomerProvisioningAcknowledgementSigningProvider
{
    public Task<CustomerProvisioningAcknowledgementSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalAcknowledgementUtf8,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CustomerProvisioningAcknowledgementSigningResult(
            false, null, null, "provisioning_acknowledgement_signer_unavailable"));
}

public sealed record CustomerProvisioningApplyResult(
    bool Success,
    bool Replayed,
    string? ErrorCode,
    CustomerProvisioningAcknowledgementEnvelope? Acknowledgement,
    string? EnrollmentToken);

public sealed record CustomerProvisioningEnrollmentResult(bool Success, string? ErrorCode);
public sealed record CustomerProvisioningTokenReissueResult(bool Success, string? ErrorCode, string? EnrollmentToken);

/// <summary>
/// Applies a verified provisioning package in one customer-database transaction.
/// Raw enrollment tokens exist only in memory until the successful response is returned.
/// </summary>
public sealed class CustomerProvisioningApplyService
{
    private static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Data.DatabaseContext _database;
    private readonly CustomerProvisioningPackageVerifier _verifier;
    private readonly ICustomerProvisioningAcknowledgementSigningProvider _acknowledgementSigner;
    private readonly CustomerProvisioningSettings _settings;
    private readonly Security.PasswordHashService _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public CustomerProvisioningApplyService(
        Data.DatabaseContext database,
        CustomerProvisioningPackageVerifier verifier,
        ICustomerProvisioningAcknowledgementSigningProvider acknowledgementSigner,
        IOptions<CustomerProvisioningSettings> settings,
        Security.PasswordHashService passwordHasher,
        TimeProvider timeProvider)
    {
        _database = database;
        _verifier = verifier;
        _acknowledgementSigner = acknowledgementSigner;
        _settings = settings.Value;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<CustomerProvisioningApplyResult> ApplyAsync(
        CustomerProvisioningPackage package,
        CancellationToken cancellationToken)
    {
        if (!_verifier.TryVerify(package, out var payload, out var verificationError) || payload is null)
            return Failure(verificationError);

        var requestId = Guid.ParseExact(payload.RequestId, "D");
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(package.PayloadJson)))
            .ToLowerInvariant();
        var nonceHash = HashHex(payload.Nonce);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var storedAcknowledgement = await FindAcknowledgementAsync(payload.RequestId, cancellationToken);
        if (storedAcknowledgement is not null)
        {
            CustomerProvisioningAcknowledgement? storedPayload;
            try
            {
                storedPayload = JsonSerializer.Deserialize<CustomerProvisioningAcknowledgement>(
                    storedAcknowledgement.AcknowledgementJson, JsonOptions);
            }
            catch (JsonException)
            {
                return Failure("provisioning_acknowledgement_corrupt");
            }

            if (storedPayload is null ||
                !string.Equals(storedPayload.RequestId, payload.RequestId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(storedPayload.Nonce, payload.Nonce, StringComparison.Ordinal) ||
                !string.Equals(storedPayload.PayloadSha256, payloadHash, StringComparison.Ordinal) ||
                !string.Equals(storedPayload.CustomerReference, payload.CustomerReference, StringComparison.Ordinal) ||
                !string.Equals(storedPayload.InstallationId, payload.InstallationId, StringComparison.Ordinal))
                return Failure("provisioning_request_id_conflict");

            var claim = await _database.ProvisioningRequestClaims.AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
            if (claim is not null &&
                storedPayload.Result is "Applied" or "AlreadyApplied" &&
                string.Equals(claim.PayloadSha256, payloadHash, StringComparison.Ordinal) &&
                string.Equals(claim.NonceHash, nonceHash, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return new CustomerProvisioningApplyResult(true, true, null, storedAcknowledgement, null);
            }

            if (claim is null && storedPayload.Result == "Rejected")
            {
                // A stored rejection is replayed exactly; no side effect or new secret is produced.
                await transaction.CommitAsync(cancellationToken);
                return new CustomerProvisioningApplyResult(false, true, "provisioning_request_rejected",
                    storedAcknowledgement, null);
            }

            return Failure("provisioning_request_id_conflict");
        }

        var existingRequest = await _database.ProvisioningRequestClaims.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
        if (existingRequest is not null)
            return Failure("provisioning_acknowledgement_missing");

        var existingNonce = await _database.ProvisioningRequestClaims.AsNoTracking()
            .AnyAsync(item => item.NonceHash == nonceHash, cancellationToken);
        if (existingNonce)
            return Failure("provisioning_nonce_replayed");

        var parentResolution = await CustomerProvisioningParentClaimResolver.ResolveAsync(
            _database,
            payload.CompanyType,
            payload.ParentCompanyReference,
            _settings,
            now,
            cancellationToken);
        if (parentResolution.ErrorCode is not null)
            return await StoreRejectedAcknowledgementAsync(
                payload, payloadHash, parentResolution.ErrorCode, now, transaction, cancellationToken);

        if (await _database.Users.AnyAsync(user =>
                user.Username == payload.InitialAdminUserName ||
                user.EmailAddress == payload.InitialAdminEmail,
                cancellationToken))
        {
            return await StoreRejectedAcknowledgementAsync(
                payload, payloadHash, "provisioning_initial_admin_conflict", now, transaction, cancellationToken);
        }

        var company = new Domain.Company
        {
            Name = payload.CompanyName,
            CustomerBoundaryId = _settings.CustomerBoundaryId,
            IsActive = true
        };
        var fiscalPeriod = new Domain.FiscalPeriod
        {
            Company = company,
            Title = payload.FiscalPeriodTitle!,
            StartDate = payload.FiscalPeriodStartDate!.Value,
            EndDate = payload.FiscalPeriodEndDate!.Value,
            Status = Domain.FiscalPeriodStatus.Open
        };
        var role = new Domain.CompanyRole
        {
            Company = company,
            Code = "company-admin",
            Name = "مدیر شرکت",
            IsSystemRole = true,
            IsActive = true
        };

        var randomUnavailablePassword = CreateOpaqueSecret();
        var admin = new Domain.User(payload.InitialAdminEmail!)
        {
            Username = payload.InitialAdminUserName,
            Password = _passwordHasher.Hash(randomUnavailablePassword),
            IsActive = true,
            IsSystemic = false,
            IsProgrammer = false,
            IsUndeletable = false,
            IsEmailAddressVerified = false,
            DefaultCompanyId = null
        };
        var membership = new Domain.UserCompanyMembership
        {
            Company = company,
            Role = role,
            UserId = admin.Id,
            IsActive = true,
            CreatedAtUtc = now
        };

        var permissionCodes = CustomerProvisioningSubsystemCatalog.ResolveExistingPermissionCodes(
            payload.Subsystems,
            await _database.PermissionDefinitions.AsNoTracking()
                .Select(permission => permission.Code)
                .ToListAsync(cancellationToken));
        var permissions = await _database.PermissionDefinitions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .ToListAsync(cancellationToken);
        foreach (var permission in permissions)
        {
            role.RolePermissions.Add(new Domain.CompanyRolePermission { Role = role, Permission = permission });
        }

        var enrollmentToken = CreateOpaqueSecret();
        var claimEntity = new Domain.ProvisioningRequestClaim
        {
            RequestId = requestId,
            NonceHash = nonceHash,
            PayloadSha256 = payloadHash,
            CompanyType = payload.CompanyType,
            ParentCompanyId = parentResolution.ParentCompanyId,
            SubsystemsJson = JsonSerializer.Serialize(payload.Subsystems.Order(StringComparer.Ordinal)),
            CustomerReference = payload.CustomerReference,
            InstallationId = payload.InstallationId,
            CompanyId = company.Id,
            FiscalPeriodId = fiscalPeriod.Id,
            AdminUserId = admin.Id,
            EnrollmentTokenHash = HashHex(enrollmentToken),
            EnrollmentExpiresAtUtc = now.Add(EnrollmentLifetime),
            EnrollmentConsumedAtUtc = null,
            AppliedAtUtc = now,
            MustChangePassword = true
        };

        var acknowledgement = CreateAcknowledgement(payload, payloadHash, "Applied", null, now);
        var signedAcknowledgement = await SignAcknowledgementAsync(acknowledgement, cancellationToken);
        if (signedAcknowledgement is null)
            return Failure("provisioning_acknowledgement_signer_unavailable");

        _database.Companies.Add(company);
        _database.FiscalPeriods.Add(fiscalPeriod);
        _database.CompanyRoles.Add(role);
        _database.Users.Add(admin);
        _database.UserCompanyMemberships.Add(membership);
        _database.ProvisioningRequestClaims.Add(claimEntity);
        _database.SecurityAuditEvents.Add(CreateAcknowledgementAudit(
            company.Id, payload.RequestId, signedAcknowledgement, "ProvisioningApplied", now));

        try
        {
            await _database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _database.ChangeTracker.Clear();
            var concurrentClaim = await _database.ProvisioningRequestClaims.AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
            if (concurrentClaim is not null &&
                string.Equals(concurrentClaim.PayloadSha256, payloadHash, StringComparison.Ordinal) &&
                string.Equals(concurrentClaim.NonceHash, nonceHash, StringComparison.Ordinal))
            {
                var concurrentAck = await FindAcknowledgementAsync(payload.RequestId, cancellationToken);
                if (concurrentAck is not null)
                    return new CustomerProvisioningApplyResult(true, true, null, concurrentAck, null);
            }

            return Failure("provisioning_apply_conflict");
        }

        return new CustomerProvisioningApplyResult(true, false, null, signedAcknowledgement, enrollmentToken);
    }

    public async Task<CustomerProvisioningTokenReissueResult> ReissueEnrollmentTokenAsync(
        Guid requestId,
        string reasonCode,
        string? remoteIpAddress,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty ||
            reasonCode is not ("delivery_lost" or "expired" or "suspected_exposure"))
            return new CustomerProvisioningTokenReissueResult(false, "enrollment_reissue_invalid", null);

        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var claim = await _database.ProvisioningRequestClaims
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
        if (claim is null || !claim.MustChangePassword || claim.EnrollmentConsumedAtUtc is not null)
            return new CustomerProvisioningTokenReissueResult(false, "enrollment_reissue_not_allowed", null);

        var token = CreateOpaqueSecret();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        claim.EnrollmentTokenHash = HashHex(token);
        claim.EnrollmentExpiresAtUtc = now.Add(EnrollmentLifetime);
        _database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            TenantId = claim.CompanyId,
            EventType = "ProvisioningEnrollmentTokenReissued",
            EntityType = "ProvisioningRequestClaim",
            EntityId = claim.Id.ToString("D"),
            OccurredAtUtc = now,
            IpAddress = NormalizeIp(remoteIpAddress),
            DetailsJson = JsonSerializer.Serialize(new { reasonCode })
        });

        await _database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CustomerProvisioningTokenReissueResult(true, null, token);
    }

    public async Task<CustomerProvisioningEnrollmentResult> CompleteEnrollmentAsync(
        string? username,
        string? enrollmentToken,
        string? newPassword,
        string? remoteIpAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 64 ||
            string.IsNullOrWhiteSpace(enrollmentToken) || enrollmentToken.Length != 43 ||
            enrollmentToken.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')) ||
            string.IsNullOrWhiteSpace(newPassword) || newPassword.Length is < 12 or > 128 ||
            newPassword.Any(char.IsControl))
            return new CustomerProvisioningEnrollmentResult(false, "enrollment_invalid");

        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var user = await _database.Users.SingleOrDefaultAsync(
            item => item.Username == username, cancellationToken);
        if (user is null || !user.IsActive)
            return new CustomerProvisioningEnrollmentResult(false, "enrollment_invalid");

        var claim = await _database.ProvisioningRequestClaims
            .SingleOrDefaultAsync(item => item.AdminUserId == user.Id && item.MustChangePassword, cancellationToken);
        if (claim is null || claim.EnrollmentConsumedAtUtc is not null)
            return new CustomerProvisioningEnrollmentResult(false, "enrollment_invalid");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (claim.EnrollmentExpiresAtUtc <= now)
            return new CustomerProvisioningEnrollmentResult(false, "enrollment_expired");

        var recentFailures = await _database.SecurityAuditEvents.AsNoTracking()
            .CountAsync(audit => audit.EventType == "ProvisioningEnrollmentFailed" &&
                audit.EntityType == "ProvisioningRequestClaim" &&
                audit.EntityId == claim.Id.ToString("D") &&
                audit.OccurredAtUtc >= now.AddMinutes(-15), cancellationToken);
        if (recentFailures >= 5)
            return new CustomerProvisioningEnrollmentResult(false, "enrollment_rate_limited");

        var suppliedHash = HashHex(enrollmentToken);
        if (!FixedHashEquals(claim.EnrollmentTokenHash, suppliedHash))
        {
            _database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
            {
                TenantId = claim.CompanyId,
                EventType = "ProvisioningEnrollmentFailed",
                EntityType = "ProvisioningRequestClaim",
                EntityId = claim.Id.ToString("D"),
                OccurredAtUtc = now,
                IpAddress = NormalizeIp(remoteIpAddress),
                DetailsJson = "{\"reasonCode\":\"invalid_token\"}"
            });
            await _database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CustomerProvisioningEnrollmentResult(false, "enrollment_invalid");
        }

        claim.EnrollmentConsumedAtUtc = now;
        claim.MustChangePassword = false;
        user.Password = _passwordHasher.Hash(newPassword);
        user.SetUpdateDateTime();
        _database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = user.Id,
            TenantId = claim.CompanyId,
            EventType = "ProvisioningEnrollmentCompleted",
            EntityType = "ProvisioningRequestClaim",
            EntityId = claim.Id.ToString("D"),
            OccurredAtUtc = now,
            IpAddress = NormalizeIp(remoteIpAddress),
            DetailsJson = "{\"passwordSet\":true}"
        });

        await _database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CustomerProvisioningEnrollmentResult(true, null);
    }

    private async Task<CustomerProvisioningApplyResult> StoreRejectedAcknowledgementAsync(
        CustomerProvisioningPayload payload,
        string payloadHash,
        string errorCode,
        DateTime now,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var acknowledgement = CreateAcknowledgement(payload, payloadHash, "Rejected", errorCode, now);
        var envelope = await SignAcknowledgementAsync(acknowledgement, cancellationToken);
        if (envelope is null)
            return Failure("provisioning_acknowledgement_signer_unavailable");

        _database.SecurityAuditEvents.Add(CreateAcknowledgementAudit(
            null, payload.RequestId, envelope, "ProvisioningRejected", now));
        await _database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CustomerProvisioningApplyResult(false, false, errorCode, envelope, null);
    }

    private async Task<CustomerProvisioningAcknowledgementEnvelope?> FindAcknowledgementAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        var item = await _database.SecurityAuditEvents.AsNoTracking()
            .Where(audit => audit.EntityType == "ProvisioningRequest" &&
                audit.EntityId == requestId &&
                (audit.EventType == "ProvisioningApplied" || audit.EventType == "ProvisioningRejected"))
            .OrderBy(audit => audit.OccurredAtUtc)
            .Select(audit => audit.DetailsJson)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(item))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CustomerProvisioningAcknowledgementEnvelope>(item, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<CustomerProvisioningAcknowledgementEnvelope?> SignAcknowledgementAsync(
        CustomerProvisioningAcknowledgement acknowledgement,
        CancellationToken cancellationToken)
    {
        var canonicalJson = CustomerProvisioningAcknowledgementValidator.Serialize(acknowledgement);
        var signing = await _acknowledgementSigner.SignAsync(
            Encoding.UTF8.GetBytes(canonicalJson), cancellationToken);
        if (!signing.Success ||
            string.IsNullOrWhiteSpace(signing.KeyId) ||
            string.IsNullOrWhiteSpace(signing.SignatureBase64))
            return null;

        return new CustomerProvisioningAcknowledgementEnvelope(
            CustomerProvisioningAcknowledgementValidator.CurrentSchemaVersion,
            signing.KeyId,
            LicenseAlgorithms.EcdsaP256Sha256,
            canonicalJson,
            signing.SignatureBase64);
    }

    private static CustomerProvisioningAcknowledgement CreateAcknowledgement(
        CustomerProvisioningPayload payload,
        string payloadHash,
        string result,
        string? errorCode,
        DateTime now) =>
        new(
            CustomerProvisioningAcknowledgementValidator.CurrentSchemaVersion,
            payload.RequestId,
            payload.Nonce,
            payloadHash,
            payload.CustomerReference,
            payload.InstallationId,
            result,
            new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)),
            errorCode);

    private static Domain.SecurityAuditEvent CreateAcknowledgementAudit(
        Guid? companyId,
        string requestId,
        CustomerProvisioningAcknowledgementEnvelope envelope,
        string eventType,
        DateTime now) =>
        new()
        {
            TenantId = companyId,
            EventType = eventType,
            EntityType = "ProvisioningRequest",
            EntityId = requestId,
            OccurredAtUtc = now,
            DetailsJson = JsonSerializer.Serialize(envelope, JsonOptions)
        };

    private static CustomerProvisioningApplyResult Failure(string errorCode) =>
        new(false, false, errorCode, null, null);

    private static bool FixedHashEquals(string storedHash, string suppliedHash)
    {
        if (storedHash.Length != 64 || suppliedHash.Length != 64)
            return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(storedHash),
                Convert.FromHexString(suppliedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? NormalizeIp(string? value) =>
        System.Net.IPAddress.TryParse(value, out var address) ? address.ToString() : null;

    private static string HashHex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string CreateOpaqueSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
