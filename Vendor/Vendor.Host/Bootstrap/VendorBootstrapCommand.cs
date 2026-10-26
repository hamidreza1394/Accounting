using System.Data;
using System.Security.Cryptography;
using System.Text;
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
            prompt.WriteLine("راه‌اندازی امن متوقف شد: متغیر FARANO_VENDOR_Bootstrap__Secret باید حداقل ۳۲ بایت راز تصادفی داشته باشد.");
            return 2;
        }

        var suppliedSecret = prompt.ReadLine("راز یک‌بارهٔ bootstrap را وارد کنید: ");
        if (!MatchesSecret(expectedSecret, suppliedSecret))
        {
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
            prompt.WriteLine("ورودی مدیر معتبر نیست یا دو گذرواژه یکسان نیستند. هیچ حسابی فعال نشد.");
            return 4;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("VendorBootstrapCommand");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
        var audit = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();

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
                        EmailConfirmed = true,
                        IsEnabled = false,
                        TwoFactorEnabled = false,
                        MustChangePassword = false,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };

                    var createResult = await users.CreateAsync(user, password);
                    if (!createResult.Succeeded)
                    {
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
            prompt.WriteLine("این QR/URI را فقط در برنامهٔ authenticator مورد اعتماد ثبت کنید؛ آن را در log یا تیکت قرار ندهید:");
            prompt.WriteLine(authenticatorUri);
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

            await audit.AppendAsync(new VendorAuditWriteRequest(
                user.Id, "vendor.bootstrap.completed", "VendorAdminUser", user.Id.ToString("N")), cancellationToken);
            await enrollmentTransaction.CommitAsync(cancellationToken);

            prompt.WriteLine("Bootstrap با موفقیت تکمیل شد. مدیر فعال است و ورود همچنان به MFA نیاز دارد.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            prompt.WriteLine("Bootstrap لغو شد؛ حساب موقت، در صورت ایجاد، غیرفعال باقی می‌ماند.");
            return 10;
        }
        catch (Exception exception)
        {
            // Do not log exception messages or objects: provider exceptions may contain connection data.
            logger.LogError("Vendor bootstrap failed. Exception type: {ExceptionType}. Sensitive exception details were intentionally omitted.",
                exception.GetType().Name);
            prompt.WriteLine("Bootstrap ناموفق بود؛ جزئیات حساس ثبت یا نمایش داده نشدند. حساب موقت، در صورت وجود، باید غیرفعال بماند.");
            return 11;
        }
    }

    private static string NormalizeCode(string? value) =>
        new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

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
}
