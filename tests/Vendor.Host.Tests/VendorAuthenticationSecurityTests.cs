using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text.Json;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuthenticationSecurityTests
{
    [Fact]
    public async Task PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
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

        var configuredConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_ConnectionStrings__VendorDb");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            Assert.Fail("The isolated Vendor authentication database must be configured in CI.");
        }

        var connectionBuilder = new SqlConnectionStringBuilder(configuredConnection);
        Assert.Contains("localhost", connectionBuilder.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FaranouVendorAuthTests", connectionBuilder.InitialCatalog);
        await ResetTestDatabaseAsync(configuredConnection, cancellationToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            try
            {
                // Diagnose the runtime model against the last migration before MigrateAsync throws its generic warning.
                var migrationsAssembly = db.GetService<IMigrationsAssembly>();
                var modelDiffer = db.GetService<IMigrationsModelDiffer>();
                var modelRuntimeInitializer = db.GetService<IModelRuntimeInitializer>();
                var migrationTargetModel = modelRuntimeInitializer.Initialize(
                    migrationsAssembly.ModelSnapshot!.Model);
                var runtimeDesignTimeModel = db.GetService<IDesignTimeModel>().Model;
                var modelDifferences = modelDiffer.GetDifferences(
                    migrationTargetModel.GetRelationalModel(),
                    runtimeDesignTimeModel.GetRelationalModel());
                Assert.True(
                    modelDifferences.Count == 0,
                    "Runtime Vendor model differs from latest migration target: " +
                    string.Join("; ", modelDifferences.Select(DescribeModelOperation)));

                await db.Database.MigrateAsync(cancellationToken);

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

            var (token, antiforgeryCookie) = await GetAntiforgeryTokenAndCookieAsync(client, cancellationToken);
            using var passwordOnlyResponse = await PostWithTokenAsync(
                client,
                "/api/auth/password",
                new { userName = passwordOnly.UserName, password = "Secure-Vendor#2026" },
                token,
                cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, passwordOnlyResponse.StatusCode);

            using var disabledResponse = await PostWithTokenAsync(
                client,
                "/api/auth/password",
                new { userName = disabled.UserName, password = "Secure-Vendor#2026" },
                token,
                cancellationToken);
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
                token,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
            using (var responseDocument = JsonDocument.Parse(await passwordResponse.Content.ReadAsStringAsync(cancellationToken)))
            {
                Assert.True(responseDocument.RootElement.GetProperty("requiresSecondFactor").GetBoolean());
            }

            var setCookies = passwordResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues)
                ? string.Join(";", cookieValues)
                : string.Empty;
            Assert.Contains("__Host-Faranou.Vendor.TwoFactor=", setCookies, StringComparison.Ordinal);
            Assert.DoesNotContain("__Host-Faranou.Vendor.Auth=", setCookies, StringComparison.Ordinal);

            // Generate a code as a real authenticator app does from the persisted shared key.
            // This tests the complete RFC 6238 interoperability path instead of testing the
            // Identity token provider by asking it to generate and validate its own output.
            var authenticatorKey = await users.GetAuthenticatorKeyAsync(user);
            Assert.False(string.IsNullOrWhiteSpace(authenticatorKey));
            var code = GenerateTotpCode(authenticatorKey!);
            Assert.True(
                await users.VerifyTwoFactorTokenAsync(
                    user,
                    users.Options.Tokens.AuthenticatorTokenProvider,
                    code),
                "A standards-compliant RFC 6238 code must validate before exercising the HTTP challenge flow.");
            var twoFactorCookie = passwordResponse.Headers.GetValues("Set-Cookie")
                .Select(value => value.Split(';', 2)[0])
                .Single(value => value.StartsWith("__Host-Faranou.Vendor.TwoFactor=", StringComparison.Ordinal));
            // Antiforgery request tokens are bound to the current claims identity. The TOTP
            // challenge cookie changes that identity, so refresh the token under the challenge.
            var (totpToken, totpAntiforgeryCookie) =
                await GetAntiforgeryTokenAndCookieAsync(client, cancellationToken, $"{antiforgeryCookie}; {twoFactorCookie}");
            using var totpResponse = await PostWithTokenAsync(
                client,
                "/api/auth/totp/verify",
                new { code },
                totpToken,
                cancellationToken,
                $"{totpAntiforgeryCookie}; {twoFactorCookie}");
            Assert.Equal(HttpStatusCode.OK, totpResponse.StatusCode);

            using var meResponse = await client.GetAsync("/api/auth/me", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
            using var meDocument = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync(cancellationToken));
            Assert.True(meDocument.RootElement.GetProperty("totpEnabled").GetBoolean());

            // Identity's two-factor sign-in writes an application cookie, then the
            // endpoint re-issues it with the verified-factor claim. Use the final cookie
            // value from the response; asserting exactly one matching Set-Cookie header
            // incorrectly treats this deliberate replacement as a failed sign-in.
            var applicationCookie = totpResponse.Headers.GetValues("Set-Cookie")
                .Select(value => value.Split(';', 2)[0])
                .Last(value => value.StartsWith("__Host-Faranou.Vendor.Auth=", StringComparison.Ordinal));
            // The application cookie establishes a new claims identity after TOTP; use a
            // request token minted for that authenticated identity for subsequent mutations.
            var (authenticatedToken, authenticatedAntiforgeryCookie) =
                await GetAntiforgeryTokenAndCookieAsync(client, cancellationToken, $"{antiforgeryCookie}; {applicationCookie}");
            var authenticatedCookieHeader = $"{authenticatedAntiforgeryCookie}; {applicationCookie}";
            using var creationOptionsResponse = await PostWithTokenAsync(
                client,
                "/api/auth/passkey/creation-options",
                new { },
                authenticatedToken,
                cancellationToken,
                authenticatedCookieHeader);
            Assert.Equal(HttpStatusCode.OK, creationOptionsResponse.StatusCode);
            using (var creationOptions = JsonDocument.Parse(await creationOptionsResponse.Content.ReadAsStringAsync(cancellationToken)))
            {
                var authenticatorSelection = creationOptions.RootElement.GetProperty("authenticatorSelection");
                Assert.Equal("required", authenticatorSelection.GetProperty("userVerification").GetString());
                Assert.Equal("required", authenticatorSelection.GetProperty("residentKey").GetString());
            }

            using var logoutResponse = await PostWithTokenAsync(
                client, "/api/auth/logout", new { }, authenticatedToken, cancellationToken, authenticatedCookieHeader);
            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
            using var afterLogout = await client.GetAsync("/api/auth/me", cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

            }
            finally
            {
                await db.Database.CloseConnectionAsync();
                await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task AnonymousAuthPostIsAntiforgeryProtectedAndRateLimited()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
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
                new { userName = "nobody", password = "not-a-real-password" },
                cancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(5), status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal((HttpStatusCode)429, statuses[5]);
    }

    [Fact]
    public Task IdentityCookieAndPasskeyOptionsEnforceSecureMfaDefaults()
    {
        using var factory = new WebApplicationFactory<Program>();
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
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublicRegistrationEndpointIsNotMapped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "attacker",
            password = "Secure-Vendor#2026"
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string DescribeModelOperation(MigrationOperation operation) => operation switch
    {
        AlterColumnOperation alter =>
            $"AlterColumn {alter.Table}.{alter.Name}: type {alter.OldColumn.ColumnType} -> {alter.ColumnType}; " +
            $"nullable {alter.OldColumn.IsNullable} -> {alter.IsNullable}; " +
            $"defaultSql {alter.OldColumn.DefaultValueSql ?? "<null>"} -> {alter.DefaultValueSql ?? "<null>"}",
        CreateIndexOperation index =>
            $"CreateIndex {index.Table}.{index.Name}({string.Join(",", index.Columns)}); " +
            $"unique={index.IsUnique}; filter={index.Filter ?? "<null>"}",
        AddForeignKeyOperation foreignKey =>
            $"AddForeignKey {foreignKey.Table}.{foreignKey.Name}({string.Join(",", foreignKey.Columns)}) -> " +
            $"{foreignKey.PrincipalTable}({string.Join(",", foreignKey.PrincipalColumns)}); onDelete={foreignKey.OnDelete}",
        _ => operation.GetType().Name + " (" + operation + ")"
    };

    private static string GenerateTotpCode(string base32Secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = base32Secret.Trim().TrimEnd('=').ToUpperInvariant();
        var secret = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in normalized)
        {
            var value = alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new InvalidOperationException("The persisted authenticator key is not valid Base32.");
            }

            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                secret.Add((byte)((buffer >> bitsInBuffer) & 0xff));
            }
        }

        var timeCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeCounter);

        using var hmac = new HMACSHA1(secret.ToArray());
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binaryCode = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static async Task ResetTestDatabaseAsync(
        string configuredConnection,
        CancellationToken cancellationToken)
    {
        const string databaseName = "FaranouVendorAuthTests";
        var masterConnectionString = new SqlConnectionStringBuilder(configuredConnection)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(N'FaranouVendorAuthTests') IS NOT NULL
            BEGIN
                ALTER DATABASE [FaranouVendorAuthTests] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [FaranouVendorAuthTests];
            END;
            CREATE DATABASE [FaranouVendorAuthTests];
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryTokenAndCookieAsync(
        HttpClient client,
        CancellationToken cancellationToken,
        string? cookieHeader = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/antiforgery");
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = document.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        // The antiforgery cookie may already be present in the client's cookie jar. In that
        // case ASP.NET Core can issue a fresh request token without emitting another Set-Cookie.
        var responseCookie = response.Headers.TryGetValues("Set-Cookie", out var responseCookies)
            ? responseCookies
                .Select(value => value.Split(';', 2)[0])
                .SingleOrDefault(value => value.StartsWith("__Host-Faranou.Vendor.AntiForgery=", StringComparison.Ordinal))
            : null;
        var requestCookie = string.IsNullOrWhiteSpace(cookieHeader)
            ? null
            : cookieHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .SingleOrDefault(value => value.StartsWith("__Host-Faranou.Vendor.AntiForgery=", StringComparison.Ordinal));
        var cookie = responseCookie ?? requestCookie;
        Assert.False(string.IsNullOrWhiteSpace(cookie), "The antiforgery endpoint must provide or reuse its antiforgery cookie.");
        return (token!, cookie!);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = document.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client,
        string uri,
        object payload,
        string token,
        CancellationToken cancellationToken,
        string? cookieHeader = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        return client.SendAsync(request, cancellationToken);
    }
}
