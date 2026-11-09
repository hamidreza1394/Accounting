using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// The personnel photo of a company employee (one per employee). Access follows the same rules as
/// the employee endpoints: the company scope and the <c>hr.employees.*</c> permission are checked
/// on the server for every call (view for reading, update for writing); the photo bytes are stored
/// in <c>CompanyEmployeePhotos</c> (never in a public path) and are served only through the
/// authorized GET. The type is decided from the file signature, never from the client-sent name
/// or content type, and only JPEG, PNG and WebP up to <see cref="MaxPhotoBytes"/> are accepted.
/// </summary>
public static class HrEmployeePhotoEndpoints
{
    /// <summary>The largest accepted photo: 2 MiB.</summary>
    public const int MaxPhotoBytes = 2 * 1024 * 1024;

    public static RouteGroupBuilder MapHrEmployeePhotoEndpoints(this RouteGroupBuilder api)
    {
        var photo = api.MapGroup("/companies/{companyId:guid}/org/employees/{employeeId:guid}/photo")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        photo.MapGet("", GetPhotoAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        photo.MapPut("", PutPhotoAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        photo.MapDelete("", DeletePhotoAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        return api;
    }

    /// <summary>Returns the image signature type, or null when the bytes are not a supported image.</summary>
    public static string? DetectImageContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";
        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "image/webp";
        return null;
    }

    private static async Task<IResult> GetPhotoAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await HrOrganizationalStructureEndpoints.AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await HrOrganizationalStructureEndpoints.IsActiveCompanyAsync(database, companyId, cancellationToken))
            return TypedResults.NotFound();

        var photo = await database.CompanyEmployeePhotos.AsNoTracking()
            .Where(current => current.EmployeeId == employeeId && current.TenantId == companyId)
            .Select(current => new { current.Content, current.ContentType })
            .FirstOrDefaultAsync(cancellationToken);
        if (photo is null) return TypedResults.NotFound();

        HrOrganizationalStructureEndpoints.SetNoStore(httpContext);
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return TypedResults.File(photo.Content, photo.ContentType);
    }

    private static async Task<IResult> PutPhotoAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await HrOrganizationalStructureEndpoints.AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId,
            HrOrganizationalStructureEndpoints.UpdateEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await HrOrganizationalStructureEndpoints.IsActiveCompanyAsync(database, companyId, cancellationToken))
            return TypedResults.NotFound();

        if (httpContext.Request.ContentLength is { } declared && declared > MaxPhotoBytes)
            return TypedResults.Json(new { error = "حجم عکس نباید بیشتر از ۲ مگابایت باشد." },
                statusCode: StatusCodes.Status413PayloadTooLarge);

        // Read at most one byte over the limit so a missing/lying Content-Length cannot exhaust memory.
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await httpContext.Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxPhotoBytes)
                return TypedResults.Json(new { error = "حجم عکس نباید بیشتر از ۲ مگابایت باشد." },
                    statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
            return TypedResults.BadRequest(new { error = "فایل عکس خالی است." });
        var contentType = DetectImageContentType(bytes);
        if (contentType is null)
            return TypedResults.BadRequest(new { error = "فقط عکس با قالب JPEG، PNG یا WebP پذیرفته می‌شود." });

        var employee = await database.CompanyEmployees.AsNoTracking()
            .AnyAsync(current => current.Id == employeeId && current.TenantId == companyId, cancellationToken);
        if (!employee) return TypedResults.NotFound();

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await database.CompanyEmployeePhotos
            .FirstOrDefaultAsync(current => current.EmployeeId == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (existing is null)
        {
            existing = new Domain.CompanyEmployeePhoto { TenantId = companyId, EmployeeId = employeeId };
            database.CompanyEmployeePhotos.Add(existing);
        }

        existing.Content = bytes;
        existing.ContentType = contentType;
        existing.ContentLength = bytes.Length;
        existing.UpdatedAtUtc = nowUtc;
        existing.UpdatedByUserId = scope!.UserId;

        AddAudit(database, httpContext, scope.UserId, companyId, employeeId, "HrEmployeePhotoSet", nowUtc,
            new { contentType, length = bytes.Length });
        await database.SaveChangesAsync(cancellationToken);

        HrOrganizationalStructureEndpoints.SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeletePhotoAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await HrOrganizationalStructureEndpoints.AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId,
            HrOrganizationalStructureEndpoints.UpdateEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await HrOrganizationalStructureEndpoints.IsActiveCompanyAsync(database, companyId, cancellationToken))
            return TypedResults.NotFound();

        var existing = await database.CompanyEmployeePhotos
            .FirstOrDefaultAsync(current => current.EmployeeId == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (existing is null) return TypedResults.NoContent();

        database.CompanyEmployeePhotos.Remove(existing);
        AddAudit(database, httpContext, scope!.UserId, companyId, employeeId, "HrEmployeePhotoRemoved",
            timeProvider.GetUtcNow().UtcDateTime, new { });
        await database.SaveChangesAsync(cancellationToken);

        HrOrganizationalStructureEndpoints.SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    private static void AddAudit(
        Data.DatabaseContext database, HttpContext httpContext, Guid actorId, Guid companyId,
        Guid employeeId, string eventType, DateTime nowUtc, object details) =>
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            TenantId = companyId,
            EventType = eventType,
            EntityType = "CompanyEmployee",
            EntityId = employeeId.ToString(),
            OccurredAtUtc = nowUtc,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(details),
        });
}
