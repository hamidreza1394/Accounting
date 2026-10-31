using System.ComponentModel.DataAnnotations.Schema;

namespace Faranou.Modules.Trade.Domain.Common;

/// <summary>
/// Common persistence contract for company-scoped Trade records.
/// </summary>
[NotMapped]
public abstract class TradeEntity
{
    protected TradeEntity()
    {
    }

    protected TradeEntity(Guid tenantId, Guid createdByUserId, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        TenantId = RequireIdentifier(tenantId, nameof(tenantId));
        CreatedByUserId = RequireIdentifier(createdByUserId, nameof(createdByUserId));
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }

    /// <summary>The existing Accounting Company.Id; never supplied as an authorization decision by the client.</summary>
    public Guid TenantId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>SQL Server rowversion used for optimistic concurrency.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public Guid? DeletedByUserId { get; private set; }

    public void RecordUpdate(Guid userId, DateTime updatedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted Trade record cannot be updated.");
        }

        var actor = RequireIdentifier(userId, nameof(userId));
        var timestamp = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
        UpdatedByUserId = actor;
        UpdatedAtUtc = timestamp;
    }

    public void SoftDelete(Guid userId, DateTime deletedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("The Trade record is already deleted.");
        }

        var actor = RequireIdentifier(userId, nameof(userId));
        var timestamp = RequireUtc(deletedAtUtc, nameof(deletedAtUtc));

        IsDeleted = true;
        DeletedByUserId = actor;
        DeletedAtUtc = timestamp;
        UpdatedByUserId = actor;
        UpdatedAtUtc = timestamp;
    }

    protected static Guid RequireIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty identifier is required.", parameterName);
        }

        return value;
    }

    protected static string RequireText(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    protected static string? OptionalText(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    protected static DateTime RequireUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Trade audit timestamps must be UTC.", parameterName);
        }

        return value;
    }
}
