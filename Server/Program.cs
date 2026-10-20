// **************************************************
using Infrastructure.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Infrastructure.DatabaseSetup;
using Server.Api;
using Infrastructure.Security;
using Infrastructure.Hosting;
// **************************************************

// Operator command: produce a new infrastructure (Master) credential and exit. It runs before
// the host is built, so it needs no database and starts no web server.
if (args.Any(arg => string.Equals(arg, Infrastructure.DatabaseSetup.MasterCredentialCommand.Argument, StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = Infrastructure.DatabaseSetup.MasterCredentialCommand.RunOnConsole();
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

builder.Services.AddRazorPages(options =>
{
    // Submissions of the sign-in form are limited per client address, in addition to the
    // per-username lockout of AuthenticationService.
    options.Conventions.AddPageRouteModelConvention("/Account/Login", model =>
    {
        foreach (var selector in model.Selectors)
        {
            selector.EndpointMetadata.Add(
                new Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute(
                    Infrastructure.Security.RateLimitingPolicies.LoginPolicy));
        }
    });
});

builder.Services.AddFaranouRateLimiting(builder.Configuration);
// Forwarding headers are honoured only from the proxies listed in ReverseProxy (none by default).
builder.Services.AddFaranouForwardedHeaders(builder.Configuration);
builder.Services.Configure<Infrastructure.Security.SecurityHeadersSettings>(
    builder.Configuration.GetSection(Infrastructure.Security.SecurityHeadersSettings.SectionName));

builder.Services.Configure<Infrastructure.Settings.ApplicationSettings>
    (builder.Configuration.GetSection(key: Infrastructure.Settings.ApplicationSettings.KeyName))
    .AddSingleton(implementationFactory: serviceType =>
        serviceType.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Settings.ApplicationSettings>>().Value);

// Stateless security helpers are safe as singletons in every hosting mode, including first-run setup.
builder.Services.AddSingleton<Infrastructure.Security.PasswordHashService>();
builder.Services.AddSingleton<InfrastructureMasterBootstrap>();

builder.Services
    .AddAuthentication(defaultScheme: Infrastructure.Security.Utility.AuthenticationScheme)
    .AddCookie(
        authenticationScheme: Infrastructure.Security.Utility.AuthenticationScheme,
        configureOptions: options =>
        {
            options.Cookie.Name = ".Faranou.Authentication";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

            // Explicit instead of relying on the framework defaults, so the sign-in flow
            // and the challenge of an anonymous request can never disagree.
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";

            // Browser pages keep the redirect to the login page; /api requests receive
            // 401/403 so the React client can handle them.
            options.Events.OnRedirectToLogin = Server.Api.ApiCookieAuthenticationEvents.RedirectToLoginAsync;
            options.Events.OnRedirectToAccessDenied = Server.Api.ApiCookieAuthenticationEvents.RedirectToAccessDeniedAsync;

            // Every request re-checks the account (active, unchanged credential); a deactivated
            // account or a changed password ends existing sessions on their next request.
            options.Events.OnValidatePrincipal = Infrastructure.Security.SessionValidator.ValidatePrincipalAsync;
        });

// The React client sends the anti-forgery request token in this header; Razor forms keep
// sending it as a form field. Both are validated by the same antiforgery service.
builder.Services.AddAntiforgery(options => options.HeaderName = Server.Api.ApiEndpoints.AntiforgeryHeaderName);

// Standard configuration (User Secrets/environment/appsettings) always wins.
// Only use the protected local settings file when no connection string was configured.
var connectionString = builder.Configuration.GetConnectionString("ConnectionString");
var connectionLoadedFromLocalSettings = false;
DatabaseConnectionSettingsStore? settingsStore = null;

if (string.IsNullOrWhiteSpace(connectionString))
{
    settingsStore = new DatabaseConnectionSettingsStore(
        NullLogger<DatabaseConnectionSettingsStore>.Instance);

    if (settingsStore.TryLoadConnectionString(out var storedConnectionString))
    {
        connectionString = storedConnectionString;
        connectionLoadedFromLocalSettings = true;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConnectionString"] = connectionString
        });
    }
}

var setupRequired = string.IsNullOrWhiteSpace(connectionString);
if (setupRequired)
{
    settingsStore!.EnsureSetupToken();
    builder.Services.AddSingleton(settingsStore!);
    builder.Services.AddScoped<DatabaseConnectionSetupService>();
}

if (!setupRequired)
{
    builder.Services.AddDbContext<Data.DatabaseContext>(options =>
        options.UseSqlServer(
            connectionString!,
            sqlServer => sqlServer.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName)));

    // DatabaseSchemaMigrator depends on DatabaseContext; register it only when the same
    // branch has registered and configured that context.
    builder.Services.AddScoped<Data.IDatabaseSchemaMigrator, Data.DatabaseSchemaMigrator>();

    // The single server-side decision point for company (tenant) access. It reads the
    // memberships from the database on each request; it never trusts a client tenant id.
    builder.Services.AddScoped<Infrastructure.Tenancy.CompanyAccessService>();

    // Scoped, because the sign-in service owns the scoped DatabaseContext. The page models
    // resolve it from the container instead of constructing it, so the dependency is created
    // and disposed exactly once per request.
    builder.Services.AddScoped<Infrastructure.Security.AuthenticationService>
        (implementationFactory: serviceProvider =>
            new Infrastructure.Security.AuthenticationService(
                databaseContext: serviceProvider.GetRequiredService<Data.DatabaseContext>(),
                logger: serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger<Infrastructure.Security.AuthenticationService>(),
                applicationSettings: serviceProvider
                    .GetRequiredService<Infrastructure.Settings.ApplicationSettings>(),
                passwordHashService: serviceProvider
                    .GetRequiredService<Infrastructure.Security.PasswordHashService>()));
}

