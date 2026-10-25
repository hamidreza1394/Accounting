using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The dedicated prefix prevents accidentally sharing customer-app settings/secrets.
builder.Configuration.AddEnvironmentVariables(prefix: "FARANO_VENDOR_");

builder.Services.AddHealthChecks();
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

// Identity schema v3 is required for the built-in ASP.NET Core 10 passkey store.
builder.Services.AddIdentityCore<VendorAdminUser>(options =>
{
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddRoles<IdentityRole<Guid>>()
  .AddEntityFrameworkStores<VendorDbContext>()
  .AddDefaultTokenProviders();

// WebAuthn must require both a discoverable credential and local user verification.
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ResidentKeyRequirement = "required";
    options.UserVerificationRequirement = "required";
});

var app = builder.Build();

// Liveness reports process availability only; it does not access the database or reveal diagnostics.
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program { }
