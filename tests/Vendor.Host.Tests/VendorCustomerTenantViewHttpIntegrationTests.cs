using System.Net;
using System.Net.Http.Json;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
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
/// D-103 V1 scope guard: the tenant view is served as a read-only derivation of the customer
/// account, and no independent tenant management API exists at /api/vendor/tenants.
/// </summary>
public sealed class VendorCustomerTenantViewHttpIntegrationTests
{
    [Fact]
    public async Task CustomerResponseCarriesDerivedTenantViewAndNoTenantApiExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor tenant-view integration tests.");
            Assert.Skip("A disposable SQL Server connection is required for Vendor tenant-view integration tests.");
            return;
        }

        var databaseName = "FaranouVendorTenantView_" + Guid.NewGuid().ToString("N");
        var testConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabaseAsync(baseConnection, databaseName, cancellationToken);

        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
            {
                webHost.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:VendorDb"] = testConnection
                    }));
            });

            Guid customerId;
            string customerNumber;
            await using (var setupScope = factory.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);

                var users = setupScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var role = new IdentityRole<Guid>("tenant-view-test") { Id = Guid.NewGuid() };
                Assert.True((await roles.CreateAsync(role)).Succeeded);
                Assert.True((await roles.AddClaimAsync(role,
                    new Claim(VendorPermissionCatalog.ClaimType, VendorPermissionCatalog.CustomersRead))).Succeeded);

                var user = new VendorAdminUser
                {
                    UserName = "tenant-view-" + Guid.NewGuid().ToString("N"),
                    Email = Guid.NewGuid().ToString("N") + "@example.invalid",
                    EmailConfirmed = true,
                    IsEnabled = true,
                    MustChangePassword = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                Assert.True((await users.CreateAsync(user, "Strong-Vendor#2026-Viewer")).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role.Name!)).Succeeded);
                Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
                Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);

                var now = DateTimeOffset.UtcNow;
                customerNumber = "TVW-" + Guid.NewGuid().ToString("N")[..12];
                var customer = new VendorCustomer
                {
                    Id = Guid.NewGuid(),
                    CustomerNumber = customerNumber,
                    LegalName = "Tenant View Integration Customer",
                    DisplayName = "Tenant View Customer",
                    Status = "Active",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
                customerId = customer.Id;
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"), HandleCookies = true
            });
            var userName = await GetUserNameAsync(factory, "tenant-view-", cancellationToken);
            await SignInAsync(client, userName, "Strong-Vendor#2026-Viewer", factory, cancellationToken);

            // The derived tenant view mirrors the persisted customer account.
            using var detail = await client.GetAsync($"/api/vendor/customers/{customerId:D}", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            var tenantView = detailJson.GetProperty("tenantView");
            Assert.Equal("customer-account", tenantView.GetProperty("derivedFrom").GetString());
            Assert.Equal(customerNumber, tenantView.GetProperty("workspaceReference").GetString());
            Assert.Equal("Active", tenantView.GetProperty("status").GetString());

            // Scope guard: no independent tenant management API is mapped in V1.
            using var tenantsGet = await client.GetAsync("/api/vendor/tenants/", cancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, tenantsGet.StatusCode);
            var tenantsPostToken = await AntiforgeryAsync(client, cancellationToken);
            using var tenantsPost = await SendPostAsync(client, "/api/vendor/tenants/",
                new { customerId = customerId }, tenantsPostToken, cancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, tenantsPost.StatusCode);
        }
        finally
        {
            await DropDatabaseAsync(baseConnection, databaseName, cancellationToken);
        }
    }

    private static async Task<string> GetUserNameAsync(
        WebApplicationFactory<Program> factory, string prefix, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        return await db.Users.AsNoTracking().Where(user => user.UserName!.StartsWith(prefix))
            .Select(user => user.UserName!).SingleAsync(ct);
    }

    private static async Task<string> AntiforgeryAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        var token = json.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<HttpResponseMessage> SendPostAsync(
        HttpClient client, string path, object body, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return await client.SendAsync(request, ct);
    }

    private static async Task SignInAsync(
        HttpClient client, string userName, string password, WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        var token = await AntiforgeryAsync(client, ct);
        using var passwordResponse = await SendPostAsync(client, "/api/auth/password",
            new { userName, password }, token, ct);
        Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);

        string authenticatorKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
            var user = await users.FindByNameAsync(userName);
            Assert.NotNull(user);
            authenticatorKey = (await users.GetAuthenticatorKeyAsync(user!))!;
        }

        var verifyToken = await AntiforgeryAsync(client, ct);
        using var verifyResponse = await SendPostAsync(client, "/api/auth/totp/verify",
            new { code = GenerateTotpCode(authenticatorKey) }, verifyToken, ct);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
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
