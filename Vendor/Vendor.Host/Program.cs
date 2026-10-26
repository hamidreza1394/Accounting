using Faranou.Vendor.Persistence;

var builder = WebApplication.CreateBuilder(args);

// The dedicated prefix prevents accidentally sharing customer-app settings/secrets.
builder.Configuration.AddEnvironmentVariables(prefix: "FARANO_VENDOR_");

builder.Services.AddHealthChecks();
builder.Services.AddVendorPersistence(builder.Configuration);

var app = builder.Build();

// Liveness reports process availability only; it does not access the database or reveal diagnostics.
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program { }
