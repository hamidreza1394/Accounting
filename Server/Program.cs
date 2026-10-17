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
//builder.Services
//	.AddAuthentication(defaultScheme: Infrastructure.Security.Utility.AuthenticationScheme)
//	.AddCookie(authenticationScheme: Infrastructure.Security.Utility.AuthenticationScheme)
//	.AddGoogle(authenticationScheme: Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme,
//	configureOptions: options =>
//	{
//		options.ClientId =
//			builder.Configuration["ApplicationSettings:Authentication:Google:ClientId"];

//		options.ClientSecret =
//			builder.Configuration["ApplicationSettings:Authentication:Google:ClientSecret"];

//		// MapJsonKey() -> using Microsoft.AspNetCore.Authentication;
//		options.ClaimActions.MapJsonKey
//			(claimType: "urn:google:picture", jsonKey: "picture", valueType: "url");
//	})
//	;
// **************************************************
// **************************************************
// **************************************************

// **************************************************
// GetConnectionString() -> using Microsoft.Extensions.Configuration;
var connectionString = builder.Configuration.GetConnectionString("ConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. Configure ConnectionStrings:ConnectionString using User Secrets or an environment variable.");
}

// AddDbContext -> using Microsoft.Extensions.DependencyInjection;
builder.Services.AddDbContext<Data.DatabaseContext>
	(optionsAction: options =>
	{
		options
			// using Microsoft.EntityFrameworkCore;
			.UseLazyLoadingProxies();

		options
			// using Microsoft.EntityFrameworkCore;
			.UseSqlServer(connectionString: connectionString);

		options
			// using Microsoft.EntityFrameworkCore;
			.MigrationsAssembly(migrationAssembly: typeof(Data.DatabaseContext).Assembly.FullName);
	});
// **************************************************

// The schema migrator is the only component that is allowed to change the database schema.
// -> using Microsoft.Extensions.DependencyInjection;
builder.Services.AddScoped<Data.IDatabaseSchemaMigrator, Data.DatabaseSchemaMigrator>();
// **************************************************

// **************************************************
var app =
	builder.Build();
// **************************************************

// **************************************************
// Database schema management.
//
// The database schema is owned by the EF Core migrations in
// "Infrastructure/Persistence/Migrations" and is never created implicitly.
//
//   dotnet run --project Server -- --migrate
//       Applies the pending migrations and exits. This is the supported way of migrating
//       a production database: it is an explicit, reviewable, single-purpose operation.
//
//   Database:ApplyMigrations = true (Development only)
//       Applies the pending migrations when the application starts. It is ignored outside
//       of the Development environment, so a production deployment can never migrate the
//       database as a side effect of a restart.
//
//   dotnet ef database update --project Infrastructure/Persistence
//       The canonical EF Core workflow, see docs/database-migrations.md.
// **************************************************
var migrateOnly =
	args.Any(arg => string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase));

var applyMigrationsOnStartup =
	(builder.Configuration.GetValue<bool?>("Database:ApplyMigrations") ?? false)
	&& app.Environment.IsDevelopment();

if (migrateOnly)
{
	using (var scope = app.Services.CreateScope())
	{
		var migrator =
			scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();

		await migrator.MigrateAsync();
	}

	return;
}

if (applyMigrationsOnStartup)
{
	using (var scope = app.Services.CreateScope())
	{
		var migrator =
			scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();

		await migrator.MigrateAsync();
	}
}
else if (!app.Environment.IsDevelopment())
{
	// The production database is never migrated automatically: only report the drift.
	try
	{
		using var scope = app.Services.CreateScope();

		var migrator =
			scope.ServiceProvider.GetRequiredService<Data.IDatabaseSchemaMigrator>();

		var pendingMigrations =
			await migrator.GetPendingMigrationsAsync();

		if (pendingMigrations.Count > 0)
		{
			app.Logger.LogWarning
				(message: "The database schema is out of date. {PendingMigrationCount} migration(s) are pending: {PendingMigrations}. Apply them with 'dotnet run --project Server -- --migrate'.",
				PendingMigrationCount: pendingMigrations.Count,
				PendingMigrations: string.Join(", ", pendingMigrations));
		}
	}
	catch (System.Exception exception)
	{
		app.Logger.LogWarning
			(exception: exception,
			message: "The pending database migrations could not be checked at start-up.");
	}
}
// **************************************************

// IsDevelopment() -> using Microsoft.Extensions.Hosting;
if (app.Environment.IsDevelopment())
{
	// **************************************************
	// UseDeveloperExceptionPage() -> using Microsoft.AspNetCore.Builder;
	app.UseDeveloperExceptionPage();
	// **************************************************
}
else
{
	// **************************************************
	// UseGlobalException() -> using Infrastructure.Middlewares;
	app.UseGlobalException();
	// **************************************************

	// **************************************************
	// UseExceptionHandler() -> using Microsoft.AspNetCore.Builder;
	app.UseExceptionHandler("/Errors/Error");
	// **************************************************

	// **************************************************
	// The default HSTS value is 30 days.
	// You may want to change this for production scenarios,
	// see https://aka.ms/aspnetcore-hsts
	// UseHsts() -> using Microsoft.AspNetCore.Builder; 
	app.UseHsts();
	// **************************************************
}

// **************************************************
// UseHttpsRedirection() -> using Microsoft.AspNetCore.Builder;
app.UseHttpsRedirection();
// **************************************************

// **************************************************
// UseStaticFiles() -> using Microsoft.AspNetCore.Builder;
app.UseStaticFiles();
// **************************************************

// **************************************************
// License/host activation is enforced outside Development. Configure ApplicationSettings:ActivationKeys in production.
if (!app.Environment.IsDevelopment())
{
    app.UseActivationKeys();
}
// **************************************************

// **************************************************
// UseRouting() -> using Microsoft.AspNetCore.Builder;
app.UseRouting();
// **************************************************

// **************************************************
// UseAuthorization() -> using Microsoft.AspNetCore.Builder;
app.UseAuthentication();
// **************************************************

// **************************************************
// UseAuthorization() -> using Microsoft.AspNetCore.Builder;
app.UseAuthorization();
// **************************************************

// **************************************************
// UseCultureCookie() -> using Infrastructure.Middlewares;
app.UseCultureCookie();
// **************************************************

// **************************************************
// MapRazorPages() -> using Microsoft.AspNetCore.Builder;
app.MapRazorPages();
// **************************************************

// **************************************************
app.Run();
// **************************************************
