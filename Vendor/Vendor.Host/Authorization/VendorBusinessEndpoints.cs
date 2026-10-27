using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Initial Vendor business APIs. Mutations are audited; resource checks always resolve persisted ownership.</summary>
public static class VendorBusinessEndpoints
{
    public static IEndpointRouteBuilder MapVendorBusinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var customers = endpoints.MapGroup("/api/vendor/customers");
        customers.MapGet("/", async (int? page, int? pageSize, VendorDbContext db, CancellationToken ct) =>
        {
            var pagination = VendorPageRequest.Normalize(page, pageSize);
            var query = db.Customers.AsNoTracking().OrderBy(item => item.CustomerNumber).ThenBy(item => item.Id)
                .Select(item => new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc, Convert.ToBase64String(item.RowVersion)));
            var totalCount = await query.CountAsync(ct);
            var items = await query.Skip(pagination.Skip).Take(pagination.PageSize).ToListAsync(ct);
            return Results.Ok(new VendorPageResponse<CustomerResponse>(items, pagination.Page, pagination.PageSize, totalCount));
        })
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
                Results.Ok(new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc, Convert.ToBase64String(item.RowVersion)));
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
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "customer_number_exists" });
            }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.customer.created",
                nameof(VendorCustomer), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/vendor/customers/{item.Id:D}",
                new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersManage));

        MapReadOnlyResource<InstallationResponse>(endpoints, "/api/vendor/installations", "installation",
            VendorPermissionCatalog.InstallationsRead,
            (db, customerId) =>
            {
                IQueryable<VendorInstallation> query = db.Installations.AsNoTracking();
                if (customerId.HasValue) query = query.Where(item => item.CustomerId == customerId.Value);
                return query.OrderBy(item => item.InstallationCode).ThenBy(item => item.Id)
                    .Select(item => new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name, item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc, Convert.ToBase64String(item.RowVersion)));
            },
            async (db, id, ct) => await db.Installations.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name, item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc, Convert.ToBase64String(item.RowVersion))).SingleOrDefaultAsync(ct));

        MapReadOnlyResource<ContractResponse>(endpoints, "/api/vendor/contracts", "contract",
            VendorPermissionCatalog.ContractsRead,
            (db, customerId) =>
            {
                IQueryable<VendorContract> query = db.Contracts.AsNoTracking();
                if (customerId.HasValue) query = query.Where(item => item.CustomerId == customerId.Value);
                return query.OrderBy(item => item.ContractNumber).ThenBy(item => item.Id)
                    .Select(item => new ContractResponse(item.Id, item.CustomerId, item.ContractNumber, item.Status, item.StartsAtUtc, item.EndsAtUtc, item.CurrencyCode, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)));
            },
            async (db, id, ct) => await db.Contracts.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new ContractResponse(item.Id, item.CustomerId, item.ContractNumber, item.Status, item.StartsAtUtc, item.EndsAtUtc, item.CurrencyCode, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)).SingleOrDefaultAsync(ct));

        MapReadOnlyResource<LicenseResponse>(endpoints, "/api/vendor/licenses", "license",
            VendorPermissionCatalog.LicensesRead,
            (db, customerId) =>
            {
                IQueryable<VendorLicense> query = db.Licenses.AsNoTracking();
                if (customerId.HasValue) query = query.Where(item => item.CustomerId == customerId.Value);
                return query.OrderBy(item => item.LicenseNumber).ThenBy(item => item.Id)
                    .Select(item => new LicenseResponse(item.Id, item.CustomerId, item.ContractId, item.InstallationId, item.LicenseNumber, item.ProductCode, item.Status, item.ValidFromUtc, item.ValidUntilUtc, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)));
            },
            async (db, id, ct) => await db.Licenses.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new LicenseResponse(item.Id, item.CustomerId, item.ContractId, item.InstallationId, item.LicenseNumber, item.ProductCode, item.Status, item.ValidFromUtc, item.ValidUntilUtc, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion))).SingleOrDefaultAsync(ct));

        var installations = endpoints.MapGroup("/api/vendor/installations")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.InstallationsManage));
        installations.MapPost("/", async (
            CreateInstallationRequest request, HttpContext context, IAntiforgery antiforgery,
            VendorDbContext db, UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            var code = request.InstallationCode?.Trim();
            var name = request.Name?.Trim();
            var environment = request.Environment?.Trim();
            if (request.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(code) || code.Length > 64 ||
                string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
                environment is not ("Production" or "Staging" or "Development"))
                return Results.BadRequest(new { error = "invalid_installation" });
            if (!await db.Customers.AnyAsync(item => item.Id == request.CustomerId, ct))
                return Results.BadRequest(new { error = "customer_not_found" });
            if (await db.Installations.AnyAsync(item => item.InstallationCode == code, ct))
                return Results.Conflict(new { error = "installation_code_exists" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = new VendorInstallation { Id = Guid.NewGuid(), CustomerId = request.CustomerId,
                InstallationCode = code, Name = name, Environment = environment, Status = "Active", CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Installations.Add(item);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "installation_code_exists" });
            }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.installation.created",
                nameof(VendorInstallation), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/vendor/installations/{item.Id:D}",
                new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name, item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        var contracts = endpoints.MapGroup("/api/vendor/contracts")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ContractsManage));
        contracts.MapPost("/", async (
            CreateContractRequest request, HttpContext context, IAntiforgery antiforgery,
            VendorDbContext db, UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            var number = request.ContractNumber?.Trim();
            var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
            if (request.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(number) || number.Length > 64 ||
                request.EndsAtUtc.HasValue && request.EndsAtUtc.Value < request.StartsAtUtc ||
                currency is null || currency.Length != 3 || !currency.All(char.IsAsciiLetter))
                return Results.BadRequest(new { error = "invalid_contract" });
            if (!await db.Customers.AnyAsync(item => item.Id == request.CustomerId, ct))
                return Results.BadRequest(new { error = "customer_not_found" });
            if (await db.Contracts.AnyAsync(item => item.CustomerId == request.CustomerId && item.ContractNumber == number, ct))
                return Results.Conflict(new { error = "contract_number_exists" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = new VendorContract { Id = Guid.NewGuid(), CustomerId = request.CustomerId,
                ContractNumber = number, Status = "Draft", StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = request.EndsAtUtc, CurrencyCode = currency, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Contracts.Add(item);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "contract_number_exists" });
            }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.contract.created",
                nameof(VendorContract), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/vendor/contracts/{item.Id:D}",
                new ContractResponse(item.Id, item.CustomerId, item.ContractNumber, item.Status, item.StartsAtUtc, item.EndsAtUtc, item.CurrencyCode, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        var licenses = endpoints.MapGroup("/api/vendor/licenses")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesManage));
        licenses.MapPost("/", async (
            CreateLicenseRequest request, HttpContext context, IAntiforgery antiforgery,
            VendorDbContext db, UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            var number = request.LicenseNumber?.Trim();
            var product = request.ProductCode?.Trim();
            if (request.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(number) || number.Length > 64 ||
                string.IsNullOrWhiteSpace(product) || product.Length > 64 ||
                request.ValidUntilUtc.HasValue && request.ValidUntilUtc.Value < request.ValidFromUtc)
                return Results.BadRequest(new { error = "invalid_license" });
            if (!await db.Customers.AnyAsync(item => item.Id == request.CustomerId, ct))
                return Results.BadRequest(new { error = "customer_not_found" });
            if (request.ContractId.HasValue && !await db.Contracts.AnyAsync(item =>
                    item.Id == request.ContractId.Value && item.CustomerId == request.CustomerId, ct))
                return Results.BadRequest(new { error = "contract_customer_mismatch" });
            if (request.InstallationId.HasValue && !await db.Installations.AnyAsync(item =>
                    item.Id == request.InstallationId.Value && item.CustomerId == request.CustomerId, ct))
                return Results.BadRequest(new { error = "installation_customer_mismatch" });
            if (await db.Licenses.AnyAsync(item => item.LicenseNumber == number, ct))
                return Results.Conflict(new { error = "license_number_exists" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = new VendorLicense { Id = Guid.NewGuid(), CustomerId = request.CustomerId,
                ContractId = request.ContractId, InstallationId = request.InstallationId,
                LicenseNumber = number, ProductCode = product, Status = "Draft",
                ValidFromUtc = request.ValidFromUtc, ValidUntilUtc = request.ValidUntilUtc, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Licenses.Add(item);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "license_number_exists" });
            }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.license.created",
                nameof(VendorLicense), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/vendor/licenses/{item.Id:D}",
                new LicenseResponse(item.Id, item.CustomerId, item.ContractId, item.InstallationId, item.LicenseNumber, item.ProductCode, item.Status, item.ValidFromUtc, item.ValidUntilUtc, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        });


        // Updates are allow-listed and concurrency-protected by the SQL Server rowversion returned in read DTOs.
        var customerMutations = endpoints.MapGroup("/api/vendor/customers")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.CustomersManage));
        customerMutations.MapPut("/{id:guid}", async (
            Guid id, UpdateCustomerRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
                return Results.BadRequest(new { error = "invalid_row_version" });
            var access = await authorization.AuthorizeAsync(context.User, new VendorResourceDescriptor("customer", id),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersManage));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Customers.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            // Reject stale tokens even when submitted values match current values.
            // EF Core may skip UPDATE for no-op changes, bypassing SQL rowversion concurrency checks.
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            var number = request.CustomerNumber?.Trim();
            var legalName = request.LegalName?.Trim();
            var displayName = request.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(number) || number.Length > 32 ||
                string.IsNullOrWhiteSpace(legalName) || legalName.Length > 200 ||
                string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
                return Results.BadRequest(new { error = "invalid_customer" });
            if (await db.Customers.AnyAsync(value => value.Id != id && value.CustomerNumber == number, ct))
                return Results.Conflict(new { error = "customer_number_exists" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Entry(item).Property(value => value.RowVersion).OriginalValue = expectedVersion;
            item.CustomerNumber = number;
            item.LegalName = legalName;
            item.DisplayName = displayName;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = "stale_row_version" });
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "customer_number_exists" });
            }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.customer.updated",
                nameof(VendorCustomer), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName,
                item.Status, item.CreatedAtUtc, item.UpdatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        customerMutations.MapPost("/{id:guid}/status", async (
            Guid id, ChangeOperationalStatusRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
                return Results.BadRequest(new { error = "invalid_row_version" });
            var access = await authorization.AuthorizeAsync(context.User, new VendorResourceDescriptor("customer", id),
                VendorResourceCatalog.PolicyFor("customer", VendorPermissionCatalog.CustomersManage));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Customers.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            // Reject stale tokens even when submitted values match current values.
            // EF Core may skip UPDATE for no-op changes, bypassing SQL rowversion concurrency checks.
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            if (!VendorBusinessMutationRules.IsOperationalStatusTransitionAllowed(item.Status, request.Status))
                return Results.Conflict(new { error = "invalid_status_transition" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Entry(item).Property(value => value.RowVersion).OriginalValue = expectedVersion;
            item.Status = request.Status!;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "stale_row_version" }); }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.customer.status-changed",
                nameof(VendorCustomer), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new CustomerResponse(item.Id, item.CustomerNumber, item.LegalName, item.DisplayName,
                item.Status, item.CreatedAtUtc, item.UpdatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        var installationMutations = endpoints.MapGroup("/api/vendor/installations")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.InstallationsManage));
        installationMutations.MapPut("/{id:guid}", async (
            Guid id, UpdateInstallationRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
                return Results.BadRequest(new { error = "invalid_row_version" });
            var access = await authorization.AuthorizeAsync(context.User, new VendorResourceDescriptor("installation", id),
                VendorResourceCatalog.PolicyFor("installation", VendorPermissionCatalog.InstallationsManage));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Installations.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            // Reject stale tokens even when submitted values match current values.
            // EF Core may skip UPDATE for no-op changes, bypassing SQL rowversion concurrency checks.
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            var code = request.InstallationCode?.Trim();
            var name = request.Name?.Trim();
            var environment = request.Environment?.Trim();
            if (string.IsNullOrWhiteSpace(code) || code.Length > 64 ||
                string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
                !VendorBusinessMutationRules.IsSupportedEnvironment(environment))
                return Results.BadRequest(new { error = "invalid_installation" });
            if (await db.Installations.AnyAsync(value => value.Id != id && value.InstallationCode == code, ct))
                return Results.Conflict(new { error = "installation_code_exists" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Entry(item).Property(value => value.RowVersion).OriginalValue = expectedVersion;
            item.InstallationCode = code;
            item.Name = name;
            item.Environment = environment!;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "stale_row_version" }); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            { return Results.Conflict(new { error = "installation_code_exists" }); }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.installation.updated",
                nameof(VendorInstallation), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name,
                item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        installationMutations.MapPost("/{id:guid}/status", async (
            Guid id, ChangeOperationalStatusRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
                return Results.BadRequest(new { error = "invalid_row_version" });
            var access = await authorization.AuthorizeAsync(context.User, new VendorResourceDescriptor("installation", id),
                VendorResourceCatalog.PolicyFor("installation", VendorPermissionCatalog.InstallationsManage));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Installations.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            // Reject stale tokens even when submitted values match current values.
            // EF Core may skip UPDATE for no-op changes, bypassing SQL rowversion concurrency checks.
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            if (!VendorBusinessMutationRules.IsOperationalStatusTransitionAllowed(item.Status, request.Status))
                return Results.Conflict(new { error = "invalid_status_transition" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Entry(item).Property(value => value.RowVersion).OriginalValue = expectedVersion;
            item.Status = request.Status!;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "stale_row_version" }); }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.installation.status-changed",
                nameof(VendorInstallation), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new InstallationResponse(item.Id, item.CustomerId, item.InstallationCode, item.Name,
                item.Environment, item.Status, item.CreatedAtUtc, item.LastSeenAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        var contractMutations = endpoints.MapGroup("/api/vendor/contracts")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ContractsManage));
        contractMutations.MapPut("/{id:guid}", async (
            Guid id, UpdateContractRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
                return Results.BadRequest(new { error = "invalid_row_version" });
            var access = await authorization.AuthorizeAsync(context.User, new VendorResourceDescriptor("contract", id),
                VendorResourceCatalog.PolicyFor("contract", VendorPermissionCatalog.ContractsManage));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Contracts.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            // Reject stale tokens even when submitted values match current values.
            // EF Core may skip UPDATE for no-op changes, bypassing SQL rowversion concurrency checks.
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            if (!VendorBusinessMutationRules.IsDraft(item.Status))
                return Results.Conflict(new { error = "contract_not_editable_after_draft" });
            var number = request.ContractNumber?.Trim();
            var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(number) || number.Length > 64 ||
                request.EndsAtUtc.HasValue && request.EndsAtUtc.Value < request.StartsAtUtc ||
                currency is null || currency.Length != 3 || !currency.All(char.IsAsciiLetter))
                return Results.BadRequest(new { error = "invalid_contract" });
            if (await db.Contracts.AnyAsync(value => value.Id != id && value.CustomerId == item.CustomerId && value.ContractNumber == number, ct))
                return Results.Conflict(new { error = "contract_number_exists" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Entry(item).Property(value => value.RowVersion).OriginalValue = expectedVersion;
            item.ContractNumber = number;
            item.StartsAtUtc = request.StartsAtUtc;
            item.EndsAtUtc = request.EndsAtUtc;
            item.CurrencyCode = currency;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "stale_row_version" }); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            { return Results.Conflict(new { error = "contract_number_exists" }); }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.contract.updated",
                nameof(VendorContract), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new ContractResponse(item.Id, item.CustomerId, item.ContractNumber, item.Status,
                item.StartsAtUtc, item.EndsAtUtc, item.CurrencyCode, item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        var licenseMutations = endpoints.MapGroup("/api/vendor/licenses")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesManage));
        licenseMutations.MapPut("/{id:guid}", async (
            Guid id, UpdateLicenseRequest request, HttpContext context, IAntiforgery antiforgery,
            IAuthorizationService authorization, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (!VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
                return Results.BadRequest(new { error = "invalid_row_version" });
            var access = await authorization.AuthorizeAsync(context.User, new VendorResourceDescriptor("license", id),
                VendorResourceCatalog.PolicyFor("license", VendorPermissionCatalog.LicensesManage));
            if (!access.Succeeded) return Results.Forbid();
            var item = await db.Licenses.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (item is null) return Results.NotFound();
            // Reject stale tokens even when submitted values match current values.
            // EF Core may skip UPDATE for no-op changes, bypassing SQL rowversion concurrency checks.
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                return Results.Conflict(new { error = "stale_row_version" });
            if (!VendorBusinessMutationRules.IsDraft(item.Status))
                return Results.Conflict(new { error = "license_not_editable_after_draft" });
            var number = request.LicenseNumber?.Trim();
            var product = request.ProductCode?.Trim();
            if (string.IsNullOrWhiteSpace(number) || number.Length > 64 ||
                string.IsNullOrWhiteSpace(product) || product.Length > 64 ||
                request.ValidUntilUtc.HasValue && request.ValidUntilUtc.Value < request.ValidFromUtc)
                return Results.BadRequest(new { error = "invalid_license" });
            if (await db.Licenses.AnyAsync(value => value.Id != id && value.LicenseNumber == number, ct))
                return Results.Conflict(new { error = "license_number_exists" });
            if (request.ContractId.HasValue && !await db.Contracts.AnyAsync(value =>
                    value.Id == request.ContractId.Value && value.CustomerId == item.CustomerId, ct))
                return Results.BadRequest(new { error = "contract_customer_mismatch" });
            if (request.InstallationId.HasValue && !await db.Installations.AnyAsync(value =>
                    value.Id == request.InstallationId.Value && value.CustomerId == item.CustomerId, ct))
                return Results.BadRequest(new { error = "installation_customer_mismatch" });
            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Entry(item).Property(value => value.RowVersion).OriginalValue = expectedVersion;
            item.ContractId = request.ContractId;
            item.InstallationId = request.InstallationId;
            item.LicenseNumber = number;
            item.ProductCode = product;
            item.ValidFromUtc = request.ValidFromUtc;
            item.ValidUntilUtc = request.ValidUntilUtc;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "stale_row_version" }); }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            { return Results.Conflict(new { error = "license_number_exists" }); }
            await audit.AppendAsync(new VendorAuditWriteRequest(actor.Id, "vendor.license.metadata-updated",
                nameof(VendorLicense), item.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new LicenseResponse(item.Id, item.CustomerId, item.ContractId, item.InstallationId,
                item.LicenseNumber, item.ProductCode, item.Status, item.ValidFromUtc, item.ValidUntilUtc,
                item.CreatedAtUtc, Convert.ToBase64String(item.RowVersion)));
        });

        return endpoints;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private static void MapReadOnlyResource<T>(
        IEndpointRouteBuilder endpoints, string route, string resourceType, string permission,
        Func<VendorDbContext, Guid?, IQueryable<T>> query,
        Func<VendorDbContext, Guid, CancellationToken, Task<T?>> get)
    {
        var group = endpoints.MapGroup(route).RequireAuthorization(VendorPermissionCatalog.PolicyFor(permission));
        group.MapGet("/", async (int? page, int? pageSize, Guid? customerId, VendorDbContext db, CancellationToken ct) =>
        {
            var pagination = VendorPageRequest.Normalize(page, pageSize);
            var orderedQuery = query(db, customerId);
            var totalCount = await orderedQuery.CountAsync(ct);
            var items = await orderedQuery.Skip(pagination.Skip).Take(pagination.PageSize).ToListAsync(ct);
            return Results.Ok(new VendorPageResponse<T>(items, pagination.Page, pagination.PageSize, totalCount));
        });
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
public sealed record UpdateCustomerRequest(string? CustomerNumber, string? LegalName, string? DisplayName, string? RowVersion);
public sealed record ChangeOperationalStatusRequest(string? Status, string? RowVersion);
public sealed record UpdateInstallationRequest(string? InstallationCode, string? Name, string? Environment, string? RowVersion);
public sealed record UpdateContractRequest(string? ContractNumber, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, string? CurrencyCode, string? RowVersion);
public sealed record UpdateLicenseRequest(Guid? ContractId, Guid? InstallationId, string? LicenseNumber, string? ProductCode, DateTimeOffset ValidFromUtc, DateTimeOffset? ValidUntilUtc, string? RowVersion);
public sealed record CustomerResponse(Guid Id, string CustomerNumber, string LegalName, string DisplayName, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);
public sealed record InstallationResponse(Guid Id, Guid CustomerId, string InstallationCode, string Name, string Environment, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastSeenAtUtc, string RowVersion);
public sealed record ContractResponse(Guid Id, Guid CustomerId, string ContractNumber, string Status, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, string CurrencyCode, DateTimeOffset CreatedAtUtc, string RowVersion);
public sealed record LicenseResponse(Guid Id, Guid CustomerId, Guid? ContractId, Guid? InstallationId, string LicenseNumber, string ProductCode, string Status, DateTimeOffset ValidFromUtc, DateTimeOffset? ValidUntilUtc, DateTimeOffset CreatedAtUtc, string RowVersion);

public sealed record CreateInstallationRequest(Guid CustomerId, string? InstallationCode, string? Name, string? Environment);
public sealed record CreateContractRequest(Guid CustomerId, string? ContractNumber, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, string? CurrencyCode);
public sealed record CreateLicenseRequest(Guid CustomerId, Guid? ContractId, Guid? InstallationId, string? LicenseNumber, string? ProductCode, DateTimeOffset ValidFromUtc, DateTimeOffset? ValidUntilUtc);


/// <summary>Normalizes list paging to bounded, deterministic values.</summary>
public readonly record struct VendorPageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public int Skip => checked((Page - 1) * PageSize);

    public static VendorPageRequest Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page.GetValueOrDefault(1);
        var normalizedSize = pageSize.GetValueOrDefault(DefaultPageSize);
        if (normalizedPage < 1) normalizedPage = 1;
        if (normalizedSize < 1) normalizedSize = DefaultPageSize;
        if (normalizedSize > MaximumPageSize) normalizedSize = MaximumPageSize;

        // EF Core Skip accepts Int32. Cap page so the offset stays representable;
        // clamping Skip alone makes different large page numbers return the same rows.
        var maximumPage = (int)Math.Min((long)int.MaxValue, (long)int.MaxValue / normalizedSize + 1);
        if (normalizedPage > maximumPage) normalizedPage = maximumPage;
        return new VendorPageRequest(normalizedPage, normalizedSize);
    }
}

public sealed record VendorPageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
