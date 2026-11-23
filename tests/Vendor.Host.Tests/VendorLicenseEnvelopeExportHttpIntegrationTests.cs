using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
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
/// Signed license envelope delivery (#394, Vendor slice): GET /api/vendor/licenses/{id}/envelope/export
/// returns exactly the stored envelope JSON of the latest signed revision, re-verifies integrity,
/// trust, signature and installation binding, and refuses fail-closed on any mismatch. The signed
/// payload itself is exported as stored (including a Restricted state), never rewritten.
/// </summary>
public sealed class VendorLicenseEnvelopeExportHttpIntegrationTests
{
    private const string TestKeyId = "test-license-v1";
    private const string TestAlgorithm = LicenseAlgorithms.EcdsaP256Sha256;
    private const string ExportActionCode = "vendor.license.envelope-exported";
    private const string Password = "Strong-Vendor#2026-Export";

    [Fact]
    public async Task ExportReturnsExactStoredEnvelopeWithAuditAndNoStore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(cancellationToken);

        using var response = await harness.Client.GetAsync(
            $"/api/vendor/licenses/{harness.LicenseId:D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        // Byte-faithful delivery: the customer import consumes the same stored envelope JSON.
        Assert.Equal(harness.StoredEnvelopeJson, body);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(item =>
            item.ActionCode == ExportActionCode && item.SubjectId == harness.LicenseId.ToString("D"),
            cancellationToken));
    }

    [Fact]
    public async Task ExportRefusesDraftLicenseTamperedHashAndUnknownLicense()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(
            cancellationToken, licenseStatus: "Draft", tamperStoredHash: true);

        using var draftResponse = await harness.Client.GetAsync(
            $"/api/vendor/licenses/{harness.LicenseId:D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, draftResponse.StatusCode);
        Assert.Equal("license_envelope_not_ready", await ReadErrorAsync(draftResponse, cancellationToken));

        // Restore Issued so the tampered stored hash is actually reached.
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
            var license = await db.Licenses.SingleAsync(item => item.Id == harness.LicenseId, cancellationToken);
            license.Status = "Issued";
            await db.SaveChangesAsync(cancellationToken);
        }

        using var hashResponse = await harness.Client.GetAsync(
            $"/api/vendor/licenses/{harness.LicenseId:D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, hashResponse.StatusCode);
        Assert.Equal("license_envelope_invalid", await ReadErrorAsync(hashResponse, cancellationToken));

        using var unknownResponse = await harness.Client.GetAsync(
            $"/api/vendor/licenses/{Guid.NewGuid():D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    public async Task ExportRefusesUntrustedKeyAndTamperedSignature()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var trustedHarness = await CreateHarnessAsync(cancellationToken, enrollTrustedKey: false);
        using var untrustedResponse = await trustedHarness.Client.GetAsync(
            $"/api/vendor/licenses/{trustedHarness.LicenseId:D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, untrustedResponse.StatusCode);
        Assert.Equal("license_envelope_signing_key_untrusted",
            await ReadErrorAsync(untrustedResponse, cancellationToken));

        await using var tamperedHarness = await CreateHarnessAsync(cancellationToken, signWithSecondKey: true);
        using var signatureResponse = await tamperedHarness.Client.GetAsync(
            $"/api/vendor/licenses/{tamperedHarness.LicenseId:D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, signatureResponse.StatusCode);
        Assert.Equal("license_envelope_signature_invalid",
            await ReadErrorAsync(signatureResponse, cancellationToken));
    }

    [Fact]
    public async Task ExportRequiresLicensesReadPermission()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(cancellationToken, grantReadPermission: false);
        using var response = await harness.Client.GetAsync(
            $"/api/vendor/licenses/{harness.LicenseId:D}/envelope/export", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        return json.GetProperty("error").GetString() ?? string.Empty;
    }

    private sealed record Harness(
        WebApplicationFactory<Program> Factory,
        HttpClient Client,
        Guid LicenseId,
        string StoredEnvelopeJson,
        string DatabaseName,
        string BaseConnection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            var master = new SqlConnectionStringBuilder(BaseConnection) { InitialCatalog = "master" }.ConnectionString;
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [" + DatabaseName +
                "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + DatabaseName + "]; END;";
            command.Parameters.AddWithValue("@databaseName", DatabaseName);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task<Harness> CreateHarnessAsync(
        CancellationToken ct,
        string licenseStatus = "Issued",
        bool enrollTrustedKey = true,
        bool tamperStoredHash = false,
        bool signWithSecondKey = false,
        bool grantReadPermission = true)
    {
        var configuredConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor envelope-export integration tests.");
            Assert.Skip("A disposable SQL Server connection is required for Vendor envelope-export integration tests.");
            return null!; // Assert.Skip throws; this line only satisfies flow analysis.
        }

        var baseConnection = configuredConnection;
        var databaseName = "FaranouVendorEnvelopeExport_" + Guid.NewGuid().ToString("N")[..24];
        var testConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabaseAsync(baseConnection, databaseName, ct);

        var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trustedSpki = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo());
        var signingKeyForEnvelope = signWithSecondKey ? secondKey : signingKey;

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:VendorDb"] = testConnection
                };
                if (enrollTrustedKey)
                    values[$"Licensing:TrustedSigningKeys:{TestKeyId}:PublicKeySubjectPublicKeyInfoBase64"] = trustedSpki;
                configuration.AddInMemoryCollection(values);
            });
        });

        Guid licenseId;
        Guid installationId;
        string storedEnvelopeJson;
        var now = DateTimeOffset.UtcNow;
        var payload = BuildPayload(now);
        var payloadJson = CanonicalJson.SerializePayload(payload);
        var payloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant();

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await db.Database.MigrateAsync(ct);

            var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var role = new IdentityRole<Guid>("envelope-export-test") { Id = Guid.NewGuid() };
            Assert.True((await roles.CreateAsync(role)).Succeeded);
            if (grantReadPermission)
                Assert.True((await roles.AddClaimAsync(role,
                    new Claim(VendorPermissionCatalog.ClaimType, VendorPermissionCatalog.LicensesRead))).Succeeded);

            var users = setupScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
            var user = new VendorAdminUser
            {
                UserName = "envelope-export-" + Guid.NewGuid().ToString("N"),
                Email = Guid.NewGuid().ToString("N") + "@example.invalid",
                EmailConfirmed = true,
                IsEnabled = true,
                MustChangePassword = false,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role.Name!)).Succeeded);
            Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
            Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);

            var customer = new VendorCustomer
            {
                Id = Guid.NewGuid(),
                CustomerNumber = "EXP-" + Guid.NewGuid().ToString("N")[..12],
                LegalName = "Envelope Export Integration Customer",
                DisplayName = "Envelope Export Customer",
                Status = "Active",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            installationId = Guid.NewGuid();
            var installation = new VendorInstallation
            {
                Id = installationId,
                CustomerId = customer.Id,
                InstallationCode = "EXP-" + Guid.NewGuid().ToString("N")[..12],
                Name = "Envelope Export Installation",
                Environment = "Staging",
                Status = "Active",
                CreatedAtUtc = now
            };
            licenseId = Guid.NewGuid();
            var license = new VendorLicense
            {
                Id = licenseId,
                CustomerId = customer.Id,
                InstallationId = installationId,
                LicenseNumber = "EXP-LIC-" + Guid.NewGuid().ToString("N")[..12],
                ProductCode = "farano-accounting",
                Status = licenseStatus,
                LicenseKind = LicensePayload.KindPerpetual,
                MaxCompanies = 1,
                SubsystemsJson = "[]",
                GraceDays = 0,
                LastIssuedSequence = 1,
                ValidFromUtc = now,
                CreatedAtUtc = now
            };
            db.Customers.Add(customer);
            db.Installations.Add(installation);
            db.Licenses.Add(license);

            var signature = signingKeyForEnvelope.SignData(
                Encoding.UTF8.GetBytes(payloadJson), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var envelope = new LicenseEnvelope(
                LicenseAlgorithms.CurrentFormatVersion, TestKeyId, TestAlgorithm,
                payloadJson, Convert.ToBase64String(signature));
            storedEnvelopeJson = JsonSerializer.Serialize(envelope);
            db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
            {
                Id = Guid.NewGuid(),
                LicenseId = licenseId,
                Sequence = 1,
                KeyId = TestKeyId,
                Algorithm = TestAlgorithm,
                EnvelopeJson = storedEnvelopeJson,
                PayloadSha256 = tamperStoredHash
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson + " "))).ToLowerInvariant()
                    : payloadSha256,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true
        });
        await SignInAsync(client, factory, ct);
        return new Harness(factory, client, licenseId, storedEnvelopeJson, databaseName, baseConnection);
    }

    private static LicensePayload BuildPayload(DateTimeOffset now) =>
        new(
            LicensePayload.CurrentFormatVersion,
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            "farano-accounting",
            LicensePayload.KindPerpetual,
            now,
            now,
            null,
            0,
            1,
            new LicenseLimits(1, []))
        {
            CompanyEntitlements =
            [
                new CompanyLicenseEntitlement(
                    Guid.NewGuid().ToString("D"), [LicenseSubsystemCodes.TradeImport], 1, [])
            ]
        };

    private static async Task SignInAsync(HttpClient client, WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        var token = await AntiforgeryAsync(client, ct);
        var userName = await GetUserNameAsync(factory, "envelope-export-", ct);
        using var passwordResponse = await SendPostAsync(client, "/api/auth/password",
            new { userName, password = Password }, token, ct);
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
}
