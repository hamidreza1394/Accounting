using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Signing;

public sealed class OfflineRevocationSigningTrustedKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string PublicKeySubjectPublicKeyInfoBase64 { get; set; } = string.Empty;
    public bool Revoked { get; set; }
}

/// <summary>
/// Independent public trust set for offline-revocation signatures. It is intentionally separate
/// from license and provisioning trust; no private key material is accepted here.
/// </summary>
public sealed class OfflineRevocationSigningTrustStore
{
    private readonly IReadOnlyDictionary<string, TrustedKey> _keys;

    public OfflineRevocationSigningTrustStore(IEnumerable<OfflineRevocationSigningTrustedKeyOptions> configuredKeys)
    {
        ArgumentNullException.ThrowIfNull(configuredKeys);
        var keys = new Dictionary<string, TrustedKey>(StringComparer.Ordinal);

        foreach (var configuredKey in configuredKeys)
        {
            if (configuredKey is null || !IsValidKeyId(configuredKey.KeyId) || keys.ContainsKey(configuredKey.KeyId))
                throw new InvalidOperationException("Offline revocation signing trust configuration contains an invalid or duplicate key ID.");

            if (string.IsNullOrWhiteSpace(configuredKey.PublicKeySubjectPublicKeyInfoBase64))
                throw new InvalidOperationException("Offline revocation signing trust configuration is missing public-key material.");

            byte[] encoded;
            try
            {
                encoded = Convert.FromBase64String(configuredKey.PublicKeySubjectPublicKeyInfoBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Offline revocation signing public-key encoding is invalid.", exception);
            }

            if (encoded.Length is 0 or > 1024)
                throw new InvalidOperationException("Offline revocation signing public keys must be between 1 and 1024 bytes.");

            try
            {
                using var publicKey = ECDsa.Create();
                publicKey.ImportSubjectPublicKeyInfo(encoded, out var bytesRead);
                if (bytesRead != encoded.Length || publicKey.KeySize != 256 ||
                    !string.Equals(publicKey.ExportParameters(false).Curve.Oid.Value,
                        ECCurve.NamedCurves.nistP256.Oid.Value, StringComparison.Ordinal))
                    throw new InvalidOperationException("Offline revocation signing keys must be ECDSA P-256 public keys.");
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException("Offline revocation signing public key is invalid.", exception);
            }

            keys.Add(configuredKey.KeyId, new TrustedKey(encoded, configuredKey.Revoked));
        }

        _keys = keys;
    }

    public bool TryGetTrustedPublicKey(string? keyId, out ReadOnlyMemory<byte> subjectPublicKeyInfo)
    {
        subjectPublicKeyInfo = ReadOnlyMemory<byte>.Empty;
        if (!IsValidKeyId(keyId) || !_keys.TryGetValue(keyId!, out var key) || key.Revoked)
            return false;

        subjectPublicKeyInfo = key.SubjectPublicKeyInfo;
        return true;
    }

    private static bool IsValidKeyId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= OfflineRevocationListValidator.MaxKeyIdLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private sealed record TrustedKey(byte[] SubjectPublicKeyInfo, bool Revoked);
}

/// <summary>
/// Snapshot freshness is an explicit deployment policy. Zero/unconfigured is invalid and fails
/// revocation closed; the application never invents an expiry policy for an offline installation.
/// </summary>
public sealed class VendorOfflineRevocationOptions
{
    public int SnapshotValidityDays { get; set; }
}

/// <summary>
/// Produces a canonical, signed, durable revocation snapshot inside the caller's maker-checker
/// transaction. The global sequence is serialized with a transaction-owned SQL application lock;
/// a failed signer or audit append cannot commit license, approval, snapshot, or sequence state.
/// </summary>
public sealed class VendorOfflineRevocationIssuanceService(
    VendorDbContext db,
    IOfflineRevocationSigningProvider signingProvider,
    OfflineRevocationSigningTrustStore trustStore,
    IOptions<VendorOfflineRevocationOptions> options,
    IVendorAuditWriter audit,
    TimeProvider timeProvider,
    ILogger<VendorOfflineRevocationIssuanceService> logger)
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<VendorOfflineRevocationIssuanceResult> RevokeApprovedAsync(
        VendorApprovalRequest approval,
        Guid reviewerUserId,
        byte[] expectedApprovalVersion,
        string? reviewComment,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(expectedApprovalVersion);

        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Offline revocation issuance must run inside the approval transaction.");

        if (approval.ResourceType != "license" || approval.TargetStatus != "Revoked" ||
            approval.Status != "Pending" || approval.RequestedByUserId == reviewerUserId ||
            approval.ResourceId == Guid.Empty || expectedApprovalVersion.Length != 8 ||
            !approval.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion) ||
            approval.ExpectedResourceRowVersion.Length != 8)
            return VendorOfflineRevocationIssuanceResult.Conflict("stale_approval_version");

        var validityDays = options.Value.SnapshotValidityDays;
        if (validityDays is < 1 or > OfflineRevocationListValidator.MaxValidityDays)
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_policy_unavailable");

        var license = await db.Licenses.SingleOrDefaultAsync(
            item => item.Id == approval.ResourceId, cancellationToken);
        if (license is null)
            return VendorOfflineRevocationIssuanceResult.NotFound("license_not_found");

