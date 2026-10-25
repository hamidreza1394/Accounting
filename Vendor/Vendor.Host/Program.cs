using Faranou.Vendor.Persistence;
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

var app = builder.Build();

// Liveness reports process availability only; it does not access the database or reveal diagnostics.
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program { }
