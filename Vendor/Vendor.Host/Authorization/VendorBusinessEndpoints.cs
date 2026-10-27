using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Initial Vendor business APIs. Mutations are audited; resource checks always resolve persisted ownership.</summary>
public static class VendorBusinessEndpoints
{
    public static IEndpointRouteBuilder MapVendorBusinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var customers = endpoints.MapGroup("/api/admin/customers");
        customers.MapGet("/", async (VendorDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Customers.AsNoTracking()
                .OrderBy(item => item.CustomerNumber)
                .Select(item => new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc))
                .ToListAsync(ct)))
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersRead));

        customers.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IAuthorizationService authorization, VendorDbContext db, CancellationToken ct) =>
        {
            var access = await authorization.AuthorizeAsync(context.User,
                new VendorResourceDescriptor("customer", id),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersRead));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Customers.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, ct);
            return item is null ? Results.NotFound() :
                Results.Ok(new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersRead));

        customers.MapPost("/", async (
            CreateCustomerRequest request, HttpContext context, IAntiforgery antiforgery,
            VendorDbContext db, UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            var number = request.CustomerNumber?.Trim();
            var legalName = request.LegalName?.Trim();
            var displayName = request.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(number) || number.Length > 32 ||
                string.IsNullOrWhiteSpace(legalName) || legalName.Length > 200 ||
                string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
                return Results.BadRequest(new { error = "invalid_customer" });
            if (await db.Customers.AnyAsync(item => item.CustomerNumber == number, ct))
                return Results.Conflict(new { error = "customer_number_exists" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = new VendorCustomer
            {
                Id = Guid.NewGuid(), CustomerNumber = number, LegalName = legalName,
                DisplayName = displayName, Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            db.Customers.Add(item);
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.customer.created",
                nameof(VendorCustomer), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/customers/{item.Id:D}",
                new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersManage));

        MapReadOnlyResource<InstallationResponse>(endpoints, "/api/admin/installations", "installation",
            VendorPermissionCatalog.InstallationsRead,
            async (db, ct) => await db.Installations.AsNoTracking().OrderBy(item => item.InstallationCode)
                .Select(item => new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name, item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc)).ToListAsync(ct),
            async (db, id, ct) => await db.Installations.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name, item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc)).SingleOrDefaultAsync(ct));

        MapReadOnlyResource<ContractResponse>(endpoints, "/api/admin/contracts", "contract",
            VendorPermissionCatalog.ContractsRead,
            async (db, ct) => await db.Contracts.AsNoTracking().OrderBy(item => item.ContractNumber)
                .Select(item => new ContractResponse(item.Id, item.CustomerId, item.ContractNumber, item.Status, item.StartsAtUtc, item.EndsAtUtc, item.CurrencyCode, item.CreatedAtUtc)).ToListAsync(ct),
            async (db, id, ct) => await db.Contracts.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new ContractResponse(item.Id, item.CustomerId, item.ContractNumber, item.Status, item.StartsAtUtc, item.EndsAtUtc, item.CurrencyCode, item.CreatedAtUtc)).SingleOrDefaultAsync(ct));

        MapReadOnlyResource<LicenseResponse>(endpoints, "/api/admin/licenses", "license",
            VendorPermissionCatalog.LicensesRead,
            async (db, ct) => await db.Licenses.AsNoTracking().OrderBy(item => item.LicenseNumber)
                .Select(item => new LicenseResponse(item.Id, item.CustomerId, item.ContractId, item.InstallationId, item.LicenseNumber, item.ProductCode, item.Status, item.ValidFromUtc, item.ValidUntilUtc, item.CreatedAtUtc)).ToListAsync(ct),
            async (db, id, ct) => await db.Licenses.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new LicenseResponse(item.Id, item.CustomerId, item.ContractId, item.InstallationId, item.LicenseNumber, item.ProductCode, item.Status, item.ValidFromUtc, item.ValidUntilUtc, item.CreatedAtUtc)).SingleOrDefaultAsync(ct));

        return endpoints;
    }

    private static void MapReadOnlyResource<T>(
        IEndpointRouteBuilder endpoints, string route, string resourceType, string permission,
        Func<VendorDbContext, CancellationToken, Task<List<T>>> list,
        Func<VendorDbContext, Guid, CancellationToken, Task<T?>> get)
    {
        var group = endpoints.MapGroup(route).RequireAuthorization(VendorPermissionCatalog.PolicyFor(permission));
        group.MapGet("/", (VendorDbContext db, CancellationToken ct) => list(db, ct));
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IAuthorizationService authorization, VendorDbContext db, CancellationToken ct) =>
        {
            var access = await authorization.AuthorizeAsync(context.User,
                new VendorResourceDescriptor(resourceType, id),
                VendorResourceCatalog.PolicyFor(resourceType, permission));
            if (!access.Succeeded) return Results.Forbid();
            var item = await get(db, id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
    }
}

public sealed record CreateCustomerRequest(string? CustomerNumber, string? LegalName, string? DisplayName);
public sealed record CustomerResponse(Guid Id, string CustomerNumber, string LegalName, string DisplayName, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record InstallationResponse(Guid Id, Guid CustomerId, string InstallationCode, string Name, string Environment, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastSeenAtUtc);
public sealed record ContractResponse(Guid Id, Guid CustomerId, string ContractNumber, string Status, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, string CurrencyCode, DateTimeOffset CreatedAtUtc);
public sealed record LicenseResponse(Guid Id, Guid CustomerId, Guid? ContractId, Guid? InstallationId, string LicenseNumber, string ProductCode, string Status, DateTimeOffset ValidFromUtc, DateTimeOffset? ValidUntilUtc, DateTimeOffset CreatedAtUtc);
