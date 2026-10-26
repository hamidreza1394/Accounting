using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Faranou.Vendor.Host.Bootstrap;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorBootstrapIntegrationTests
{
    [Fact]
    public async Task BootstrapCreatesExactlyOneEnabledMfaAdministratorUnderConcurrentAttempts()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
            {
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor bootstrap integration tests.");
            }

            Assert.Skip("A disposable SQL Server connection is required for Vendor bootstrap integration tests.");
            return;
        }

        var connectionBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FaranouVendorBootstrapTests_{Guid.NewGuid():N}"
        };
        var bootstrapSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VendorDb"] = connectionBuilder.ConnectionString,
                ["Bootstrap:Secret"] = bootstrapSecret
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVendorPersistence(configuration);
        new IdentityBuilder(typeof(VendorAdminUser), services).AddDefaultTokenProviders();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            var promptA = new FakePrompt(bootstrapSecret, "bootstrap-a", "bootstrap-a@example.invalid");
            var promptB = new FakePrompt(bootstrapSecret, "bootstrap-b", "bootstrap-b@example.invalid");

            var results = await Task.WhenAll(
                VendorBootstrapCommand.RunAsync(provider, configuration, promptA, TestContext.Current.CancellationToken),
                VendorBootstrapCommand.RunAsync(provider, configuration, promptB, TestContext.Current.CancellationToken));

            Assert.Single(results.Where(result => result == 0));
            Assert.Single(results.Where(result => result != 0));

            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var users = await db.Users.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
                var user = Assert.Single(users);
                Assert.True(user.IsEnabled);
                Assert.True(user.TwoFactorEnabled);
                Assert.False(user.MustChangePassword);

                var audit = await VendorAuditChainVerifier.VerifyAsync(db, TestContext.Current.CancellationToken);
                Assert.True(audit.IsValid, audit.FailureCode);
                Assert.Equal(2, audit.EventCount);
            }
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
            await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void BootstrapSecretComparisonRejectsMissingAndDifferentValues()
    {
        const string expected = "random-test-secret-with-at-least-32-bytes";

        Assert.True(VendorBootstrapCommand.MatchesSecret(expected, expected));
        Assert.False(VendorBootstrapCommand.MatchesSecret(expected, "another-random-test-secret-value"));
        Assert.False(VendorBootstrapCommand.MatchesSecret(expected, null));
        Assert.False(VendorBootstrapCommand.MatchesSecret(null, expected));
    }

    private sealed class FakePrompt(string bootstrapSecret, string userName, string email) : IVendorBootstrapPrompt
    {
        private string? _authenticatorKey;
        private int _lineReadCount;

        public string? ReadLine(string prompt)
        {
            _ = prompt;
            return _lineReadCount++ switch
            {
                0 => bootstrapSecret,
                1 => userName,
                2 => email,
                _ => GenerateTotpCode(_authenticatorKey ?? throw new InvalidOperationException("TOTP URI was not provided."))
            };
        }

        public string ReadPassword(string prompt)
        {
            _ = prompt;
            return "Secure-Vendor#2026";
        }

        public void WriteLine(string message)
        {
            if (!message.StartsWith("otpauth://totp/", StringComparison.Ordinal))
            {
                return;
            }

            var uri = new Uri(message);
            var secret = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Single(part => part[0] == "secret");
            _authenticatorKey = Uri.UnescapeDataString(secret[1]);
        }
    }

    private static string GenerateTotpCode(string base32Key)
    {
        var key = DecodeBase32(base32Key);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> counterBytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var binaryCode = ((hash[offset] & 0x7f) << 24) |
                         ((hash[offset + 1] & 0xff) << 16) |
                         ((hash[offset + 2] & 0xff) << 8) |
                         (hash[offset + 3] & 0xff);
        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException("Invalid Base32 authenticator key.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
