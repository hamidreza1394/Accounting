using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
/// D-108 SQL Server cycle: manual multi-payment recording, independent payment-approval
/// verification (salesOrderPayment / vendor.payments.verify), read-time derived statuses and the
/// receivables report. Payment verification never touches license issuance state.
/// </summary>
public sealed class VendorSalesPaymentSqlServerTests
{
    private const string MakerPassword = "Strong-Vendor#2026-Maker";

    [Fact]
    public async Task PaymentCycle_DerivesUnpaidPartialPaidOverdue_AndKeepsCyclesIndependent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor payment cycle integration tests.");
            Assert.Skip("A disposable SQL Server connection is required for Vendor payment cycle integration tests.");
            return;
        }

        var databaseName = "FaranouVendorPayments_" + Guid.NewGuid().ToString("N");
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
            await using (var setupScope = factory.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);

                var users = setupScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                customerId = (await db.Customers.AddAsync(new VendorCustomer
                {
                    Id = Guid.NewGuid(),
                    CustomerNumber = "PAY-" + Guid.NewGuid().ToString("N")[..12],
                    LegalName = "Payment Cycle Customer",
                    DisplayName = "Payment Customer",
                    Status = "Active",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, cancellationToken)).Entity.Id;
                await db.SaveChangesAsync(cancellationToken);

                await CreateAdminAsync(users, roles, "maker-", MakerPassword,
                [
                    VendorPermissionCatalog.PaymentsRead,
                    VendorPermissionCatalog.PaymentsManage,
                    VendorPermissionCatalog.PaymentsVerify,
                    VendorPermissionCatalog.ApprovalsReview
                ]);
            }

            var maker = new VendorTestAdmin(factory, "maker-", MakerPassword);

            // Maker creates two orders: one collectible, one fully paid.
            var paidOrderId = await maker.CreateOrderAsync(customerId, totalAmount: 500m, dueInDays: null, cancellationToken);
            var collectibleOrderId = await maker.CreateOrderAsync(
                customerId, totalAmount: 1200m, dueInDays: -3, cancellationToken);

            // Maker records a partial payment on the collectible order and the full amount on the paid one.
            var collectiblePaymentId = await maker.RecordPaymentAsync(collectibleOrderId, 300m, cancellationToken);
            var paidPaymentId = await maker.RecordPaymentAsync(paidOrderId, 500m, cancellationToken);

            // Before verification the debt is unchanged (D-108 §5).
            var beforeVerification = await maker.GetOrderAsync(collectibleOrderId, cancellationToken);
            Assert.Equal("Unpaid", beforeVerification.GetProperty("paymentStatus").GetString());
            Assert.Equal(1200m, beforeVerification.GetProperty("remainingAmount").GetDecimal());

            // The same authorized operator requests and decides verification per D-109.
            var verifyApprovalId = await maker.RequestPaymentDecisionAsync(
                collectiblePaymentId, "verify", cancellationToken);
            await maker.DecideAsync(verifyApprovalId, "Approved", cancellationToken);
            var rejectApprovalId = await maker.RequestPaymentDecisionAsync(paidPaymentId, "verify", cancellationToken);
            await maker.DecideAsync(rejectApprovalId, "Approved", cancellationToken);

            // Self-approval is explicitly allowed for the single-operator Vendor v1 policy (D-109).
            // The same identity is persisted as both requester and reviewer; no second administrator is required.
            var secondPaymentId = await maker.RecordPaymentAsync(collectibleOrderId, 100m, cancellationToken);
            var selfApprovalId = await maker.RequestPaymentDecisionAsync(secondPaymentId, "verify", cancellationToken);
            await maker.DecideAsync(selfApprovalId, "Approved", cancellationToken);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var verifiedByMaker = await db.PaymentRecords.AsNoTracking()
                    .SingleAsync(item => item.Id == secondPaymentId, cancellationToken);
                Assert.Equal(VendorPaymentRecord.StatusVerified, verifiedByMaker.Status);
                Assert.Equal(verifiedByMaker.VerifiedByUserId,
                    (await db.ApprovalRequests.AsNoTracking()
                        .SingleAsync(item => item.Id == selfApprovalId, cancellationToken)).ReviewedByUserId);
            }

            var collectible = await maker.GetOrderAsync(collectibleOrderId, cancellationToken);
            Assert.Equal("PartiallyPaid", collectible.GetProperty("paymentStatus").GetString());
            Assert.True(collectible.GetProperty("overdue").GetBoolean());
            Assert.Equal(400m, collectible.GetProperty("verifiedPaidAmount").GetDecimal());
            Assert.Equal(800m, collectible.GetProperty("remainingAmount").GetDecimal());

            var paid = await maker.GetOrderAsync(paidOrderId, cancellationToken);
            Assert.Equal("Paid", paid.GetProperty("paymentStatus").GetString());
            Assert.False(paid.GetProperty("overdue").GetBoolean());

            // The receivables report lists only orders with remaining debt, without any scheduler.
            using var receivables = await maker.GetAsync("/api/vendor/receivables", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, receivables.StatusCode);
            var report = JsonDocument.Parse(await receivables.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            Assert.Equal(1, report.GetProperty("totalCount").GetInt32());
            var receivable = report.GetProperty("items").EnumerateArray().Single();
            Assert.Equal(collectibleOrderId, receivable.GetProperty("id").GetGuid());
            Assert.Equal("PartiallyPaid", receivable.GetProperty("paymentStatus").GetString());
            Assert.True(receivable.GetProperty("overdue").GetBoolean());
        }
        finally
        {
            await DropDatabaseAsync(baseConnection, databaseName, cancellationToken);
        }
    }

    [Fact]
    public async Task PaymentRecording_RejectsCurrencyMismatch_AndUnknownOrders()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor payment cycle integration tests.");
            Assert.Skip("A disposable SQL Server connection is required for Vendor payment cycle integration tests.");
            return;
        }

        var databaseName = "FaranouVendorPaymentsNeg_" + Guid.NewGuid().ToString("N");
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
            await using (var setupScope = factory.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                customerId = (await db.Customers.AddAsync(new VendorCustomer
                {
                    Id = Guid.NewGuid(),
                    CustomerNumber = "PAYN-" + Guid.NewGuid().ToString("N")[..12],
                    LegalName = "Payment Negative Customer",
                    DisplayName = "Payment Negative",
                    Status = "Active",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, cancellationToken)).Entity.Id;
                await db.SaveChangesAsync(cancellationToken);

                var users = setupScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await CreateAdminAsync(users, roles, "maker-", MakerPassword,
                [
                    VendorPermissionCatalog.PaymentsRead,
                    VendorPermissionCatalog.PaymentsManage
                ]);
            }

            var maker = new VendorTestAdmin(factory, "maker-", MakerPassword);
            var orderId = await maker.CreateOrderAsync(customerId, 200m, dueInDays: null, cancellationToken, currencyCode: "EUR");

            using var mismatch = await maker.PostAsync(
                $"/api/vendor/sales-orders/{orderId:D}/payments",
                new { amount = 50m, currencyCode = "USD", method = "bank-transfer" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
            Assert.Contains("payment_currency_mismatch", await mismatch.Content.ReadAsStringAsync(cancellationToken));

            using var unknown = await maker.PostAsync(
                $"/api/vendor/sales-orders/{Guid.NewGuid():D}/payments",
                new { amount = 50m, currencyCode = "EUR", method = "bank-transfer" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            await DropDatabaseAsync(baseConnection, databaseName, cancellationToken);
        }
    }

    private static async Task CreateAdminAsync(
        UserManager<VendorAdminUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        string userNamePrefix,
        string password,
        IReadOnlyCollection<string> permissions)
    {
        var role = new IdentityRole<Guid>("payment-" + userNamePrefix.TrimEnd('-')) { Id = Guid.NewGuid() };
        Assert.True((await roles.CreateAsync(role)).Succeeded);
        foreach (var permission in permissions)
        {
            Assert.True((await roles.AddClaimAsync(role,
                new Claim(VendorPermissionCatalog.ClaimType, permission))).Succeeded);
        }

        var user = new VendorAdminUser
        {
            UserName = userNamePrefix + Guid.NewGuid().ToString("N"),
            Email = Guid.NewGuid().ToString("N") + "@example.invalid",
            EmailConfirmed = true,
            IsEnabled = true,
            MustChangePassword = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role.Name!)).Succeeded);
        Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
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

    /// <summary>One signed-in administrator with cookie session, antiforgery and TOTP helpers.</summary>
    private sealed class VendorTestAdmin : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        private readonly HttpClient client;
        private readonly string password;

        public VendorTestAdmin(WebApplicationFactory<Program> factory, string userNamePrefix, string password)
        {
            this.factory = factory;
            this.password = password;
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
            UserName = ResolveUserNameAsync(userNamePrefix).GetAwaiter().GetResult();
            SignInAsync().GetAwaiter().GetResult();
        }

        public string UserName { get; }

        public void Dispose() => client.Dispose();

        private async Task<string> ResolveUserNameAsync(string prefix)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            return await db.Users.AsNoTracking().Where(user => user.UserName!.StartsWith(prefix))
                .Select(user => user.UserName!).SingleAsync();
        }

        private async Task<string> AntiforgeryAsync()
        {
            using var response = await client.GetAsync("/api/auth/antiforgery");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
            return json.GetProperty("requestToken").GetString()!;
        }

        private async Task<HttpResponseMessage> SendPostAsync(string path, object body, string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
            return await client.SendAsync(request);
        }

        private async Task SignInAsync()
        {
            var token = await AntiforgeryAsync();
            using var passwordResponse = await SendPostAsync(
                "/api/auth/password", new { userName = UserName, password }, token);
            Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);

            string authenticatorKey;
            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
            var user = await users.FindByNameAsync(UserName);
            Assert.NotNull(user);
            authenticatorKey = (await users.GetAuthenticatorKeyAsync(user!))!;

            var verifyToken = await AntiforgeryAsync();
            using var verifyResponse = await SendPostAsync(
                "/api/auth/totp/verify", new { code = GenerateTotpCode(authenticatorKey) }, verifyToken);
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        }

        public async Task<HttpResponseMessage> GetAsync(string path, CancellationToken? ct = null)
        {
            var response = await client.GetAsync(path, ct ?? CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return response;
        }

        public async Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken ct)
        {
            var token = await AntiforgeryAsync();
            return await SendPostAsync(path, body, token);
        }

        public async Task<Guid> CreateOrderAsync(
            Guid customerId, decimal totalAmount, int? dueInDays, CancellationToken ct,
            string currencyCode = "EUR")
        {
            DateTimeOffset? due = dueInDays is { } days ? DateTimeOffset.UtcNow.AddDays(days) : null;
            using var response = await PostAsync(
                "/api/vendor/sales-orders",
                new
                {
                    customerId,
                    orderNumber = "ORD-" + Guid.NewGuid().ToString("N")[..12],
                    totalAmount,
                    currencyCode,
                    dueAtUtc = due
                }, ct);
            Assert.True(response.IsSuccessStatusCode,
                $"Create order returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            return json.GetProperty("id").GetGuid();
        }

        public async Task<Guid> RecordPaymentAsync(Guid orderId, decimal amount, CancellationToken ct)
        {
            using var response = await PostAsync(
                $"/api/vendor/sales-orders/{orderId:D}/payments",
                new { amount, currencyCode = "EUR", method = "bank-transfer" }, ct);
            Assert.True(response.IsSuccessStatusCode,
                $"Record payment returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            return json.GetProperty("id").GetGuid();
        }

        public async Task<Guid> RequestPaymentDecisionAsync(Guid paymentId, string action, CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            var rowVersion = Convert.ToBase64String(
                (await db.PaymentRecords.AsNoTracking().SingleAsync(item => item.Id == paymentId, ct)).RowVersion);

            using var response = await PostAsync(
                $"/api/vendor/payments/{paymentId:D}/approval-request",
                new { action, rowVersion }, ct);
            Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Accepted,
                $"Approval request returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            return json.GetProperty("id").GetGuid();
        }

        public async Task<HttpStatusCode> RawDecisionAsync(Guid approvalId, string decision, CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            var rowVersion = Convert.ToBase64String(
                (await db.ApprovalRequests.AsNoTracking().SingleAsync(item => item.Id == approvalId, ct)).RowVersion);

            using var response = await PostAsync(
                $"/api/vendor/approvals/{approvalId:D}/decision",
                new { decision, rowVersion, comment = (string?)null }, ct);
            return response.StatusCode;
        }

        public async Task DecideAsync(Guid approvalId, string decision, CancellationToken ct)
        {
            var status = await RawDecisionAsync(approvalId, decision, ct);
            Assert.Equal(HttpStatusCode.OK, status);
        }

        public async Task<JsonElement> GetOrderAsync(Guid orderId, CancellationToken ct)
        {
            using var response = await GetAsync($"/api/vendor/sales-orders/{orderId:D}");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
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
    }
}
