using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// The in-memory test server has no TCP connection. This filter, which runs before the
/// application's middleware, sets the connection's remote address from a test-only header so
/// that "request arrives from proxy X" can be simulated. It is registered by the tests only.
/// </summary>
internal sealed class TestRemoteAddressStartupFilter : IStartupFilter
{
	public const string HeaderName = "X-Test-Remote-Address";

	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		return app =>
		{
			app.Use(async (context, nextMiddleware) =>
			{
				if (context.Request.Headers.TryGetValue(HeaderName, out var value) &&
					IPAddress.TryParse(value.ToString(), out var address))
				{
					context.Connection.RemoteIpAddress = address;
					context.Request.Headers.Remove(HeaderName);
				}

				await nextMiddleware(context);
			});

			next(app);
		};
	}
}

public abstract class ProxyFactoryBase : FaranouApplicationFactory
{
	public const string TrustedProxy = "10.0.0.5";
	public const string UntrustedSender = "192.0.2.9";

	protected abstract IDictionary<string, string?> ProxySettings { get; }

	protected override IDictionary<string, string?> Settings
	{
		get
		{
			var settings = base.Settings;
			// A known HTTPS port turns on the HTTPS redirection of plain HTTP requests.
			settings["https_port"] = "443";
			settings["RateLimiting:Api:PermitLimit"] = "2";
			settings["RateLimiting:Api:WindowSeconds"] = "3600";

			foreach (var (key, value) in ProxySettings)
			{
				settings[key] = value;
			}

			return settings;
		}
	}

	protected override void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient<IStartupFilter, TestRemoteAddressStartupFilter>();
	}

	/// <summary>A plain-HTTP client, as a TLS-terminating proxy talks to the application.</summary>
	public HttpClient CreateHttpClient()
	{
		return CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("http://faranou.test"),
			AllowAutoRedirect = false,
			HandleCookies = false,
		});
	}

	public static HttpRequestMessage Request(string path, string from, string? forwardedFor = null, string? forwardedProto = null)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, path);
		request.Headers.Add(TestRemoteAddressStartupFilter.HeaderName, from);

		if (forwardedFor is not null)
		{
			request.Headers.Add("X-Forwarded-For", forwardedFor);
		}

		if (forwardedProto is not null)
		{
			request.Headers.Add("X-Forwarded-Proto", forwardedProto);
		}

		return request;
	}
}

public sealed class TrustedProxyFactory : ProxyFactoryBase
{
	protected override IDictionary<string, string?> ProxySettings => new Dictionary<string, string?>
	{
		["ReverseProxy:KnownProxies:0"] = TrustedProxy,
	};
}

public sealed class NoProxyFactory : ProxyFactoryBase
{
	protected override IDictionary<string, string?> ProxySettings => new Dictionary<string, string?>();
}

/// <summary>Forwarding headers are honoured from the configured proxy only.</summary>
public sealed class TrustedProxyTests : IClassFixture<TrustedProxyFactory>
{
	public TrustedProxyTests(TrustedProxyFactory factory)
	{
		Factory = factory;
	}

	private TrustedProxyFactory Factory { get; }

	[Fact]
	public async Task HttpsTerminatedAtTheTrustedProxy_IsNotRedirectedAgain_AndGetsHsts()
	{
		using var client = Factory.CreateHttpClient();

		var response = await client.SendAsync(
			ProxyFactoryBase.Request("/Account/Login", ProxyFactoryBase.TrustedProxy, forwardedFor: "203.0.113.10", forwardedProto: "https"));

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.True(response.Headers.Contains("Strict-Transport-Security"));
	}

	[Fact]
	public async Task ThePlainHttpRequestOfTheProxy_IsStillRedirectedToHttps()
	{
		using var client = Factory.CreateHttpClient();

		var response = await client.SendAsync(
			ProxyFactoryBase.Request("/Account/Login", ProxyFactoryBase.TrustedProxy, forwardedFor: "203.0.113.10", forwardedProto: "http"));

		Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
		Assert.Equal("https", response.Headers.Location!.Scheme);
	}

	[Fact]
	public async Task HeadersFromAnUntrustedSender_AreIgnored()
	{
		using var client = Factory.CreateHttpClient();

		var response = await client.SendAsync(
			ProxyFactoryBase.Request("/Account/Login", ProxyFactoryBase.UntrustedSender, forwardedFor: "203.0.113.10", forwardedProto: "https"));

		// The spoofed "https" is not believed: the request is treated as plain HTTP.
		Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
	}

	[Fact]
	public async Task TheClientAddressFromTheTrustedProxy_IsUsedForRateLimiting()
	{
		using var client = Factory.CreateHttpClient();

		async Task<HttpStatusCode> ApiAs(string clientAddress)
		{
			var response = await client.SendAsync(ProxyFactoryBase.Request(
				"/api/v1/session", ProxyFactoryBase.TrustedProxy, forwardedFor: clientAddress, forwardedProto: "https"));
			return response.StatusCode;
		}

		Assert.Equal(HttpStatusCode.Unauthorized, await ApiAs("203.0.113.21"));
		Assert.Equal(HttpStatusCode.Unauthorized, await ApiAs("203.0.113.21"));
		Assert.Equal((HttpStatusCode)429, await ApiAs("203.0.113.21"));

		// Another client behind the same proxy has its own limit.
		Assert.Equal(HttpStatusCode.Unauthorized, await ApiAs("203.0.113.22"));
	}
}

