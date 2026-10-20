using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Infrastructure.Hosting;

/// <summary>
/// The reverse proxies whose forwarding headers are trusted (section <c>ReverseProxy</c>).
/// </summary>
/// <remarks>
/// <para>
/// Nothing is trusted by default: without an entry, <c>X-Forwarded-For</c> and
/// <c>X-Forwarded-Proto</c> are ignored and the client address and scheme are those of the TCP
/// connection. List only the addresses (or networks) of the proxies in front of this
/// application; a header that arrives from any other address is ignored.
/// </para>
/// <para>
/// Once a proxy is trusted, the client address is taken from <c>X-Forwarded-For</c> (used by
/// rate limiting, the audit trail and the login log) and the scheme from
/// <c>X-Forwarded-Proto</c> (used by HTTPS redirection, HSTS and the Secure cookie). The host
/// header is never taken from the proxy; <c>AllowedHosts</c> restricts it.
/// </para>
/// </remarks>
public sealed class ReverseProxySettings
{
	public const string SectionName = "ReverseProxy";

	/// <summary>IP addresses of trusted proxies, e.g. <c>10.0.0.5</c>.</summary>
	public string[] KnownProxies { get; set; } = [];

	/// <summary>Networks of trusted proxies in CIDR notation, e.g. <c>10.0.0.0/24</c>.</summary>
	public string[] KnownNetworks { get; set; } = [];

	/// <summary>How many proxy hops are processed (one proxy: 1).</summary>
	public int ForwardLimit { get; set; } = 1;

	public bool IsConfigured => KnownProxies.Length > 0 || KnownNetworks.Length > 0;
}

public static class ReverseProxyConfiguration
{
	public static IServiceCollection AddFaranouForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<ReverseProxySettings>(configuration.GetSection(ReverseProxySettings.SectionName));

		services.AddOptions<ForwardedHeadersOptions>()
			.Configure<IOptions<ReverseProxySettings>>((options, settings) => Apply(options, settings.Value));

		return services;
	}

	/// <summary>
	/// Applies the settings. Invalid entries stop the application at start-up instead of being
	/// ignored, because an ignored entry would silently change who is trusted.
	/// </summary>
	public static void Apply(ForwardedHeadersOptions options, ReverseProxySettings settings)
	{
		// The framework trusts every sender when both lists are empty, and loopback by default.
		// Both lists are therefore cleared and the middleware is switched off unless a proxy
		// is configured explicitly.
		options.KnownProxies.Clear();
		options.KnownIPNetworks.Clear();
		options.ForwardedHeaders = ForwardedHeaders.None;

		if (!settings.IsConfigured)
		{
			return;
		}

		foreach (var proxy in settings.KnownProxies)
		{
			if (!IPAddress.TryParse(proxy?.Trim(), out var address))
			{
				throw new InvalidOperationException($"ReverseProxy:KnownProxies contains an invalid IP address: '{proxy}'.");
			}

			options.KnownProxies.Add(address);
		}

		foreach (var network in settings.KnownNetworks)
		{
			if (!System.Net.IPNetwork.TryParse(network?.Trim(), out var parsed))
			{
				throw new InvalidOperationException($"ReverseProxy:KnownNetworks contains an invalid CIDR network: '{network}'.");
			}

			options.KnownIPNetworks.Add(parsed);
		}

		if (settings.ForwardLimit < 1)
		{
			throw new InvalidOperationException("ReverseProxy:ForwardLimit must be at least 1.");
		}

		options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
		options.ForwardLimit = settings.ForwardLimit;
		// The original values are kept for diagnostics (X-Original-For / X-Original-Proto).
		options.RequireHeaderSymmetry = false;
	}
}

/// <summary>
/// Configuration problems that must be fixed before a production deployment; they are logged
/// as warnings at start-up (outside Development).
/// </summary>
public static class DeploymentConfigurationChecks
{
	public static IReadOnlyList<string> GetWarnings(IConfiguration configuration, IHostEnvironment environment)
	{
		var warnings = new List<string>();

		// In every environment: the infrastructure credential still uses the legacy format.
		if (Security.PasswordHashService.IsLegacySha256Hash(configuration["ApplicationSettings:MasterPassword"]))
		{
			warnings.Add("ApplicationSettings:MasterPassword uses the legacy SHA-256 format. Create a new value with 'dotnet Server.dll --create-master-credential' and then set ApplicationSettings:AllowLegacyMasterPasswordHash=false.");
		}

		if (environment.IsDevelopment())
		{
			return warnings;
		}

		var allowedHosts = configuration["AllowedHosts"];

		if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(host => host.Trim() == "*"))
		{
			warnings.Add("AllowedHosts is '*': every Host header is accepted. Set AllowedHosts to the real host name(s) of the deployment (e.g. AllowedHosts=accounting.example.com).");
		}

		var settings = configuration.GetSection(ReverseProxySettings.SectionName).Get<ReverseProxySettings>() ?? new ReverseProxySettings();

		if (!settings.IsConfigured)
		{
			warnings.Add("No trusted reverse proxy is configured (ReverseProxy:KnownProxies/KnownNetworks). If the application runs behind a proxy or load balancer, configure it; otherwise the client address and scheme are those of the proxy.");
		}

		return warnings;
	}
}
