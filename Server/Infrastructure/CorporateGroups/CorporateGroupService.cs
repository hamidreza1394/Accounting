using System.Data;
using System.Text.Json;
using Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.CorporateGroups;

/// <summary>The outcome of a corporate group operation.</summary>
public enum CorporateGroupOutcome
{
	Succeeded = 1,

	/// <summary>
	/// The group, company, profile or fiscal period does not exist or is not visible to
	/// the user; both cases look the same so identifiers can not be probed (D-013).
	/// </summary>
	NotFound,

	/// <summary>The user sees the group but lacks a permission the operation needs.</summary>
	AccessDenied,

	/// <summary>The request breaks a rule of the model; <see cref="CorporateGroupResult{T}.ErrorCode" /> says which.</summary>
	Invalid,

	/// <summary>The request conflicts with stored data (for example a duplicate code).</summary>
	Conflict
}

/// <summary>The result of a corporate group operation.</summary>
public sealed record CorporateGroupResult<T>(CorporateGroupOutcome Outcome, T? Value, string? ErrorCode)
{
	public bool Succeeded => Outcome == CorporateGroupOutcome.Succeeded;

	internal static CorporateGroupResult<T> Success(T value) => new(CorporateGroupOutcome.Succeeded, value, null);

	internal static CorporateGroupResult<T> Failure(CorporateGroupOutcome outcome, string? errorCode = null) =>
		new(outcome, default, errorCode);
}

/// <summary>A visible corporate group.</summary>
public sealed record CorporateGroupSummary(Guid Id, string Code, string Name, Guid OwnerCompanyId);

/// <summary>The input of a new ownership relationship.</summary>
public sealed record OwnershipRequest(
	Guid ParentCompanyId,
	Guid SubsidiaryCompanyId,
	decimal OwnershipPercentage,
	decimal VotingPercentage,
	Domain.ControlBasis ControlBasis,
	DateOnly ValidFrom,
	DateOnly? ValidTo);

/// <summary>The input of a new group report run.</summary>
public sealed record GroupReportRunRequest(
	Guid CorporateGroupId,
	Domain.GroupReportKind Kind,
	Guid? ConsolidationProfileId,
	DateOnly PeriodStart,
	DateOnly PeriodEnd);

/// <summary>A recorded group report run and the companies of its scope.</summary>
public sealed record GroupReportRunSummary(
	Guid RunId,
	Domain.ConsolidationRunStatus Status,
	string? FailureCode,
	IReadOnlyList<Domain.ResolvedScopeCompany> Companies);

/// <summary>
/// The use cases of the corporate group model (D-033, D-034): groups, memberships,
/// ownership, consolidation profiles and the scope of a report run.
/// </summary>
/// <remarks>
/// <para>
/// Every permission is decided by <see cref="CompanyAccessService" /> from the database
/// (active user, membership, company and role, and the permission of the role). A
/// membership of a company in a group grants no access to the data of that company.
/// </para>
/// <para>
/// The rules that span several rows (overlapping periods, totals, control cycles) are
/// checked inside a serializable transaction on a relational database, so two requests can
/// not both pass the check and then both write. No financial figure is produced: the
/// ledger does not exist yet (D-034).
/// </para>
/// </remarks>
public sealed class CorporateGroupService
{
	public const string ErrorDuplicateCode = "DuplicateCode";
	public const string ErrorDuplicateProfileName = "DuplicateProfileName";
	public const string ErrorInvalidInput = "InvalidInput";
	public const string ErrorInactiveProfile = "InactiveProfile";
	public const string ErrorProfileRequired = "ProfileRequired";
	public const string ErrorScopeAccessDenied = "ScopeAccessDenied";
	public const string ErrorAlreadyEnded = "MembershipAlreadyEnded";
	public const string ErrorConcurrentChange = "ConcurrentChange";
	public const string ErrorCustomerBoundaryNotProvisioned = "CustomerBoundaryNotProvisioned";

	private const int MaximumCodeLength = 40;
	private const int MaximumNameLength = 200;
	private const int MaximumProfileNameLength = 120;

