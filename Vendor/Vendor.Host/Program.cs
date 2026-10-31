using System.Threading.RateLimiting;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authentication;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Bootstrap;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var bootstrapCommandRequested = VendorBootstrapCommand.IsRequested(args);
if (bootstrapCommandRequested && args.Length != 1)
{
    Console.Error.WriteLine("bootstrap-admin accepts no command-line arguments; enter all secrets interactively.");
    Environment.ExitCode = 2;
    return;
}

if (bootstrapCommandRequested && (Console.IsInputRedirected || Console.IsOutputRedirected))
{
    Console.Error.WriteLine("bootstrap-admin requires an interactive terminal; redirected input/output is refused.");
    Environment.ExitCode = 2;
    return;
}

// A bare command name is not valid input for the default command-line configuration provider.
var builder = WebApplication.CreateBuilder(bootstrapCommandRequested ? Array.Empty<string>() : args);

// Vendor has its own configuration namespace and never falls back to customer settings.
builder.Configuration.AddEnvironmentVariables(prefix: "FARANO_VENDOR_");

// This maintenance command intentionally bypasses the HTTP host and its web-only production
// prerequisites. It uses the Vendor database only and never accepts credentials in arguments.
if (bootstrapCommandRequested)
{
    builder.Services.AddVendorPersistence(builder.Configuration);
    new IdentityBuilder(typeof(VendorAdminUser), builder.Services).AddDefaultTokenProviders();

    // Identity's default token-provider set also requires Data Protection services, even though
    // this command never issues an HTTP session. Production must use the same persistent key store.
    var commandKeyDirectory = builder.Configuration["DataProtection:KeysDirectory"];
    if (builder.Environment.IsProduction() &&
        (string.IsNullOrWhiteSpace(commandKeyDirectory) || !Path.IsPathFullyQualified(commandKeyDirectory)))
    {
        throw new InvalidOperationException(
            "Vendor Data Protection keys require a fully qualified persistent directory. Set FARANO_VENDOR_DataProtection__KeysDirectory.");
    }

    var commandDataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("Faranou.Vendor.ControlPlane");
    if (!string.IsNullOrWhiteSpace(commandKeyDirectory))
    {
        var fullCommandKeyDirectory = Path.GetFullPath(commandKeyDirectory);
        Directory.CreateDirectory(fullCommandKeyDirectory);
        commandDataProtection.PersistKeysToFileSystem(new DirectoryInfo(fullCommandKeyDirectory));
    }

    var commandApp = builder.Build();
    var exitCode = await VendorBootstrapCommand.RunAsync(
        commandApp.Services,
        builder.Configuration,
        new ConsoleVendorBootstrapPrompt());
    await commandApp.DisposeAsync();
    Environment.ExitCode = exitCode;
    return;
}

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<VendorAuditCheckpointHealthState>();
builder.Services.AddVendorPersistence(builder.Configuration);
// Until approved production KMS/HSM adapters are configured, signing is deliberately unavailable.
// License, provisioning and offline-revocation signing have separate provider registrations and key boundaries.
builder.Services.TryAddSingleton<ILicenseSigningProvider, UnavailableLicenseSigningProvider>();
builder.Services.TryAddSingleton<IProvisioningSigningProvider, UnavailableProvisioningSigningProvider>();
builder.Services.TryAddSingleton<IOfflineRevocationSigningProvider, UnavailableOfflineRevocationSigningProvider>();

// PKCS#11 is opt-in and disabled by default. With no provisioned hardware HSM, Production
// remains on the unavailable providers and signing endpoints fail closed with HTTP 503.
if (builder.Configuration.GetValue<bool>("Signing:Pkcs11:Enabled"))
{
    builder.Services.Configure<Pkcs11SigningOptions>(
        builder.Configuration.GetSection("Signing:Pkcs11"));
    builder.Services.RemoveAll<ILicenseSigningProvider>();
    builder.Services.RemoveAll<IProvisioningSigningProvider>();
    builder.Services.RemoveAll<IOfflineRevocationSigningProvider>();
    builder.Services.AddSingleton<IPkcs11SigningModule, Pkcs11SigningModule>();
    builder.Services.AddScoped<ILicenseSigningProvider, Pkcs11LicenseSigningProvider>();
    builder.Services.AddScoped<IProvisioningSigningProvider, Pkcs11ProvisioningSigningProvider>();
    builder.Services.AddScoped<IOfflineRevocationSigningProvider, Pkcs11OfflineRevocationSigningProvider>();
    builder.Services.AddHostedService<Pkcs11SigningAssuranceStartupService>();
    builder.Services.AddHealthChecks().AddCheck<Pkcs11SigningHealthCheck>(
        "vendor-signing-provider",
        tags: new[] { "signing" });
}
else
{
    builder.Services.AddHealthChecks().AddCheck(
        "vendor-signing-provider",
        () => HealthCheckResult.Unhealthy("PKCS#11 signing provider is not configured."),
        tags: new[] { "signing" });
}

