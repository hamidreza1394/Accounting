using System.Data;
using System.Security.Claims;
using Faranou.Vendor.Host.Authorization;
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
        CancellationToken cancellationToken = default,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(prompt);

        var expectedSecret = configuration["Bootstrap:Secret"];
        if (string.IsNullOrWhiteSpace(expectedSecret) || Encoding.UTF8.GetByteCount(expectedSecret) < 32)
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.secret-missing", cancellationToken);
            prompt.WriteLine("Secure bootstrap stopped: FARANO_VENDOR_Bootstrap__Secret must contain at least 32 bytes of random data.");
            return 2;
        }

        var suppliedSecret = prompt.ReadPassword("Enter the one-time bootstrap secret: ");
        if (!MatchesSecret(expectedSecret, suppliedSecret))
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.secret-rejected", cancellationToken);
            prompt.WriteLine("The bootstrap secret is invalid. No account was created or enabled.");
            return 3;
        }

        var userName = prompt.ReadLine("Administrator username: ")?.Trim();
        var email = prompt.ReadLine("Administrator email: ")?.Trim();
        var password = prompt.ReadPassword("Administrator password: ");
        var passwordConfirmation = prompt.ReadPassword("Confirm administrator password: ");

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 256 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            string.IsNullOrEmpty(password) || password.Length > 1024 ||
            !string.Equals(password, passwordConfirmation, StringComparison.Ordinal))
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.input-rejected", cancellationToken);
            prompt.WriteLine("Administrator input is invalid or the passwords do not match. No account was enabled.");
            return 4;
        }

        // Resolve the clock only after early secret/input validation so rejected bootstrap
        // attempts still follow the same safe audit and response path without extra services.
        timeProvider ??= services.GetRequiredService<TimeProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("VendorBootstrapCommand");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var audit = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();

        if (!db.Database.IsSqlServer())
        {
            prompt.WriteLine("Secure bootstrap is supported only with SQL Server.");
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
                        CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime()
                    };

                    var createResult = await users.CreateAsync(user, password);
                    if (!createResult.Succeeded)
                    {
                        await audit.AppendAsync(new VendorAuditWriteRequest(
                            null, "vendor.bootstrap.account-create-rejected"), cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        prompt.WriteLine("The pending administrator account could not be created. Check the password policy and email uniqueness. Sensitive details are not displayed.");
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
                    prompt.WriteLine("Bootstrap has already started or an administrator already exists. The new attempt was rejected.");
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
                prompt.WriteLine("The pending account state changed. Bootstrap stopped safely.");
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
            prompt.WriteLine("Add this URI only to a trusted authenticator app. Do not put it in logs or tickets:");
            prompt.WriteSensitiveLine(authenticatorUri);
            prompt.WriteLine("Enter the current six-digit code to verify TOTP enrollment.");
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
                prompt.WriteLine("The TOTP code is invalid. The account remains disabled; rerun the command to resume enrollment.");
                return 9;
            }

            // The one-time bootstrap path is the only path allowed to grant the immutable
            // SuperAdmin role and its privileged administrator-management permission.
            var superAdminRole = await roles.FindByNameAsync("Vendor.SuperAdmin");
            if (superAdminRole is null)
            {
                superAdminRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Vendor.SuperAdmin" };
                var createRole = await roles.CreateAsync(superAdminRole);
                if (!createRole.Succeeded)
                {
                    throw new InvalidOperationException("Could not create the initial Vendor administrator role.");
                }
            }

            foreach (var permission in VendorPermissionCatalog.All)
            {
                var existingClaims = await roles.GetClaimsAsync(superAdminRole);
                if (!existingClaims.Any(claim =>
                        claim.Type == VendorPermissionCatalog.ClaimType &&
                        string.Equals(claim.Value, permission, StringComparison.Ordinal)))
                {
                    var addPermission = await roles.AddClaimAsync(
                        superAdminRole,
                        new Claim(VendorPermissionCatalog.ClaimType, permission));
                    if (!addPermission.Succeeded)
                    {
                        throw new InvalidOperationException("Could not assign the initial Vendor administrator permissions.");
                    }
                }
            }

            if (!await users.IsInRoleAsync(user, superAdminRole.Name!))
            {
                var assignRole = await users.AddToRoleAsync(user, superAdminRole.Name!);
                if (!assignRole.Succeeded)
                {
                    throw new InvalidOperationException("Could not assign the initial Vendor administrator role.");
                }
            }

            var enableMfa = await users.SetTwoFactorEnabledAsync(user, true);
            if (!enableMfa.Succeeded)
            {
                throw new InvalidOperationException("Could not enable authenticator-based MFA.");
            }

            user.IsEnabled = true;
            user.MustChangePassword = true;
            var updateResult = await users.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException("Could not activate the verified Vendor administrator.");
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                null, "vendor.bootstrap.superadmin-role-assigned", "VendorAdminUser", user.Id.ToString("N")), cancellationToken);

            // The local bootstrap operator is not authenticated as a Vendor admin yet;
            // do not falsely attribute this event to the account being created.
            await audit.AppendAsync(new VendorAuditWriteRequest(
                null, "vendor.bootstrap.completed", "VendorAdminUser", user.Id.ToString("N")), cancellationToken);
            await enrollmentTransaction.CommitAsync(cancellationToken);

            prompt.WriteLine("Bootstrap completed successfully. The administrator is enabled and sign-in still requires MFA.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.cancelled", CancellationToken.None);
            prompt.WriteLine("Bootstrap was cancelled. Any pending account remains disabled.");
            return 10;
        }
        catch (Exception exception)
        {
            await TryAuditFailureAsync(services, "vendor.bootstrap.failed", CancellationToken.None);
            // Do not log exception messages or objects: provider exceptions may contain connection data.
            LogBootstrapFailure(logger, exception.GetType().Name, null);
            prompt.WriteLine("Bootstrap failed. Sensitive details were not logged or displayed. Any pending account remains disabled.");
            return 11;
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
