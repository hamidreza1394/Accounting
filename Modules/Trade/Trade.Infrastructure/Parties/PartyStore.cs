using Faranou.Modules.Trade.Application.Parties;
using Faranou.Modules.Trade.Domain.Parties;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faranou.Modules.Trade.Infrastructure.Parties;

/// <summary>
/// EF Core implementation of <see cref="IPartyStore" />. No global query filter exists (D-013), so every
/// query states the tenant and the soft-delete predicate explicitly, including the role navigation.
/// </summary>
internal sealed partial class PartyStore(TradeDbContext database, ILogger<PartyStore> logger) : IPartyStore
{
    private const string ActiveCodeIndex = "UX_Parties_Tenant_Code_Active";
    private const string ActiveRoleIndex = "UX_PartyRoleAssignments_Tenant_Party_Role_Active";

    public async Task<PartyPage> ListAsync(Guid tenantId, PartyListFilter filter, CancellationToken cancellationToken)
    {
        var query = database.Parties.AsNoTracking()
            .Where(party => party.TenantId == tenantId && !party.IsDeleted);

        if (filter.Search is { } search)
        {
            // Contains is translated with escaped wildcards, so % and _ match literally.
            // The legal identifier is intentionally never searched.
            query = query.Where(party =>
                party.Code.Contains(search) ||
                party.LegalName.Contains(search) ||
                (party.DisplayName != null && party.DisplayName.Contains(search)));
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(party => party.IsActive == isActive);
        }

        if (filter.Role is { } role)
        {
            query = query.Where(party => party.Roles.Any(assignment =>
                assignment.TenantId == tenantId && !assignment.IsDeleted && assignment.Role == role));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(party => party.Code).ThenBy(party => party.Id)
            .Skip(filter.Skip).Take(filter.Take)
            .Include(party => party.Roles.Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted))
            .ToListAsync(cancellationToken);

        return new PartyPage(items, totalCount);
    }

    public async Task<Party?> FindAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken) =>
        await database.Parties.AsNoTracking()
            .Include(party => party.Roles.Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted))
            .SingleOrDefaultAsync(
                party => party.Id == partyId && party.TenantId == tenantId && !party.IsDeleted,
                cancellationToken);

    public async Task<Party?> FindForUpdateAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken) =>
        await database.Parties
            .Include(party => party.Roles.Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted))
            .SingleOrDefaultAsync(
                party => party.Id == partyId && party.TenantId == tenantId && !party.IsDeleted,
                cancellationToken);

    public Task<bool> ActiveCodeExistsAsync(
        Guid tenantId, string code, Guid? excludingPartyId, CancellationToken cancellationToken) =>
        database.Parties.AsNoTracking().AnyAsync(
            party => party.TenantId == tenantId && !party.IsDeleted && party.Code == code &&
                     (excludingPartyId == null || party.Id != excludingPartyId),
            cancellationToken);

    public async Task<PartySaveOutcome> AddAsync(Party party, CancellationToken cancellationToken)
    {
        database.Parties.Add(party);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return PartySaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (IsUnique(exception, ActiveCodeIndex))
        {
            database.ChangeTracker.Clear();
            LogOutcome(logger, party.Id, "trade_party_code_duplicate");
            return PartySaveOutcome.DuplicateCode;
        }
    }

    public async Task<PartySaveOutcome> SaveChangesAsync(
        Party party, byte[] originalRowVersion, CancellationToken cancellationToken)
    {
        // A role added through the aggregate carries a client-generated key; make sure EF inserts it
        // instead of treating it as an existing (modified) row.
        database.ChangeTracker.DetectChanges();
        foreach (var entry in database.ChangeTracker.Entries<PartyRoleAssignment>()
                     .Where(entry => entry.State == EntityState.Modified && entry.Entity.RowVersion.Length == 0)
                     .ToList())
        {
            entry.State = EntityState.Added;
        }

        database.Entry(party).Property(item => item.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return PartySaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            LogOutcome(logger, party.Id, "trade_concurrency_conflict");
            return PartySaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsUnique(exception, ActiveCodeIndex))
        {
            database.ChangeTracker.Clear();
            LogOutcome(logger, party.Id, "trade_party_code_duplicate");
            return PartySaveOutcome.DuplicateCode;
        }
        catch (DbUpdateException exception) when (IsUnique(exception, ActiveRoleIndex))
        {
            // Two concurrent role changes of the same party: the loser saw stale data.
            database.ChangeTracker.Clear();
            LogOutcome(logger, party.Id, "trade_concurrency_conflict");
            return PartySaveOutcome.ConcurrencyConflict;
        }
    }

    private static bool IsUnique(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains(indexName, StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trade party {PartyId} write rejected: {Outcome}.")]
    private static partial void LogOutcome(ILogger logger, Guid partyId, string outcome);
}