var app = builder.Build();

if (setupRequired)
{
    app.Logger.LogWarning(
        "Taraz Ava requires first-run database setup. Open /DatabaseSetup on the application host and read the one-time token from {SetupTokenFilePath}. The token and database credentials are never written to application logs.",
        settingsStore!.SetupTokenFilePath);
}

var migrateOnly = args.Any(arg => string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase));
// Connections configured by the operator through the setup form are migrated during first-run
// initialization only for a new/empty database. Existing databases require an explicit migration
// step so a legacy EnsureCreated schema is never upgraded blindly on application startup.
var applyMigrationsOnStartup = !connectionLoadedFromLocalSettings
    && app.Environment.IsDevelopment()
    && string.Equals(builder.Configuration["Database:ApplyMigrations"], "true", StringComparison.OrdinalIgnoreCase);

if (!setupRequired && migrateOnly)
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();
    await migrator.MigrateAsync();
    return;
}

if (!setupRequired && applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();
    await migrator.MigrateAsync();
}
else if (!setupRequired && (!app.Environment.IsDevelopment() || connectionLoadedFromLocalSettings))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();
        var pendingMigrations = await migrator.GetPendingMigrationsAsync();

        if (pendingMigrations.Count > 0)
        {
            app.Logger.LogWarning(
                "The database schema is out of date. {PendingMigrationCount} migration(s) are pending: {PendingMigrations}. Apply them with 'dotnet run --project Server -- --migrate'.",
                pendingMigrations.Count,
                string.Join(", ", pendingMigrations));
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(exception, "The pending database migrations could not be checked at start-up.");
    }
}

foreach (var warning in Infrastructure.Hosting.DeploymentConfigurationChecks.GetWarnings(app.Configuration, app.Environment))
{
    app.Logger.LogWarning("Deployment configuration: {Warning}", warning);
}

// First: the client address and scheme from a trusted proxy are needed by HSTS, HTTPS
// redirection, rate limiting and the audit trail. Without a configured proxy it does nothing.
app.UseForwardedHeaders();

// Then, so every response (pages, API, static files, errors) carries the security headers.
app.UseMiddleware<Infrastructure.Security.SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseGlobalException();
    app.UseExceptionHandler("/Errors/Error");
    app.UseHsts();
}

// API requests never receive an HTML error page or a stack trace, in any environment: the
// exception is logged and a generic application/problem+json response is returned.
app.UseWhen(
    context => ApiEndpoints.IsApiRequest(context.Request.Path),
    api => api.UseExceptionHandler(new ExceptionHandlerOptions
    {
        ExceptionHandler = ApiEndpoints.WriteUnexpectedErrorAsync,
    }));

app.UseHttpsRedirection();
app.UseStaticFiles();

// Do not let application pages run without a configured database. The setup endpoint is the
// only dynamic page exposed until the operator completes setup and restarts the application.
app.Use(async (context, next) =>
{
    if (setupRequired)
    {
        if (context.Request.Path.StartsWithSegments("/DatabaseSetup"))
        {
            await next();
            return;
        }

        context.Response.Redirect("/DatabaseSetup");
        return;
    }

    if (context.Request.Path.StartsWithSegments("/DatabaseSetup"))
    {
        context.Response.Redirect("/");
        return;
    }

    await next();
});

app.UseRouting();
app.UseAuthentication();
// After authentication (partitions per signed-in user) and before authorization, so that
// anonymous floods are limited too.
app.UseRateLimiter();
app.UseAuthorization();
app.UseCultureCookie();
app.MapRazorPages();

if (!setupRequired)
{
    // JSON API of the React client (same origin, cookie authentication, anti-forgery).
    app.MapFaranouApi();

    // The React client is built into wwwroot/app (see ClientApp/README.md). Client-side
    // routes under /app fall back to its index.html; the data itself is protected by the API.
    // index.html is revalidated on every visit so a new release is picked up immediately;
    // the hashed files under /app/assets can be cached by the browser.
    app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html", new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
    });
}

app.Run();

// Exposes the entry point to the HTTP integration tests (WebApplicationFactory<Program>).
public partial class Program
{
}
