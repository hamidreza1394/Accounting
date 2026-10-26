using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Environment variables prefixed with FARANO_VENDOR_ map to the isolated Vendor
// configuration namespace. Example: FARANO_VENDOR_ConnectionStrings__VendorDb.
// This never falls back to the customer application's connection string.
builder.Configuration.AddEnvironmentVariables(prefix: "FARANO_VENDOR_");

var vendorConnectionString = builder.Configuration.GetConnectionString(DependencyInjection.ConnectionStringName);
if (!string.IsNullOrWhiteSpace(vendorConnectionString))
{
    builder.Services.AddVendorPersistence(builder.Configuration);
}

builder.Services.AddHealthChecks();

var app = builder.Build();

// This endpoint reports process availability only. It must not expose configuration,
// database details, dependency status, secrets, or other operational diagnostics.
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program { }