/// <summary>Without configuration, no forwarding header is trusted, not even from loopback.</summary>
public sealed class NoTrustedProxyTests : IClassFixture<NoProxyFactory>
{
	public NoTrustedProxyTests(NoProxyFactory factory)
	{
		Factory = factory;
	}

	private NoProxyFactory Factory { get; }

	[Theory]
	[InlineData(ProxyFactoryBase.TrustedProxy)]
	[InlineData("127.0.0.1")]
	public async Task ForwardingHeaders_AreIgnoredByDefault(string sender)
	{
		using var client = Factory.CreateHttpClient();

		var response = await client.SendAsync(
			ProxyFactoryBase.Request("/Account/Login", sender, forwardedFor: "203.0.113.10", forwardedProto: "https"));

		Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
	}

	[Fact]
	public async Task AForgedClientAddress_DoesNotOpenANewRateLimitPartition()
	{
		using var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("https://faranou.test"),
			AllowAutoRedirect = false,
		});

		async Task<HttpStatusCode> ApiForging(string forgedAddress)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/session");
			request.Headers.Add(TestRemoteAddressStartupFilter.HeaderName, "198.51.100.7");
			request.Headers.Add("X-Forwarded-For", forgedAddress);
			return (await client.SendAsync(request)).StatusCode;
		}

		Assert.Equal(HttpStatusCode.Unauthorized, await ApiForging("203.0.113.41"));
		Assert.Equal(HttpStatusCode.Unauthorized, await ApiForging("203.0.113.42"));
		// A new forged address does not help: the limit is per real sender.
		Assert.Equal((HttpStatusCode)429, await ApiForging("203.0.113.43"));
	}
}

/// <summary>AllowedHosts restricts the Host header when it is configured.</summary>
public sealed class AllowedHostsTests : IClassFixture<AllowedHostsTests.Factory>
{
	public sealed class Factory : FaranouApplicationFactory
	{
		protected override IDictionary<string, string?> Settings
		{
			get
			{
				var settings = base.Settings;
				settings["AllowedHosts"] = "faranou.test";
				return settings;
			}
		}
	}

	public AllowedHostsTests(Factory factory)
	{
		ApplicationFactory = factory;
	}

	private Factory ApplicationFactory { get; }

	[Theory]
	[InlineData("faranou.test", HttpStatusCode.OK)]
	[InlineData("evil.example", HttpStatusCode.BadRequest)]
	public async Task OnlyTheConfiguredHostIsServed(string host, HttpStatusCode expected)
	{
		using var client = ApplicationFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri($"https://{host}"),
			AllowAutoRedirect = false,
		});

		Assert.Equal(expected, (await client.GetAsync("/Account/Login")).StatusCode);
	}
}

/// <summary>Unit tests of the proxy settings and the start-up warnings.</summary>
public sealed class ReverseProxyConfigurationTests
{
	[Fact]
	public void NothingIsTrusted_WithoutConfiguration_NotEvenLoopback()
	{
		var options = new ForwardedHeadersOptions();

		Infrastructure.Hosting.ReverseProxyConfiguration.Apply(options, new Infrastructure.Hosting.ReverseProxySettings());

		Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
		Assert.Empty(options.KnownProxies);
		Assert.Empty(options.KnownIPNetworks);
	}

	[Fact]
	public void ConfiguredProxiesAndNetworks_AreTheOnlyTrustedSenders()
	{
		var options = new ForwardedHeadersOptions();

		Infrastructure.Hosting.ReverseProxyConfiguration.Apply(options, new Infrastructure.Hosting.ReverseProxySettings
		{
			KnownProxies = ["10.0.0.5"],
			KnownNetworks = ["10.1.0.0/16"],
		});

		Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
		Assert.Equal(IPAddress.Parse("10.0.0.5"), Assert.Single(options.KnownProxies));
		Assert.Single(options.KnownIPNetworks);
		Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
	}

	[Theory]
	[InlineData("not-an-ip", null)]
	[InlineData(null, "10.0.0.0/99")]
	public void InvalidEntries_StopTheStart(string? proxy, string? network)
	{
		var settings = new Infrastructure.Hosting.ReverseProxySettings
		{
			KnownProxies = proxy is null ? [] : [proxy],
			KnownNetworks = network is null ? [] : [network],
		};

		Assert.Throws<InvalidOperationException>(() =>
			Infrastructure.Hosting.ReverseProxyConfiguration.Apply(new ForwardedHeadersOptions(), settings));
	}

	private sealed class TestEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = "Production";
		public string ApplicationName { get; set; } = "Server";
		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
		public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
			new Microsoft.Extensions.FileProviders.NullFileProvider();
	}

	[Fact]
	public void Production_WarnsAboutAWildcardHost_AndAMissingProxyConfiguration()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "*" })
			.Build();

		var warnings = Infrastructure.Hosting.DeploymentConfigurationChecks.GetWarnings(configuration, new TestEnvironment());

		Assert.Contains(warnings, warning => warning.StartsWith("AllowedHosts", StringComparison.Ordinal));
		Assert.Contains(warnings, warning => warning.Contains("reverse proxy", StringComparison.Ordinal));
	}

	[Fact]
	public void Production_HasNoWarning_WhenHostsAndProxyAreConfigured()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AllowedHosts"] = "accounting.example.com",
				["ReverseProxy:KnownProxies:0"] = "10.0.0.5",
			})
			.Build();

		Assert.Empty(Infrastructure.Hosting.DeploymentConfigurationChecks.GetWarnings(configuration, new TestEnvironment()));
	}
}
