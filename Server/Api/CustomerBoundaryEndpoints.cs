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
        return api;
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

        databaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            EventType = "CustomerBoundaryProvisioned",
            EntityType = "CustomerBoundary",
            EntityId = request.BoundaryId.ToString("D"),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { CompanyIds = request.CompanyIds, Count = request.CompanyIds.Count }),
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