        if (!license.RowVersion.AsSpan().SequenceEqual(approval.ExpectedResourceRowVersion) ||
            license.Status != "Issued" || license.LastIssuedSequence < 1)
            return VendorOfflineRevocationIssuanceResult.Conflict("approval_resource_stale");

        var hasIssuedEnvelope = await db.SignedLicenseEnvelopes.AsNoTracking().AnyAsync(
            item => item.LicenseId == license.Id && item.Sequence == license.LastIssuedSequence,
            cancellationToken);
        if (!hasIssuedEnvelope)
            return VendorOfflineRevocationIssuanceResult.Conflict("license_issuance_record_missing");

        if (!await TryAcquireSequenceLockAsync(cancellationToken))
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_sequence_unavailable");

        var previousSequence = await db.SignedRevocationSnapshots.AsNoTracking()
            .Select(item => (long?)item.Sequence)
            .MaxAsync(cancellationToken) ?? 0L;
        if (previousSequence == long.MaxValue)
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_sequence_exhausted");

        var sequence = previousSequence + 1;
        var revokedLicenseIds = await db.Licenses.AsNoTracking()
            .Where(item => item.Status == "Revoked")
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        revokedLicenseIds.Add(license.Id);

        var canonicalIds = revokedLicenseIds
            .Distinct()
            .Select(item => item.ToString("D"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (canonicalIds.Length > OfflineRevocationListValidator.MaxRevokedLicenseIds)
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_snapshot_limit_exceeded");

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var listId = Guid.NewGuid().ToString("D");
        var payload = new OfflineRevocationList(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            listId,
            sequence,
            now,
            now.AddDays(validityDays),
            canonicalIds);
        var payloadJson = OfflineRevocationListValidator.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        LicenseSigningResult signing;
        try
        {
            signing = await signingProvider.SignAsync(payloadBytes, cancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            logger.LogError(
                "Offline revocation signing failed with exception type {ExceptionType}.",
                exception.GetType().Name);
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_signing_unavailable");
        }

        if (!signing.Succeeded || string.IsNullOrWhiteSpace(signing.KeyId) ||
            signing.KeyId.Length > OfflineRevocationListValidator.MaxKeyIdLength ||
            !string.Equals(signing.Algorithm, LicenseAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal) ||
            signing.Signature.Length != 64)
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_signing_unavailable");

        if (!trustStore.TryGetTrustedPublicKey(signing.KeyId, out var trustedPublicKey))
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_signing_unavailable");

        var envelope = new OfflineRevocationListEnvelope(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            signing.KeyId,
            signing.Algorithm!,
            payloadJson,
            Convert.ToBase64String(signing.Signature.Span));
        if (!OfflineRevocationListValidator.TryValidateAndVerify(
                envelope, trustedPublicKey.Span, now, previousSequence, out var verified, out _) ||
            verified is null || verified.Sequence != sequence ||
            !string.Equals(verified.ListId, listId, StringComparison.Ordinal))
            return VendorOfflineRevocationIssuanceResult.Unavailable("offline_revocation_signing_unavailable");

        var envelopeJson = JsonSerializer.Serialize(envelope, EnvelopeJsonOptions);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

        db.Entry(license).Property(item => item.RowVersion).OriginalValue =
            approval.ExpectedResourceRowVersion;
        license.Status = "Revoked";

        db.Entry(approval).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAtUtc = now;
        approval.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();

        var snapshot = new VendorSignedRevocationSnapshot
        {
            Id = Guid.NewGuid(),
            ApprovalRequestId = approval.Id,
            TriggeringLicenseId = license.Id,
            Sequence = sequence,
            ListId = listId,
            KeyId = envelope.KeyId,
            Algorithm = envelope.Algorithm,
            EnvelopeJson = envelopeJson,
            PayloadSha256 = payloadHash,
            CreatedAtUtc = now
        };
        db.SignedRevocationSnapshots.Add(snapshot);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return VendorOfflineRevocationIssuanceResult.Conflict("stale_approval_or_resource_version");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return VendorOfflineRevocationIssuanceResult.Conflict("offline_revocation_sequence_conflict");
        }

        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.approval.approved", nameof(VendorApprovalRequest),
            approval.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.license.revoked", nameof(VendorLicense),
            license.Id.ToString("D"), correlationId), cancellationToken);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            reviewerUserId, "vendor.offline-revocation.snapshot-issued", nameof(VendorSignedRevocationSnapshot),
            snapshot.Id.ToString("D"), correlationId), cancellationToken);

        return VendorOfflineRevocationIssuanceResult.Success();
    }

    private async Task<bool> TryAcquireSequenceLockAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'Faranou.Vendor.OfflineRevocationSequence.v1',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 15000;
            SELECT @result;
            """;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) >= 0;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed record VendorOfflineRevocationIssuanceResult(bool Succeeded, int StatusCode, string? ErrorCode)
{
    public static VendorOfflineRevocationIssuanceResult Success() =>
        new(true, StatusCodes.Status200OK, null);
    public static VendorOfflineRevocationIssuanceResult Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);
    public static VendorOfflineRevocationIssuanceResult NotFound(string code) =>
        new(false, StatusCodes.Status404NotFound, code);
    public static VendorOfflineRevocationIssuanceResult Unavailable(string code) =>
        new(false, StatusCodes.Status503ServiceUnavailable, code);
}
