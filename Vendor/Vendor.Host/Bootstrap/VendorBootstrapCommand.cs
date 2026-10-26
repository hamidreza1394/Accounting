using System.Data;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faranou.Vendor.Host.Bootstrap;

/// <summary>
/// Interactive, one-time bootstrap for the first Vendor administrator. This is a command-mode
/// operation only; it must never be mapped to an HTTP endpoint or accept secrets in arguments.
/// </summary>
public static class VendorBootstrapCommand
{
    public const string CommandName = "bootstrap-admin";
    private const string ApplicationLockResource = "FaranouVendorBootstrap";
    private const int ApplicationLockTimeoutMilliseconds = 10_000;
    private const string Issuer = "Faranou Vendor";

    private static readonly Action<ILogger, string, Exception?> LogBootstrapFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5101, nameof(LogBootstrapFailure)),
            "Vendor bootstrap failed. Exception type: {ExceptionType}. Sensitive exception details were intentionally omitted.");

    private static readonly Action<ILogger, string, Exception?> LogAuditFailureUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5102, nameof(LogAuditFailureUnavailable)),
            "Could not persist Vendor bootstrap failure audit event. Exception type: {ExceptionType}; details omitted.");

    public static bool IsRequested(IEnumerable<string> args) =>
        args.Any(argument => string.Equals(argument, CommandName, StringComparison.Ordinal));

    /// <summary>Hashes both values first so the fixed-time comparison always receives equal-length buffers.</summary>
    public static bool MatchesSecret(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public static async Task<int> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IVendorBootstrapPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(prompt);

        var expectedSecret = configuration["Bootstrap:Secret"];
        if (string.IsNullOrWhiteSpace(expectedSecret) || Encoding.UTF8.GetByteCount(expectedSecret) < 32)
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.secret-missing", cancellationToken);
            prompt.WriteLine("راه‌اندازی امن متوقف شد: متغیر FARANO_VENDOR_Bootstrap__Secret باید حداقل ۳۲ بایت راز تصادفی داشته باشد.");
            return 2;
        }

        var suppliedSecret = prompt.ReadPassword("راز یک‌بارهٔ bootstrap را وارد کنید: ");
        if (!MatchesSecret(expectedSecret, suppliedSecret))
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.secret-rejected", cancellationToken);
            prompt.WriteLine("راز bootstrap معتبر نیست. هیچ حسابی ایجاد یا فعال نشد.");
            return 3;
        }

        var userName = prompt.ReadLine("نام کاربری مدیر: ")?.Trim();
        var email = prompt.ReadLine("ایمیل مدیر: ")?.Trim();
        var password = prompt.ReadPassword("گذرواژهٔ قوی مدیر: ");
        var passwordConfirmation = prompt.ReadPassword("تکرار گذرواژه: ");

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 256 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            string.IsNullOrEmpty(password) || password.Length > 1024 ||
            !string.Equals(password, passwordConfirmation, StringComparison.Ordinal))
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.input-rejected", cancellationToken);
            prompt.WriteLine("ورودی مدیر معتبر نیست یا دو گذرواژه یکسان نیستند. هیچ حسابی فعال نشد.");
            return 4;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("VendorBootstrapCommand");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
        var audit = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if (!db.Database.IsSqlServer())
        {
            prompt.WriteLine("راه‌اندازی امن فقط روی SQL Server پشتیبانی می‌شود.");
            return 5;
        }

        VendorAdminUser user;
        try
        {
            // Commit the disabled pending identity separately. If the process is interrupted
            // during MFA enrollment, a subsequent invocation can recover this same account.
            await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
            {
                await AcquireApplicationLockAsync(db, transaction, cancellationToken);

                var allUsers = await users.Users.OrderBy(candidate => candidate.Id).Take(2).ToListAsync(cancellationToken);
                var existing = allUsers.SingleOrDefault(candidate =>
                    string.Equals(candidate.UserName, userName, StringComparison.OrdinalIgnoreCase));

                if (allUsers.Count == 0)
                {
                    user = new VendorAdminUser
                    {
                        UserName = userName,
                        Email = email,
                        EmailConfirmed = false,
                        IsEnabled = false,
                        TwoFactorEnabled = false,
                        MustChangePassword = false,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };

                    var createResult = await users.CreateAsync(user, password);
                    if (!createResult.Succeeded)
                    {
                        await audit.AppendAsync(new VendorAuditWriteRequest(
                            null, "vendor.bootstrap.account-create-rejected"), cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        prompt.WriteLine("ساخت حساب موقت انجام نشد. سیاست گذرواژه و یکتایی ایمیل را بررسی کنید؛ جزئیات محرمانه نمایش داده نمی‌شود.");
                        return 6;
                    }

                    await audit.AppendAsync(new VendorAuditWriteRequest(
                        null, "vendor.bootstrap.pending-created", "VendorAdminUser", user.Id.ToString("N")), cancellationToken);
                }
                else if (allUsers.Count == 1 &&
                         existing is { IsEnabled: false, TwoFactorEnabled: false } &&
                         await users.CheckPasswordAsync(existing, password))
                {
                    user = existing;
                }
                else
                {
                    await audit.AppendAsync(new VendorAuditWriteRequest(
                        null, "vendor.bootstrap.rejected"), cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    prompt.WriteLine("Bootstrap قبلاً شروع شده یا مدیر دیگری وجود دارد؛ عملیات جدید رد شد.");
                    return 7;
                }

                await transaction.CommitAsync(cancellationToken);
            }

            // Keep the application lock for the complete TOTP enrollment so concurrent
            // bootstrap processes cannot rotate the authenticator key underneath each other.
            await using var enrollmentTransaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await AcquireApplicationLockAsync(db, enrollmentTransaction, cancellationToken);

            var currentUsers = await users.Users.OrderBy(candidate => candidate.Id).Take(2).ToListAsync(cancellationToken);
            user = await users.FindByIdAsync(user.Id.ToString())
                ?? throw new InvalidOperationException("Pending Vendor administrator disappeared.");
            if (currentUsers.Count != 1 || currentUsers[0].Id != user.Id ||
                user.IsEnabled || user.TwoFactorEnabled ||
                !await users.CheckPasswordAsync(user, password))
            {
                await audit.AppendAsync(new VendorAuditWriteRequest(
                    null, "vendor.bootstrap.rejected"), cancellationToken);
                await enrollmentTransaction.CommitAsync(cancellationToken);
                prompt.WriteLine("وضعیت حساب موقت تغییر کرده است؛ bootstrap به‌صورت امن متوقف شد.");
                return 8;
            }

            var resetKey = await users.ResetAuthenticatorKeyAsync(user);
            if (!resetKey.Succeeded)
            {
                throw new InvalidOperationException("Could not initialize the authenticator key.");
            }

            var authenticatorKey = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(authenticatorKey))
            {
                throw new InvalidOperationException("The authenticator key was not persisted.");
            }

            var label = Uri.EscapeDataString(string.IsNullOrWhiteSpace(email) ? userName : email);
            var issuer = Uri.EscapeDataString(Issuer);
            var authenticatorUri = $"otpauth://totp/{issuer}:{label}?secret={Uri.EscapeDataString(authenticatorKey)}&issuer={issuer}&digits=6&period=30";
            prompt.WriteLine("این URI را فقط در برنامهٔ authenticator مورد اعتماد ثبت کنید؛ آن را در log یا تیکت قرار ندهید:");
            prompt.WriteSensitiveLine(authenticatorUri);
            prompt.WriteLine("کد شش‌رقمی جاری را برای تأیید واقعی TOTP وارد کنید.");
            var code = NormalizeCode(prompt.ReadLine("کد TOTP: "));

            var validCode = code.Length == 6 &&
                await users.VerifyTwoFactorTokenAsync(
                    user,
                    users.Options.Tokens.AuthenticatorTokenProvider,
                    code);

            if (!validCode)
            {
                await audit.AppendAsync(new VendorAuditWriteRequest(
                    null, "vendor.bootstrap.totp-rejected", "VendorAdminUser", user.Id.ToString("N")), cancellationToken);
                await enrollmentTransaction.CommitAsync(cancellationToken);
                prompt.WriteLine("کد TOTP معتبر نیست. حساب همچنان غیرفعال است؛ اجرای مجدد می‌تواند enrollment را بازیابی کند.");
                return 9;
            }

            await EnsureOwnerRoleAsync(roles, users, user);

            var enableMfa = await users.SetTwoFactorEnabledAsync(user, true);
            if (!enableMfa.Succeeded)
            {
                throw new InvalidOperationException("Could not enable authenticator-based MFA.");
            }

            user.IsEnabled = true;
            user.MustChangePassword = false;
            var updateResult = await users.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException("Could not activate the verified Vendor administrator.");
            }

            // The local bootstrap operator is not authenticated as a Vendor admin yet;
            // do not falsely attribute this event to the account being created.
            await audit.AppendAsync(new VendorAuditWriteRequest(
                null, "vendor.bootstrap.completed", "VendorAdminUser", user.Id.ToString("N")), cancellationToken);
            await enrollmentTransaction.CommitAsync(cancellationToken);

            prompt.WriteLine("Bootstrap با موفقیت تکمیل شد. مدیر فعال است و ورود همچنان به MFA نیاز دارد.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.cancelled", CancellationToken.None);
            prompt.WriteLine("Bootstrap لغو شد؛ حساب موقت، در صورت ایجاد، غیرفعال باقی می‌ماند.");
            return 10;
        }
        catch (Exception exception)
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.failed", CancellationToken.None);
            // Do not log exception messages or objects: provider exceptions may contain connection data.
            LogBootstrapFailure(logger, exception.GetType().Name, null);
            prompt.WriteLine("Bootstrap ناموفق بود؛ جزئیات حساس ثبت یا نمایش داده نشدند. حساب موقت، در صورت وجود، باید غیرفعال بماند.");
            return 11;
        }
    }


    private static async Task EnsureOwnerRoleAsync(
        RoleManager<IdentityRole<Guid>> roles,
        UserManager<VendorAdminUser> users,
        VendorAdminUser user)
    {
        var role = await roles.FindByNameAsync(VendorPermissionCatalog.OwnerRoleName);
        if (role is null)
        {
            role = new IdentityRole<Guid>(VendorPermissionCatalog.OwnerRoleName);
            var createRole = await roles.CreateAsync(role);
            if (!createRole.Succeeded)
            {
                throw new InvalidOperationException("Could not create the initial Vendor owner role.");
            }
        }

        var existingClaims = await roles.GetClaimsAsync(role);
        foreach (var permission in VendorPermissionCatalog.All)
        {
            if (existingClaims.Any(claim =>
                    string.Equals(claim.Type, VendorPermissionCatalog.ClaimType, StringComparison.Ordinal) &&
                    string.Equals(claim.Value, permission.Code, StringComparison.Ordinal)))
            {
                continue;
            }

            var addClaim = await roles.AddClaimAsync(
                role,
                new Claim(VendorPermissionCatalog.ClaimType, permission.Code));
            if (!addClaim.Succeeded)
            {
                throw new InvalidOperationException("Could not grant the initial Vendor owner permission catalog.");
            }
        }

        var userRoles = await users.GetRolesAsync(user);
        if (!userRoles.Contains(VendorPermissionCatalog.OwnerRoleName, StringComparer.OrdinalIgnoreCase))
        {
            var addRole = await users.AddToRoleAsync(user, VendorPermissionCatalog.OwnerRoleName);
            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException("Could not assign the initial Vendor owner role.");
            }
        }

    }

    private static async Task TryAuditFailureAsync(
        IServiceProvider services,
        string actionCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var audit = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();
            await audit.AppendAsync(new VendorAuditWriteRequest(null, actionCode), cancellationToken);
        }
        catch (Exception exception)
        {
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger("VendorBootstrapCommand");
            if (logger is not null)
            {
                LogAuditFailureUnavailable(logger, exception.GetType().Name, null);
            }
        }
    }

    private static string NormalizeCode(string? value)
    {
        var normalized = (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.Length == 6 && normalized.All(char.IsAsciiDigit)
            ? normalized
            : string.Empty;
    }

    private static async Task AcquireApplicationLockAsync(
        VendorDbContext db,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @timeout;
            SELECT @result;
            """;

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.Value = ApplicationLockResource;
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@timeout";
        timeoutParameter.Value = ApplicationLockTimeoutMilliseconds;
        command.Parameters.Add(timeoutParameter);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new TimeoutException("Could not acquire the Vendor bootstrap application lock.");
        }
    }
}

public interface IVendorBootstrapPrompt
{
    string? ReadLine(string prompt);
    string ReadPassword(string prompt);
    void WriteLine(string message);
    void WriteSensitiveLine(string message);
}

public sealed class ConsoleVendorBootstrapPrompt : IVendorBootstrapPrompt
{
    public string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    public string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }
    }

    public void WriteLine(string message) => Console.WriteLine(message);

    public void WriteSensitiveLine(string message)
    {
        if (Console.IsOutputRedirected)
        {
            throw new InvalidOperationException("TOTP enrollment requires an interactive console; sensitive output is never written to redirected output.");
        }

        Console.WriteLine(message);
    }
}
