using System.Diagnostics;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Host.Authentication;

public static class VendorPasswordChangeEndpoints
{
    public static IEndpointRouteBuilder MapVendorPasswordChange(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            VendorDbContext db,
            IVendorAuditWriter auditWriter) =>
        {
            context.Response.Headers.CacheControl = "no-store";

            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                string.IsNullOrEmpty(request.NewPassword) ||
                request.CurrentPassword.Length > 1024 ||
                request.NewPassword.Length is < 14 or > 1024 ||
                string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = "Password change failed. Check the current password and password policy." });
            }

            // The Identity update and hash-chain audit entry share one SQL Server transaction.
            // If validation or audit persistence fails, neither the new hash nor the flag is committed.
            await using var transaction = await db.Database.BeginTransactionAsync(context.RequestAborted);
            var mustChangePassword = user.MustChangePassword;
            user.MustChangePassword = false;
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                user.MustChangePassword = mustChangePassword;
                await transaction.RollbackAsync(CancellationToken.None);
                return Results.BadRequest(new { error = "Password change failed. Check the current password and password policy." });
            }

            await auditWriter.AppendAsync(new VendorAuditWriteRequest(
                user.Id,
                "admin.password.changed",
                nameof(VendorAdminUser),
                user.Id.ToString("D"),
                Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), context.RequestAborted);
            await transaction.CommitAsync(context.RequestAborted);

            return Results.Ok(new { changed = true, mustChangePassword = false });
        })
        .RequireAuthorization()
        .RequireRateLimiting("vendor-password-change");

        return endpoints;
    }

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
