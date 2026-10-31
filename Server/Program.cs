// **************************************************
using Infrastructure.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.DatabaseSetup;
using Server.Api;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Security;
using Infrastructure.Hosting;
using Faranou.Modules.Trade.Infrastructure.Persistence;
// **************************************************

// Operator command: produce a new infrastructure (Master) credential and exit. It runs before
// the host is built, so it needs no database and starts no web server.
if (args.Any(arg => string.Equals(arg, Infrastructure.DatabaseSetup.MasterCredentialCommand.Argument, StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = Infrastructure.DatabaseSetup.MasterCredentialCommand.RunOnConsole();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Explicit offline operator command: creates a non-exportable customer ACK key and emits only its public key.
if (Infrastructure.Provisioning.WindowsCngCustomerAcknowledgementKeyCommand.IsCommand(args))
{
    Environment.ExitCode = Infrastructure.Provisioning.WindowsCngCustomerAcknowledgementKeyCommand.RunOnConsole(
        args, builder.Configuration);
    return;
}

// Explicit, one-time customer license installation identity key ceremony. This command never
// runs during normal startup and never creates a replacement key automatically.
if (Infrastructure.Licensing.WindowsCngLicenseInstallationKeyCommand.IsCommand(args))
{
    Environment.ExitCode = Infrastructure.Licensing.WindowsCngLicenseInstallationKeyCommand.RunOnConsole(
        args, builder.Configuration);
    return;
}

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
DatabaseSettingsLoadResult? settingsLoad = null;

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Location: Farano:Settings:Directory (env Farano__Settings__Directory) or
    // LocalApplicationData/Farano/Settings. A legacy TarazAva file is only read, never changed.
    var settingsLocation = DatabaseSettingsLocation.Resolve(
        builder.Configuration,
        contentRootPath: builder.Environment.ContentRootPath,
        webRootPath: builder.Environment.WebRootPath);
    settingsStore = new DatabaseConnectionSettingsStore(settingsLocation.Root, settingsLocation.LegacyRoot);

    // Operator command (before the host starts): --settings status | migrate [...]
    if (SettingsCommand.IsSettingsCommand(args))
    {
        Environment.ExitCode = SettingsCommand.RunOnConsole(args, settingsStore, settingsLocation);
        return;
    }

    settingsLoad = settingsStore.Load();
    if (settingsLoad.HasConnectionString)
    {
        connectionString = settingsLoad.ConnectionString;
        connectionLoadedFromLocalSettings = true;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConnectionString"] = connectionString
        });
    }
}
else if (SettingsCommand.IsSettingsCommand(args))
{
    Console.Error.WriteLine("A connection string is configured explicitly (ConnectionStrings:ConnectionString); the settings file is not used, so --settings has nothing to report.");
    Environment.ExitCode = 2;
    return;
}

