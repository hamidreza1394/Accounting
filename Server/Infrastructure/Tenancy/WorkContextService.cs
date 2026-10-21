using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

/// <summary>
/// The server-side decisions about the working context (company and fiscal period) of a user.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here trusts the client. The company is accepted only through
/// <see cref="CompanyAccessService" /> (an active membership in the database); the fiscal period
/// is looked up with the <em>verified</em> company identifier as tenant predicate, so a period of
/// another company is indistinguishable from one that does not exist. Which periods may be chosen
/// is decided by <see cref="FiscalPeriodSelectionPolicy" />. The stored selection is validated
/// again on every read, so a revoked membership or a closed period ends the context at once.
/// </para>
/// </remarks>
public sealed class WorkContextService
{
	/// <summary>The upper bound of fiscal periods offered per company.</summary>
	public const int MaxFiscalPeriodsPerCompany = 50;

	public WorkContextService(
		Data.DatabaseContext databaseContext,
		CompanyAccessService companyAccessService,
		WorkContextCookie cookie)
	{
		DatabaseContext = databaseContext;
		CompanyAccessService = companyAccessService;
		Cookie = cookie;
	}

	private Data.DatabaseContext DatabaseContext { get; }

	private CompanyAccessService CompanyAccessService { get; }

	private WorkContextCookie Cookie { get; }

	/// <summary>
	/// The companies and periods the user can choose from, and what the client must do next.
	/// </summary>
	public async Task<WorkContextOptions> GetOptionsAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var companies = await CompanyAccessService.GetAccessibleCompaniesAsync(userId, cancellationToken);

		if (companies.Count == 0)
		{
			return new WorkContextOptions(Array.Empty<CompanyWithPeriods>(), EntryMode.NoCompany, null, null);
		}

		var companyIds = companies.Select(company => company.CompanyId).ToList();

		// One query for all companies; the tenant predicate is the verified identifier list.
		var periods = await DatabaseContext.FiscalPeriods
			.AsNoTracking()
			.Where(period => companyIds.Contains(period.TenantId))
			.OrderByDescending(period => period.StartDate)
			.ThenBy(period => period.Id)
			.Select(period => new
			{
				period.TenantId,
				Info = new FiscalPeriodInfo(period.Id, period.Title, period.StartDate, period.EndDate, period.Status),
			})
			.ToListAsync(cancellationToken);

		var periodsByCompany = periods
			.GroupBy(period => period.TenantId)
			.ToDictionary(
				group => group.Key,
				group => (IReadOnlyList<FiscalPeriodInfo>)group
					.Select(item => item.Info)
					.Take(MaxFiscalPeriodsPerCompany)
					.ToList());

		var result = companies
			.Select(company => new CompanyWithPeriods(
				company,
				periodsByCompany.TryGetValue(company.CompanyId, out var list)
					? list
					: Array.Empty<FiscalPeriodInfo>()))
			.ToList();

		var withSelectable = result.Where(company => company.SelectablePeriodCount > 0).ToList();

		if (withSelectable.Count == 0)
		{
			return new WorkContextOptions(result, EntryMode.NoEligiblePeriod, null, null);
		}

		if (result.Count == 1 && withSelectable[0].SelectablePeriodCount == 1)
		{
			var only = withSelectable[0];

			return new WorkContextOptions(
				result,
				EntryMode.DirectEntry,
				only.Company.CompanyId,
				only.FiscalPeriods.Single(period => period.IsSelectable).Id);
		}

		return new WorkContextOptions(result, EntryMode.SelectionRequired, null, null);
	}

	/// <summary>
	/// Validates a requested selection and, only when it is valid, stores it for this sign-in.
	/// </summary>
	public async Task<SelectionResult> SelectAsync(
		HttpContext httpContext,
		Guid userId,
		Guid sessionId,
		Guid requestedCompanyId,
		Guid requestedFiscalPeriodId,
		CancellationToken cancellationToken = default)
	{
		var context = await ResolveAsync(userId, requestedCompanyId, requestedFiscalPeriodId, cancellationToken);

		if (context.Outcome == SelectionOutcome.Selected)
		{
			Cookie.Write(
				httpContext,
				userId,
				sessionId,
				context.Context!.Company.CompanyId,
				context.Context.FiscalPeriod.Id);
		}

		return context;
	}

	/// <summary>
	/// The stored selection of this sign-in, validated against the database right now. A selection
	/// that is no longer valid is removed and reported as "nothing selected".
	/// </summary>
	public async Task<WorkContext?> GetCurrentAsync(
		HttpContext httpContext,
		Guid userId,
		Guid sessionId,
		CancellationToken cancellationToken = default)
	{
		var stored = Cookie.Read(httpContext, userId, sessionId);

		if (stored is null)
		{
			return null;
		}

		var result = await ResolveAsync(userId, stored.Value.CompanyId, stored.Value.FiscalPeriodId, cancellationToken);

		if (result.Outcome != SelectionOutcome.Selected)
		{
			Cookie.Delete(httpContext);
			return null;
		}

		return result.Context;
	}

	/// <summary>Forgets the selection (change of company or period, sign-out).</summary>
	public void Clear(HttpContext httpContext)
	{
		Cookie.Delete(httpContext);
	}

	private async Task<SelectionResult> ResolveAsync(
		Guid userId,
		Guid requestedCompanyId,
		Guid requestedFiscalPeriodId,
		CancellationToken cancellationToken)
	{
		var access = await CompanyAccessService.FindAccessibleCompanyAsync(userId, requestedCompanyId, cancellationToken);

		if (access is null)
		{
			return new SelectionResult(SelectionOutcome.CompanyNotAccessible, null);
		}

		if (requestedFiscalPeriodId == Guid.Empty)
		{
			return new SelectionResult(SelectionOutcome.PeriodNotFound, null);
		}

		// The tenant predicate is the verified company, not the raw request value.
		var period = await DatabaseContext.FiscalPeriods
			.AsNoTracking()
			.Where(item => item.Id == requestedFiscalPeriodId && item.TenantId == access.CompanyId)
			.Select(item => new FiscalPeriodInfo(item.Id, item.Title, item.StartDate, item.EndDate, item.Status))
			.FirstOrDefaultAsync(cancellationToken);

		if (period is null)
		{
			return new SelectionResult(SelectionOutcome.PeriodNotFound, null);
		}

		return period.IsSelectable
			? new SelectionResult(SelectionOutcome.Selected, new WorkContext(access, period))
			: new SelectionResult(SelectionOutcome.PeriodNotSelectable, null);
	}
}
