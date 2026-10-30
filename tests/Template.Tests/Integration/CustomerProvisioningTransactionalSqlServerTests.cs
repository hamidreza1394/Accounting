using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain;
using Faranou.Licensing.Contracts;
using Infrastructure.Provisioning;
using Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Transaction, concurrency, token-lifecycle, permission and replay behavior of the customer provisioning
/// consumer on a real SQL Server. Each test uses its own uniquely named disposable database.
/// </summary>
public sealed class CustomerProvisioningTransactionalSqlServerTests
{
    private const string AcknowledgementKeyId = "customer-ack-test-key";
    private const string EnrollmentPassword = "Strong-Passphrase-2026";

    // -- harness ------------------------------------------------------------------------------------------

    private sealed class Harness : IDisposable
    {
        private readonly ECDsa _vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly ECDsa _acknowledgementKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly IConfiguration _configuration;

        public Harness(string databaseName)
        {
            DatabaseName = databaseName;
            Settings = new CustomerProvisioningSettings
            {
                CustomerReference = "customer-ref",
                InstallationId = "installation-ref",
                CustomerBoundaryId = Guid.NewGuid(),
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Provisioning:TrustedVendorKeys:vendor-key-1"] =
                        Convert.ToBase64String(_vendorKey.ExportSubjectPublicKeyInfo()),
                    [ConfigurationCustomerProvisioningAcknowledgementTrustStore.SectionPath + ":" + AcknowledgementKeyId] =
                        Convert.ToBase64String(_acknowledgementKey.ExportSubjectPublicKeyInfo()),
                })
                .Build();
        }

        public string DatabaseName { get; }
        public CustomerProvisioningSettings Settings { get; }
        public OffsetTimeProvider Clock { get; } = new();

