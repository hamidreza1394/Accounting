using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Faranou.Vendor.Host;

internal static class VendorDevelopmentDatabaseSetup
{
    private const string CredentialTarget = "Faranou.Vendor.Development.VendorDb";
    private const string HtmlResource = "Faranou.Vendor.Host.Development.StaticFiles.index.html";
    private const string CssResource = "Faranou.Vendor.Host.Development.StaticFiles.setup.css";
    private const string JsResource = "Faranou.Vendor.Host.Development.StaticFiles.setup.js";
    private static bool _loadedFromCredentialManager;
    private static bool _explicitConfiguration;

    public static void SetConfigurationSource(bool hasExplicitConfiguration) =>
        _explicitConfiguration = hasExplicitConfiguration;

    /// <summary>Persists the validated runtime connection for the next Host run (setup flow).</summary>
    internal static void WriteSavedConnection(string connectionString) =>
        WindowsCredential.Write(CredentialTarget, connectionString);

    public static string? ReadSavedConnection()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var value = WindowsCredential.Read(CredentialTarget);
        _loadedFromCredentialManager = !string.IsNullOrWhiteSpace(value);
        return value;
    }

    public static void Map(WebApplication app, bool setupOnly)
    {
        app.Use(async (context, next) =>
        {
            if (setupOnly || IsSetupRequest(context.Request.Path))
            {
                // Loopback peer and a loopback Host name: a public tunnel or DNS-rebinding
                // name reaching the local port is rejected before any setup endpoint runs.
                if (!IsLoopback(context) || !IsLoopbackHostName(context.Request.Host.Host))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            }

            await next();
        });

        if (setupOnly)
        {
            app.UseRouting();
            app.UseRateLimiter();
        }

        if (setupOnly)
        {
            app.MapGet("/", () => Results.Redirect("/development/database-setup"));
        }

        // In setup-only mode no Vendor identity exists yet, so the local surface is anonymous.
        // Next to the full application every setup endpoint keeps the Vendor fallback policy
        // (MFA administrator), so a local non-browser process cannot repoint the database.
        TBuilder Access<TBuilder>(TBuilder endpoint) where TBuilder : IEndpointConventionBuilder =>
            setupOnly ? endpoint.AllowAnonymous() : endpoint;

        Access(app.MapGet("/development/database-setup", () => EmbeddedFile(HtmlResource, "text/html; charset=utf-8")));
        Access(app.MapGet("/development/database-setup.css", () => EmbeddedFile(CssResource, "text/css; charset=utf-8")));
        Access(app.MapGet("/development/database-setup.js", () => EmbeddedFile(JsResource, "text/javascript; charset=utf-8")));

        Access(app.MapGet("/api/development/database-setup/antiforgery",
            (IAntiforgery antiforgery, HttpContext context) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Json(new { requestToken = tokens.RequestToken });
            }));

        Access(app.MapGet("/api/development/database-setup/status", (IConfiguration configuration) =>
        {
            var source = GetSource(configuration);
            return Results.Json(new
            {
                source,
                canSave = source is "none" or "windowsCredentialManager",
                savedInWindowsCredentialManager = WindowsCredential.Exists(CredentialTarget)
            });
        }));

        Access(app.MapPost("/api/development/database-setup/test", async (
            HttpContext context,
            IAntiforgery antiforgery,
            SetupRequest request) =>
        {
            if (!await IsAuthorizedMutationAsync(context, antiforgery))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await TestConnectionAsync(request, context.RequestAborted);
            return result.Success
                ? Results.Json(new { success = true, message = "اتصال با موفقیت برقرار شد." })
                : Results.BadRequest(new { success = false, message = result.SafeMessage });
        }).RequireRateLimiting("vendor-development-setup"));

        Access(app.MapPost("/api/development/database-setup/save", async (
            HttpContext context,
            IAntiforgery antiforgery,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            SetupRequest request) =>
        {
            if (!await IsAuthorizedMutationAsync(context, antiforgery))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (GetSource(configuration) is not ("none" or "windowsCredentialManager"))
            {
                return Results.Conflict(new { message = "تنظیم از پیکربندی محیط می‌آید؛ ابتدا همان منبع را تغییر دهید." });
            }

            var result = await TestConnectionAsync(request, context.RequestAborted);
            if (!result.Success)
            {
                return Results.BadRequest(new { success = false, message = result.SafeMessage });
            }

            try
            {
                WindowsCredential.Write(CredentialTarget, result.ConnectionString!);
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                // The exception carries only the Win32 error; never log the connection string itself.
                CreateLogger(loggerFactory).LogError(exception, "Saving the Vendor development connection to Windows Credential Manager failed.");
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "ذخیره در Windows Credential Manager انجام نشد.");
            }

            return Results.Json(new
            {
                success = true,
                message = "اتصال تأیید و برای کاربر فعلی ذخیره شد. برای اعمال آن، Vendor را دوباره اجرا کنید."
            });
        }).RequireRateLimiting("vendor-development-setup"));

        Access(app.MapDelete("/api/development/database-setup/saved", async (
            HttpContext context,
            IAntiforgery antiforgery,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            if (!await IsAuthorizedMutationAsync(context, antiforgery))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (GetSource(configuration) is not ("none" or "windowsCredentialManager"))
            {
                return Results.Conflict(new { message = "تنظیم از پیکربندی محیط می‌آید و از این صفحه قابل حذف نیست." });
            }

            try
            {
                WindowsCredential.Delete(CredentialTarget);
            }
            catch (Win32Exception exception)
            {
                CreateLogger(loggerFactory).LogError(exception, "Deleting the saved Vendor development connection from Windows Credential Manager failed.");
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "حذف تنظیم ذخیره‌شده انجام نشد.");
            }

            return Results.Json(new { success = true, message = "تنظیم ذخیره‌شده حذف شد. برای اعمال، Vendor را دوباره اجرا کنید." });
        }).RequireRateLimiting("vendor-development-setup"));

        if (setupOnly)
        {
            app.MapFallback(() => Results.Redirect("/development/database-setup"));
        }
    }

    private static ILogger CreateLogger(ILoggerFactory loggerFactory) =>
        loggerFactory.CreateLogger("Faranou.Vendor.Host.DevelopmentDatabaseSetup");

    private static string GetSource(IConfiguration configuration)
    {
        if (_explicitConfiguration)
        {
            return Environment.GetEnvironmentVariable("FARANO_VENDOR_ConnectionStrings__VendorDb") is not null
                ? "environment"
                : "configuration";
        }

        return _loadedFromCredentialManager || WindowsCredential.Exists(CredentialTarget)
            ? "windowsCredentialManager"
            : "none";
    }

    private static IResult EmbeddedFile(string resourceName, string contentType)
    {
        var stream = typeof(VendorDevelopmentDatabaseSetup).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.Stream(stream, contentType, enableRangeProcessing: false);
    }

    private static bool IsSetupRequest(PathString path) =>
        path.Value?.StartsWith("/development/database-setup", StringComparison.OrdinalIgnoreCase) == true ||
        path.StartsWithSegments("/api/development/database-setup");

    private static bool IsLoopbackHostName(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    private static bool IsLoopback(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);

    private static async Task<bool> IsAuthorizedMutationAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        if (!IsLoopback(context) ||
            !context.Request.Headers.TryGetValue("Origin", out var originValues) ||
            !Uri.TryCreate(originValues.ToString(), UriKind.Absolute, out var origin) ||
            !string.Equals(origin.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(origin.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static async Task<ConnectionTestResult> TestConnectionAsync(
        SetupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Authentication is not ("windows" or "sql") ||
            string.IsNullOrWhiteSpace(request.Server) ||
            string.IsNullOrWhiteSpace(request.Database) ||
            request.Server.Length > 256 ||
            request.Database.Length > 128 ||
            request.Username?.Length > 256 ||
            request.Password?.Length > 1024)
        {
            return ConnectionTestResult.Failed("نام سرور، دیتابیس یا اطلاعات ورود معتبر نیست.");
        }

        try
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.Server.Trim(),
                InitialCatalog = request.Database.Trim(),
                IntegratedSecurity = string.Equals(request.Authentication, "windows", StringComparison.Ordinal),
                Encrypt = true,
                TrustServerCertificate = request.TrustServerCertificate,
                ConnectTimeout = 8,
                PersistSecurityInfo = false,
                ApplicationName = "Faranou Vendor Development Setup"
            };

            if (!builder.IntegratedSecurity)
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return ConnectionTestResult.Failed("برای ورود SQL نام کاربری و گذرواژه لازم است.");
                }

                builder.UserID = request.Username.Trim();
                builder.Password = request.Password;
            }

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DB_NAME()";
            var databaseName = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(databaseName, request.Database.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return ConnectionTestResult.Failed("اتصال برقرار شد، اما دیتابیس انتخاب‌شده تأیید نشد.");
            }

            return ConnectionTestResult.Passed(builder.ConnectionString);
        }
        catch (SqlException exception) when (exception.Number == 18456)
        {
            return ConnectionTestResult.Failed("ورود به SQL Server رد شد؛ نام کاربری و گذرواژه را بررسی کنید.");
        }
        catch (SqlException exception) when (exception.Number == 4060)
        {
            return ConnectionTestResult.Failed("دیتابیس در دسترس نیست؛ نام دیتابیس و دسترسی کاربر را بررسی کنید.");
        }
        catch (SqlException)
        {
            return ConnectionTestResult.Failed("اتصال برقرار نشد؛ آدرس سرور، شبکه، گواهی TLS و دسترسی دیتابیس را بررسی کنید.");
        }
        catch (ArgumentException)
        {
            return ConnectionTestResult.Failed("اطلاعات اتصال معتبر نیست.");
        }
    }

    private sealed record SetupRequest(
        string Server,
        string Database,
        string Authentication,
        string? Username,
        string? Password,
        bool TrustServerCertificate);

    private sealed record ConnectionTestResult(bool Success, string? ConnectionString, string SafeMessage)
    {
        public static ConnectionTestResult Passed(string connectionString) =>
            new(true, connectionString, string.Empty);

        public static ConnectionTestResult Failed(string message) =>
            new(false, null, message);
    }

    private static class WindowsCredential
    {
        private const int ErrorNotFound = 1168;
        private const uint Generic = 1;
        private const uint LocalMachine = 2;
        private const uint MaximumBlobSize = 2560;

        public static bool Exists(string target)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            if (!CredRead(target, Generic, 0, out var credential))
            {
                return Marshal.GetLastWin32Error() != ErrorNotFound;
            }

            CredFree(credential);
            return true;
        }

        public static string? Read(string target)
        {
            if (!OperatingSystem.IsWindows() || !CredRead(target, Generic, 0, out var pointer))
            {
                return null;
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
                if (credential.CredentialBlobSize == 0 ||
                    credential.CredentialBlobSize > MaximumBlobSize ||
                    credential.CredentialBlob == IntPtr.Zero)
                {
                    return null;
                }

                return Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / sizeof(char)));
            }
            finally
            {
                CredFree(pointer);
            }
        }

        public static void Write(string target, string value)
        {
            var blob = Encoding.Unicode.GetBytes(value);
            if (blob.Length > MaximumBlobSize)
            {
                CryptographicOperations.ZeroMemory(blob);
                throw new InvalidOperationException("Credential data exceeds the Windows limit.");
            }

            var blobPointer = Marshal.AllocHGlobal(blob.Length);
            var targetPointer = Marshal.StringToHGlobalUni(target);
            var userPointer = Marshal.StringToHGlobalUni(Environment.UserName);
            try
            {
                Marshal.Copy(blob, 0, blobPointer, blob.Length);
                var credential = new NativeCredential
                {
                    Type = Generic,
                    TargetName = targetPointer,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = blobPointer,
                    Persist = LocalMachine,
                    UserName = userPointer
                };

                if (!CredWrite(ref credential, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(blob);
                Marshal.FreeHGlobal(targetPointer);
                Marshal.FreeHGlobal(userPointer);
                for (var index = 0; index < blob.Length; index++)
                {
                    Marshal.WriteByte(blobPointer, index, 0);
                }

                Marshal.FreeHGlobal(blobPointer);
            }
        }

        public static void Delete(string target)
        {
            if (CredDelete(target, Generic, 0))
            {
                return;
            }

            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWrite(ref NativeCredential credential, uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("Advapi32.dll", SetLastError = false)]
        private static extern void CredFree(IntPtr buffer);
    }
}
