var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// This endpoint reports process availability only. It must not expose configuration,
// database details, dependency status, secrets, or other operational diagnostics.
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program { }
