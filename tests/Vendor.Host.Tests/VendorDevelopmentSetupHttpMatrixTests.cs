using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Real HTTP matrix for the Development setup surface in setup-only mode. Every mutating
/// endpoint sits behind loopback + same-origin + antiforgery + the process console challenge,
/// and the provisioning mutation additionally consumes a single-use bootstrap token paired
/// with a strict HttpOnly cookie. This suite drives the real endpoints through
/// WebApplicationFactory and asserts each guard over actual requests: missing challenge,
/// wrong origin, disallowed SQL targets, missing/mismatched bootstrap pairs, single-use
/// semantics (a refusal must never burn the pair, a use always does) and the stricter
/// provision rate limit. The surface is Windows-only (Credential Manager + console host);
/// elsewhere the tests skip, and the Windows CI job runs them by name.
/// </summary>
[Collection("VendorDevelopmentDatabaseSequential")]
public sealed class VendorDevelopmentSetupHttpMatrixTests
{
    /// <summary>Loopback destination with a closed port: passes the allow-list, then fails the
    /// connection stage immediately, so the pipeline wiring is exercised without a server.</summary>
    private const string ProvisionBody = """
        {"server":"127.0.0.1,59999","database":"FaranouVendorHttpMatrix","authentication":"sql","username":"sa","password":"Xy9!aaaa","trustServerCertificate":true,"runtimeUsername":"FaranouHttpMatrixRuntime","runtimePassword":null}
        """;

    private const string DisallowedServerBody = """
        {"server":"db.attacker.example","database":"FaranouVendorHttpMatrix","authentication":"sql","username":"sa","password":"Xy9!aaaa","trustServerCertificate":false}
        """;

    private sealed record AntiforgeryPayload(string RequestToken);

    private sealed record TokenPayload(string Token);