	public CorporateGroupService(
		Data.DatabaseContext databaseContext,
		CompanyAccessService companyAccess,
		TimeProvider timeProvider)
	{
		DatabaseContext = databaseContext;
		CompanyAccess = companyAccess;
		TimeProvider = timeProvider;
	}

	private Data.DatabaseContext DatabaseContext { get; }

	private CompanyAccessService CompanyAccess { get; }

	private TimeProvider TimeProvider { get; }

	private DateTime UtcNow => TimeProvider.GetUtcNow().UtcDateTime;

	/// <summary>
	/// The active groups the user may see (permission <see cref="CorporateGroupPermissions.View" />
	/// in the owner company).
	/// </summary>
	public async Task<IReadOnlyList<CorporateGroupSummary>> GetVisibleGroupsAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		// The user's companies first (few), then only the groups they own (filtered in SQL).
		var userCompanies = (await CompanyAccess.GetAccessibleCompaniesAsync(userId, cancellationToken))
			.Select(company => company.CompanyId)
			.ToList();

		var visibleOwners = (await CompanyAccess.GetCompaniesWithPermissionAsync(
			userId,
			userCompanies,
			CorporateGroupPermissions.View,
			cancellationToken)).ToList();

		if (visibleOwners.Count == 0)
		{
			return [];
		}

		var groups = await DatabaseContext.CorporateGroups
			.AsNoTracking()
			.Where(group => group.IsActive && visibleOwners.Contains(group.OwnerCompanyId))
			.Where(group => group.OwnerCompany.CustomerBoundaryId != null)
			.Where(group => !group.Members.Any(member => member.Company.CustomerBoundaryId == null || member.Company.CustomerBoundaryId != group.OwnerCompany.CustomerBoundaryId))
			.Select(group => new CorporateGroupSummary(group.Id, group.Code, group.Name, group.OwnerCompanyId))
			.ToListAsync(cancellationToken);

