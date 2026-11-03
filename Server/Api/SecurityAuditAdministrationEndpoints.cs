using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Read-only, installation-wide security audit reporting for installation administrators.
/// Sensitive event payloads and network metadata are deliberately excluded from the contract.
/// </summary>
public static class SecurityAuditAdministrationEndpoints
{
    private const int MaxPage = 100_000;
    private const int MaxPageSize = 100;
    private const int MaxEventTypeLength = 120;

    public static void MapSecurityAuditAdministrationEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/admin/security/audit-events", GetAuditEventsAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
    }

    public static async Task<IResult> GetAuditEventsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        string? eventType = null,
        Guid? userId = null,
        Guid? companyId = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        SetNoStore(httpContext);

        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        eventType = string.IsNullOrWhiteSpace(eventType) ? null : eventType.Trim();
        if (eventType?.Length > MaxEventTypeLength ||
            userId == Guid.Empty ||
            companyId == Guid.Empty ||
            (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value))
        {
            return TypedResults.BadRequest(new { error = "فیلترهای گزارش امنیتی معتبر نیستند." });
        }

        page = Math.Clamp(page, 1, MaxPage);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        IQueryable<Domain.SecurityAuditEvent> query = database.SecurityAuditEvents.AsNoTracking();
        if (eventType is not null)
            query = query.Where(item => item.EventType == eventType);
        if (userId.HasValue)
            query = query.Where(item => item.UserId == userId.Value);
        if (companyId.HasValue)
            query = query.Where(item => item.TenantId == companyId.Value);
        if (fromUtc.HasValue)
        {
            var from = fromUtc.Value.UtcDateTime;
            query = query.Where(item => item.OccurredAtUtc >= from);
        }
        if (toUtc.HasValue)
        {
            var to = toUtc.Value.UtcDateTime;
            query = query.Where(item => item.OccurredAtUtc <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditEventRow(
                item.Id,
                item.UserId,
                item.TenantId,
                item.EventType,
                item.EntityType,
                item.EntityId,
                item.OccurredAtUtc,
                item.CorrelationId))
            .ToListAsync(cancellationToken);

        var actorIds = rows.Where(item => item.UserId.HasValue)
            .Select(item => item.UserId!.Value)
            .Distinct()
            .ToArray();
        var actors = await database.Users.AsNoTracking()
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new ActorRow(user.Id, user.Username, user.FirstName, user.LastName))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var companyIds = rows.Where(item => item.CompanyId.HasValue)
            .Select(item => item.CompanyId!.Value)
            .Distinct()
            .ToArray();
        var companies = await database.Companies.AsNoTracking()
            .Where(company => companyIds.Contains(company.Id))
            .Select(company => new CompanyRow(company.Id, company.Name))
            .ToDictionaryAsync(company => company.Id, cancellationToken);

        var items = rows.Select(item =>
        {
            actors.TryGetValue(item.UserId.GetValueOrDefault(), out var actor);
            companies.TryGetValue(item.CompanyId.GetValueOrDefault(), out var company);
            var actorName = actor is null ? null : $"{actor.FirstName} {actor.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(actorName))
                actorName = null;

            return new SecurityAuditEventItemResponse(
                item.Id,
                item.UserId,
                actor?.Username,
                actorName,
                item.CompanyId,
                company?.Name,
                item.EventType,
                item.EntityType,
                item.EntityId,
                item.OccurredAtUtc,
                item.CorrelationId);
        }).ToArray();

        return TypedResults.Ok(new SecurityAuditEventListResponse(items, totalCount, page, pageSize));
    }

    private static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    private sealed record AuditEventRow(
        Guid Id,
        Guid? UserId,
        Guid? CompanyId,
        string EventType,
        string? EntityType,
        string? EntityId,
        DateTime OccurredAtUtc,
        string? CorrelationId);

    private sealed record ActorRow(Guid Id, string? Username, string? FirstName, string? LastName);
    private sealed record CompanyRow(Guid Id, string Name);
}

public sealed record SecurityAuditEventListResponse(
    IReadOnlyList<SecurityAuditEventItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Safe administrative projection. IpAddress and DetailsJson are intentionally not exposed.
/// </summary>
public sealed record SecurityAuditEventItemResponse(
    Guid Id,
    Guid? UserId,
    string? ActorUsername,
    string? ActorDisplayName,
    Guid? CompanyId,
    string? CompanyName,
    string EventType,
    string? EntityType,
    string? EntityId,
    DateTime OccurredAtUtc,
    string? CorrelationId);
