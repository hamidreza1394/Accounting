using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Text.Json;
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
    private const int MaxExportRows = 10_000;
    private static readonly TimeSpan MaxExportRange = TimeSpan.FromDays(31);

    public static void MapSecurityAuditAdministrationEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/admin/security/audit-events", GetAuditEventsAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        api.MapPost("/admin/security/audit-events/export", ExportAuditEventsAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind))
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
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


    /// <summary>
    /// Exports a bounded, safe projection of audit events. Network addresses and event payloads
    /// are deliberately excluded, and spreadsheet formula cells are neutralized.
    /// </summary>
    public static async Task<IResult> ExportAuditEventsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] SecurityAuditExportRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        CancellationToken cancellationToken = default)
    {
        SetNoStore(httpContext);

        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        var eventType = string.IsNullOrWhiteSpace(request.EventType) ? null : request.EventType.Trim();
        var userId = request.UserId;
        var companyId = request.CompanyId;
        var fromUtc = request.FromUtc;
        var toUtc = request.ToUtc;
        if (eventType?.Length > MaxEventTypeLength ||
            userId == Guid.Empty ||
            companyId == Guid.Empty ||
            !fromUtc.HasValue ||
            !toUtc.HasValue ||
            fromUtc.Value.UtcDateTime > toUtc.Value.UtcDateTime ||
            toUtc.Value.UtcDateTime - fromUtc.Value.UtcDateTime > MaxExportRange)
        {
            return TypedResults.BadRequest(new
            {
                error = "برای خروجی، بازه زمانی معتبر حداکثر ۳۱ روز و فیلترهای معتبر الزامی است.",
            });
        }

        IQueryable<Domain.SecurityAuditEvent> query = database.SecurityAuditEvents.AsNoTracking();
        if (eventType is not null)
            query = query.Where(item => item.EventType == eventType);
        if (userId.HasValue)
            query = query.Where(item => item.UserId == userId.Value);
        if (companyId.HasValue)
            query = query.Where(item => item.TenantId == companyId.Value);

        var from = fromUtc.Value.UtcDateTime;
        var to = toUtc.Value.UtcDateTime;
        query = query.Where(item => item.OccurredAtUtc >= from && item.OccurredAtUtc <= to);

        var rows = await query
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(MaxExportRows + 1)
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

        if (rows.Count > MaxExportRows)
        {
            return TypedResults.Json(
                new { error = "تعداد رکوردهای خروجی بیش از حد مجاز است؛ بازه یا فیلترها را محدود کنید." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

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

        var csv = new StringBuilder();
        csv.Append("Id,UserId,ActorUsername,ActorDisplayName,CompanyId,CompanyName,EventType,EntityType,EntityId,OccurredAtUtc,CorrelationId\r\n");
        foreach (var item in rows)
        {
            actors.TryGetValue(item.UserId.GetValueOrDefault(), out var actor);
            companies.TryGetValue(item.CompanyId.GetValueOrDefault(), out var company);
            var actorName = actor is null ? null : $"{actor.FirstName} {actor.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(actorName))
                actorName = null;

            var occurredAtUtc = DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc)
                .ToString("O", CultureInfo.InvariantCulture);
            csv.Append(item.Id.ToString("D")).Append(',')
                .Append(CsvCell(item.UserId?.ToString("D"))).Append(',')
                .Append(CsvCell(actor?.Username)).Append(',')
                .Append(CsvCell(actorName)).Append(',')
                .Append(CsvCell(item.CompanyId?.ToString("D"))).Append(',')
                .Append(CsvCell(company?.Name)).Append(',')
                .Append(CsvCell(item.EventType)).Append(',')
                .Append(CsvCell(item.EntityType)).Append(',')
                .Append(CsvCell(item.EntityId)).Append(',')
                .Append(occurredAtUtc).Append(',')
                .Append(CsvCell(item.CorrelationId))
                .Append("\r\n");
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return TypedResults.Forbid();

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            EventType = "SecurityAuditExported",
            EntityType = "SecurityAuditEvent",
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = httpContext.TraceIdentifier.Length <= 100
                ? httpContext.TraceIdentifier
                : httpContext.TraceIdentifier[..100],
            DetailsJson = JsonSerializer.Serialize(new
            {
                eventType,
                userId,
                companyId,
                fromUtc = fromUtc.Value,
                toUtc = toUtc.Value,
                rowCount = rows.Count,
            }),
        });
        await database.SaveChangesAsync(cancellationToken);

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csv.ToString());
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);

        var fileName = $"security-audit-events-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return TypedResults.File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static string CsvCell(string? value)
    {
        if (value is null)
            return "\"\"";

        var startsWithControlCharacter = value[0] is '\t' or '\r' or '\n';
        var startsWithFormula = value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@';
        if (startsWithControlCharacter || startsWithFormula)
            value = "'" + value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
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

public sealed record SecurityAuditExportRequest(
    string? EventType,
    Guid? UserId,
    Guid? CompanyId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);

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
