using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// SQL Server-backed append-only-by-contract writer. A transaction-scoped application lock serializes
/// writers across processes/instances; a process-local SemaphoreSlim would not provide that guarantee.
/// Database permissions and independent integrity checkpoints remain separate deployment controls.
/// </summary>
public sealed class VendorAuditWriter : IVendorAuditWriter
{
    private const string ApplicationLockResource = "FaranouVendorAuditChain";
    private const int ApplicationLockTimeoutMilliseconds = 10_000;
    private static readonly Regex ActionCodePattern =
        new("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SubjectTypePattern =
        new("^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly VendorDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public VendorAuditWriter(VendorDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<VendorAuditWriteResult> AppendAsync(
        VendorAuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        if (!_dbContext.Database.IsSqlServer())
        {
            throw new NotSupportedException("Vendor audit-chain writes require SQL Server transactional application locks.");
        }

        IDbContextTransaction? ownedTransaction = null;
        try
        {
            if (_dbContext.Database.CurrentTransaction is null)
            {
                ownedTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            }

            var transaction = _dbContext.Database.CurrentTransaction
                ?? throw new InvalidOperationException("The Vendor audit writer requires an active database transaction.");

            await AcquireTransactionLockAsync(transaction, cancellationToken);

            var leafCandidates = await _dbContext.AuditEvents
                .AsNoTracking()
                .Where(current => !_dbContext.AuditEvents.Any(next => next.PreviousHash == current.EventHash))
                .Take(2)
                .ToListAsync(cancellationToken);

            if (leafCandidates.Count > 1)
            {
                throw new VendorAuditChainIntegrityException("chain-fork-detected");
            }

            VendorAuditEvent? leaf = leafCandidates.SingleOrDefault();
            var hasAnyEvents = leaf is not null || await _dbContext.AuditEvents.AnyAsync(cancellationToken);
            if (hasAnyEvents && leaf is null)
            {
                throw new VendorAuditChainIntegrityException("missing-chain-leaf");
            }

            var previousHash = leaf?.EventHash ?? VendorAuditChainVerifier.GenesisHash;
            var rootCount = await _dbContext.AuditEvents.CountAsync(
                entry => entry.PreviousHash == VendorAuditChainVerifier.GenesisHash, cancellationToken);
            if ((leaf is null && rootCount != 0) || (leaf is not null && rootCount != 1))
            {
                throw new VendorAuditChainIntegrityException("invalid-root-count");
            }

            if (leaf is not null)
            {
                if (!VendorAuditChainVerifier.IsHash(leaf.EventHash) ||
                    !IsValidActionCode(leaf.ActionCode) ||
                    !IsValidSubjectType(leaf.SubjectType) ||
                    !IsSafeOpaqueId(leaf.SubjectId, 200) ||
                    !IsSafeOpaqueId(leaf.CorrelationId, 100) ||
                    leaf.MetadataJson is not null ||
                    !string.Equals(VendorAuditChainVerifier.ComputeEventHash(leaf), leaf.EventHash, StringComparison.Ordinal))
                {
                    throw new VendorAuditChainIntegrityException("invalid-chain-leaf");
                }

                if (!string.Equals(leaf.PreviousHash, VendorAuditChainVerifier.GenesisHash, StringComparison.Ordinal) &&
                    !await _dbContext.AuditEvents.AsNoTracking().AnyAsync(
                        entry => entry.EventHash == leaf.PreviousHash, cancellationToken))
                {
                    throw new VendorAuditChainIntegrityException("leaf-parent-missing");
                }
            }

            var eventUtc = _timeProvider.GetUtcNow().ToUniversalTime();
            var auditEvent = new VendorAuditEvent
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = eventUtc,
                ActorUserId = request.ActorUserId,
                ActionCode = request.ActionCode,
                SubjectType = request.SubjectType,
                SubjectId = request.SubjectId,
                CorrelationId = request.CorrelationId,
                MetadataJson = null,
                PreviousHash = previousHash
            };
            auditEvent.EventHash = VendorAuditChainVerifier.ComputeEventHash(auditEvent);

            _dbContext.AuditEvents.Add(auditEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }

            return new VendorAuditWriteResult(auditEvent.Id, auditEvent.EventHash, auditEvent.PreviousHash);
        }
        catch
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    internal static bool IsValidActionCode(string? actionCode) =>
        actionCode is { Length: >= 1 and <= 100 } && ActionCodePattern.IsMatch(actionCode);

    internal static bool IsValidSubjectType(string? subjectType) =>
        subjectType is null || (subjectType.Length is >= 1 and <= 100 && SubjectTypePattern.IsMatch(subjectType));

    internal static bool IsSafeOpaqueId(string? value, int maximumLength) =>
        value is null || (value.Length >= 1 && value.Length <= maximumLength &&
            value.All(character => (character >= '0' && character <= '9') ||
                                   (character >= 'a' && character <= 'f') ||
                                   (character >= 'A' && character <= 'F') ||
                                   character is '-' or ':'));

    internal static bool IsValidSubjectId(string? value) =>
        value is null || Guid.TryParseExact(value, "N", out _) || Guid.TryParseExact(value, "D", out _);

    internal static bool IsValidCorrelationId(string? value) =>
        value is null || Guid.TryParseExact(value, "N", out _) || Guid.TryParseExact(value, "D", out _) ||
        IsW3CTraceId(value);

    private static void ValidateRequest(VendorAuditWriteRequest request)
    {
        if (!IsValidActionCode(request.ActionCode))
        {
            throw new ArgumentException("ActionCode must be a lowercase, non-PII audit identifier.", nameof(request));
        }

        if (!IsValidSubjectType(request.SubjectType))
        {
            throw new ArgumentException("SubjectType must be a bounded identifier, not display text or personal data.", nameof(request));
        }

        if (!IsValidSubjectId(request.SubjectId))
        {
            throw new ArgumentException("SubjectId must be an opaque GUID; human-readable identifiers are prohibited.", nameof(request));
        }

        if (!IsValidCorrelationId(request.CorrelationId))
        {
            throw new ArgumentException("CorrelationId must be an opaque GUID or W3C trace identifier.", nameof(request));
        }
    }

    private static bool IsW3CTraceId(string value) =>
        value.Length == 32 && value.Any(character => character != '0') &&
        value.All(character => (character >= '0' && character <= '9') ||
                               (character >= 'a' && character <= 'f') ||
                               (character >= 'A' && character <= 'F'));

    private static async Task AcquireTransactionLockAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var connection = transaction.GetDbTransaction().Connection
            ?? throw new InvalidOperationException("The Vendor database transaction has no connection.");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "DECLARE @result int; " +
                              "EXEC @result = sys.sp_getapplock " +
                              "@Resource = @lockResource, @LockMode = N'Exclusive', " +
                              "@LockOwner = N'Transaction', @LockTimeout = @lockTimeout; " +
                              "SELECT @result;";

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@lockResource";
        resourceParameter.Value = ApplicationLockResource;
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@lockTimeout";
        timeoutParameter.Value = ApplicationLockTimeoutMilliseconds;
        command.Parameters.Add(timeoutParameter);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var result = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new TimeoutException("The Vendor audit-chain transaction lock could not be acquired.");
        }
    }
}

public sealed class VendorAuditChainIntegrityException(string failureCode)
    : InvalidOperationException("The Vendor audit chain failed an integrity precondition: " + failureCode)
{
    public string FailureCode { get; } = failureCode;
}
