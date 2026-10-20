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
// **************************************************

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

builder.Services.AddRazorPages();

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
app.UseAuthorization();
app.UseCultureCookie();
app.MapRazorPages();

if (!setupRequired)
{
    // JSON API of the React client (same origin, cookie authentication, anti-forgery).
    app.MapFaranouApi();

    // The React client is built into wwwroot/app (see ClientApp/README.md). Client-side
    // routes under /app fall back to its index.html; the data itself is protected by the API.
    app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");
}

app.Run();
