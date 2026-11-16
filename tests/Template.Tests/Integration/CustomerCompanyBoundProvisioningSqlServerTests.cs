using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Company-bound (schema v5) provisioning on a real SQL Server (Issue #392): the fixed check
/// order entitlement → duplicate → capacity, the atomic company + CompanySetupRecord insert,
/// replay determinism of every new rejection code and the count-all capacity semantics of
/// D-059 §2/§6. Each test uses its own uniquely named disposable database.
/// </summary>
public sealed class CustomerCompanyBoundProvisioningSqlServerTests
{
    private const string AcknowledgementKeyId = "customer-ack-test-key";
    private const string LicenseId = "0a5f7d20-3a2c-4c8d-9e1f-2a6b7c8d9e10";

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
                TimeProvider.System,
                NullLogger<CustomerProvisioningApplyService>.Instance);

        /// <summary>
        /// Seeds an accepted license envelope for the installation. The reference set is what the
        /// entitlement check trusts; MaxCompanies carries the numeric installation cap.
        /// </summary>
        public async Task SeedLicenseAsync(
            Data.DatabaseContext database,
            int? maxCompanies,
            string state = InstalledLicense.StateCurrent,
            int sequence = 1,
            params string[] entitledReferences)
        {
            var identity = new InstallationIdentity
            {
                InstallationId = Settings.InstallationId,
                KeyId = "identity-key-1",
                KeyVersion = 1,
                ProviderName = "Microsoft Platform Crypto Provider",
                AssuranceLevel = InstallationIdentity.AssuranceHardwareTpm,
                OsInstallationMarkerDigest = new string('a', 64),
                PublicKeyFingerprint = new string('b', 64),
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                State = InstallationIdentity.StateCurrent
            };
            database.InstallationIdentities.Add(identity);

            var payload = new LicensePayload(
                LicensePayload.CurrentFormatVersion,
                LicenseId,
                Settings.InstallationId,
                "FARANOU",
                LicensePayload.KindPerpetual,
                DateTimeOffset.UtcNow,
                null,
                null,
                0,
                1,
                new LicenseLimits(maxCompanies, new[] { "finance" }))
            {
                CompanyEntitlements = entitledReferences.Select(reference =>
                    new CompanyLicenseEntitlement(reference, new[] { "finance" }, 5, [])).ToList(),
            };
            database.InstalledLicenses.Add(new InstalledLicense
            {
                InstallationIdentityId = identity.Id,
                InstallationId = Settings.InstallationId,
                LicenseId = LicenseId,
                Sequence = sequence,
                FormatVersion = LicensePayload.CurrentFormatVersion,
                Kid = "license-key-1",
                Algorithm = LicenseAlgorithms.EcdsaP256Sha256,
                PayloadJson = CanonicalJson.SerializePayload(payload),
                SignatureBase64 = "c2lnbmF0dXJl",
                EnvelopeJson = "{}",
                PayloadDigest = new string('c', 64),
                ImportedAtUtc = DateTime.UtcNow,
                State = state
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public CustomerProvisioningPackage NewPackage(
            string adminName,
            string companyReference,
            string? requestId = null,
            string? nonce = null)
        {
            var now = DateTimeOffset.UtcNow;
            var payload = new CustomerProvisioningPayload(
                CustomerProvisioningPackageValidator.CompanyReferencePayloadSchemaVersion,
                requestId ?? Guid.NewGuid().ToString("D"),
                Settings.CustomerReference,
                Settings.InstallationId,
                "شرکت نگاشت‌شده " + adminName,
                "independent",
                adminName,
                new[] { "finance" },
                now.AddMinutes(-1),
                now.AddHours(2),
                nonce ?? Guid.NewGuid().ToString("N"),
                "سال مالی ۱۴۰۵",
                new DateOnly(2026, 3, 21),
                new DateOnly(2027, 3, 20),
                null,
                adminName + "@example.test",
                LicenseId,
                1,
                companyReference);
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

    /// <summary>Fails the final SaveChanges of a company-bound apply once armed.</summary>
    private sealed class FailingSaveInterceptor(Func<Exception> failure) : SaveChangesInterceptor
    {
        public volatile bool Armed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<CompanySetupRecord>()
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
        var name = "FaranouTests_CompanyBound_" + Guid.NewGuid().ToString("N")[..12];
        using var harness = new Harness(name);
        try
        {
            await using (var setup = harness.CreateContext())
            {
                await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
                setup.PermissionDefinitions.AddRange(
                    new PermissionDefinition { Code = "finance.invoice.create", Name = "Create invoice" });
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

    // -- tests --------------------------------------------------------------------------------------------

    [Fact]
    public async Task CompanyBoundRequest_CreatesCompanyWithAtomicMapping_AndReplaysIdentically()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
                await harness.SeedLicenseAsync(setup, maxCompanies: 3, entitledReferences: "company-ref-0001");

            var package = harness.NewPackage("mapped-admin", "company-ref-0001");
            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var result = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.True(result.Success, result.ErrorCode);
                Assert.False(result.Replayed);
                Assert.NotNull(result.Acknowledgement);
                Assert.Null(result.ErrorCode);

                Assert.Equal(1, await serviceContext.Companies.CountAsync(TestContext.Current.CancellationToken));
                var record = await serviceContext.CompanySetupRecords.SingleAsync(TestContext.Current.CancellationToken);
                var company = await serviceContext.Companies.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(company.Id, record.CompanyId);
                Assert.Equal("company-ref-0001", record.CompanyReference);
                Assert.Equal(harness.Settings.InstallationId, record.InstallationId);
                Assert.True(record.RowVersion.Length > 0);
                Assert.Equal(1, await serviceContext.ProvisioningRequestClaims.CountAsync(TestContext.Current.CancellationToken));
            }

            // An identical replay returns the stored acknowledgement and adds nothing.
            await using (var replayContext = harness.CreateContext())
            {
                var service = harness.CreateService(replayContext);
                var replay = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.True(replay.Success);
                Assert.True(replay.Replayed);
                Assert.Equal(1, await replayContext.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await replayContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }

    [Fact]
    public async Task NotEntitledReference_IsRejectedDeterministically_WithoutSideEffects()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
                await harness.SeedLicenseAsync(setup, maxCompanies: 3, entitledReferences: "company-ref-0001");

            var package = harness.NewPackage("unentitled-admin", "company-ref-0002");
            string rejectionAcknowledgementJson;
            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var result = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.False(result.Success);
                Assert.Equal("provisioning_not_entitled_for_company", result.ErrorCode);
                Assert.NotNull(result.Acknowledgement);
                rejectionAcknowledgementJson = result.Acknowledgement!.AcknowledgementJson;
                Assert.Equal(0, await serviceContext.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(0, await serviceContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(0, await serviceContext.ProvisioningRequestClaims.CountAsync(TestContext.Current.CancellationToken));
                Assert.Contains(await serviceContext.SecurityAuditEvents.ToListAsync(TestContext.Current.CancellationToken),
                    audit => audit.EventType == "ProvisioningRejected");
            }

            // The stored rejection is replayed exactly: same acknowledgement, no new side effect.
            await using (var replayContext = harness.CreateContext())
            {
                var service = harness.CreateService(replayContext);
                var replay = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.False(replay.Success);
                Assert.Equal("provisioning_request_rejected", replay.ErrorCode);
                Assert.NotNull(replay.Acknowledgement);
                Assert.Equal(rejectionAcknowledgementJson, replay.Acknowledgement.AcknowledgementJson);
            }
        });
    }

    [Fact]
    public async Task DuplicateReferenceWithNewPackage_IsRejectedAsConflict_RegardlessOfCapacity()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
                await harness.SeedLicenseAsync(setup, maxCompanies: 2, entitledReferences: "company-ref-0001");

            var first = harness.NewPackage("first-admin", "company-ref-0001");
            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var applied = await service.ApplyAsync(first, TestContext.Current.CancellationToken);
                Assert.True(applied.Success, applied.ErrorCode);

                // A new package for the same reference is a conflict even though capacity remains.
                var duplicate = harness.NewPackage("second-admin", "company-ref-0001");
                var conflict = await service.ApplyAsync(duplicate, TestContext.Current.CancellationToken);

                Assert.False(conflict.Success);
                Assert.Equal("provisioning_company_reference_conflict", conflict.ErrorCode);
                Assert.Equal(1, await serviceContext.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await serviceContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }

    [Fact]
    public async Task Capacity_CountsEveryCompanyIncludingLegacy_AndBlocksOnlyNewProvisioning()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
            {
                // A legacy company that never went through provisioning still occupies capacity.
                setup.Companies.Add(new Company { Name = "شرکت Legacy", IsActive = true });
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
                await harness.SeedLicenseAsync(setup, maxCompanies: 1, entitledReferences: "company-ref-0001");
            }

            var package = harness.NewPackage("capped-admin", "company-ref-0001");
            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var rejected = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.False(rejected.Success);
                Assert.Equal("provisioning_company_capacity_exceeded", rejected.ErrorCode);

                // Block-new: the legacy company is neither removed nor deactivated.
                var legacy = await serviceContext.Companies.SingleAsync(TestContext.Current.CancellationToken);
                Assert.True(legacy.IsActive);
                Assert.Equal(0, await serviceContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
            }

            // Raising the cap by amendment makes the same provisioning succeed.
            await using (var setup = harness.CreateContext())
            {
                var license = await setup.InstalledLicenses.SingleAsync(TestContext.Current.CancellationToken);
                setup.InstalledLicenses.Remove(license);
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
                await harness.SeedLicenseAsync(setup, maxCompanies: 2, entitledReferences: "company-ref-0001");
            }

            await using (var retryContext = harness.CreateContext())
            {
                var service = harness.CreateService(retryContext);
                // The first rejection is stored as an ACK for its request id, so a retry uses a new request.
                var retry = harness.NewPackage("capped-admin-retry", "company-ref-0001");
                var accepted = await service.ApplyAsync(retry, TestContext.Current.CancellationToken);

                Assert.True(accepted.Success, accepted.ErrorCode);
                Assert.Equal(2, await retryContext.Companies.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }

    [Fact]
    public async Task FailingSave_RollsBackCompanyAndMappingAtomically()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
                await harness.SeedLicenseAsync(setup, maxCompanies: 3, entitledReferences: "company-ref-0001");

            var package = harness.NewPackage("rollback-admin", "company-ref-0001");
            var interceptor = new FailingSaveInterceptor(() => new InvalidOperationException("forced failure"));
            await using (var serviceContext = harness.CreateContext(interceptor))
            {
                interceptor.Armed = true;
                var service = harness.CreateService(serviceContext);
                var result = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.False(result.Success);
                Assert.Equal("provisioning_apply_conflict", result.ErrorCode);
            }

            await using (var after = harness.CreateContext())
            {
                Assert.Equal(0, await after.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(0, await after.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(0, await after.ProvisioningRequestClaims.CountAsync(TestContext.Current.CancellationToken));

                // Nothing was persisted, so the same request can be applied cleanly afterwards.
                interceptor.Armed = false;
                var service = harness.CreateService(after);
                var retry = await service.ApplyAsync(package, TestContext.Current.CancellationToken);
                Assert.True(retry.Success, retry.ErrorCode);
                Assert.Equal(1, await after.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }

    [Fact]
    public async Task ConcurrentSameReferenceRequests_ProduceOneCompanyAndDeterministicConflicts()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
                await harness.SeedLicenseAsync(setup, maxCompanies: 3, entitledReferences: "company-ref-0001");

            var packages = Enumerable.Range(0, 6)
                .Select(index => harness.NewPackage($"race-admin-{index}", "company-ref-0001"))
                .ToArray();

            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var outcomes = await Task.WhenAll(packages.Select(package =>
                    service.ApplyAsync(package, TestContext.Current.CancellationToken)));

                Assert.Equal(1, await serviceContext.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await serviceContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, outcomes.Count(outcome => outcome.Success));
                Assert.All(outcomes, outcome => Assert.True(outcome.Success || outcome.ErrorCode is
                    "provisioning_company_reference_conflict" or "provisioning_apply_conflict",
                    outcome.ErrorCode));
            }
        });
    }

    [Fact]
    public async Task AbsentMaxCompanies_IsBoundedByTheSignedEntitlementList_Alone()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
            {
                // A licensed install far above any legacy size still provisions when the vendor
                // signed no numeric cap: the entitlement list is the binding boundary (D-059 §14).
                for (var index = 0; index < 5; index++)
                    setup.Companies.Add(new Company { Name = "شرکت Legacy " + index, IsActive = true });
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
                await harness.SeedLicenseAsync(setup, maxCompanies: null, entitledReferences: "company-ref-0001");
            }

            var package = harness.NewPackage("uncapped-admin", "company-ref-0001");
            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var result = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.True(result.Success, result.ErrorCode);
                Assert.Equal(6, await serviceContext.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await serviceContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }

    [Fact]
    public async Task SupersededRevision_IsNotTrustedForEntitlements()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithHarnessAsync(async harness =>
        {
            await using (var setup = harness.CreateContext())
            {
                // The superseded revision entitles the reference; the current one does not.
                // Amendments never change company references, but a re-import with an empty
                // entitlement list is a legitimately narrower current revision.
                await harness.SeedLicenseAsync(setup, maxCompanies: 5,
                    state: InstalledLicense.StateSuperseded, sequence: 1, entitledReferences: "company-ref-0001");
                await harness.SeedLicenseAsync(setup, maxCompanies: 5,
                    state: InstalledLicense.StateCurrent, sequence: 2);
            }

            var package = harness.NewPackage("superseded-admin", "company-ref-0001");
            await using (var serviceContext = harness.CreateContext())
            {
                var service = harness.CreateService(serviceContext);
                var result = await service.ApplyAsync(package, TestContext.Current.CancellationToken);

                Assert.False(result.Success);
                Assert.Equal("provisioning_not_entitled_for_company", result.ErrorCode);
                Assert.Equal(0, await serviceContext.CompanySetupRecords.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }
}
