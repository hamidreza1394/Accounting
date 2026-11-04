using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

/// <summary>
/// The delegation ceiling of one actor in one company: the set of permissions the actor
/// holds through active company roles there. A company manager may only delegate
/// permissions inside this ceiling — never more than they themselves hold (D-071).
/// </summary>
public static class DelegationCeilingRules
{
    /// <summary>
    /// Whether every candidate permission is covered by the ceiling. The exceeding
    /// identifiers are returned so the caller can fail closed with a stable message.
    /// </summary>
    public static bool Allows(
        IReadOnlySet<Guid> ceilingPermissionIds,
        IEnumerable<Guid> candidatePermissionIds,
        out IReadOnlyList<Guid> exceedingPermissionIds)
    {
        ArgumentNullException.ThrowIfNull(ceilingPermissionIds);
        ArgumentNullException.ThrowIfNull(candidatePermissionIds);

        var exceeding = new List<Guid>();
        foreach (var permissionId in candidatePermissionIds)
        {
            if (!ceilingPermissionIds.Contains(permissionId))
            {
                exceeding.Add(permissionId);
            }
        }

        exceedingPermissionIds = exceeding;
        return exceeding.Count == 0;
    }

    /// <summary>Whether the candidate permission set is covered by the ceiling codes.</summary>
    public static bool AllowsCodes(
        IReadOnlyCollection<string> ceilingPermissionCodes,
        IReadOnlyCollection<string> candidatePermissionCodes,
        out IReadOnlyList<string> exceedingPermissionCodes)
    {
        ArgumentNullException.ThrowIfNull(ceilingPermissionCodes);
        ArgumentNullException.ThrowIfNull(candidatePermissionCodes);

        var ceiling = ceilingPermissionCodes.ToHashSet(StringComparer.Ordinal);
        var exceeding = candidatePermissionCodes
            .Where(code => !ceiling.Contains(code))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        exceedingPermissionCodes = exceeding;
        return exceeding.Count == 0;
    }
}

/// <summary>
/// The delegation ceiling of one actor in one company, computed from tracked rows that the
/// caller loaded inside its transaction. The rowversion no-op guards make a concurrent
/// membership or role change fail closed instead of delegating from a stale snapshot.
/// </summary>
public sealed class TrackedDelegationCeiling
{
    private TrackedDelegationCeiling(
        IReadOnlyList<Domain.UserCompanyMembership> memberships,
        IReadOnlySet<Guid> permissionIds)
    {
        Memberships = memberships;
        PermissionIds = permissionIds;
    }

    /// <summary>The tracked active memberships that establish the ceiling.</summary>
    public IReadOnlyList<Domain.UserCompanyMembership> Memberships { get; }

    /// <summary>Union of the permission identifiers of those memberships' active roles.</summary>
    public IReadOnlySet<Guid> PermissionIds { get; }

    /// <summary>
    /// Forces a no-op update on every tracked row that establishes the ceiling, so its
    /// rowversion protects the delegation decision inside the caller's transaction.
    /// </summary>
    public void ApplyRowVersionGuards(Data.DatabaseContext database)
    {
        ArgumentNullException.ThrowIfNull(database);

        foreach (var membership in Memberships)
        {
            database.Entry(membership).Property(current => current.RoleId).IsModified = true;
            database.Entry(membership.Role).Property(current => current.IsActive).IsModified = true;
        }
    }

    /// <summary>Whether the candidate permission set exceeds the ceiling.</summary>
    public bool Violates(IEnumerable<Guid> candidatePermissionIds, out IReadOnlyList<Guid> exceedingPermissionIds) =>
        !DelegationCeilingRules.Allows(PermissionIds, candidatePermissionIds, out exceedingPermissionIds);

    internal static async Task<TrackedDelegationCeiling?> LoadAsync(
        Data.DatabaseContext database,
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || companyId == Guid.Empty)
        {
            return null;
        }

        var memberships = await database.UserCompanyMemberships
            .Include(membership => membership.Role)
                .ThenInclude(role => role.RolePermissions)
            .Where(membership =>
                membership.UserId == userId &&
                membership.TenantId == companyId &&
                membership.IsActive &&
                membership.Company.IsActive &&
                membership.Role.TenantId == companyId &&
                membership.Role.IsActive)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return null;
        }

        var permissionIds = memberships
            .SelectMany(membership => membership.Role.RolePermissions)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToHashSet();

        return new TrackedDelegationCeiling(memberships, permissionIds);
    }
}

/// <summary>
/// Computes delegation ceilings from fresh database rows. Every call re-reads the active
/// memberships, so a revoked role or permission takes effect on the next operation and
/// never from a cached snapshot. The caller is responsible for opening the transaction:
/// load the ceiling inside it, evaluate, then apply the rowversion guards before saving.
/// </summary>
public sealed class DelegationCeilingService
{
    public DelegationCeilingService(Data.DatabaseContext databaseContext)
    {
        DatabaseContext = databaseContext;
    }

    private Data.DatabaseContext DatabaseContext { get; }

    /// <summary>
    /// The tracked ceiling of the actor in one company, or <c>null</c> when the actor has no
    /// active membership there. Installation administrators have no ceiling; callers must
    /// check that case before consulting this service.
    /// </summary>
    public Task<TrackedDelegationCeiling?> LoadTrackedCeilingAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        TrackedDelegationCeiling.LoadAsync(DatabaseContext, userId, companyId, cancellationToken);

    /// <summary>
    /// The union of permission codes the user holds through active memberships across every
    /// company. Used to filter the permission catalog for company-scoped actors, who may
    /// only see and delegate what they possess; installation-level codes stay invisible.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUnionPermissionCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        return await DatabaseContext.UserCompanyMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == userId &&
                membership.IsActive &&
                membership.Company.IsActive &&
                membership.Role.IsActive &&
                membership.Role.TenantId == membership.TenantId)
            .SelectMany(membership => membership.Role.RolePermissions
                .Select(rolePermission => rolePermission.Permission.Code))
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }
}
