namespace Domain.SaaS;

public enum AccountingDocumentStatus { Draft = 1, Posted = 2, Voided = 3 }

/// <summary>Initial document header; lines and posting rules are a separate accounting task.</summary>
public sealed class AccountingDocument : ITenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public AccountingDocumentStatus Status { get; set; } = AccountingDocumentStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