// Setup mode: NotConfigured/Corrupt/Undecryptable/LegacyUnreadable (the page asks for a replace
// confirmation when a file exists). Blocked: AccessDenied/UnsupportedVersion (no form). A file that
// exists but cannot be read never shows the plain first-run screen.
var setupRequired = string.IsNullOrWhiteSpace(connectionString);
if (setupRequired)
{
    if (!settingsLoad!.IsBlocked && !settingsStore!.TryEnsureSetupToken())
    {
        settingsLoad = new DatabaseSettingsLoadResult
        {
            Status = DatabaseSettingsStatus.AccessDenied,
            SettingsDirectory = settingsStore.SettingsDirectory,
        };
        settingsStore.OverrideLastLoad(settingsLoad);
    }

    builder.Services.AddSingleton(settingsStore!);
    builder.Services.AddSingleton<DatabaseConnectionSetupService>();
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

    // Trade uses the same physical database, with its own schema and migration history.
    // Register it only after the shared connection is configured; setup mode stays DB-free.
    builder.Services.AddTradePersistence(connectionString!);

    // The single server-side decision point for company (tenant) access. It reads the
    // memberships from the database on each request; it never trusts a client tenant id.
    builder.Services.AddScoped<Infrastructure.Tenancy.CompanyAccessService>();

    // The company and fiscal period a user chose for the current sign-in (D-031). The cookie
    // helper is stateless; the service validates every stored choice against the database.
    builder.Services.TryAddSingleton(TimeProvider.System);

    // Customer-side offline revocation is registered fail-closed. A real TPM provider is
    // selected only when the deployment explicitly supplies its TPM NV index configuration.
    var offlineRevocationTpmSection = builder.Configuration.GetSection("Licensing:OfflineRevocation:Tpm");
    builder.Services.Configure<Infrastructure.Licensing.TpmNvExtendOptions>(offlineRevocationTpmSection);
    if (offlineRevocationTpmSection.Exists())
    {
        builder.Services.AddSingleton<Faranou.Licensing.Contracts.IOfflineRevocationRollbackAnchor,
            Infrastructure.Licensing.Tpm2NvExtendRollbackAnchor>();
    }
    else
    {
        builder.Services.TryAddSingleton<Faranou.Licensing.Contracts.IOfflineRevocationRollbackAnchor,
            Infrastructure.Licensing.UnavailableOfflineRevocationRollbackAnchor>();
    }

    builder.Services.TryAddSingleton<Faranou.Licensing.Contracts.IOfflineRevocationKeyResolver,
        Infrastructure.Licensing.ConfigurationOfflineRevocationKeyResolver>();
    builder.Services.AddScoped<Faranou.Licensing.Contracts.IOfflineRevocationStateStore,
        Data.TpmAnchoredOfflineRevocationStateStore>();
    builder.Services.AddScoped<Faranou.Licensing.Contracts.OfflineRevocationConsumer>();

    // License signature and revocation checks are composed into one customer-side validator.
    // Missing trust keys or TPM configuration remain fail-closed; no unsigned fallback exists.
    builder.Services.TryAddSingleton<Faranou.Licensing.Contracts.ITrustedKeyResolver,
        Infrastructure.Licensing.ConfigurationLicenseTrustStore>();
    builder.Services.TryAddSingleton<Faranou.Licensing.Contracts.ILicenseSignatureVerifier,
        Faranou.Licensing.Contracts.EcdsaP256Sha256Verifier>();
    builder.Services.TryAddSingleton<Faranou.Licensing.Contracts.LicenseEvaluator>();
    // Expose only the revocation-aware customer validation contract through DI. The raw
    // signature/time validator remains an internal dependency of this pipeline, not a
    // service that runtime consumers can accidentally inject to bypass revocation checks.
    builder.Services.AddScoped<Faranou.Licensing.Contracts.ICustomerLicenseEnvelopeValidator>(serviceProvider =>
        new Faranou.Licensing.Contracts.RevocationAwareLicenseEnvelopeValidator(
            Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<
                Faranou.Licensing.Contracts.LicenseEnvelopeValidator>(serviceProvider),
            serviceProvider.GetRequiredService<Faranou.Licensing.Contracts.OfflineRevocationConsumer>()));
    builder.Services.Configure<Infrastructure.Licensing.WindowsCngLicenseInstallationKeyOptions>(
        builder.Configuration.GetSection(
            Infrastructure.Licensing.WindowsCngLicenseInstallationKeyOptions.SectionName));
    builder.Services.AddScoped<Infrastructure.Licensing.IInstallationIdentityProofVerifier,
        Infrastructure.Licensing.WindowsCngLicenseInstallationIdentityProofVerifier>();
    builder.Services.AddScoped<Infrastructure.Licensing.ILocalInstallationKeyMetadataProvider,
        Infrastructure.Licensing.WindowsCngLocalInstallationKeyMetadataProvider>();
    builder.Services.AddScoped<Infrastructure.Licensing.CustomerLicenseImportService>();
    builder.Services.AddScoped<Infrastructure.Licensing.CustomerInstallationIdentityEnrollmentService>();
    builder.Services.Configure<Infrastructure.Provisioning.CustomerProvisioningSettings>(
        builder.Configuration.GetSection(Infrastructure.Provisioning.CustomerProvisioningSettings.SectionName));
    builder.Services.AddSingleton<Infrastructure.Provisioning.ICustomerProvisioningTrustStore,
        Infrastructure.Provisioning.ConfigurationCustomerProvisioningTrustStore>();
    builder.Services.AddSingleton<Infrastructure.Provisioning.ICustomerProvisioningAcknowledgementTrustStore,
        Infrastructure.Provisioning.ConfigurationCustomerProvisioningAcknowledgementTrustStore>();
    builder.Services.AddSingleton<Infrastructure.Provisioning.CustomerProvisioningPackageVerifier>();
    builder.Services.AddScoped<Infrastructure.Provisioning.CustomerProvisioningApplyService>();
    builder.Services.TryAddSingleton<
        Infrastructure.Provisioning.ICustomerProvisioningAcknowledgementSigningProvider,
        Infrastructure.Provisioning.UnavailableCustomerProvisioningAcknowledgementSigningProvider>();
    builder.Services.Configure<Infrastructure.Provisioning.WindowsCngCustomerAcknowledgementSigningOptions>(
        builder.Configuration.GetSection(
            Infrastructure.Provisioning.WindowsCngCustomerAcknowledgementSigningOptions.SectionName));
    // The Windows CNG signer is opt-in and uses only the explicitly configured KSP. A missing
    // TPM/OS key never falls back to another provider; non-Windows hosts keep the unavailable signer.
    if (OperatingSystem.IsWindows() &&
        builder.Configuration.GetValue<bool>(
            Infrastructure.Provisioning.WindowsCngCustomerAcknowledgementSigningOptions.SectionName + ":Enabled"))
    {
        builder.Services.AddSingleton<
            Infrastructure.Provisioning.ICustomerProvisioningAcknowledgementSigningProvider,
            Infrastructure.Provisioning.WindowsCngCustomerProvisioningAcknowledgementSigningProvider>();
    }
    builder.Services.AddSingleton<Infrastructure.Tenancy.WorkContextCookie>();
    builder.Services.AddScoped<Infrastructure.Tenancy.WorkContextService>();

    // Trade authorization composes the shared company-role permissions with the
    // validated company/fiscal-period work context (D-062).
    builder.Services.AddScoped<Faranou.Modules.Trade.Application.Authorization.ITradeAuthorization,
        Infrastructure.Trade.TradeAuthorizationAdapter>();
    builder.Services.AddScoped<Faranou.Modules.Trade.Application.Licensing.ITradeModuleEntitlement,
        Infrastructure.Trade.DenyAllTradeModuleEntitlement>();
    builder.Services.AddScoped<Faranou.Modules.Trade.Application.Authorization.TradeAccessGate>();

    // Corporate groups, ownership and the scope of group report runs (D-033, D-034). Every
    // permission is decided by CompanyAccessService; a group membership grants no access.
    builder.Services.AddScoped<Infrastructure.CorporateGroups.CorporateGroupService>();

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

if (settingsLoad is not null)
{
    // Status, folder and warning codes only; never the connection string.
    app.Logger.Log(
        settingsLoad.Status is DatabaseSettingsStatus.Loaded or DatabaseSettingsStatus.MigratedFromLegacy or DatabaseSettingsStatus.NotConfigured
            ? LogLevel.Information
            : LogLevel.Warning,
        "Database settings: Status={SettingsStatus}; Folder={SettingsFolder}; WarningCodes={WarningCodes}.",
        settingsLoad.Status,
        settingsLoad.SettingsDirectory,
        string.Join(",", settingsLoad.WarningCodes));
}

if (settingsStore is not null)
{
    foreach (var warningCode in settingsStore.DrainWarnings())
    {
        app.Logger.LogWarning("Database settings warning: {WarningCode}.", warningCode);
    }
}

if (setupRequired && settingsLoad!.IsBlocked)
{
    app.Logger.LogError(
        "Farano database settings cannot be used ({SettingsStatus}). /DatabaseSetup shows the folder and the remedy; the setup form is disabled until the problem is fixed. Folder: {SettingsFolder}",
        settingsLoad.Status,
        settingsLoad.SettingsDirectory);
}
else if (setupRequired)
{
    app.Logger.LogWarning(
        "Farano requires first-run database setup. Open /DatabaseSetup on the application host and read the one-time token from {SetupTokenFilePath}. The token and database credentials are never written to application logs.",
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

    // CSP violation reports (answers 404 unless SecurityHeaders:CspReportingEnabled is true).
    app.MapCspReports();

    // The React client is built into wwwroot/app (see ClientApp/README.md). Client-side
    // routes under /app fall back to its index.html; the data itself is protected by the API.
    // index.html is revalidated on every visit so a new release is picked up immediately;
    // the hashed files under /app/assets can be cached by the browser.
    app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html", new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
    });
    app.MapFallbackToFile("/enroll", "app/index.html", new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
    });
    app.MapFallbackToFile("/provisioning/import", "app/index.html", new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
    });
}

app.Run();

// Exposes the entry point to the HTTP integration tests (WebApplicationFactory<Program>).
public partial class Program
{
}