// Customer acknowledgement trust is supplied independently by deployment configuration.
// An empty key set fails closed; malformed configured keys prevent host startup.
builder.Services.AddSingleton(sp =>
{
    var configuredKeys = sp.GetRequiredService<IConfiguration>()
        .GetSection("ProvisioningAcknowledgementTrust:Keys")
        .Get<ProvisioningAcknowledgementTrustedKeyOptions[]>() ?? [];
    return new ProvisioningAcknowledgementTrustStore(configuredKeys);
});
// Vendor package-signing public keys are independently configured and contain no private material.
// An empty trust set blocks issuance; malformed or duplicate entries prevent host startup.
builder.Services.AddSingleton(sp =>
{
    var configuredKeys = sp.GetRequiredService<IConfiguration>()
        .GetSection("ProvisioningSigningTrust:Keys")
        .Get<ProvisioningPackageSigningTrustedKeyOptions[]>() ?? [];
    return new ProvisioningPackageSigningTrustStore(configuredKeys);
});
// License and offline-revocation public keys use independent trust stores.
builder.Services.AddSingleton<LicenseSigningTrustStore>();
builder.Services.AddSingleton<IOfflineRevocationKeyResolver, VendorOfflineRevocationKeyResolver>();
builder.Services.AddScoped<VendorProvisioningLicenseResolver>();
builder.Services.AddScoped<VendorLicenseEnvelopeFactory>();
builder.Services.AddScoped<VendorLicenseIssuanceService>();
builder.Services.AddScoped<VendorOfflineRevocationEnvelopeFactory>();
builder.Services.AddScoped<VendorOfflineRevocationIssuanceService>();
builder.Services.AddScoped<VendorProvisioningIssuanceService>();
// SignInManager belongs to the ASP.NET Core host layer, not the persistence class library.
// Sign-in manager and token providers are host-layer Identity services; register them on the same IdentityBuilder.
new IdentityBuilder(typeof(VendorAdminUser), builder.Services)
    .AddSignInManager()
    .AddDefaultTokenProviders();
var keyDirectory = builder.Configuration["DataProtection:KeysDirectory"];
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(keyDirectory) || !Path.IsPathFullyQualified(keyDirectory)))
{
    throw new InvalidOperationException(
        "Vendor Data Protection keys require a fully qualified persistent directory. Set FARANO_VENDOR_DataProtection__KeysDirectory.");
}

if (builder.Environment.IsProduction())
{
    var allowedHosts = builder.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(host => host.Trim() == "*"))
    {
        throw new InvalidOperationException(
            "Vendor Production requires an explicit AllowedHosts list; wildcard host filtering is not permitted.");
    }
}

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Faranou.Vendor.ControlPlane");
if (!string.IsNullOrWhiteSpace(keyDirectory))
{
    var fullKeyDirectory = Path.GetFullPath(keyDirectory);
    Directory.CreateDirectory(fullKeyDirectory);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(fullKeyDirectory));
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 128 * 1024;
});

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.Name = "__Host-Faranou.Vendor.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, options =>
{
    options.Cookie.Name = "__Host-Faranou.Vendor.TwoFactor";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    options.SlidingExpiration = false;
});

builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, options =>
{
    options.Cookie.Name = "__Host-Faranou.Vendor.RememberMfa";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = false;
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-Faranou.Vendor.AntiForgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});

builder.Services.AddHealthChecks();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("vendor-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("vendor-auth-attempt", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("vendor-password-change", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("provisioning-acknowledgement", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Identity options, stores, sign-in manager and token providers are registered once by AddVendorPersistence.

var passkeyServerDomain = builder.Configuration["IdentityPasskeys:ServerDomain"];
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(passkeyServerDomain))
    {
        throw new InvalidOperationException(
            "Passkey relying-party domain is not configured. Set FARANO_VENDOR_IdentityPasskeys__ServerDomain.");
    }

    var allowedHosts = builder.Configuration["AllowedHosts"] ?? string.Empty;
    if (!allowedHosts.Split(';').Any(host =>
            string.Equals(host.Trim(), passkeyServerDomain, StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "Vendor AllowedHosts must explicitly include the configured passkey relying-party domain.");
    }
}

builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ServerDomain = passkeyServerDomain;
    options.ResidentKeyRequirement = "required";
    options.UserVerificationRequirement = "required";
});

builder.Services.AddScoped<IAuthorizationHandler, VendorAdminAccessHandler>();
builder.Services.AddScoped<IAuthorizationHandler, VendorPermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, VendorResourcePermissionAuthorizationHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, VendorPermissionPolicyProvider>();
builder.Services.AddScoped<IVendorResourceAccessEvaluator, VendorResourceAccessEvaluator>();
builder.Services.AddAuthorization(VendorAuthorizationPolicies.Configure);

VendorAuditCheckpointStartupGuard.EnsureConfigured(builder.Environment.IsProduction(), builder.Services);
if (builder.Environment.IsProduction())
{
    // The production adapter is registered by the deployment-specific provider integration.
    // Resolve it through the shared contract and verify/read+append the checkpoint before readiness.
    // Do not let a generic/test store registration shadow the production marker adapter.
    builder.Services.RemoveAll<IVendorAuditCheckpointStore>();
    builder.Services.AddScoped<IVendorAuditCheckpointStore>(
        services => services.GetRequiredService<IProductionVendorAuditCheckpointStore>());
    builder.Services.AddScoped<VendorAuditCheckpointService>();
    builder.Services.Configure<VendorAuditCheckpointOptions>(
        builder.Configuration.GetSection("AuditCheckpoint"));
    builder.Services.AddHostedService<VendorAuditCheckpointBackgroundService>();
    builder.Services.AddHealthChecks().AddCheck<VendorAuditCheckpointHealthCheck>(
        "vendor-audit-checkpoint",
        tags: new[] { "audit-checkpoint", "ready" });
}

builder.Services.AddHostedService<VendorAuditCheckpointStartupService>();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseRouting();

// The standalone Vendor UI is bundled into this Host at publish time. Serve only local
// assets; the login screen and compiled JS/CSS are intentionally public, while APIs remain
// protected by the endpoint authorization policy below.
app.UseDefaultFiles();
app.Use(async (context, next) =>
{
    // Vite emits content-hashed filenames under /assets; immutable caching avoids
    // repeat downloads while a new index.html immediately points to the latest bundle.
    if (context.Request.Path.StartsWithSegments("/assets"))
    {
        context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
    else if (string.Equals(Path.GetFileName(context.Request.Path), "index.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.CacheControl = "no-store";
    }

    await next();
});
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();

// A newly bootstrapped administrator may establish an MFA session, but must not use
// management APIs until the initial password has been changed successfully.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        context.Request.Path.StartsWithSegments("/api"))
    {
        var users = context.RequestServices.GetRequiredService<UserManager<VendorAdminUser>>();
        var user = await users.GetUserAsync(context.User);
        var path = context.Request.Path;
        var allowedDuringForcedPasswordChange =
            path.Equals("/api/auth/antiforgery", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase);

        if (user is { IsEnabled: true, MustChangePassword: true } && !allowedDuringForcedPasswordChange)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(
                new { error = "password_change_required" },
                context.RequestAborted);
            return;
        }
    }

    await next();
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/signing", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("signing"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var entries = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                data = entry.Value.Data
            });
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new { status = report.Status.ToString(), entries },
            cancellationToken: context.RequestAborted);
    }
}).AllowAnonymous();
if (app.Environment.IsProduction())
{
    app.MapHealthChecks("/health/audit-checkpoint", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("audit-checkpoint"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description
                });
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new { status = report.Status.ToString(), entries },
                cancellationToken: context.RequestAborted);
        }
    }).AllowAnonymous();
}
app.MapVendorAuthentication();
app.MapVendorPasswordChange();
app.MapVendorRoleManagement();
app.MapVendorBusinessEndpoints();
app.MapVendorProvisioningEndpoints();
app.MapVendorApprovalEndpoints();
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? "/";
    if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) ||
        path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        Path.HasExtension(path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var webRoot = app.Environment.WebRootPath ??
        Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-store";
    await context.Response.SendFileAsync(indexPath);
}).AllowAnonymous();

app.Run();

public partial class Program { }
