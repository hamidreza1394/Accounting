using System.Diagnostics;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Vendor commercial sales orders, manual multi-payment recording, independent payment
/// maker-checker verification and read-time receivables reporting (D-108). Payment state never
/// gates license issuance and issuance never changes payment state; both keep separate
/// resource types, permissions and audit cycles.
/// </summary>
public static class VendorSalesEndpoints
{
    public static IEndpointRouteBuilder MapVendorSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var orders = endpoints.MapGroup("/api/vendor/sales-orders");
        var payments = endpoints.MapGroup("/api/vendor/payments");

        orders.MapGet("/", async (
            int? page, int? pageSize, Guid? customerId, VendorDbContext db,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            var paging = VendorPageRequest.Normalize(page, pageSize);
            IQueryable<VendorSalesOrder> query = db.SalesOrders.AsNoTracking();
            if (customerId is { } filter) query = query.Where(item => item.CustomerId == filter);
            query = query.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id);
            var totalCount = await query.CountAsync(ct);
            var rows = await query.Skip(paging.Skip).Take(paging.PageSize)
                .Select(item => new
                {
                    item.Id, item.CustomerId, item.OrderNumber, item.TotalAmount,
                    item.CurrencyCode, item.DueAtUtc, item.CreatedAtUtc, item.RowVersion,
                    CustomerNumber = item.Customer.CustomerNumber,
                    VerifiedPaid = item.Payments
                        .Where(payment => payment.Status == VendorPaymentRecord.StatusVerified)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m
                })
                .ToListAsync(ct);
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var items = rows.Select(item => ToResponse(
                (item.Id, item.CustomerId, item.OrderNumber, item.TotalAmount, item.CurrencyCode,
                    item.DueAtUtc, item.CreatedAtUtc, item.RowVersion, item.CustomerNumber,
                    item.VerifiedPaid),
                now)).ToArray();
            return Results.Ok(new VendorPageResponse<VendorSalesOrderResponse>(items, paging.Page, paging.PageSize, totalCount));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.PaymentsRead));

        orders.MapGet("/{id:guid}", async (
            Guid id, VendorDbContext db, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var order = await db.SalesOrders.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.Id, item.CustomerId, item.OrderNumber, item.TotalAmount,
                    item.CurrencyCode, item.DueAtUtc, item.CreatedAtUtc, item.RowVersion,
                    CustomerNumber = item.Customer.CustomerNumber,
                    Lines = item.Lines.OrderBy(line => line.CreatedAtUtc).ThenBy(line => line.Id)
                        .Select(line => new VendorSalesOrderLineResponse(
                            line.Id, line.ProductCode, line.Description, line.Quantity,
                            line.UnitAmount, line.LineAmount, line.LicenseId)).ToList(),
                    Payments = item.Payments.OrderBy(payment => payment.CreatedAtUtc).ThenBy(payment => payment.Id)
                        .Select(payment => new VendorPaymentResponse(
                            payment.Id, payment.OrderId, payment.Amount, payment.CurrencyCode,
                            payment.Method, payment.ExternalReference, payment.ClaimedPaidAtUtc,
                            payment.Status, payment.VerifiedByUserId, payment.VerifiedAtUtc,
                            payment.RejectionReason,
                            Convert.ToBase64String(payment.RowVersion))).ToList()
                })
                .SingleOrDefaultAsync(ct);
            if (order is null) return Results.NotFound();
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var verifiedPaid = order.Payments
                .Where(payment => payment.Status == VendorPaymentRecord.StatusVerified)
                .Sum(payment => payment.Amount);
            return Results.Ok(ToResponse(
                (order.Id, order.CustomerId, order.OrderNumber, order.TotalAmount, order.CurrencyCode,
                    order.DueAtUtc, order.CreatedAtUtc, order.RowVersion, order.CustomerNumber, verifiedPaid),
                now, order.Lines, order.Payments));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.PaymentsRead));

        orders.MapPost("/", async (
            CreateVendorSalesOrderRequest request, HttpContext context, IAntiforgery antiforgery,
            VendorDbContext db, UserManager<VendorAdminUser> users, IVendorAuditWriter audit,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (request is null ||
                request.CustomerId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.OrderNumber) ||
                request.OrderNumber.Trim().Length > 64 ||
                !IsValidCurrency(request.CurrencyCode) ||
                !FitsDecimal19Scale4(request.TotalAmount) ||
                request.TotalAmount < 0)
                return Results.BadRequest(new { error = "invalid_sales_order" });
            if (request.Lines is { Count: > 0 })
            {
                foreach (var line in request.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.ProductCode) || line.ProductCode.Trim().Length > 64 ||
                        string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 500 ||
                        line.Quantity < 1 || !FitsDecimal19Scale4(line.UnitAmount) ||
                        !FitsDecimal19Scale4(line.LineAmount) || line.UnitAmount < 0 ||
                        line.LineAmount != decimal.Round(line.Quantity * line.UnitAmount, 4))
                        return Results.BadRequest(new { error = "invalid_sales_order_line" });
                }

                if (request.Lines.Sum(line => line.LineAmount) != request.TotalAmount)
                    return Results.BadRequest(new { error = "sales_order_totals_mismatch" });
            }

            var customerExists = await db.Customers.AsNoTracking()
                .AnyAsync(item => item.Id == request.CustomerId && item.Status == "Active", ct);
            if (!customerExists)
                return Results.NotFound();

            if (request.ContractId is { } contractId &&
                !await db.Contracts.AsNoTracking().AnyAsync(
                    item => item.Id == contractId && item.CustomerId == request.CustomerId, ct))
                return Results.BadRequest(new { error = "contract_customer_mismatch" });

            var licenseIds = request.Lines?
                .Where(line => line.LicenseId.HasValue)
                .Select(line => line.LicenseId!.Value)
                .Distinct()
                .ToArray() ?? [];
            if (licenseIds.Length > 0)
            {
                var matchingLicenseCount = await db.Licenses.AsNoTracking().CountAsync(
                    item => licenseIds.Contains(item.Id) && item.CustomerId == request.CustomerId, ct);
                if (matchingLicenseCount != licenseIds.Length)
                    return Results.BadRequest(new { error = "license_customer_mismatch" });
            }

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var order = new VendorSalesOrder
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                ContractId = request.ContractId,
                OrderNumber = request.OrderNumber.Trim(),
                TotalAmount = request.TotalAmount,
                CurrencyCode = request.CurrencyCode!.ToUpperInvariant(),
                DueAtUtc = request.DueAtUtc?.ToUniversalTime(),
                CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime()
            };
            if (request.Lines is { Count: > 0 })
            {
                foreach (var line in request.Lines)
                {
                    order.Lines.Add(new VendorSalesOrderLine
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductCode = line.ProductCode.Trim(),
                        Description = line.Description.Trim(),
                        Quantity = line.Quantity,
                        UnitAmount = line.UnitAmount,
                        LineAmount = line.LineAmount,
                        LicenseId = line.LicenseId,
                        CreatedAtUtc = order.CreatedAtUtc
                    });
                }
            }

            db.SalesOrders.Add(order);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "sales_order_number_conflict" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.sales-order.created", nameof(VendorSalesOrder),
                order.Id.ToString("D"), CorrelationId()), ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/vendor/sales-orders/{order.Id:D}", new { order.Id });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.PaymentsManage));

        orders.MapPost("/{id:guid}/payments", async (
            Guid id, RecordVendorPaymentRequest request, HttpContext context, IAntiforgery antiforgery,
            VendorDbContext db, UserManager<VendorAdminUser> users, IVendorAuditWriter audit,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (request is null || request.Amount <= 0 || !FitsDecimal19Scale4(request.Amount) ||
                !IsValidCurrency(request.CurrencyCode) ||
                string.IsNullOrWhiteSpace(request.Method) || request.Method.Trim().Length > 32 ||
                (!string.IsNullOrWhiteSpace(request.ExternalReference) && request.ExternalReference.Trim().Length > 200))
                return Results.BadRequest(new { error = "invalid_payment" });

            var order = await db.SalesOrders.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (order is null) return Results.NotFound();
            if (!string.Equals(order.CurrencyCode, request.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "payment_currency_mismatch" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var payment = new VendorPaymentRecord
            {
                Id = Guid.NewGuid(),
                OrderId = id,
                Amount = request.Amount,
                CurrencyCode = order.CurrencyCode,
                Method = request.Method!.Trim(),
                ExternalReference = string.IsNullOrWhiteSpace(request.ExternalReference)
                    ? null
                    : request.ExternalReference.Trim(),
                ClaimedPaidAtUtc = request.ClaimedPaidAtUtc?.ToUniversalTime(),
                Status = VendorPaymentRecord.StatusRecorded,
                CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime()
            };
            db.PaymentRecords.Add(payment);
            await db.SaveChangesAsync(ct);

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.payment.recorded", nameof(VendorPaymentRecord),
                payment.Id.ToString("D"), CorrelationId()), ct);
            await tx.CommitAsync(ct);
            return Results.Created(
                $"/api/vendor/payments/{payment.Id:D}",
                new { payment.Id, payment.Status });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.PaymentsManage));

        payments.MapPost("/{id:guid}/approval-request", async (
            Guid id, CreateVendorPaymentApprovalRequest request, HttpContext context,
            IAntiforgery antiforgery, VendorDbContext db, UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit, TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (request?.Action is not ("verify" or "reject") ||
                !VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedPaymentVersion))
                return Results.BadRequest(new { error = "invalid_payment_approval_request" });

            var payment = await db.PaymentRecords.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (payment is null) return Results.NotFound();
            if (payment.Status != VendorPaymentRecord.StatusRecorded)
                return Results.Conflict(new { error = "payment_already_decided" });
            if (!payment.RowVersion.AsSpan().SequenceEqual(expectedPaymentVersion))
                return Results.Conflict(new { error = "stale_row_version" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var approval = new VendorApprovalRequest
            {
                Id = Guid.NewGuid(),
                ResourceType = "salesOrderPayment",
                ResourceId = id,
                TargetStatus = request.Action == "verify"
                    ? VendorPaymentRecord.StatusVerified
                    : VendorPaymentRecord.StatusRejected,
                ExpectedResourceRowVersion = expectedPaymentVersion,
                RequestedByUserId = actor.Id,
                RequestedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
                Status = "Pending"
            };
            db.ApprovalRequests.Add(approval);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "approval_already_pending" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.payment.approval-requested", nameof(VendorPaymentRecord),
                id.ToString("D"), CorrelationId()), ct);
            await tx.CommitAsync(ct);
            return Results.Accepted($"/api/vendor/approvals/{approval.Id:D}", new { approval.Id });
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.PaymentsManage));

        endpoints.MapGet("/api/vendor/receivables", async (
            int? page, int? pageSize, VendorDbContext db, TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            // Read-time receivables report (D-108 §7): no scheduler or background job; every read
            // derives the remaining debt from the order amount and Verified payments only.
            var paging = VendorPageRequest.Normalize(page, pageSize);
            var query = db.SalesOrders.AsNoTracking()
                .Select(item => new
                {
                    item.Id, item.CustomerId, item.OrderNumber, item.TotalAmount,
                    item.CurrencyCode, item.DueAtUtc, item.CreatedAtUtc,
                    CustomerNumber = item.Customer.CustomerNumber,
                    VerifiedPaid = item.Payments
                        .Where(payment => payment.Status == VendorPaymentRecord.StatusVerified)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m
                })
                .Where(item => item.TotalAmount > item.VerifiedPaid)
                .OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.Id);
            var totalCount = await query.CountAsync(ct);
            var rows = await query.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var items = rows.Select(item =>
            {
                var derived = VendorSalesOrderPaymentRules.Derive(
                    item.TotalAmount, item.VerifiedPaid, item.DueAtUtc, now);
                return new VendorReceivableResponse(
                    item.Id, item.CustomerId, item.CustomerNumber, item.OrderNumber,
                    item.TotalAmount, item.CurrencyCode, item.DueAtUtc,
                    derived.PaymentStatus, derived.Overdue,
                    derived.VerifiedPaidAmount, derived.RemainingAmount);
            }).ToArray();
            return Results.Ok(new VendorPageResponse<VendorReceivableResponse>(
                items, paging.Page, paging.PageSize, totalCount));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.PaymentsRead));

        return endpoints;
    }

    private static VendorSalesOrderResponse ToResponse(
        (Guid Id, Guid CustomerId, string OrderNumber, decimal TotalAmount, string CurrencyCode,
            DateTimeOffset? DueAtUtc, DateTimeOffset CreatedAtUtc, byte[] RowVersion,
            string CustomerNumber, decimal VerifiedPaid) order,
        DateTimeOffset now,
        List<VendorSalesOrderLineResponse>? lines = null,
        List<VendorPaymentResponse>? payments = null)
    {
        var derived = VendorSalesOrderPaymentRules.Derive(
            order.TotalAmount, order.VerifiedPaid, order.DueAtUtc, now);
        return new VendorSalesOrderResponse(
            order.Id, order.CustomerId, order.CustomerNumber, order.OrderNumber,
            order.TotalAmount, order.CurrencyCode, order.DueAtUtc, order.CreatedAtUtc,
            derived.PaymentStatus, derived.Overdue, derived.VerifiedPaidAmount,
            derived.RemainingAmount,
            lines ?? [],
            payments ?? [],
            Convert.ToBase64String(order.RowVersion));
    }

    private static bool FitsDecimal19Scale4(decimal value) =>
        value >= -999999999999999.9999m &&
        value <= 999999999999999.9999m &&
        decimal.Round(value, 4, MidpointRounding.ToEven) == value;

    private static bool IsValidCurrency(string? currency) =>
        !string.IsNullOrWhiteSpace(currency) && currency.Length == 3 &&
        currency.All(char.IsAsciiLetter);

    private static string CorrelationId() =>
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed record CreateVendorSalesOrderLineRequest(
    string? ProductCode, string? Description, int Quantity, decimal UnitAmount,
    decimal LineAmount, Guid? LicenseId);