        public Data.DatabaseContext CreateContext(params IInterceptor[] interceptors)
        {
            var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString)
            {
                InitialCatalog = DatabaseName,
            };
            var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseSqlServer(builder.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName));
            if (interceptors.Length > 0)
                options.AddInterceptors(interceptors);
            return new Data.DatabaseContext(options.Options);
        }

        public CustomerProvisioningApplyService CreateService(Data.DatabaseContext database) =>
            new(database,
                new CustomerProvisioningPackageVerifier(
                    new ConfigurationCustomerProvisioningTrustStore(_configuration), Options.Create(Settings), TimeProvider.System),
                new LocalSigner(_acknowledgementKey),
                new ConfigurationCustomerProvisioningAcknowledgementTrustStore(_configuration),
                Options.Create(Settings),
                new PasswordHashService(),
                Clock,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomerProvisioningApplyService>.Instance);

        public CustomerProvisioningPackage NewPackage(
            string adminName,
            string[]? subsystems = null,
            string? requestId = null,
            string? nonce = null,
            string companyType = "independent",
            string? parentReference = null)
        {
            var now = DateTimeOffset.UtcNow;
            var payload = new CustomerProvisioningPayload(
                3,
                requestId ?? Guid.NewGuid().ToString("D"),
                Settings.CustomerReference,
                Settings.InstallationId,
                "شرکت آزمایشی " + adminName,
                companyType,
                adminName,
                subsystems ?? new[] { "finance" },
                now.AddMinutes(-1),
                now.AddHours(2),
                nonce ?? Guid.NewGuid().ToString("N"),
                "سال مالی ۱۴۰۵",
                new DateOnly(2026, 3, 21),
                new DateOnly(2027, 3, 20),
                parentReference,
                adminName + "@example.test");
            return Sign(payload);
        }

        public CustomerProvisioningPackage Sign(CustomerProvisioningPayload payload)
        {
            var json = CustomerProvisioningCanonicalJson.Serialize(payload);
            var signature = _vendorKey.SignData(
                Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return new CustomerProvisioningPackage(
                CustomerProvisioningPackageValidator.CurrentFormatVersion,
                "vendor-key-1",
                LicenseAlgorithms.EcdsaP256Sha256,
                json,
                Convert.ToBase64String(signature));
        }

        public void Dispose()
        {
            _vendorKey.Dispose();
            _acknowledgementKey.Dispose();
        }
    }

    private sealed class LocalSigner(ECDsa key) : ICustomerProvisioningAcknowledgementSigningProvider
    {
        public Task<CustomerProvisioningAcknowledgementSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalAcknowledgementUtf8, CancellationToken cancellationToken)
        {
            var signature = key.SignData(
                canonicalAcknowledgementUtf8.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return Task.FromResult(new CustomerProvisioningAcknowledgementSigningResult(
                true, AcknowledgementKeyId, Convert.ToBase64String(signature), null));
        }
    }

    /// <summary>A clock that follows real time plus an explicit offset, so expiry can be crossed deterministically.</summary>
    private sealed class OffsetTimeProvider : TimeProvider
    {
        private long _offsetTicks;
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _offsetTicks, amount.Ticks);
        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref _offsetTicks));
    }

    /// <summary>Fails the final SaveChanges of a provisioning apply (the one that adds the membership) once armed.</summary>
    private sealed class FailingSaveInterceptor(Func<Exception> failure) : SaveChangesInterceptor
    {
        public volatile bool Armed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<UserCompanyMembership>()
                    .Any(entry => entry.State == EntityState.Added))
                throw failure();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static async Task WithHarnessAsync(Func<Harness, Task> test)
    {
        var name = "FaranouTests_ProvTx_" + Guid.NewGuid().ToString("N")[..12];
        using var harness = new Harness(name);
        try
        {
            await using (var setup = harness.CreateContext())
            {
                await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
                setup.PermissionDefinitions.AddRange(
                    new PermissionDefinition { Code = "finance.invoice.create", Name = "Create invoice" },
                    new PermissionDefinition { Code = "finance.invoice.read", Name = "Read invoice" },
                    new PermissionDefinition { Code = "sales.customer.read", Name = "Read customer" },
                    new PermissionDefinition { Code = "inventory.item.read", Name = "Read item" },
                    new PermissionDefinition { Code = "treasury.cash.read", Name = "Read cash" });
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await test(harness);
        }
        finally
        {
            await using var cleanup = harness.CreateContext();
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task<int> CountAsync<T>(IQueryable<T> query) =>
        await query.CountAsync(TestContext.Current.CancellationToken);

    private static async Task AssertNothingPersistedAsync(Data.DatabaseContext database)
    {
        Assert.Equal(0, await CountAsync(database.Companies));
        Assert.Equal(0, await CountAsync(database.FiscalPeriods));
        Assert.Equal(0, await CountAsync(database.CompanyRoles));
        Assert.Equal(0, await CountAsync(database.Users));
        Assert.Equal(0, await CountAsync(database.UserCompanyMemberships));
        Assert.Equal(0, await CountAsync(database.ProvisioningRequestClaims));
        Assert.Equal(0, await CountAsync(database.SecurityAuditEvents.Where(item =>
            item.EventType == "ProvisioningApplied" || item.EventType == "ProvisioningRejected")));
    }

    // -- tests --------------------------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentDuplicateRequests_CreateExactlyOneCompanyAndOneEnrollmentToken()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            var package = harness.NewPackage("concurrent-admin");
            const int parallelRequests = 8;

            var outcomes = await Task.WhenAll(Enumerable.Range(0, parallelRequests).Select(async _ =>
            {
                await Task.Yield();
                try
                {
                    await using var database = harness.CreateContext();
                    return (Result: await harness.CreateService(database).ApplyAsync(package, CancellationToken.None),
                            Error: (Exception?)null);
                }
                catch (Exception exception)
                {
                    return (Result: (CustomerProvisioningApplyResult?)null, Error: exception);
                }
            }));

            // No request may fail with an unhandled database error (for example an uncaught deadlock).
            Assert.All(outcomes, outcome => Assert.Null(outcome.Error));

            var results = outcomes.Select(outcome => outcome.Result!).ToList();
            var created = results.Where(result => result.Success && !result.Replayed).ToList();
            Assert.Single(created);
            Assert.NotNull(created[0].EnrollmentToken);
            Assert.All(results.Where(result => !ReferenceEquals(result, created[0])), other =>
            {
                Assert.Null(other.EnrollmentToken);
                Assert.True(
                    (other.Success && other.Replayed) || other.ErrorCode == "provisioning_apply_conflict",
                    "A duplicate must be an idempotent replay or a stable conflict, not a second application.");
            });

            await using var verification = harness.CreateContext();
            Assert.Equal(1, await CountAsync(verification.Companies));
            Assert.Equal(1, await CountAsync(verification.Users));
            Assert.Equal(1, await CountAsync(verification.UserCompanyMemberships));
            Assert.Equal(1, await CountAsync(verification.ProvisioningRequestClaims));
            Assert.Equal(1, await CountAsync(verification.SecurityAuditEvents.Where(item =>
                item.EventType == "ProvisioningApplied")));
        });
    }

    [Fact]
    public async Task FailureAfterTheTransactionStarts_RollsBackEverything_AndALaterRetrySucceeds()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            var package = harness.NewPackage("rollback-admin");

            // 1) An unexpected failure while saving the company, role, user and membership together.
            var crash = new FailingSaveInterceptor(() => new InvalidOperationException("simulated crash")) { Armed = true };
            await using (var failing = harness.CreateContext(crash))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    harness.CreateService(failing).ApplyAsync(package, CancellationToken.None));
            }

            await using (var afterCrash = harness.CreateContext())
                await AssertNothingPersistedAsync(afterCrash);

            // 2) A database error on the same save takes the conflict path and must also persist nothing.
            var conflict = new FailingSaveInterceptor(() => new DbUpdateException("simulated database error")) { Armed = true };
            await using (var failing = harness.CreateContext(conflict))
            {
                var result = await harness.CreateService(failing).ApplyAsync(package, CancellationToken.None);
                Assert.False(result.Success);
                Assert.Equal("provisioning_apply_conflict", result.ErrorCode);
                Assert.Null(result.EnrollmentToken);
            }

            await using (var afterConflict = harness.CreateContext())
                await AssertNothingPersistedAsync(afterConflict);

            // 3) Nothing is stuck: the very same package (same request id and nonce) now applies once.
            await using (var retry = harness.CreateContext())
            {
                var applied = await harness.CreateService(retry).ApplyAsync(package, CancellationToken.None);
                Assert.True(applied.Success);
                Assert.False(applied.Replayed);
                Assert.NotNull(applied.EnrollmentToken);
            }

            await using var final = harness.CreateContext();
            Assert.Equal(1, await CountAsync(final.Companies));
            Assert.Equal(1, await CountAsync(final.ProvisioningRequestClaims));
        });
    }

    [Fact]
    public async Task EnrollmentToken_ExpiresIsSingleUse_AndIsReplacedOnlyWhileUnconsumed()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            var package = harness.NewPackage("token-admin");
            await using var database = harness.CreateContext();
            var service = harness.CreateService(database);

            var applied = await service.ApplyAsync(package, CancellationToken.None);
            Assert.True(applied.Success);
            var firstToken = applied.EnrollmentToken!;
            var payload = System.Text.Json.JsonSerializer.Deserialize<CustomerProvisioningPayload>(
                package.PayloadJson, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
            var requestId = Guid.Parse(payload.RequestId);

            // Expiry: 30 minutes plus one.
            harness.Clock.Advance(TimeSpan.FromMinutes(31));
            var expired = await service.CompleteEnrollmentAsync(
                "token-admin", firstToken, EnrollmentPassword, "10.0.0.1", CancellationToken.None);
            Assert.False(expired.Success);
            Assert.Equal("enrollment_expired", expired.ErrorCode);

            // An expired token can be replaced; the replaced one is revoked.
            var reissued = await service.ReissueEnrollmentTokenAsync(requestId, "expired", "10.0.0.1", CancellationToken.None);
            Assert.True(reissued.Success);
            var secondToken = reissued.EnrollmentToken!;
            Assert.NotEqual(firstToken, secondToken);
            var revoked = await service.CompleteEnrollmentAsync(
                "token-admin", firstToken, EnrollmentPassword, "10.0.0.1", CancellationToken.None);
            Assert.False(revoked.Success);

            // Single use.
            var consumed = await service.CompleteEnrollmentAsync(
                "token-admin", secondToken, EnrollmentPassword, "10.0.0.1", CancellationToken.None);
            Assert.True(consumed.Success);
            var again = await service.CompleteEnrollmentAsync(
                "token-admin", secondToken, "Another-Passphrase-2027", "10.0.0.1", CancellationToken.None);
            Assert.False(again.Success);
            var reissueAfterUse = await service.ReissueEnrollmentTokenAsync(requestId, "suspected_exposure", null, CancellationToken.None);
            Assert.False(reissueAfterUse.Success);
            Assert.Equal("enrollment_reissue_not_allowed", reissueAfterUse.ErrorCode);

            // Neither token, nor the password, ever reaches the audit trail.
            var audit = await database.SecurityAuditEvents.AsNoTracking()
                .Select(item => item.DetailsJson).ToListAsync(TestContext.Current.CancellationToken);
            foreach (var secret in new[] { firstToken, secondToken, EnrollmentPassword })
                Assert.DoesNotContain(audit, value => value?.Contains(secret, StringComparison.Ordinal) == true);

            var admin = await database.Users.AsNoTracking().SingleAsync(
                item => item.Username == "token-admin", TestContext.Current.CancellationToken);
            Assert.Equal(PasswordVerificationOutcome.Success, new PasswordHashService().Verify(admin.Password, EnrollmentPassword));
        });
    }

    [Fact]
    public async Task EnrollmentToken_CannotBeConsumedTwiceByConcurrentRequests()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            string token;
            await using (var database = harness.CreateContext())
            {
                var applied = await harness.CreateService(database).ApplyAsync(
                    harness.NewPackage("race-admin"), CancellationToken.None);
                token = applied.EnrollmentToken!;
            }

            var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
            {
                await Task.Yield();
                try
                {
                    await using var database = harness.CreateContext();
                    return (Result: await harness.CreateService(database).CompleteEnrollmentAsync(
                                "race-admin", token, $"Concurrent-Passphrase-{index}-2026", "10.0.0.2", CancellationToken.None),
                            Error: (Exception?)null);
                }
                catch (Exception exception)
                {
                    return (Result: (CustomerProvisioningEnrollmentResult?)null, Error: exception);
                }
            }));

            Assert.All(outcomes, outcome => Assert.Null(outcome.Error));
            Assert.Equal(1, outcomes.Count(outcome => outcome.Result!.Success));

            await using var verification = harness.CreateContext();
            var claim = await verification.ProvisioningRequestClaims.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.False(claim.MustChangePassword);
            Assert.NotNull(claim.EnrollmentConsumedAtUtc);
            Assert.Equal(1, await CountAsync(verification.SecurityAuditEvents.Where(item =>
                item.EventType == "ProvisioningEnrollmentCompleted")));
        });
    }

    [Fact]
    public async Task EnrollmentGuessing_IsRateLimited_WithoutConsumingTheRealToken()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using var database = harness.CreateContext();
            var service = harness.CreateService(database);
            var applied = await service.ApplyAsync(harness.NewPackage("guess-admin"), CancellationToken.None);
            var realToken = applied.EnrollmentToken!;

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var wrong = new string((char)('A' + attempt), 43);
                var result = await service.CompleteEnrollmentAsync(
                    "guess-admin", wrong, EnrollmentPassword, "10.0.0.3", CancellationToken.None);
                Assert.False(result.Success);
                Assert.Equal("enrollment_invalid", result.ErrorCode);
            }

            // The correct token is refused while the window is exhausted, and stays unused.
            var limited = await service.CompleteEnrollmentAsync(
                "guess-admin", realToken, EnrollmentPassword, "10.0.0.3", CancellationToken.None);
            Assert.False(limited.Success);
            Assert.Equal("enrollment_rate_limited", limited.ErrorCode);
            var claim = await database.ProvisioningRequestClaims.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.True(claim.MustChangePassword);
            Assert.Null(claim.EnrollmentConsumedAtUtc);

            // After the 15 minute window (and before the 30 minute expiry) it works.
            harness.Clock.Advance(TimeSpan.FromMinutes(16));
            var recovered = await service.CompleteEnrollmentAsync(
                "guess-admin", realToken, EnrollmentPassword, "10.0.0.3", CancellationToken.None);
            Assert.True(recovered.Success);
        });
    }

    [Fact]
    public async Task OperationalPermissions_AreExactlyThoseOfThePurchasedSubsystems_AndStayInsideTheirCompany()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using var database = harness.CreateContext();
            var service = harness.CreateService(database);

            Assert.True((await service.ApplyAsync(
                harness.NewPackage("finance-admin", new[] { "finance" }), CancellationToken.None)).Success);
            Assert.True((await service.ApplyAsync(
                harness.NewPackage("sales-admin", new[] { "sales", "inventory" }), CancellationToken.None)).Success);

            async Task<(Guid UserId, Guid CompanyId)> Identify(string username)
            {
                var user = await database.Users.AsNoTracking().SingleAsync(item => item.Username == username, TestContext.Current.CancellationToken);
                var membership = await database.UserCompanyMemberships.AsNoTracking()
                    .SingleAsync(item => item.UserId == user.Id, TestContext.Current.CancellationToken);
                return (user.Id, membership.TenantId);
            }

            var finance = await Identify("finance-admin");
            var sales = await Identify("sales-admin");
            Assert.NotEqual(finance.CompanyId, sales.CompanyId);

            var access = new Infrastructure.Tenancy.CompanyAccessService(database);
            async Task<bool> Has((Guid UserId, Guid CompanyId) who, string code) =>
                await access.HasPermissionAsync(who.UserId, who.CompanyId, code, TestContext.Current.CancellationToken);

            // Purchased: finance. Not purchased: everything else.
            Assert.True(await Has(finance, "finance.invoice.create"));
            Assert.True(await Has(finance, "finance.invoice.read"));
            Assert.False(await Has(finance, "sales.customer.read"));
            Assert.False(await Has(finance, "inventory.item.read"));
            Assert.False(await Has(finance, "treasury.cash.read"));

            // Purchased: sales and inventory. Not purchased: finance and treasury.
            Assert.True(await Has(sales, "sales.customer.read"));
            Assert.True(await Has(sales, "inventory.item.read"));
            Assert.False(await Has(sales, "finance.invoice.create"));
            Assert.False(await Has(sales, "treasury.cash.read"));

            // A user of one company has nothing in the other company, even for a purchased code.
            Assert.False(await access.HasPermissionAsync(
                finance.UserId, sales.CompanyId, "sales.customer.read", TestContext.Current.CancellationToken));
            Assert.False(await access.HasPermissionAsync(
                sales.UserId, finance.CompanyId, "finance.invoice.create", TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task Replay_ConflictingReuseAndTamperedPackages_NeverApplyASecondTime()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using var database = harness.CreateContext();
            var service = harness.CreateService(database);
            var requestId = Guid.NewGuid().ToString("D");
            var nonce = Guid.NewGuid().ToString("N");
            var original = harness.NewPackage("replay-admin", requestId: requestId, nonce: nonce);

            var first = await service.ApplyAsync(original, CancellationToken.None);
            Assert.True(first.Success);
            Assert.False(first.Replayed);

            // Exact same package: idempotent replay returning the stored acknowledgement, never a token.
            var replay = await service.ApplyAsync(original, CancellationToken.None);
            Assert.True(replay.Success);
            Assert.True(replay.Replayed);
            Assert.Null(replay.EnrollmentToken);
            Assert.Equal(first.Acknowledgement!.AcknowledgementJson, replay.Acknowledgement!.AcknowledgementJson);
            Assert.Equal(first.Acknowledgement.SignatureBase64, replay.Acknowledgement.SignatureBase64);

            // Same request id, different signed content: a conflict, not an overwrite.
            var sameRequestOtherContent = harness.NewPackage(
                "replay-other", requestId: requestId, nonce: Guid.NewGuid().ToString("N"));
            var conflict = await service.ApplyAsync(sameRequestOtherContent, CancellationToken.None);
            Assert.False(conflict.Success);
            Assert.Equal("provisioning_request_id_conflict", conflict.ErrorCode);

            // New request id but a nonce that was already used: replay.
            var reusedNonce = harness.NewPackage("replay-nonce", nonce: nonce);
            var replayedNonce = await service.ApplyAsync(reusedNonce, CancellationToken.None);
            Assert.False(replayedNonce.Success);
            Assert.Equal("provisioning_nonce_replayed", replayedNonce.ErrorCode);

            // A tampered payload (signature no longer matches) is refused before any state change.
            var tampered = original with { PayloadJson = original.PayloadJson.Replace("replay-admin", "replay-evil", StringComparison.Ordinal) };
            var tamperedResult = await service.ApplyAsync(tampered, CancellationToken.None);
            Assert.False(tamperedResult.Success);
            Assert.Null(tamperedResult.EnrollmentToken);

            // A foreign installation is refused too.
            var foreign = harness.Sign(System.Text.Json.JsonSerializer.Deserialize<CustomerProvisioningPayload>(
                original.PayloadJson, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })! with
            {
                RequestId = Guid.NewGuid().ToString("D"),
                Nonce = Guid.NewGuid().ToString("N"),
                InstallationId = "another-installation",
                InitialAdminUserName = "foreign-admin",
                InitialAdminEmail = "foreign-admin@example.test",
            });
            Assert.False((await service.ApplyAsync(foreign, CancellationToken.None)).Success);

            Assert.Equal(1, await CountAsync(database.Companies));
            Assert.Equal(1, await CountAsync(database.Users));
            Assert.Equal(1, await CountAsync(database.ProvisioningRequestClaims));
        });
    }
}