		return groups
			.OrderBy(group => group.Name, StringComparer.Ordinal)
			.ThenBy(group => group.Id)
			.ToList();
	}

	/// <summary>Creates a group administered by <paramref name="ownerCompanyId" />.</summary>
	public async Task<CorporateGroupResult<Guid>> CreateGroupAsync(
		Guid userId,
		Guid ownerCompanyId,
		string code,
		string name,
		CancellationToken cancellationToken = default)
	{
		code = (code ?? string.Empty).Trim();
		name = (name ?? string.Empty).Trim();

		if (!await CompanyAccess.HasPermissionAsync(userId, ownerCompanyId, CorporateGroupPermissions.Manage, cancellationToken))
		{
			// The requested owner company has not been verified as accessible; do not persist it in the audit tenant field.
			await AuditDeniedAsync(userId, "CorporateGroupCreate", null, null, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		if (!await ShareProvisionedCustomerBoundaryAsync(cancellationToken, ownerCompanyId))
		{
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Invalid, ErrorCustomerBoundaryNotProvisioned);
		}

		if (!IsValidCode(code) || name.Length == 0 || name.Length > MaximumNameLength)
		{
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Invalid, ErrorInvalidInput);
		}

		return await InSerializableTransactionAsync(async () =>
		{
			if (await DatabaseContext.CorporateGroups.AnyAsync(group => group.OwnerCompanyId == ownerCompanyId && group.Code == code, cancellationToken))
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Conflict, ErrorDuplicateCode);
			}

			var group = new Domain.CorporateGroup
			{
				Code = code,
				Name = name,
				OwnerCompanyId = ownerCompanyId,
				IsActive = true,
				CreatedAtUtc = UtcNow,
				CreatedByUserId = userId,
			};
			DatabaseContext.CorporateGroups.Add(group);
			AddAudit(userId, ownerCompanyId, "CorporateGroupCreated", "CorporateGroup", group.Id, new { group.Code });
			await DatabaseContext.SaveChangesAsync(cancellationToken);
			return CorporateGroupResult<Guid>.Success(group.Id);
		}, cancellationToken);
	}

	/// <summary>
	/// Adds a company to a group for a validity period. Needs
	/// <see cref="CorporateGroupPermissions.ManageMembers" /> in the owner company and in the
	/// member company, so nobody can pull another company into a group.
	/// </summary>
	public async Task<CorporateGroupResult<Guid>> AddMemberAsync(
		Guid userId,
		Guid groupId,
		Guid companyId,
		Domain.CorporateGroupMemberRole memberRole,
		DateOnly validFrom,
		DateOnly? validTo,
		CancellationToken cancellationToken = default)
	{
		var group = await FindVisibleGroupAsync(userId, groupId, cancellationToken);
		if (group is null)
		{
			await AuditDeniedAsync(userId, "CorporateGroupMemberAdd", null, groupId, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		var granted = await CompanyAccess.GetCompaniesWithPermissionAsync(
			userId,
			[group.OwnerCompanyId, companyId],
			CorporateGroupPermissions.ManageMembers,
			cancellationToken);

		if (!granted.Contains(group.OwnerCompanyId))
		{
			await AuditDeniedAsync(userId, "CorporateGroupMemberAdd", group.OwnerCompanyId, groupId, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.AccessDenied);
		}

		if (!granted.Contains(companyId))
		{
			// The member company is not visible to the user: same answer as an unknown company.
			await AuditDeniedAsync(userId, "CorporateGroupMemberAdd", group.OwnerCompanyId, groupId, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		if (!await ShareProvisionedCustomerBoundaryAsync(cancellationToken, group.OwnerCompanyId, companyId))
		{
			// Missing or different customer boundaries are deliberately indistinguishable.
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		var candidate = new Domain.GroupMembershipFact(companyId, memberRole, validFrom, validTo);

		return await InSerializableTransactionAsync(async () =>
		{
			// Re-checked inside the transaction: the guarantee must not depend on the boundary being write-once.
			if (!await ShareProvisionedCustomerBoundaryAsync(cancellationToken, group.OwnerCompanyId, companyId))
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
			}

			var existing = await DatabaseContext.CorporateGroupMembers
				.Where(member => member.CorporateGroupId == groupId && member.CompanyId == companyId)
				.Select(member => new Domain.GroupMembershipFact(member.CompanyId, member.MemberRole, member.ValidFrom, member.ValidTo))
				.ToListAsync(cancellationToken);

			var violation = Domain.CorporateGroupRules.ValidateMembership(candidate, existing);
			if (violation is not null)
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Invalid, violation.Value.ToString());
			}

			var member = new Domain.CorporateGroupMember
			{
				CorporateGroupId = groupId,
				CompanyId = companyId,
				MemberRole = memberRole,
				ValidFrom = validFrom,
				ValidTo = validTo,
				CreatedAtUtc = UtcNow,
				CreatedByUserId = userId,
			};
			DatabaseContext.CorporateGroupMembers.Add(member);
			AddAudit(userId, group.OwnerCompanyId, "CorporateGroupMemberAdded", "CorporateGroupMember", member.Id,
				new { GroupId = groupId, CompanyId = companyId, Role = memberRole.ToString() });
			if (companyId != group.OwnerCompanyId)
			{
				// The member company sees in its own audit trail that it joined a group.
				AddAudit(userId, companyId, "CorporateGroupMemberAdded", "CorporateGroupMember", member.Id,
					new { GroupId = groupId, Role = memberRole.ToString() });
			}
			await DatabaseContext.SaveChangesAsync(cancellationToken);
			return CorporateGroupResult<Guid>.Success(member.Id);
		}, cancellationToken);
	}

	/// <summary>
	/// Ends an open membership on <paramref name="validTo" />. A membership is never deleted.
	/// </summary>
	public async Task<CorporateGroupResult<Guid>> EndMembershipAsync(
		Guid userId,
		Guid memberId,
		DateOnly validTo,
		CancellationToken cancellationToken = default)
	{
		var membership = await DatabaseContext.CorporateGroupMembers
			.AsNoTracking()
			.Where(member => member.Id == memberId)
			.Select(member => new { member.CorporateGroupId, member.CompanyId })
			.FirstOrDefaultAsync(cancellationToken);

		var group = membership is null
			? null
			: await FindVisibleGroupAsync(userId, membership.CorporateGroupId, cancellationToken);

		if (membership is null || group is null)
		{
			await AuditDeniedAsync(userId, "CorporateGroupMemberEnd", null, null, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		var granted = await CompanyAccess.GetCompaniesWithPermissionAsync(
			userId,
			[group.OwnerCompanyId, membership.CompanyId],
			CorporateGroupPermissions.ManageMembers,
			cancellationToken);

		if (!granted.Contains(group.OwnerCompanyId))
		{
			await AuditDeniedAsync(userId, "CorporateGroupMemberEnd", group.OwnerCompanyId, group.Id, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.AccessDenied);
		}

		if (!granted.Contains(membership.CompanyId))
		{
			// Same answer as AddMemberAsync: no permission in the member company.
			await AuditDeniedAsync(userId, "CorporateGroupMemberEnd", group.OwnerCompanyId, group.Id, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		return await InSerializableTransactionAsync(async () =>
		{
			var member = await DatabaseContext.CorporateGroupMembers
				.FirstAsync(candidate => candidate.Id == memberId, cancellationToken);

			if (member.ValidTo is not null)
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Conflict, ErrorAlreadyEnded);
			}

			if (!Domain.CorporateGroupRules.IsValidPeriod(member.ValidFrom, validTo))
			{
				return CorporateGroupResult<Guid>.Failure(
					CorporateGroupOutcome.Invalid,
					nameof(Domain.CorporateGroupRuleViolation.InvalidPeriod));
			}

			member.ValidTo = validTo;
			member.EndedAtUtc = UtcNow;
			member.EndedByUserId = userId;
			AddAudit(userId, group.OwnerCompanyId, "CorporateGroupMemberEnded", "CorporateGroupMember", member.Id,
				new { GroupId = group.Id, member.CompanyId });
			if (member.CompanyId != group.OwnerCompanyId)
			{
				AddAudit(userId, member.CompanyId, "CorporateGroupMemberEnded", "CorporateGroupMember", member.Id,
					new { GroupId = group.Id });
			}
			await DatabaseContext.SaveChangesAsync(cancellationToken);
			return CorporateGroupResult<Guid>.Success(member.Id);
		}, cancellationToken);
	}

	/// <summary>
	/// Records an ownership relationship. Needs
	/// <see cref="CorporateGroupPermissions.ManageOwnership" /> in the parent and in the subsidiary.
	/// </summary>
	public async Task<CorporateGroupResult<Guid>> RecordOwnershipAsync(
		Guid userId,
		OwnershipRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var granted = await CompanyAccess.GetCompaniesWithPermissionAsync(
			userId,
			[request.ParentCompanyId, request.SubsidiaryCompanyId],
			CorporateGroupPermissions.ManageOwnership,
			cancellationToken);

		if (!granted.Contains(request.ParentCompanyId) || !granted.Contains(request.SubsidiaryCompanyId))
		{
			await AuditDeniedAsync(userId, "OwnershipRelationshipRecord", null, null, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		if (!await ShareProvisionedCustomerBoundaryAsync(cancellationToken, request.ParentCompanyId, request.SubsidiaryCompanyId))
		{
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		var candidate = new Domain.OwnershipFact(
			request.ParentCompanyId,
			request.SubsidiaryCompanyId,
			request.OwnershipPercentage,
			request.VotingPercentage,
			request.ControlBasis,
			request.ValidFrom,
			request.ValidTo);

		return await InSerializableTransactionAsync(async () =>
		{
			if (!await ShareProvisionedCustomerBoundaryAsync(cancellationToken, request.ParentCompanyId, request.SubsidiaryCompanyId))
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
			}

			var boundaryId = await DatabaseContext.Companies
				.Where(company => company.Id == request.ParentCompanyId)
				.Select(company => company.CustomerBoundaryId)
				.SingleAsync(cancellationToken);

			// Every relationship of the subsidiary (totals, single control) and every control edge
			// (cycles), limited to the candidate's customer boundary: legacy rows of another or an
			// unprovisioned boundary must neither change the outcome nor reveal their existence (D-037).
			var existing = await DatabaseContext.OwnershipRelationships
				.Where(relationship =>
					relationship.ParentCompany.CustomerBoundaryId == boundaryId &&
					relationship.SubsidiaryCompany.CustomerBoundaryId == boundaryId)
				.Where(relationship =>
					relationship.SubsidiaryCompanyId == request.SubsidiaryCompanyId ||
					relationship.ControlBasis == Domain.ControlBasis.Control ||
					relationship.ControlBasis == Domain.ControlBasis.JointControl)
				.Select(relationship => new Domain.OwnershipFact(
					relationship.ParentCompanyId,
					relationship.SubsidiaryCompanyId,
					relationship.OwnershipPercentage,
					relationship.VotingPercentage,
					relationship.ControlBasis,
					relationship.ValidFrom,
					relationship.ValidTo))
				.ToListAsync(cancellationToken);

			var violation = Domain.CorporateGroupRules.ValidateOwnership(candidate, existing);
			if (violation is not null)
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Invalid, violation.Value.ToString());
			}

			var relationship = new Domain.OwnershipRelationship
			{
				ParentCompanyId = request.ParentCompanyId,
				SubsidiaryCompanyId = request.SubsidiaryCompanyId,
				OwnershipPercentage = request.OwnershipPercentage,
				VotingPercentage = request.VotingPercentage,
				ControlBasis = request.ControlBasis,
				ValidFrom = request.ValidFrom,
				ValidTo = request.ValidTo,
				CreatedAtUtc = UtcNow,
				CreatedByUserId = userId,
			};
			DatabaseContext.OwnershipRelationships.Add(relationship);
			AddAudit(userId, request.ParentCompanyId, "OwnershipRelationshipRecorded", "OwnershipRelationship", relationship.Id,
				new { request.SubsidiaryCompanyId, Basis = request.ControlBasis.ToString() });
			AddAudit(userId, request.SubsidiaryCompanyId, "OwnershipRelationshipRecorded", "OwnershipRelationship", relationship.Id,
				new { request.ParentCompanyId, Basis = request.ControlBasis.ToString() });
			await DatabaseContext.SaveChangesAsync(cancellationToken);
			return CorporateGroupResult<Guid>.Success(relationship.Id);
		}, cancellationToken);
	}

	/// <summary>Creates a consolidation profile of a group.</summary>
	public async Task<CorporateGroupResult<Guid>> CreateProfileAsync(
		Guid userId,
		Guid groupId,
		string name,
		Domain.AccountingFramework accountingFramework,
		string reportingCurrencyCode,
		CancellationToken cancellationToken = default)
	{
		name = (name ?? string.Empty).Trim();
		reportingCurrencyCode = (reportingCurrencyCode ?? string.Empty).Trim();

		var group = await FindVisibleGroupAsync(userId, groupId, cancellationToken);
		if (group is null)
		{
			await AuditDeniedAsync(userId, "ConsolidationProfileCreate", null, groupId, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.NotFound);
		}

		if (!await CompanyAccess.HasPermissionAsync(userId, group.OwnerCompanyId, CorporateGroupPermissions.Manage, cancellationToken))
		{
			await AuditDeniedAsync(userId, "ConsolidationProfileCreate", group.OwnerCompanyId, groupId, cancellationToken);
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.AccessDenied);
		}

		if (name.Length == 0 ||
			name.Length > MaximumProfileNameLength ||
			!Enum.IsDefined(accountingFramework) ||
			!IsCurrencyCode(reportingCurrencyCode))
		{
			return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Invalid, ErrorInvalidInput);
		}

		return await InSerializableTransactionAsync(async () =>
		{
			if (await DatabaseContext.ConsolidationProfiles.AnyAsync(
				profile => profile.CorporateGroupId == groupId && profile.Name == name,
				cancellationToken))
			{
				return CorporateGroupResult<Guid>.Failure(CorporateGroupOutcome.Conflict, ErrorDuplicateProfileName);
			}

			var profile = new Domain.ConsolidationProfile
			{
				CorporateGroupId = groupId,
				Name = name,
				AccountingFramework = accountingFramework,
				ReportingCurrencyCode = reportingCurrencyCode,
				FiscalPeriodMappingPolicy = Domain.FiscalPeriodMappingPolicy.RequireCoveringPeriod,
				IsActive = true,
				CreatedAtUtc = UtcNow,
				CreatedByUserId = userId,
			};
			DatabaseContext.ConsolidationProfiles.Add(profile);
			AddAudit(userId, group.OwnerCompanyId, "ConsolidationProfileCreated", "ConsolidationProfile", profile.Id,
				new { GroupId = groupId });
			await DatabaseContext.SaveChangesAsync(cancellationToken);
			return CorporateGroupResult<Guid>.Success(profile.Id);
		}, cancellationToken);
	}

	/// <summary>
	/// Records a group report run and resolves its scope: the member companies and the
	/// fiscal period of each. The user needs the report permission in the owner company and
	/// in every company of the scope; otherwise nothing is recorded but an audit event.
	/// No figure is computed: the run ends in <see cref="Domain.ConsolidationRunStatus.ScopeResolved" />
	/// or <see cref="Domain.ConsolidationRunStatus.Failed" />.
	/// </summary>
	/// <remarks>
	/// Contract: <see cref="CorporateGroupOutcome.Succeeded" /> means "the run was recorded",
	/// not "the scope is usable". A run whose scope could not be resolved (no covering fiscal
	/// period, partial membership, empty scope) is recorded for the audit trail and returned
	/// as succeeded with <see cref="GroupReportRunSummary.Status" /> = Failed and a
	/// <see cref="GroupReportRunSummary.FailureCode" />; callers must check the status. Invalid
	/// requests and denied access record no run and return the other outcomes.
	/// </remarks>
	public async Task<CorporateGroupResult<GroupReportRunSummary>> StartReportRunAsync(
		Guid userId,
		GroupReportRunRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var group = await FindVisibleGroupAsync(userId, request.CorporateGroupId, cancellationToken);
		if (group is null)
		{
			await AuditDeniedAsync(userId, "ConsolidationRunStart", null, request.CorporateGroupId, cancellationToken);
			return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.NotFound);
		}

		if (!Enum.IsDefined(request.Kind) || request.PeriodEnd < request.PeriodStart)
		{
			return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.Invalid, ErrorInvalidInput);
		}

		var permission = request.Kind == Domain.GroupReportKind.Consolidated
			? CorporateGroupPermissions.RunConsolidatedReport
			: CorporateGroupPermissions.ViewAggregateReport;

		if (!await CompanyAccess.HasPermissionAsync(userId, group.OwnerCompanyId, permission, cancellationToken))
		{
			await AuditDeniedAsync(userId, "ConsolidationRunStart", group.OwnerCompanyId, group.Id, cancellationToken);
			return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.AccessDenied);
		}

		if (request.Kind == Domain.GroupReportKind.Consolidated && request.ConsolidationProfileId is null)
		{
			return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.Invalid, ErrorProfileRequired);
		}

		if (request.ConsolidationProfileId is { } profileId)
		{
			// The profile must belong to this group (the database enforces it as well with
			// the composite foreign key). A profile of another group looks like no profile.
			var profile = await DatabaseContext.ConsolidationProfiles
				.AsNoTracking()
				.Where(candidate => candidate.Id == profileId && candidate.CorporateGroupId == group.Id)
				.Select(candidate => new { candidate.IsActive })
				.FirstOrDefaultAsync(cancellationToken);

			if (profile is null)
			{
				return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.NotFound);
			}

			if (!profile.IsActive)
			{
				return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.Invalid, ErrorInactiveProfile);
			}
		}

		var memberships = await DatabaseContext.CorporateGroupMembers
			.AsNoTracking()
			.Where(member => member.CorporateGroupId == group.Id)
			.Select(member => new Domain.GroupMembershipFact(member.CompanyId, member.MemberRole, member.ValidFrom, member.ValidTo))
			.ToListAsync(cancellationToken);

		var companyIds = memberships
			.Where(member => Domain.CorporateGroupRules.Overlaps(member.ValidFrom, member.ValidTo, request.PeriodStart, request.PeriodEnd))
			.Select(member => member.CompanyId)
			.Distinct()
			.ToList();

		// Every company whose data would be in the report needs the permission: a report
		// with silently missing companies would be misleading, and a holding role alone
		// grants nothing in a subsidiary (D-034).
		var granted = await CompanyAccess.GetCompaniesWithPermissionAsync(userId, companyIds, permission, cancellationToken);
		if (companyIds.Any(companyId => !granted.Contains(companyId)))
		{
			await AuditDeniedAsync(userId, "ConsolidationRunStart", group.OwnerCompanyId, group.Id, cancellationToken,
				new { MissingCompanies = companyIds.Count(companyId => !granted.Contains(companyId)) });
			return CorporateGroupResult<GroupReportRunSummary>.Failure(CorporateGroupOutcome.AccessDenied, ErrorScopeAccessDenied);
		}

		var fiscalPeriods = await DatabaseContext.FiscalPeriods
			.AsNoTracking()
			.Where(period => companyIds.Contains(period.TenantId))
			.Select(period => new Domain.FiscalPeriodFact(period.Id, period.TenantId, period.StartDate, period.EndDate, period.Status))
			.ToListAsync(cancellationToken);

		var resolution = Domain.CorporateGroupRules.ResolveScope(request.PeriodStart, request.PeriodEnd, memberships, fiscalPeriods);
		var now = UtcNow;

		var run = new Domain.ConsolidationRun
		{
			CorporateGroupId = group.Id,
			ConsolidationProfileId = request.ConsolidationProfileId,
			Kind = request.Kind,
			PeriodStart = request.PeriodStart,
			PeriodEnd = request.PeriodEnd,
			Status = resolution.Succeeded ? Domain.ConsolidationRunStatus.ScopeResolved : Domain.ConsolidationRunStatus.Failed,
			FailureCode = resolution.FailureCode,
			StartedAtUtc = now,
			CompletedAtUtc = resolution.Succeeded ? null : now,
			ExecutedByUserId = userId,
		};

		foreach (var company in resolution.Companies)
		{
			run.Companies.Add(new Domain.ConsolidationRunCompany
			{
				CompanyId = company.CompanyId,
				FiscalPeriodId = company.FiscalPeriodId,
				MemberRole = company.MemberRole,
			});
		}

		DatabaseContext.ConsolidationRuns.Add(run);
		AddAudit(userId, group.OwnerCompanyId,
			resolution.Succeeded ? "ConsolidationRunScopeResolved" : "ConsolidationRunFailed",
			"ConsolidationRun", run.Id,
			new { GroupId = group.Id, Kind = request.Kind.ToString(), Companies = resolution.Companies.Count, resolution.FailureCode });
		await DatabaseContext.SaveChangesAsync(cancellationToken);

		return CorporateGroupResult<GroupReportRunSummary>.Success(
			new GroupReportRunSummary(run.Id, run.Status, run.FailureCode, resolution.Companies));
	}

	private async Task<bool> ShareProvisionedCustomerBoundaryAsync(CancellationToken cancellationToken, params Guid[] companyIds)
	{
		var ids = companyIds.Distinct().ToArray();
		if (ids.Length == 0 || ids.Any(id => id == Guid.Empty))
		{
			return false;
		}

		var boundaries = await DatabaseContext.Companies
			.AsNoTracking()
			.Where(company => ids.Contains(company.Id))
			.Select(company => new { company.Id, company.CustomerBoundaryId })
			.ToListAsync(cancellationToken);

		return boundaries.Count == ids.Length &&
			boundaries.All(company => company.CustomerBoundaryId is not null) &&
			boundaries.Select(company => company.CustomerBoundaryId).Distinct().Count() == 1;
	}
	private async Task<CorporateGroupSummary?> FindVisibleGroupAsync(
		Guid userId,
		Guid groupId,
		CancellationToken cancellationToken)
	{
		if (groupId == Guid.Empty)
		{
			return null;
		}

		var group = await DatabaseContext.CorporateGroups
			.AsNoTracking()
			.Where(candidate => candidate.Id == groupId && candidate.IsActive)
			.Where(candidate => candidate.OwnerCompany.CustomerBoundaryId != null)
			.Where(candidate => !candidate.Members.Any(member => member.Company.CustomerBoundaryId == null || member.Company.CustomerBoundaryId != candidate.OwnerCompany.CustomerBoundaryId))
			.Select(candidate => new CorporateGroupSummary(candidate.Id, candidate.Code, candidate.Name, candidate.OwnerCompanyId))
			.FirstOrDefaultAsync(cancellationToken);

		if (group is null)
		{
			return null;
		}

		return await CompanyAccess.HasPermissionAsync(userId, group.OwnerCompanyId, CorporateGroupPermissions.View, cancellationToken)
			? group
			: null;
	}

	private async Task<CorporateGroupResult<T>> InSerializableTransactionAsync<T>(
		Func<Task<CorporateGroupResult<T>>> operation,
		CancellationToken cancellationToken)
	{
		if (!DatabaseContext.Database.IsRelational())
		{
			// The in-memory provider of the unit tests has no transactions.
			return await operation();
		}

		await using var transaction = await DatabaseContext.Database
			.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		try
		{
			var result = await operation();
			if (result.Succeeded)
			{
				await transaction.CommitAsync(cancellationToken);
			}
			else
			{
				await transaction.RollbackAsync(cancellationToken);
			}

			return result;
		}
		catch (Exception exception) when (IsConcurrencyConflict(exception))
		{
			// A concurrent request won: a unique index (duplicate code or start date) or a
			// deadlock of the serializable range locks. First writer wins; the loser gets a
			// Conflict instead of an unhandled error and may retry.
			try
			{
				// A deadlock victim was already rolled back by SQL Server; rolling back again
				// may throw, which must not turn the Conflict into an unhandled error.
				await transaction.RollbackAsync(CancellationToken.None);
			}
			catch (InvalidOperationException)
			{
			}

			DatabaseContext.ChangeTracker.Clear();
			return CorporateGroupResult<T>.Failure(CorporateGroupOutcome.Conflict, ErrorConcurrentChange);
		}
	}

	/// <summary>
	/// A unique index violation (2601/2627) or a deadlock victim (1205) anywhere in the
	/// exception chain. EF Core wraps a deadlock in an InvalidOperationException of its
	/// execution strategy around the DbUpdateException, so the whole chain is searched.
	/// </summary>
	private static bool IsConcurrencyConflict(Exception exception)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 or 1205 })
			{
				return true;
			}
		}

		return false;
	}

	private void AddAudit(Guid userId, Guid? tenantId, string eventType, string entityType, Guid entityId, object details)
	{
		DatabaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
		{
			UserId = userId == Guid.Empty ? null : userId,
			TenantId = tenantId,
			EventType = eventType,
			EntityType = entityType,
			EntityId = entityId.ToString(),
			OccurredAtUtc = UtcNow,
			CorrelationId = System.Diagnostics.Activity.Current?.Id,
			DetailsJson = JsonSerializer.Serialize(details),
		});
	}

	private async Task AuditDeniedAsync(
		Guid userId,
		string operation,
		Guid? tenantId,
		Guid? groupId,
		CancellationToken cancellationToken,
		object? extra = null)
	{
		// Only the operation and identifiers the user already proved access to are stored;
		// a requested company or group the user can not see is not written (no probing log).
		// This saves on its own, so it must only run before a transaction starts: inside
		// InSerializableTransactionAsync it would commit pending changes of the operation.
		if (DatabaseContext.Database.CurrentTransaction is not null)
		{
			throw new InvalidOperationException("A denial audit must not be written inside the transaction of an operation.");
		}

		DatabaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
		{
			UserId = userId == Guid.Empty ? null : userId,
			TenantId = tenantId,
			EventType = "CorporateGroupAccessDenied",
			EntityType = "CorporateGroup",
			EntityId = tenantId is null ? null : groupId?.ToString(),
			OccurredAtUtc = UtcNow,
			CorrelationId = System.Diagnostics.Activity.Current?.Id,
			DetailsJson = JsonSerializer.Serialize(new { Operation = operation, Extra = extra }),
		});
		await DatabaseContext.SaveChangesAsync(cancellationToken);
	}

	private static bool IsValidCode(string code) =>
		code.Length is > 0 and <= MaximumCodeLength &&
		code.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

	private static bool IsCurrencyCode(string code) =>
		code.Length == 3 && code.All(char.IsAsciiLetterUpper);
}
