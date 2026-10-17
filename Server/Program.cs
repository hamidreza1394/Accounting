// **************************************************
using Infrastructure.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
// **************************************************

// **************************************************
var builder = WebApplication.CreateBuilder(args);
// **************************************************

// **************************************************
// AddHttpContextAccessor() -> using Microsoft.Extensions.DependencyInjection;
builder.Services.AddHttpContextAccessor();
// **************************************************

// **************************************************
builder.Services.AddRouting(options =>
{
	options.LowercaseUrls = true;
	options.LowercaseQueryStrings = true;

	//options.AppendTrailingSlash
	//options.SuppressCheckForUnhandledSecurityMetadata = false;
});
// **************************************************

// **************************************************
// AddRazorPages() -> using Microsoft.Extensions.DependencyInjection;
builder.Services.AddRazorPages();
// **************************************************

// **************************************************
// Configure()-> using Microsoft.Extensions.DependencyInjection;
builder.Services.Configure<Infrastructure.Settings.ApplicationSettings>
	(builder.Configuration.GetSection(key: Infrastructure.Settings.ApplicationSettings.KeyName))
	// AddSingleton()-> using Microsoft.Extensions.DependencyInjection;
	.AddSingleton
	(implementationFactory: serviceType =>
	{
		var result =
			// GetRequiredService()-> using Microsoft.Extensions.DependencyInjection;
			serviceType.GetRequiredService
			<Microsoft.Extensions.Options.IOptions
			<Infrastructure.Settings.ApplicationSettings>>().Value;

		return result;
	});
// **************************************************

// **************************************************
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
// **************************************************

// **************************************************
// GetConnectionString() -> using Microsoft.Extensions.Configuration;
var connectionString = builder.Configuration.GetConnectionString("ConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. Configure ConnectionStrings:ConnectionString using User Secrets or an environment variable.");
}

// Change-tracking proxies and lazy-loading proxies are intentionally not enabled.
// The domain model includes sealed entities (for example Domain.Company), and normal EF Core
// change tracking does not require proxy-compatible entities. Load related data explicitly
// with Include/ThenInclude or project directly into view models to avoid hidden database queries.
builder.Services.AddDbContext<Data.DatabaseContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServer => sqlServer.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName)));

// **************************************************
builder.Services.AddScoped<Data.IDatabaseSchemaMigrator, Data.DatabaseSchemaMigrator>();
// **************************************************

var app = builder.Build();

var migrateOnly = args.Any(arg => string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase));
var applyMigrationsOnStartup = app.Environment.IsDevelopment()
    && string.Equals(builder.Configuration["Database:ApplyMigrations"], "true", StringComparison.OrdinalIgnoreCase);

if (migrateOnly)
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();
    await migrator.MigrateAsync();
    return;
}

if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();
    await migrator.MigrateAsync();
}
else if (!app.Environment.IsDevelopment())
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

if (!app.Environment.IsDevelopment())
{
    app.UseActivationKeys();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseCultureCookie();
app.MapRazorPages();
app.Run();