    /// <summary>Presents the loopback peer for every request of the factory (see
    /// CreateSetupHost); registered as an IStartupFilter so it wraps the app pipeline from
    /// outside and runs before the setup guard middleware.</summary>
    private sealed class LoopbackPeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(nextContext => async requestContext =>
                {
                    requestContext.Connection.RemoteIpAddress = IPAddress.Loopback;
                    await nextContext(requestContext);
                });
                next(app);
            };
    }

    private static (WebApplicationFactory<Program> Factory, HttpClient Client, string Challenge) CreateSetupHost()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("The Development setup surface is Windows-only (Credential Manager + console host).");
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseSetting("environment", "Development");
            webHost.UseSetting("DevelopmentSetup:Enabled", "true");
            // TestServer requests carry no loopback RemoteIpAddress, so the setup guard
            // middleware would 404 every request (that behaviour is the dedicated exposure
            // test). The matrix drives the real local-operator flow, so it presents the
            // loopback peer through an IStartupFilter — the outermost pipeline layer, which
            // runs BEFORE the app's own guard middleware; the loopback HOST name still comes
            // from the request itself.
            webHost.ConfigureTestServices(services =>
                services.AddTransient<IStartupFilter>(_ => new LoopbackPeerStartupFilter()));
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false
        });
        var challenge = factory.Services.GetRequiredService<VendorDevelopmentSetupChallenge>().Issue();
        return (factory, client, challenge);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/development/database-setup/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryPayload>();
        Assert.False(string.IsNullOrWhiteSpace(payload!.RequestToken));
        return payload.RequestToken;
    }

    private static async Task<string> GetBootstrapTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/development/database-setup/provision-token");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>();
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
        return payload.Token;
    }

    private static async Task<HttpResponseMessage> PostSetupAsync(
        HttpClient client,
        string path,
        string json,
        string? antiforgeryToken = null,
        string? challenge = null,
        string? bootstrapToken = null,
        string? origin = "https://localhost") =>
        await SendGuardedAsync(client, HttpMethod.Post, path, json, antiforgeryToken, challenge, bootstrapToken, origin);

    private static async Task<HttpResponseMessage> SendGuardedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string json,
        string? antiforgeryToken = null,
        string? challenge = null,
        string? bootstrapToken = null,
        string? origin = "https://localhost",
        string? cookieOverride = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (cookieOverride is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieOverride);
        }

        if (!string.IsNullOrWhiteSpace(origin))
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        if (!string.IsNullOrWhiteSpace(antiforgeryToken))
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiforgeryToken);
        }

        if (!string.IsNullOrWhiteSpace(challenge))
        {
            request.Headers.TryAddWithoutValidation(VendorDevelopmentSetupChallenge.HeaderName, challenge);
        }

        if (!string.IsNullOrWhiteSpace(bootstrapToken))
        {
            request.Headers.TryAddWithoutValidation(VendorDevelopmentSetupBootstrapToken.HeaderName, bootstrapToken);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SetupResponsesCarryNoStoreHeaders()
    {
        var (factory, client, _) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;

        var page = await client.GetAsync("/development/database-setup");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(page.Headers.CacheControl!.NoStore);

        var status = await client.GetAsync("/api/development/database-setup/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.True(status.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task BootstrapTokenIsIssuedWithAStrictCookieAndNoStore()
    {
        var (factory, client, _) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;

        var response = await client.GetAsync("/api/development/database-setup/provision-token");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("FaranouSetupBootstrap=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProvisionWithoutTheChallengeIsRefusedAndKeepsTheBootstrapPairUsable()
    {
        var (factory, client, challenge) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
        var bootstrapToken = await GetBootstrapTokenAsync(client);

        var refused = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge: null, bootstrapToken);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // The guard refusal must not burn the pair: the same pair drives a real run, which
        // fails at the connection stage and reports a failed outcome over 422.
        var run = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge, bootstrapToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, run.StatusCode);
        var payload = await run.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Null(payload.GetProperty("runtimeUsername").GetString());
        Assert.Null(payload.GetProperty("generatedPassword").GetString());
        Assert.False(payload.GetProperty("savedToCredentialManager").GetBoolean());

        // Single use: the consumed pair is worthless for a replay.
        var replay = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge, bootstrapToken);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }

    [Fact]
    public async Task ProvisionWithAMismatchedCookieBurnsThePair()
    {
        var (factory, client, challenge) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;
        var bootstrapToken = await GetBootstrapTokenAsync(client);

        // A token presented with a WRONG cookie value fails closed and burns the pair; a
        // MISSING cookie fails closed before the lookup without burning (the unit tests
        // cover both branches, this one proves the burn over real HTTP).
        using var cookieless = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false
        });
        var cookielessAntiforgery = await GetAntiforgeryTokenAsync(cookieless);
        var wrongCookie = await SendGuardedAsync(
            cookieless, HttpMethod.Post, "/api/development/database-setup/provision", ProvisionBody,
            cookielessAntiforgery, challenge, bootstrapToken,
            cookieOverride: $"{VendorDevelopmentSetupBootstrapToken.CookieName}=deliberately-wrong");
        Assert.Equal(HttpStatusCode.Forbidden, wrongCookie.StatusCode);

        // The paired client can no longer use the burned pair either.
        var pairedAntiforgery = await GetAntiforgeryTokenAsync(client);
        var burned = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, pairedAntiforgery, challenge, bootstrapToken);
        Assert.Equal(HttpStatusCode.Forbidden, burned.StatusCode);
    }

    [Fact]
    public async Task TestAndSaveRejectDisallowedSqlTargets()
    {
        var (factory, client, challenge) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        var test = await PostSetupAsync(
            client, "/api/development/database-setup/test", DisallowedServerBody, antiforgeryToken, challenge);
        Assert.Equal(HttpStatusCode.BadRequest, test.StatusCode);

        var save = await PostSetupAsync(
            client, "/api/development/database-setup/save", DisallowedServerBody, antiforgeryToken, challenge);
        Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
    }

    [Fact]
    public async Task MutatingEndpointsWithoutChallengeOrWithWrongOriginFailClosed()
    {
        var (factory, client, challenge) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        // The guard is one shared call site, so each path only needs its no-challenge refusal;
        // the request budget stays under the 10-per-minute setup rate limit (the provision
        // window has its own dedicated test).
        foreach (var (method, path) in new[]
                 {
                     (HttpMethod.Post, "/api/development/database-setup/test"),
                     (HttpMethod.Post, "/api/development/database-setup/save"),
                     (HttpMethod.Delete, "/api/development/database-setup/saved"),
                     (HttpMethod.Post, "/api/development/database-setup/inspect"),
                 })
        {
            var noChallenge = await SendGuardedAsync(client, method, path, ProvisionBody, antiforgeryToken, challenge: null);
            Assert.Equal(HttpStatusCode.Forbidden, noChallenge.StatusCode);
        }

        var wrongChallenge = await SendGuardedAsync(
            client, HttpMethod.Post, "/api/development/database-setup/test", ProvisionBody, antiforgeryToken, challenge: "wrong-code");
        Assert.Equal(HttpStatusCode.Forbidden, wrongChallenge.StatusCode);

        var wrongOrigin = await SendGuardedAsync(
            client, HttpMethod.Post, "/api/development/database-setup/test", ProvisionBody, antiforgeryToken, challenge,
            origin: "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, wrongOrigin.StatusCode);

        var wrongOriginSaved = await SendGuardedAsync(
            client, HttpMethod.Delete, "/api/development/database-setup/saved", ProvisionBody, antiforgeryToken, challenge,
            origin: "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, wrongOriginSaved.StatusCode);
    }

    [Fact]
    public async Task InspectRunsThroughTheSameGuardsAndReportsTheFailedConnection()
    {
        var (factory, client, challenge) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        var refused = await PostSetupAsync(
            client, "/api/development/database-setup/inspect", ProvisionBody, antiforgeryToken, challenge: null);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var run = await PostSetupAsync(
            client, "/api/development/database-setup/inspect", ProvisionBody, antiforgeryToken, challenge);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        var payload = await run.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(payload.GetProperty("success").GetBoolean());
        // Stage "connection" only validates the request shape; the REAL connect attempt is
        // stage "test", which must fail against the closed loopback port.
        var stages = payload.GetProperty("stages");
        var testStage = stages.EnumerateArray()
            .Single(stage => stage.GetProperty("key").GetString() == "test");
        Assert.Equal("failed", testStage.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ProvisionIsRateLimitedToThreeRunsPerWindow()
    {
        var (factory, client, challenge) = CreateSetupHost();
        using var _factory = factory;
        using var _client = client;
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var bootstrapToken = await GetBootstrapTokenAsync(client);
            var run = await PostSetupAsync(
                client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge, bootstrapToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, run.StatusCode);
        }

        var fourthToken = await GetBootstrapTokenAsync(client);
        var limited = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge, fourthToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
