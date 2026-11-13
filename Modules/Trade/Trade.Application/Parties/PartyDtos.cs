namespace Faranou.Modules.Trade.Application.Parties;

/// <summary>Party list row. It intentionally carries no legal identifier.</summary>
public sealed record PartyListItem(
    Guid Id,
    string Code,
    string LegalName,
    string? DisplayName,
    string? CountryCode,
    bool IsActive,
    IReadOnlyList<string> Roles,
    string RowVersion);

public sealed record PartyListResponse(
    IReadOnlyList<PartyListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Party detail: the list fields plus the legal identifier and audit timestamps (no user ids).</summary>
public sealed record PartyDetail(
    Guid Id,
    string Code,
    string LegalName,
    string? DisplayName,
    string? LegalIdentifier,
    string? CountryCode,
    bool IsActive,
    IReadOnlyList<string> Roles,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Create request. Unknown fields (for example a tenant id) are ignored by deserialization.</summary>
public sealed record CreatePartyRequest(
    string? Code,
    string? LegalName,
    string? DisplayName,
    string? LegalIdentifier,
    string? CountryCode,
    IReadOnlyList<string?>? Roles);

/// <summary>Update request; roles are a full replacement and the row version is mandatory.</summary>
public sealed record UpdatePartyRequest(
    string? Code,
    string? LegalName,
    string? DisplayName,
    string? LegalIdentifier,
    string? CountryCode,
    IReadOnlyList<string?>? Roles,
    string? RowVersion);

/// <summary>Raw list query as received from the client; validated by <see cref="PartyService" />.</summary>
public sealed record PartyListQuery(
    string? Search,
    string? Role,
    bool? IsActive,
    int Page,
    int PageSize);

public enum PartyOutcome
{
    Success = 1,
    ValidationFailed = 2,
    NotFound = 3,
    DuplicateCode = 4,
    ConcurrencyConflict = 5,
}

public sealed record PartyResult<T>(
    PartyOutcome Outcome,
    T? Value = default,
    IReadOnlyDictionary<string, string[]>? Errors = null);
