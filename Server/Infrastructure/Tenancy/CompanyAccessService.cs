using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

/// <summary>
/// A company that the current user may work in, together with the role the user has in
/// that company.
/// </summary>
public sealed record CompanyAccess(
	Guid CompanyId,
	string CompanyName,
	string? NationalId,
	Guid RoleId,
	string RoleCode,
	string RoleName);

/// <summary>
/// The single server-side decision point for "may this user work in this company?".
/// </summary>
/// <remarks>
/// <para>
/// The company (tenant) of a request is never taken from client input alone. A company
/// identifier that arrives in a route or a body is only a <em>request</em>; it is accepted
/// only when the database contains an active membership of the signed-in user in an
/// active company with an active company role, and the user account itself is active.
/// </para>
/// <para>
/// The memberships are read from the database on every call instead of from cookie claims,
/// so a revoked membership or a deactivated account takes effect on the next request and
/// not only when the authentication cookie expires.
/// </para>
/// <para>
/// Unknown companies and companies of other tenants produce the same result (no access),
/// so a caller cannot probe which company identifiers exist.
/// </para>
/// </remarks>
public sealed class CompanyAccessService
{
	/// <summary>The claim that marks a company user (as opposed to the infrastructure account).</summary>
	public const string AccountKindClaimType = "AccountKind";

	/// <summary>The <see cref="AccountKindClaimType" /> value of a company user.</summary>
	public const string UserAccountKind = "User";

	public CompanyAccessService(Data.DatabaseContext databaseContext)
	{
		DatabaseContext = databaseContext;
	}

	private Data.DatabaseContext DatabaseContext { get; }

	/// <summary>
	/// Returns the identifier of the signed-in company user, or <c>null</c> when the
	/// principal is anonymous, is the infrastructure account, or was issued before the
	/// identifier claim existed (such a session has to sign in again to use the API).
	/// </summary>
	public static Guid? GetUserId(ClaimsPrincipal? principal)
	{
		if (principal?.Identity?.IsAuthenticated != true)
		{
			return null;
		}

		if (!string.Equals(
			principal.FindFirst(AccountKindClaimType)?.Value,
			UserAccountKind,
			StringComparison.Ordinal))
		{
			return null;
		}

		var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		return Guid.TryParse(value, out var userId) && userId != Guid.Empty
			? userId
			: null;
	}

	/// <summary>
	/// All companies the user may currently work in, ordered by name.
	/// </summary>
	public async Task<IReadOnlyList<CompanyAccess>> GetAccessibleCompaniesAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		return await ProjectToAccess(QueryActiveMemberships(userId)
				.OrderBy(membership => membership.Company.Name)
				.ThenBy(membership => membership.TenantId))
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	/// The access of the user to one company, or <c>null</c> when the user has no active
	/// membership in it (including when the company does not exist).
	/// </summary>
	public async Task<CompanyAccess?> FindAccessibleCompanyAsync(
		Guid userId,
		Guid companyId,
		CancellationToken cancellationToken = default)
	{
		if (companyId == Guid.Empty)
		{
			return null;
		}

		return await ProjectToAccess(QueryActiveMemberships(userId)
				.Where(membership => membership.TenantId == companyId))
			.FirstOrDefaultAsync(cancellationToken);
	}

	/// <summary>
	/// Whether the user may perform the operation <paramref name="permissionCode" /> in the
	/// company: an active membership (same rules as <see cref="FindAccessibleCompanyAsync" />)
	/// whose company role has the permission assigned (D-034).
	/// </summary>
	public async Task<bool> HasPermissionAsync(
		Guid userId,
		Guid companyId,
		string permissionCode,
		CancellationToken cancellationToken = default)
	{
		if (companyId == Guid.Empty)
		{
			return false;
		}

		var granted = await GetCompaniesWithPermissionAsync(
			userId,
			[companyId],
			permissionCode,
			cancellationToken);

		return granted.Contains(companyId);
	}

	/// <summary>
	/// The subset of <paramref name="companyIds" /> in which the user holds the permission
	/// <paramref name="permissionCode" /> through an active membership. One query, so a
	/// group report can check every company of its scope at once.
	/// </summary>
	public async Task<IReadOnlySet<Guid>> GetCompaniesWithPermissionAsync(
		Guid userId,
		IReadOnlyCollection<Guid> companyIds,
		string permissionCode,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(companyIds);
		ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

		var requested = companyIds.Where(id => id != Guid.Empty).Distinct().ToList();
		if (userId == Guid.Empty || requested.Count == 0)
		{
			return new HashSet<Guid>();
		}

		var permissionIds = DatabaseContext.PermissionDefinitions
			.AsNoTracking()
			.Where(permission => permission.Code == permissionCode)
			.Select(permission => permission.Id);

		var granted = await QueryActiveMemberships(userId)
			.Where(membership => requested.Contains(membership.TenantId))
			.Where(membership => DatabaseContext.CompanyRolePermissions
				.Any(rolePermission =>
					rolePermission.RoleId == membership.RoleId &&
					permissionIds.Contains(rolePermission.PermissionId)))
			.Select(membership => membership.TenantId)
			.Distinct()
			.ToListAsync(cancellationToken);

		return granted.ToHashSet();
	}

	private IQueryable<Domain.UserCompanyMembership> QueryActiveMemberships(Guid userId)
	{
		var activeUserIds = DatabaseContext.Users
			.AsNoTracking()
			.Where(user => user.Id == userId && user.IsActive)
			.Select(user => user.Id);

		return DatabaseContext.UserCompanyMemberships
			.AsNoTracking()
			.Where(membership => membership.UserId == userId)
			.Where(membership => activeUserIds.Contains(membership.UserId))
			.Where(membership => membership.IsActive)
			.Where(membership => membership.Company.IsActive)
			.Where(membership => membership.Role.IsActive)
			// The composite foreign key (RoleId, TenantId) already guarantees that the role
			// belongs to the same company; the predicate keeps the rule explicit here too.
			.Where(membership => membership.Role.TenantId == membership.TenantId);
	}

	// The projection is the last operator of every query: filtering and ordering happen on
	// the entity, which EF Core always translates to SQL.
	private static IQueryable<CompanyAccess> ProjectToAccess(
		IQueryable<Domain.UserCompanyMembership> memberships)
	{
		return memberships
			.Select(membership => new CompanyAccess(
				membership.TenantId,
				membership.Company.Name,
				membership.Company.NationalId,
				membership.RoleId,
				membership.Role.Code,
				membership.Role.Name));
	}
}
