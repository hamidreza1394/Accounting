// **************************************************
using Infrastructure.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Infrastructure.DatabaseSetup;
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

builder.Services
    .AddAuthentication(defaultScheme: Infrastructure.Security.Utility.AuthenticationScheme)
    .AddCookie(
        authenticationScheme: Infrastructure.Security.Utility.AuthenticationScheme,
        configureOptions: options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        });

// Standard configuration (User Secrets/environment/appsettings) always wins.
// Only use the protected local settings file when no connection string was configured.
var settingsStore = new DatabaseConnectionSettingsStore(
    NullLogger<DatabaseConnectionSettingsStore>.Instance);
var connectionString = builder.Configuration.GetConnectionString("ConnectionString");
var connectionLoadedFromLocalSettings = false;

if (string.IsNullOrWhiteSpace(connectionString) &&
    settingsStore.TryLoadConnectionString(out var storedConnectionString))
{
    connectionString = storedConnectionString;
    connectionLoadedFromLocalSettings = true;
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:ConnectionString"] = connectionString
    });
}

var setupRequired = string.IsNullOrWhiteSpace(connectionString);
if (setupRequired)
{
    settingsStore.EnsureSetupToken();
}

builder.Services.AddSingleton(settingsStore);
builder.Services.AddScoped<DatabaseConnectionSetupService>();

if (!setupRequired)
{
    builder.Services.AddDbContext<Data.DatabaseContext>(options =>
        options.UseSqlServer(
            connectionString!,
            sqlServer => sqlServer.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName)));
}

builder.Services.AddScoped<Data.IDatabaseSchemaMigrator, Data.DatabaseSchemaMigrator>();

var app = builder.Build();

if (setupRequired)
{
    app.Logger.LogWarning(
        "Taraz Ava requires first-run database setup. Open /DatabaseSetup on the application host and read the one-time token from {SetupTokenFilePath}. The token and database credentials are never written to application logs.",
        settingsStore.SetupTokenFilePath);
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
else if (!setupRequired && !app.Environment.IsDevelopment())
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
if (setupRequired)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/DatabaseSetup"))
        {
            await next();
            return;
        }

        context.Response.Redirect("/DatabaseSetup");
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseCultureCookie();
app.MapRazorPages();
app.Run();
