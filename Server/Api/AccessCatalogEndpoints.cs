using System.Security.Claims;
using Infrastructure.Access;

namespace Server.Api;

/// <summary>
/// قرارداد فقط-خواندنی کاتالوگ دسترسی (Stage 2-D، D-075): زیرسیستم‌ها، منوها و فرم‌های
/// واقعی. کاتالوگ کد-محور سرور است (AccessCatalogRegistry) و هیچ جدول/Migration
/// جدیدی پشت آن نیست. عملیات و مجوزها توسط GET /admin/permission-catalog موجود
/// سرو می‌شوند تا API تکراری ساخته نشود؛ مدیر شرکت همچنان همان endpoint را در
/// محدودهٔ سقف تفویض خود می‌بیند (D-071 §6) و این کاتالوگ سطح نصب فقط برای مدیر نصب است.
/// قرارداد کامل در docs/api/USER_ACCESS_MANAGEMENT_API.md ثبت می‌شود.
/// </summary>
public static class AccessCatalogEndpoints
{
    public static RouteGroupBuilder MapAccessCatalogEndpoints(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/admin/access/catalog")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        catalog.MapGet("/subsystems", GetSubsystemsAsync);
        catalog.MapGet("/menus", GetMenusAsync);
        catalog.MapGet("/forms", GetFormsAsync);
        return catalog;
    }

    /// <summary>GET /api/v1/admin/access/catalog/subsystems — کاتالوگ زیرسیستم‌ها با پیوند entitlement.</summary>
    public static async Task<IResult> GetSubsystemsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        SetNoStore(httpContext);
        var denial = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (denial is not null)
            return denial;

        return TypedResults.Ok(AccessCatalogRegistry.Subsystems.ToArray());
    }

    /// <summary>GET /api/v1/admin/access/catalog/menus — درخت منوی واقعی با وضعیت فعال/به‌زودی.</summary>
    public static async Task<IResult> GetMenusAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        SetNoStore(httpContext);
        var denial = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (denial is not null)
            return denial;

        return TypedResults.Ok(AccessCatalogRegistry.Menus.ToArray());
    }

    /// <summary>GET /api/v1/admin/access/catalog/forms — همهٔ Routeهای واقعی با Parent و hint نمایشی.</summary>
    public static async Task<IResult> GetFormsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        SetNoStore(httpContext);
        var denial = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (denial is not null)
            return denial;

        return TypedResults.Ok(AccessCatalogRegistry.Forms.ToArray());
    }

    private static void SetNoStore(HttpContext httpContext) =>
        httpContext.Response.Headers.CacheControl = "no-store";
}
