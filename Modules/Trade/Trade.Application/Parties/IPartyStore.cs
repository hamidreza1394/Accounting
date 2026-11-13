using Faranou.Modules.Trade.Domain.Parties;

namespace Faranou.Modules.Trade.Application.Parties;

/// <summary>Validated list filter; every store query is additionally bound to one tenant.</summary>
public sealed record PartyListFilter(string? Search, PartyRole? Role, bool? IsActive, int Skip, int Take);

public sealed record PartyPage(IReadOnlyList<Party> Items, int TotalCount);

public enum PartySaveOutcome
{
    Saved = 1,
    DuplicateCode = 2,
    ConcurrencyConflict = 3,
}

/// <summary>
/// Narrow persistence port of the Party use cases (not a generic repository). Every method takes
/// the tenant explicitly; soft-deleted parties and roles are never returned.
/// </summary>
public interface IPartyStore
{
    Task<PartyPage> ListAsync(Guid tenantId, PartyListFilter filter, CancellationToken cancellationToken);

    /// <summary>Read-only lookup with active roles; null when missing, soft-deleted or of another tenant.</summary>
    Task<Party?> FindAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken);

    /// <summary>Tracked lookup with active roles for a following <see cref="SaveChangesAsync" />.</summary>
    Task<Party?> FindForUpdateAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken);

    Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingPartyId, CancellationToken cancellationToken);

    Task<PartySaveOutcome> AddAsync(Party party, CancellationToken cancellationToken);

    /// <summary>Persists the changes of a party obtained by <see cref="FindForUpdateAsync" /> against the given row version.</summary>
    Task<PartySaveOutcome> SaveChangesAsync(Party party, byte[] originalRowVersion, CancellationToken cancellationToken);
}
