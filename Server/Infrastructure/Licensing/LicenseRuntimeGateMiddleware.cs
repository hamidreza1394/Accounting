using Faranou.Licensing.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Licensing;

/// <summary>
/// License-first runtime gate (D-060 §1-6, D-026, D-059 §7, D-098; Issue #396). Enforced
/// server-side for every normal request; hiding menus in the React client is not access control.
/// <list type="bullet">
/// <item>Disabled (default): pass-through, so rollout stays phased per deployment configuration.</item>
/// <item>Unlicensed/Invalid: only the activation and recovery paths stay reachable.</item>
/// <item>Restricted + Dedicated: reads stay reachable, normal writes are blocked (block-new/no data loss).</item>
/// <item>Restricted + Cloud: normal operations are blocked; renewal/recovery paths stay reachable (D-098).</item>
/// <item>Active: normal operations.</item>
/// </list>
/// </summary>
public sealed class LicenseRuntimeGateMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Paths that stay reachable in every state: activation, provisioning, installation identity
    /// and status. Authorization on these endpoints remains independently enforced.
    /// </summary>
    private static readonly string[] AlwaysAllowedPathPrefixes =
    [
        "/api/v1/licensing",
        "/api/v1/provisioning",
        "/DatabaseSetup",
        "/error"
    ];

    /// <summary>Session hygiene that must survive every state (logout, sign-out callback).</summary>
    private static readonly string[] SessionExitPathPrefixes = ["/account/logout"];

    public async Task InvokeAsync(HttpContext context)
    {
        // Services resolve lazily from the request scope: with the gate disabled no licensing
        // dependency is resolved at all, so first-run setup and any deployment that has not
        // registered the licensing stack keep working unchanged.
        var requestServices = context.RequestServices;
        var gate = requestServices.GetRequiredService<IOptions<LicenseRuntimeGateOptions>>().Value;
        if (!gate.Enabled)
        {
            await next(context);
            return;
        }

        var stateService = requestServices.GetRequiredService<ILicenseRuntimeStateService>();
        var logger = requestServices.GetRequiredService<ILogger<LicenseRuntimeGateMiddleware>>();

        var path = context.Request.Path;
        if (IsAlwaysAllowed(path) || IsSessionExit(path))
        {
            await next(context);
            return;
        }

        var snapshot = await stateService.ResolveAsync(context.RequestAborted);
        switch (snapshot.State)
        {
            case LicenseRuntimeState.Active:
                await next(context);
                return;

            case LicenseRuntimeState.Restricted when
                string.Equals(snapshot.DeploymentMode, LicenseDeploymentModes.Dedicated, StringComparison.OrdinalIgnoreCase):
                // D-059 §7: expiry never deletes or locks data; reads stay available, ordinary
                // writes stop. No implicit grace period applies.
                if (IsReadRequest(context.Request.Method))
                {
                    await next(context);
                    return;
                }

                await DenyAsync(context, StatusCodes.Status403Forbidden, "license_restricted", snapshot);
                logger.LogInformation(
                    "License gate denied a write while restricted. DeploymentMode={DeploymentMode}.",
                    snapshot.DeploymentMode);
                return;

            default:
                // Unlicensed, Invalid, and Restricted under Cloud (D-098: normal operations stop,
                // renewal/recovery stay reachable) all fail closed for normal requests.
                var (status, errorCode) = snapshot.State switch
                {
                    LicenseRuntimeState.Restricted => (StatusCodes.Status403Forbidden, "license_cloud_expired"),
                    LicenseRuntimeState.Unlicensed => (StatusCodes.Status503ServiceUnavailable, "license_required"),
                    _ => (StatusCodes.Status503ServiceUnavailable, "license_invalid")
                };
                await DenyAsync(context, status, errorCode, snapshot);
                logger.LogInformation(
                    "License gate denied a normal request. State={State} DeploymentMode={DeploymentMode}.",
                    snapshot.State,
                    snapshot.DeploymentMode);
                return;
        }
    }

    private static async Task DenyAsync(HttpContext context, int statusCode, string errorCode, LicenseRuntimeStateSnapshot snapshot)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = errorCode,
            state = snapshot.State.ToString().ToLowerInvariant(),
            deploymentMode = snapshot.DeploymentMode.ToLowerInvariant()
        });
        await context.Response.WriteAsync(body, context.RequestAborted);
    }

    private static bool IsAlwaysAllowed(PathString path) =>
        MatchesAnyPrefix(path, AlwaysAllowedPathPrefixes);

    private static bool IsSessionExit(PathString path) =>
        MatchesAnyPrefix(path, SessionExitPathPrefixes);

    private static bool MatchesAnyPrefix(PathString path, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReadRequest(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
}

/// <summary>DI registration for the license-first runtime gate.</summary>
public static class LicenseRuntimeGateServiceCollectionExtensions
{
    public static IServiceCollection AddLicenseRuntimeGate(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LicenseRuntimeGateOptions>()
            .Bind(configuration.GetSection(LicenseRuntimeGateOptions.SectionName))
            .PostConfigure(gate =>
            {
                var configuredMode = configuration[LicenseRuntimeGateOptions.DeploymentModeSectionName];
                if (!string.IsNullOrWhiteSpace(configuredMode))
                {
                    gate.DeploymentMode = configuredMode;
                }
            })
            .Validate(
                gate => string.Equals(gate.DeploymentMode, LicenseDeploymentModes.Dedicated, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(gate.DeploymentMode, LicenseDeploymentModes.Cloud, StringComparison.OrdinalIgnoreCase),
                "Licensing:DeploymentMode must be either Dedicated or Cloud.")
            .ValidateOnStart();

        services.AddScoped<ILicenseRuntimeStateService, LicenseRuntimeStateService>();
        return services;
    }
}
