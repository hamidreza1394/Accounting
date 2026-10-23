using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>Privileged provisioning of opaque customer security boundaries in a shared database.</summary>
/// <remarks>
/// CustomerAccount remains in the vendor system (D-020). Only the configured infrastructure
/// account may assign the opaque operational boundary; company users cannot submit or change it.
/// Existing companies with no boundary remain unclassified and fail closed for group relations.
/// </remarks>
public static class CustomerBoundaryEndpoints
{
    private const int MaximumCompaniesPerRequest = 500;

    public static RouteGroupBuilder MapCustomerBoundaryEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/operator/customer-boundaries/assign", AssignAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        api.MapGet("/operator/customer-boundaries/diagnostics", DiagnoseAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        return api;
    }

    /// <summary>
    /// Read-only report of what still needs provisioning (backfill support). It lists identifiers only,
    /// never names or business data, and changes nothing; it can be repeated safely.
    /// </summary>
    public static async Task<Results<Ok<CustomerBoundaryDiagnostics>, NotFound>> DiagnoseAsync(
        ClaimsPrincipal user,
        [FromServices] Data.DatabaseContext databaseContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
            user.FindFirst(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value,
            "Infrastructure",
            StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

        var companies = await databaseContext.Companies.AsNoTracking()
            .Select(company => new { company.Id, company.CustomerBoundaryId })
            .ToListAsync(cancellationToken);
        var boundaries = companies.ToDictionary(company => company.Id, company => company.CustomerBoundaryId);

        var ownerships = await databaseContext.OwnershipRelationships.AsNoTracking()
            .Select(item => new { item.ParentCompanyId, item.SubsidiaryCompanyId })
            .ToListAsync(cancellationToken);
        var members = await databaseContext.CorporateGroupMembers.AsNoTracking()
            .Select(item => new { item.CorporateGroupId, item.CompanyId, OwnerId = item.CorporateGroup.OwnerCompanyId })
            .ToListAsync(cancellationToken);

        bool Unresolved(params Guid[] ids) =>
            ids.Any(id => boundaries.GetValueOrDefault(id) is null) ||
            ids.Select(id => boundaries[id]).Distinct().Count() > 1;

        var unresolvedCompanies = new HashSet<Guid>();
        var unresolvedOwnerships = 0;
        foreach (var item in ownerships.Where(item => Unresolved(item.ParentCompanyId, item.SubsidiaryCompanyId)))
        {
            unresolvedOwnerships++;
            unresolvedCompanies.Add(item.ParentCompanyId);
            unresolvedCompanies.Add(item.SubsidiaryCompanyId);
        }

        var unresolvedGroups = new HashSet<Guid>();
        foreach (var item in members.Where(item => Unresolved(item.CompanyId, item.OwnerId)))
        {
            unresolvedGroups.Add(item.CorporateGroupId);
            unresolvedCompanies.Add(item.CompanyId);
            unresolvedCompanies.Add(item.OwnerId);
        }

        var unprovisioned = companies.Where(company => company.CustomerBoundaryId is null).Select(company => company.Id).ToList();
        return TypedResults.Ok(new CustomerBoundaryDiagnostics(
            companies.Count,
            unprovisioned.Count,
            companies.Where(company => company.CustomerBoundaryId is not null).Select(company => company.CustomerBoundaryId).Distinct().Count(),
            unresolvedOwnerships,
            unresolvedGroups.Count,
            unresolvedCompanies.Order().Take(200).ToList()));
    }

    public static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult, NotFound, Conflict<ProblemDetails>>> AssignAsync(
        HttpContext httpContext,
        ClaimsPrincipal user,
        [FromBody] CustomerBoundaryProvisionRequest? request,
        [FromServices] IAntiforgery antiforgery,
        [FromServices] Data.DatabaseContext databaseContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
            user.FindFirst(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value,
            "Infrastructure",
            StringComparison.Ordinal))
        {
            // Do not advertise the operator-only endpoint to ordinary company accounts.
            return TypedResults.NotFound();
        }

        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            await AuditRejectionAsync(httpContext, user, databaseContext, "AntiforgeryRejected", 0, cancellationToken);
            return TypedResults.Problem(
                title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request is null || request.BoundaryId == Guid.Empty ||
            request.CompanyIds is null || request.CompanyIds.Count is < 1 or > MaximumCompaniesPerRequest ||
            request.CompanyIds.Any(id => id == Guid.Empty) ||
            request.CompanyIds.Distinct().Count() != request.CompanyIds.Count)
        {
            return TypedResults.Problem(
                title: "شناسه مرز مشتری و فهرست شرکت‌ها معتبر نیست.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Serializable: two concurrent requests must not both see an unassigned company and then
        // silently overwrite each other (the later one would move a company across customers).
        var strategy = databaseContext.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            databaseContext.ChangeTracker.Clear();
            // Serializable isolation exists only on a relational provider (the in-memory test provider has no transactions).
            await using var transaction = databaseContext.Database.IsRelational()
                ? await databaseContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var outcome = await AssignWithinTransactionAsync(httpContext, user, databaseContext, request, cancellationToken);
                if (outcome.Result is NoContent && transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return outcome;
            }
            catch (Exception exception) when (IsConcurrencyConflict(exception))
            {
                // The database chose this request as the deadlock victim: another provisioning of the
                // same companies won. Nothing was changed; the operator can retry after checking.
                return TypedResults.Conflict(new ProblemDetails
                {
                    Title = "تخصیص هم‌زمان دیگری روی همین شرکت‌ها در حال انجام بود؛ وضعیت را بررسی و دوباره تلاش کنید.",
                    Status = StatusCodes.Status409Conflict,
                });
            }
        });

        if (result.Result is Conflict<ProblemDetails>)
        {
            await AuditRejectionAsync(httpContext, user, databaseContext, "Conflict", request.CompanyIds.Count, cancellationToken);
        }

        return result;
    }

    private static async Task AuditRejectionAsync(
        HttpContext httpContext,
        ClaimsPrincipal user,
        Data.DatabaseContext databaseContext,
        string outcome,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteRejectionAuditAsync(httpContext, user, databaseContext, outcome, count, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The audit of a refusal must never turn the intended 400/409 into a 500.
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Server.Api.CustomerBoundaryEndpoints")
                .LogError(exception, "The rejection of a customer boundary provisioning could not be audited.");
        }
    }

    private static async Task WriteRejectionAuditAsync(
        HttpContext httpContext,
        ClaimsPrincipal user,
        Data.DatabaseContext databaseContext,
        string outcome,
        int count,
        CancellationToken cancellationToken)
    {
        databaseContext.ChangeTracker.Clear();
        databaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            EventType = "CustomerBoundaryProvisionRejected",
            EntityType = "CustomerBoundary",
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { Outcome = outcome, Operator = user.Identity?.Name, Count = count }),
        });
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsConcurrencyConflict(Exception exception)
    {
        const int lockTimeout = 1222;
        const int deadlockVictim = 1205;
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is Microsoft.Data.SqlClient.SqlException sql &&
                sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(error => error.Number is deadlockVictim or lockTimeout))
            {
                return true;
            }

            if (current.InnerException is null)
            {
                return false;
            }
        }

        return false;
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult, NotFound, Conflict<ProblemDetails>>> AssignWithinTransactionAsync(
        HttpContext httpContext,
        ClaimsPrincipal user,
        Data.DatabaseContext databaseContext,
        CustomerBoundaryProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var companies = await databaseContext.Companies
            .Where(company => request.CompanyIds.Contains(company.Id))
            .ToListAsync(cancellationToken);

        if (companies.Count != request.CompanyIds.Count)
        {
            return TypedResults.NotFound();
        }

        // Never merge an already provisioned company into another customer boundary via this
        // endpoint. A correction needs a separately reviewed, audited migration procedure.
        if (companies.Any(company => company.CustomerBoundaryId is { } existing && existing != request.BoundaryId))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "حداقل یکی از شرکت‌ها قبلاً به مرز مشتری دیگری تخصیص یافته است.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        if (!await ExistingRelationshipsRemainWithinBoundaryAsync(databaseContext, request.CompanyIds, request.BoundaryId, cancellationToken))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "تخصیص مرز باعث عبور یک رابطه ثبت‌شده از مرز مشتری می‌شود؛ همه شرکت‌های مرتبط را با یک مرز معتبر تخصیص دهید.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        foreach (var company in companies)
        {
            company.CustomerBoundaryId = request.BoundaryId;
        }

        var operatorName = user.Identity?.Name;
        databaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            EventType = "CustomerBoundaryProvisioned",
            EntityType = "CustomerBoundary",
            EntityId = request.BoundaryId.ToString("D"),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            // Bounded size (the column holds 4000 characters): the count and a hash of the sorted ids
            // identify the request without storing up to 500 identifiers.
            DetailsJson = JsonSerializer.Serialize(new
            {
                Outcome = "Assigned",
                Operator = operatorName,
                Count = request.CompanyIds.Count,
                CompaniesSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(string.Join(',', request.CompanyIds.Order().Select(id => id.ToString("D")))))),
            }),
        });

        await databaseContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<bool> ExistingRelationshipsRemainWithinBoundaryAsync(
        Data.DatabaseContext databaseContext,
        IReadOnlyList<Guid> requestedCompanyIds,
        Guid boundaryId,
        CancellationToken cancellationToken)
    {
        var requested = requestedCompanyIds.ToHashSet();
        var ownerships = await databaseContext.OwnershipRelationships
            .AsNoTracking()
            .Where(relationship => requested.Contains(relationship.ParentCompanyId) || requested.Contains(relationship.SubsidiaryCompanyId))
            .Select(relationship => new { relationship.ParentCompanyId, relationship.SubsidiaryCompanyId })
            .ToListAsync(cancellationToken);

        var groupIds = await databaseContext.CorporateGroups
            .AsNoTracking()
            .Where(group => requested.Contains(group.OwnerCompanyId) || group.Members.Any(member => requested.Contains(member.CompanyId)))
            .Select(group => group.Id)
            .ToListAsync(cancellationToken);
        var groupOwners = await databaseContext.CorporateGroups
            .AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .Select(group => new { group.Id, group.OwnerCompanyId })
            .ToListAsync(cancellationToken);
        var groupMembers = await databaseContext.CorporateGroupMembers
            .AsNoTracking()
            .Where(member => groupIds.Contains(member.CorporateGroupId))
            .Select(member => new { member.CorporateGroupId, member.CompanyId })
            .ToListAsync(cancellationToken);

        var relatedCompanyIds = ownerships.SelectMany(item => new[] { item.ParentCompanyId, item.SubsidiaryCompanyId })
            .Concat(groupOwners.Select(item => item.OwnerCompanyId))
            .Concat(groupMembers.Select(item => item.CompanyId))
            .Concat(requestedCompanyIds)
            .Distinct()
            .ToArray();
        var currentBoundaries = await databaseContext.Companies
            .AsNoTracking()
            .Where(company => relatedCompanyIds.Contains(company.Id))
            .ToDictionaryAsync(company => company.Id, company => company.CustomerBoundaryId, cancellationToken);

        Guid? EffectiveBoundary(Guid companyId) => requested.Contains(companyId)
            ? boundaryId
            : currentBoundaries.GetValueOrDefault(companyId);

        if (ownerships.Any(item => EffectiveBoundary(item.ParentCompanyId) != boundaryId || EffectiveBoundary(item.SubsidiaryCompanyId) != boundaryId))
        {
            return false;
        }

        foreach (var group in groupOwners)
        {
            if (EffectiveBoundary(group.OwnerCompanyId) != boundaryId ||
                groupMembers.Where(member => member.CorporateGroupId == group.Id).Any(member => EffectiveBoundary(member.CompanyId) != boundaryId))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record CustomerBoundaryProvisionRequest(Guid BoundaryId, IReadOnlyList<Guid> CompanyIds);

/// <summary>Counts and identifiers that block group relations; no names or business data.</summary>
public sealed record CustomerBoundaryDiagnostics(
    int Companies,
    int UnprovisionedCompanies,
    int Boundaries,
    int UnresolvedOwnershipRelationships,
    int UnresolvedCorporateGroups,
    IReadOnlyList<Guid> UnresolvedCompanyIds);
