using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
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
        string? origin = "https://localhost")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
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
        Assert.Contains("HttpOnly", setCookie, StringComparison.Ordinal);
        Assert.Contains("Secure", setCookie, StringComparison.Ordinal);
        Assert.Contains("SameSite=Strict", setCookie, StringComparison.Ordinal);
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

        // The guard refusal must not burn the pair: the same pair drives a real run.
        var run = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge, bootstrapToken);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        var payload = await run.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(payload.GetProperty("success").GetBoolean());

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

        // A token without its paired cookie fails closed and burns the pair.
        using var cookieless = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false
        });
        var cookielessAntiforgery = await GetAntiforgeryTokenAsync(cookieless);
        var refused = await PostSetupAsync(
            cookieless, "/api/development/database-setup/provision", ProvisionBody, cookielessAntiforgery, challenge, bootstrapToken);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

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

        foreach (var path in new[]
                 {
                     "/api/development/database-setup/test",
                     "/api/development/database-setup/save",
                     "/api/development/database-setup/saved",
                     "/api/development/database-setup/inspect",
                 })
        {
            var noChallenge = await PostSetupAsync(client, path, ProvisionBody, antiforgeryToken, challenge: null);
            Assert.Equal(HttpStatusCode.Forbidden, noChallenge.StatusCode);

            var wrongChallenge = await PostSetupAsync(client, path, ProvisionBody, antiforgeryToken, challenge: "wrong-code");
            Assert.Equal(HttpStatusCode.Forbidden, wrongChallenge.StatusCode);

            var wrongOrigin = await PostSetupAsync(
                client, path, ProvisionBody, antiforgeryToken, challenge, origin: "https://attacker.example");
            Assert.Equal(HttpStatusCode.Forbidden, wrongOrigin.StatusCode);
        }
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
        Assert.Equal("failed", payload.GetProperty("stages")[0].GetProperty("state").GetString());
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
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        }

        var fourthToken = await GetBootstrapTokenAsync(client);
        var limited = await PostSetupAsync(
            client, "/api/development/database-setup/provision", ProvisionBody, antiforgeryToken, challenge, fourthToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
