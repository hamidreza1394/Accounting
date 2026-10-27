using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// A narrow end-to-end mutation test: real Vendor SQL Server, Identity MFA cookie,
/// production permission/resource handlers, anti-forgery, rowversion and audit writer.
/// </summary>
public sealed class VendorCustomerMutationHttpIntegrationTests
{
    [Fact]
    public async Task CustomerUpdateEnforcesAntiforgeryAndRowVersionAndAuditsOnlyCommittedWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor mutation integration tests.");
            Assert.Skip("A disposable SQL Server connection is required for Vendor mutation integration tests.");
            return;
        }

        var databaseName = "FaranouVendorMutation_" + Guid.NewGuid().ToString("N");
        var testConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabaseAsync(baseConnection, databaseName, cancellationToken);

        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
                webHost.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:VendorDb"] = testConnection
                    })));

            await using (var setupScope = factory.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);

                var users = setupScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var role = new IdentityRole<Guid>("customer-mutation-test") { Id = Guid.NewGuid() };
                var roleResult = await roles.CreateAsync(role);
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Code)));
                var permissionResult = await roles.AddClaimAsync(role, new Claim(
                    VendorPermissionCatalog.ClaimType, VendorPermissionCatalog.CustomersManage));
                Assert.True(permissionResult.Succeeded, string.Join("; ", permissionResult.Errors.Select(error => error.Code)));

                var user = new VendorAdminUser
                {
                    UserName = "mutation-" + Guid.NewGuid().ToString("N"),
                    Email = Guid.NewGuid().ToString("N") + "@example.invalid",
                    EmailConfirmed = true,
                    IsEnabled = true,
                    MustChangePassword = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var userResult = await users.CreateAsync(user, "Strong-Vendor#2026-Password");
                Assert.True(userResult.Succeeded, string.Join("; ", userResult.Errors.Select(error => error.Code)));
                var addRoleResult = await users.AddToRoleAsync(user, role.Name!);
                Assert.True(addRoleResult.Succeeded, string.Join("; ", addRoleResult.Errors.Select(error => error.Code)));
                var keyResult = await users.ResetAuthenticatorKeyAsync(user);
                Assert.True(keyResult.Succeeded);
                var mfaResult = await users.SetTwoFactorEnabledAsync(user, true);
                Assert.True(mfaResult.Succeeded);

                var customer = new VendorCustomer
                {
                    Id = Guid.NewGuid(),
                    CustomerNumber = "IT-" + Guid.NewGuid().ToString("N")[..20],
                    LegalName = "Integration Test Customer",
                    DisplayName = "Before Update",
                    Status = "Active",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
                // Read the server-generated SQL Server rowversion after insert.
                await db.Entry(customer).ReloadAsync(cancellationToken);
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

            var (antiForgeryToken, _) = await GetAntiforgeryTokenAsync(client, cancellationToken);
            using (var passwordResponse = await PostWithTokenAsync(client, "/api/auth/password",
                new { userName = await GetTestUserNameAsync(factory, cancellationToken), password = "Strong-Vendor#2026-Password" },
                antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
                using var passwordJson = JsonDocument.Parse(await passwordResponse.Content.ReadAsStringAsync(cancellationToken));
                Assert.True(passwordJson.RootElement.GetProperty("requiresSecondFactor").GetBoolean());
            }

            string testUserName;
            string authenticatorKey;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var user = await users.FindByNameAsync(await GetTestUserNameAsync(factory, cancellationToken));
                Assert.NotNull(user);
                testUserName = user!.UserName!;
                authenticatorKey = (await users.GetAuthenticatorKeyAsync(user))!;
            }

            using (var verifyResponse = await PostWithTokenAsync(client, "/api/auth/totp/verify",
                new { code = GenerateTotpCode(authenticatorKey) }, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
            }

            Guid customerId;
            byte[] initialRowVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var customer = await db.Customers.SingleAsync(item => item.CustomerNumber.StartsWith("IT-"), cancellationToken);
                customerId = customer.Id;
                initialRowVersion = customer.RowVersion.ToArray();
            }

            var path = $"/api/vendor/customers/{customerId:D}";
            var updateBody = new
            {
                rowVersion = Convert.ToBase64String(initialRowVersion),
                customerNumber = "IT-" + customerId.ToString("N")[..20],
                legalName = "Integration Test Customer",
                displayName = "After Update"
            };

            using (var missingTokenRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            using (var response = await client.SendAsync(missingTokenRequest, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var invalidTokenRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            {
                invalidTokenRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", "not-a-valid-antiforgery-token");
                using var response = await client.SendAsync(invalidTokenRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var malformedVersionRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(new
                {
                    rowVersion = "not-base64",
                    customerNumber = "IT-" + customerId.ToString("N")[..20],
                    legalName = "Integration Test Customer",
                    displayName = "Must Not Persist"
                })
            })
            {
                malformedVersionRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
                using var response = await client.SendAsync(malformedVersionRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("invalid_row_version", json.RootElement.GetProperty("error").GetString());
            }

            using (var validRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            {
                validRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
                using var response = await client.SendAsync(validRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var staleRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            {
                staleRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
                using var response = await client.SendAsync(staleRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("stale_row_version", json.RootElement.GetProperty("error").GetString());
            }

            await using (var verifyScope = factory.Services.CreateAsyncScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var persisted = await db.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId, cancellationToken);
                Assert.Equal("After Update", persisted.DisplayName);
                Assert.NotEqual(initialRowVersion, persisted.RowVersion);
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.customer.updated" && item.SubjectId == customerId.ToString("D"), cancellationToken));
            }
        }
        finally
        {
            await DropDatabaseAsync(baseConnection, databaseName, CancellationToken.None);
        }
    }

    private static async Task<string> GetTestUserNameAsync(WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        return await db.Users.AsNoTracking().Where(user => user.UserName!.StartsWith("mutation-"))
            .Select(user => user.UserName!).SingleAsync(ct);
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var token = json.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return (token!, response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith("__Host-Faranou.Vendor.AntiForgery=", StringComparison.Ordinal)) ?? string.Empty
            : string.Empty);
    }

    private static async Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client, string path, object body, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return await client.SendAsync(request, ct);
    }

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
            if (value < 0) throw new InvalidOperationException("The persisted authenticator key is not valid Base32.");
            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                secret.Add((byte)((buffer >> bitsInBuffer) & 0xff));
            }
        }

        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        using var hmac = new HMACSHA1(secret.ToArray());
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binaryCode = ((hash[offset] & 0x7f) << 24) | ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) | (hash[offset + 3] & 0xff);
        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static async Task CreateDatabaseAsync(string configuredConnection, string databaseName, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task DropDatabaseAsync(string configuredConnection, string databaseName, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [" + databaseName +
            "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + databaseName + "]; END;";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(ct);
    }
}