public sealed record CreateVendorSalesOrderRequest(
    Guid CustomerId, Guid? ContractId, string? OrderNumber, decimal TotalAmount,
    string? CurrencyCode, DateTimeOffset? DueAtUtc, List<CreateVendorSalesOrderLineRequest>? Lines);

public sealed record RecordVendorPaymentRequest(
    decimal Amount, string? CurrencyCode, string? Method, string? ExternalReference,
    DateTimeOffset? ClaimedPaidAtUtc);

public sealed record CreateVendorPaymentApprovalRequest(string? Action, string? RowVersion);

public sealed record VendorSalesOrderLineResponse(
    Guid Id, string ProductCode, string Description, int Quantity,
    decimal UnitAmount, decimal LineAmount, Guid? LicenseId);

public sealed record VendorPaymentResponse(
    Guid Id, Guid OrderId, decimal Amount, string CurrencyCode, string Method,
    string? ExternalReference, DateTimeOffset? ClaimedPaidAtUtc, string Status,
    Guid? VerifiedByUserId, DateTimeOffset? VerifiedAtUtc, string? RejectionReason,
    string RowVersion);

public sealed record VendorSalesOrderResponse(
    Guid Id, Guid CustomerId, string CustomerNumber, string OrderNumber,
    decimal TotalAmount, string CurrencyCode, DateTimeOffset? DueAtUtc, DateTimeOffset CreatedAtUtc,
    string PaymentStatus, bool Overdue, decimal VerifiedPaidAmount, decimal RemainingAmount,
    IReadOnlyList<VendorSalesOrderLineResponse> Lines,
    IReadOnlyList<VendorPaymentResponse> Payments,
    string RowVersion);

public sealed record VendorReceivableResponse(
    Guid Id, Guid CustomerId, string CustomerNumber, string OrderNumber,
    decimal TotalAmount, string CurrencyCode, DateTimeOffset? DueAtUtc,
    string PaymentStatus, bool Overdue, decimal VerifiedPaidAmount, decimal RemainingAmount);
