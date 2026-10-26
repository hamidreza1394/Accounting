using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuthenticationSecurityTests
{
    [Fact]
    public async Task Password_only_is_denied_and_totp_challenge_does_not_issue_application_cookie()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
            {
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for authentication integration tests.");
            }

            Assert.Skip("A disposable SQL Server connection is required for authentication integration tests.");
            return;
        }

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
            var passwordOnly = new VendorAdminUser
            {
                UserName = $"password-only-{Guid.NewGuid():N}",
                Email = $"password-only-{Guid.NewGuid():N}@example.invalid",
                EmailConfirmed = true,
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var createPasswordOnly = await users.CreateAsync(passwordOnly, "Secure-Vendor#2026");
            Assert.True(createPasswordOnly.Succeeded, string.Join("; ", createPasswordOnly.Errors.Select(error => error.Code)));

            var disabled = new VendorAdminUser
            {
                UserName = $"disabled-{Guid.NewGuid():N}",
                Email = $"disabled-{Guid.NewGuid():N}@example.invalid",
                EmailConfirmed = true,
                IsEnabled = false,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var createDisabled = await users.CreateAsync(disabled, "Secure-Vendor#2026");
            Assert.True(createDisabled.Succeeded, string.Join("; ", createDisabled.Errors.Select(error => error.Code)));

            var token = await GetAntiforgeryTokenAsync(client);
            using var passwordOnlyResponse = await PostWithTokenAsync(
                client,
                "/api/auth/password",
                new { userName = passwordOnly.UserName, password = "Secure-Vendor#2026" },
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, passwordOnlyResponse.StatusCode);

            using var disabledResponse = await PostWithTokenAsync(
                client,
                "/api/auth/password",
                new { userName = disabled.UserName, password = "Secure-Vendor#2026" },
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, disabledResponse.StatusCode);

            var user = new VendorAdminUser
            {
                UserName = $"totp-{Guid.NewGuid():N}",
                Email = $"totp-{Guid.NewGuid():N}@example.invalid",
                EmailConfirmed = true,
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var createUser = await users.CreateAsync(user, "Secure-Vendor#2026");
            Assert.True(createUser.Succeeded, string.Join("; ", createUser.Errors.Select(error => error.Code)));
            var resetKey = await users.ResetAuthenticatorKeyAsync(user);
            Assert.True(resetKey.Succeeded);
            var enableTotp = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(enableTotp.Succeeded);

            using var passwordResponse = await PostWithTokenAsync(
                client,
                "/api/auth/password",
                new { userName = user.UserName, password = "Secure-Vendor#2026" },
                token);
            Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
            using (var responseDocument = JsonDocument.Parse(await passwordResponse.Content.ReadAsStringAsync()))
            {
                Assert.True(responseDocument.RootElement.GetProperty("requiresSecondFactor").GetBoolean());
            }

            var setCookies = passwordResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues)
                ? string.Join(";", cookieValues)
                : string.Empty;
            Assert.Contains("__Host-Faranou.Vendor.TwoFactor=", setCookies, StringComparison.Ordinal);
            Assert.DoesNotContain("__Host-Faranou.Vendor.Auth=", setCookies, StringComparison.Ordinal);

            using var beforeTotp = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, beforeTotp.StatusCode);

            var code = await users.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider);
            using var totpResponse = await PostWithTokenAsync(
                client,
                "/api/auth/totp/verify",
                new { code },
                token);
            Assert.Equal(HttpStatusCode.OK, totpResponse.StatusCode);

            using var meResponse = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
            using var meDocument = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
            Assert.True(meDocument.RootElement.GetProperty("totpEnabled").GetBoolean());

            using var logoutResponse = await PostWithTokenAsync(client, "/api/auth/logout", new { }, token);
            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
            using var afterLogout = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Anonymous_auth_post_is_antiforgery_protected_and_rate_limited()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/password",
                new { userName = "nobody", password = "not-a-real-password" });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(5), status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal((HttpStatusCode)429, statuses[5]);
    }

    [Fact]
    public async Task Identity_cookie_and_passkey_options_enforce_secure_mfa_defaults()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();

        var appCookie = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Assert.Equal("__Host-Faranou.Vendor.Auth", appCookie.Cookie.Name);
        Assert.True(appCookie.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, appCookie.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, appCookie.Cookie.SameSite);
        Assert.Equal(TimeSpan.FromMinutes(15), appCookie.ExpireTimeSpan);
        Assert.False(appCookie.SlidingExpiration);

        var passkeyOptions = scope.ServiceProvider.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;
        Assert.Equal("required", passkeyOptions.UserVerificationRequirement);
        Assert.Equal("required", passkeyOptions.ResidentKeyRequirement);
    }

    [Fact]
    public async Task Public_registration_endpoint_is_not_mapped()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "attacker",
            password = "Secure-Vendor#2026"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client,
        string uri,
        object payload,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }
}
