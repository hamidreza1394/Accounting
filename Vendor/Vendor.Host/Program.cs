using System.Threading.RateLimiting;
using Faranou.Vendor.Host.Authentication;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Vendor has its own configuration namespace and never falls back to customer settings.
builder.Configuration.AddEnvironmentVariables(prefix: "FARANO_VENDOR_");

var keyDirectory = builder.Configuration["DataProtection:KeysDirectory"];
if (string.IsNullOrWhiteSpace(keyDirectory) && builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "Vendor Data Protection keys are not configured. Set FARANO_VENDOR_DataProtection__KeysDirectory to a dedicated persistent directory.");
}

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Faranou.Vendor.ControlPlane");
if (!string.IsNullOrWhiteSpace(keyDirectory))
{
    var fullKeyDirectory = Path.GetFullPath(keyDirectory);
    Directory.CreateDirectory(fullKeyDirectory);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(fullKeyDirectory));
}

builder.Services.AddDbContext<VendorDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("VendorDb");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Vendor database is not configured. Set FARANO_VENDOR_ConnectionStrings__VendorDb.");
    }

    options.UseSqlServer(connectionString);
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
});

builder.Services.AddIdentityCore<VendorAdminUser>(options =>
{
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Password.RequiredLength = 14;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<VendorDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

var passkeyServerDomain = builder.Configuration["IdentityPasskeys:ServerDomain"];
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(passkeyServerDomain))
{
    throw new InvalidOperationException(
        "Passkey relying-party domain is not configured. Set FARANO_VENDOR_IdentityPasskeys__ServerDomain.");
}

builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ServerDomain = passkeyServerDomain;
    options.ResidentKeyRequirement = "required";
    options.UserVerificationRequirement = "required";
});

builder.Services.AddScoped<IAuthorizationHandler, VendorAdminAccessHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new VendorAdminAccessRequirement())
        .Build();
});

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapVendorAuthentication();

app.Run();

public partial class Program { }
