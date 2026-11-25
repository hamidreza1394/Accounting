namespace Faranou.Modules.Trade.Application.Proformas;

/// <summary>Machine-readable reason a validity extension was refused. Never reflects caller input.</summary>
public enum ProformaValidityExtensionIssue
{
    None = 0,
    TenantMissing,
    CompanyMissing,
    ProformaMissing,
    ActorMissing,

    /// <summary>The new date is not later than the previous one. Shortening is an undecided owner question (A-2) and is not recorded as an extension.</summary>
    NotAnExtension,

    RecordedAtNotUtc,
}

/// <summary>
/// The standalone history event for extending only a Proforma validity date (D-116 item 2): it records the previous
/// date, the new date, an optional reason, the user and the time, and never creates a Revision. It is a pure value:
/// it exists only through <see cref="TryCreate" />, has no setters, and carries <c>TenantId</c> and <c>CompanyId</c>
/// as independent identifiers (D-087; the legacy equality of the two is not assumed). Where the validity date itself is
/// stored (A-1) and whether it may be shortened (A-2) are undecided and are not modelled here.
/// </summary>
public sealed record ProformaValidityExtendedEvent
{
    private ProformaValidityExtendedEvent(
        Guid tenantId,
        Guid companyId,
        Guid proformaId,
        DateOnly previousValidUntil,
        DateOnly newValidUntil,
        string? reason,
        Guid actorUserId,
        DateTime recordedAtUtc)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        ProformaId = proformaId;
        PreviousValidUntil = previousValidUntil;
        NewValidUntil = newValidUntil;
        Reason = reason;
        ActorUserId = actorUserId;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid ProformaId { get; }

    public DateOnly PreviousValidUntil { get; }

    public DateOnly NewValidUntil { get; }

    /// <summary>Trimmed; null when none was given or it was blank.</summary>
    public string? Reason { get; }

    public Guid ActorUserId { get; }

    public DateTime RecordedAtUtc { get; }

    /// <summary>
    /// Validates and builds the event. The identifiers must come from the validated server-side context, never from a
    /// client body; this type only refuses empty values.
    /// </summary>
    public static bool TryCreate(
        Guid tenantId,
        Guid companyId,
        Guid proformaId,
        DateOnly previousValidUntil,
        DateOnly newValidUntil,
        string? reason,
        Guid actorUserId,
        DateTime recordedAtUtc,
        out ProformaValidityExtendedEvent? extension,
        out ProformaValidityExtensionIssue issue)
    {
        extension = null;
        issue = Validate(tenantId, companyId, proformaId, previousValidUntil, newValidUntil, actorUserId, recordedAtUtc);
        if (issue != ProformaValidityExtensionIssue.None)
        {
            return false;
        }

        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        extension = new(tenantId, companyId, proformaId, previousValidUntil, newValidUntil, trimmed, actorUserId, recordedAtUtc);
        return true;
    }

    private static ProformaValidityExtensionIssue Validate(
        Guid tenantId,
        Guid companyId,
        Guid proformaId,
        DateOnly previous,
        DateOnly proposed,
        Guid actorUserId,
        DateTime recordedAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            return ProformaValidityExtensionIssue.TenantMissing;
        }

        if (companyId == Guid.Empty)
        {
            return ProformaValidityExtensionIssue.CompanyMissing;
        }

        if (proformaId == Guid.Empty)
        {
            return ProformaValidityExtensionIssue.ProformaMissing;
        }

        if (actorUserId == Guid.Empty)
        {
            return ProformaValidityExtensionIssue.ActorMissing;
        }

        if (proposed <= previous)
        {
            return ProformaValidityExtensionIssue.NotAnExtension;
        }

        return recordedAtUtc.Kind == DateTimeKind.Utc
            ? ProformaValidityExtensionIssue.None
            : ProformaValidityExtensionIssue.RecordedAtNotUtc;
    }
}
