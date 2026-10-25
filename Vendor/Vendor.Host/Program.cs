using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The dedicated prefix prevents accidentally sharing customer-app settings/secrets.
builder.Configuration.AddEnvironmentVariables(prefix: "FARANO_VENDOR_");

// Vendor Data Protection must have an independent, persistent key directory in Production.
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
