using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Faranou.Licensing.Contracts;
using Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Infrastructure.Security;
using Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Validates the customer provisioning claim migration and SQL Server constraints.
/// Each test uses a uniquely named disposable database; it never touches the shared test database.
/// </summary>
public sealed class CustomerProvisioningClaimsSqlServerTests
{
    private const string PreviousMigration = "20261023191000_AddCompanyCustomerBoundary";
    private const string ClaimsMigration = "20261029161402_AddCustomerProvisioningClaims";

    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;

        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");

        return false;
    }

    private static Data.DatabaseContext CreateContext(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString)
        {
            InitialCatalog = databaseName,
        };

        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var name = "FaranouTests_Provisioning_" + Guid.NewGuid().ToString("N")[..12];
        try
        {
            await test(name);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Migration_IsReversibleAndDatabaseRejectsReplayAndCrossCompanyClaims()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            Guid companyId;
            Guid periodId;
            await using (var before = CreateContext(name))
            {
                await before.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration, TestContext.Current.CancellationToken);

                var company = new Company { Name = "شرکت آزمایشی provisioning", IsActive = true };
                before.Companies.Add(company);
                await before.SaveChangesAsync(TestContext.Current.CancellationToken);
                companyId = company.Id;

                var period = new FiscalPeriod
                {
                    TenantId = company.Id,
                    Title = "سال مالی آزمایشی",
                    StartDate = new DateOnly(2026, 3, 21),
                    EndDate = new DateOnly(2027, 3, 20),
                };
                before.FiscalPeriods.Add(period);
                await before.SaveChangesAsync(TestContext.Current.CancellationToken);
                periodId = period.Id;
            }

            Guid requestId = Guid.NewGuid();
            var nonceHash = Hash("nonce-one");
            await using (var context = CreateContext(name))
            {
                await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));

                var claim = NewClaim(requestId, nonceHash, companyId, periodId);
                context.ProvisioningRequestClaims.Add(claim);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                var persisted = await context.ProvisioningRequestClaims.SingleAsync(
                    item => item.RequestId == requestId, TestContext.Current.CancellationToken);
                Assert.Equal(nonceHash, persisted.NonceHash);
                Assert.Equal(8, persisted.RowVersion.Length);
                Assert.True(persisted.MustChangePassword);
                Assert.Null(persisted.EnrollmentConsumedAtUtc);

                context.ChangeTracker.Clear();
                context.ProvisioningRequestClaims.Add(NewClaim(
                    requestId, Hash("nonce-two"), companyId, periodId, Hash("token-two")));
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken));

                context.ChangeTracker.Clear();
                context.ProvisioningRequestClaims.Add(NewClaim(
                    Guid.NewGuid(), nonceHash, companyId, periodId, Hash("token-three")));
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken));

                context.ChangeTracker.Clear();
                context.ProvisioningRequestClaims.Add(NewClaim(
                    Guid.NewGuid(), Hash("nonce-four"), companyId, periodId, Hash("token-four"),
                    parentCompanyId: Guid.NewGuid()));
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken));

                context.ChangeTracker.Clear();
                await context.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration, TestContext.Current.CancellationToken);
            }

            await using (var afterDown = CreateContext(name))
            {
                Assert.Equal(1, await afterDown.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await afterDown.FiscalPeriods.CountAsync(TestContext.Current.CancellationToken));
                var tables = await afterDown.Database.SqlQueryRaw<string>(
                    "SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'ProvisioningRequestClaims'")
                    .ToListAsync(TestContext.Current.CancellationToken);
                Assert.Empty(tables);

                await afterDown.Database.MigrateAsync(TestContext.Current.CancellationToken);
                Assert.Empty(await afterDown.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await afterDown.Companies.CountAsync(TestContext.Current.CancellationToken));
                Assert.Equal(1, await afterDown.FiscalPeriods.CountAsync(TestContext.Current.CancellationToken));
                Assert.Empty(await afterDown.ProvisioningRequestClaims.ToListAsync(TestContext.Current.CancellationToken));
            }
        });
    }

    [Fact]
    public async Task ParentResolver_UsesOnlyAppliedClaimWithinExactCustomerInstallationAndBoundary()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using (var setup = CreateContext(name))
            {
                await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
                var boundaryId = Guid.NewGuid();
                var parentCompany = new Company
                {
                    Name = "شرکت مادر",
                    CustomerBoundaryId = boundaryId,
                    IsActive = true
                };
                setup.Companies.Add(parentCompany);
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

                var period = new FiscalPeriod
                {
                    TenantId = parentCompany.Id,
                    Title = "سال مالی مادر",
                    StartDate = new DateOnly(2026, 3, 21),
                    EndDate = new DateOnly(2027, 3, 20)
                };
                setup.FiscalPeriods.Add(period);
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

                var requestId = Guid.NewGuid();
                var claim = NewClaim(requestId, Hash("parent-nonce"), parentCompany.Id, period.Id);
                claim.CompanyType = "parent";
                claim.CustomerReference = "customer-ref";
                claim.InstallationId = "installation-ref";
                setup.ProvisioningRequestClaims.Add(claim);
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

                var settings = new Infrastructure.Provisioning.CustomerProvisioningSettings
                {
                    CustomerReference = "customer-ref",
                    InstallationId = "installation-ref",
                    CustomerBoundaryId = boundaryId
                };
                var resolved = await Infrastructure.Provisioning.CustomerProvisioningParentClaimResolver.ResolveAsync(
                    setup, "subsidiary", requestId.ToString("D"), settings,
                    DateTime.UtcNow, TestContext.Current.CancellationToken);

                Assert.Null(resolved.ErrorCode);
                Assert.Equal(parentCompany.Id, resolved.ParentCompanyId);

                var wrongBoundary = new Infrastructure.Provisioning.CustomerProvisioningSettings
                {
                    CustomerReference = settings.CustomerReference,
                    InstallationId = settings.InstallationId,
                    CustomerBoundaryId = Guid.NewGuid()
                };
                var rejected = await Infrastructure.Provisioning.CustomerProvisioningParentClaimResolver.ResolveAsync(
                    setup, "subsidiary", requestId.ToString("D"), wrongBoundary,
                    DateTime.UtcNow, TestContext.Current.CancellationToken);
                Assert.Null(rejected.ParentCompanyId);
                Assert.Equal(
                    Infrastructure.Provisioning.CustomerProvisioningParentClaimResolver.InvalidParentReferenceError,
                    rejected.ErrorCode);
            }
        });
    }

    [Fact]
    public async Task ApplyIsAtomicIdempotentAndEnrollmentTokenCanBeReissuedAndConsumedOnce()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);

            var boundaryId = Guid.NewGuid();
            var settings = new CustomerProvisioningSettings
            {
                CustomerReference = "customer-ref",
                InstallationId = "installation-ref",
                CustomerBoundaryId = boundaryId
            };
            database.PermissionDefinitions.AddRange(
                new PermissionDefinition { Code = "finance.invoice.create", Name = "Create invoice" },
                new PermissionDefinition { Code = "sales.customer.read", Name = "Read customer" });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            using var vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var acknowledgementKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Provisioning:TrustedVendorKeys:vendor-key-1"] =
                        Convert.ToBase64String(vendorKey.ExportSubjectPublicKeyInfo())
                })
                .Build();
            var verifier = new CustomerProvisioningPackageVerifier(
                new ConfigurationCustomerProvisioningTrustStore(configuration),
                Options.Create(settings),
                TimeProvider.System);
            var signer = new TestAcknowledgementSigner(acknowledgementKey);
            var passwordHasher = new PasswordHashService();
            var service = new CustomerProvisioningApplyService(
                database, verifier, signer, Options.Create(settings), passwordHasher, TimeProvider.System);

            var now = DateTimeOffset.UtcNow;
            var payload = new CustomerProvisioningPayload(
                3,
                Guid.NewGuid().ToString("D"),
                settings.CustomerReference,
                settings.InstallationId,
                "شرکت راه‌اندازی‌شده",
                "parent",
                "initial-admin",
                new[] { "finance" },
                now.AddMinutes(-1),
                now.AddHours(1),
                "0123456789abcdefghijklmnopqrstuv",
                "سال مالی ۱۴۰۵",
                new DateOnly(2026, 3, 21),
                new DateOnly(2027, 3, 20),
                null,
                "admin@example.test");
            var payloadJson = CustomerProvisioningCanonicalJson.Serialize(payload);
            var signature = vendorKey.SignData(
                Encoding.UTF8.GetBytes(payloadJson),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var package = new CustomerProvisioningPackage(
                CustomerProvisioningPackageValidator.CurrentFormatVersion,
                "vendor-key-1",
                LicenseAlgorithms.EcdsaP256Sha256,
                payloadJson,
                Convert.ToBase64String(signature));

            var applied = await service.ApplyAsync(package, TestContext.Current.CancellationToken);
            Assert.True(applied.Success);
            Assert.False(applied.Replayed);
            Assert.NotNull(applied.EnrollmentToken);
            Assert.NotNull(applied.Acknowledgement);
            Assert.Equal(43, applied.EnrollmentToken!.Length);

            var company = await database.Companies.SingleAsync(
                item => item.Name == payload.CompanyName, TestContext.Current.CancellationToken);
            Assert.Equal(boundaryId, company.CustomerBoundaryId);
            Assert.True(company.IsActive);
            Assert.Equal(1, await database.FiscalPeriods.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await database.Users.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await database.UserCompanyMemberships.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await database.CompanyRoles.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await database.CompanyRolePermissions.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await database.CorporateGroupMembers.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await database.OwnershipRelationships.CountAsync(TestContext.Current.CancellationToken));

            var claim = await database.ProvisioningRequestClaims.SingleAsync(
                item => item.RequestId == Guid.Parse(payload.RequestId), TestContext.Current.CancellationToken);
            Assert.Equal(applied.EnrollmentToken is null ? string.Empty : Hash(applied.EnrollmentToken), claim.EnrollmentTokenHash);
            Assert.True(claim.MustChangePassword);
            Assert.Null(claim.EnrollmentConsumedAtUtc);
            Assert.True(claim.EnrollmentExpiresAtUtc <= DateTime.UtcNow.AddMinutes(31));

            var auditJson = await database.SecurityAuditEvents.Select(item => item.DetailsJson)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(auditJson, value => value?.Contains(applied.EnrollmentToken, StringComparison.Ordinal) == true);

            var ackHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(applied.Acknowledgement!.AcknowledgementJson)))
                .ToLowerInvariant();
            Assert.True(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
                applied.Acknowledgement,
                acknowledgementKey.ExportSubjectPublicKeyInfo(),
                payload.RequestId,
                payload.Nonce,
                ackHash,
                payload.CustomerReference,
                payload.InstallationId,
                DateTimeOffset.UtcNow,
                out var ack,
                out var ackError), ackError);
            Assert.Equal("Applied", ack!.Result);

            var reissued = await service.ReissueEnrollmentTokenAsync(
                Guid.Parse(payload.RequestId), "delivery_lost", null, TestContext.Current.CancellationToken);
            Assert.True(reissued.Success);
            Assert.NotNull(reissued.EnrollmentToken);
            Assert.NotEqual(applied.EnrollmentToken, reissued.EnrollmentToken);

            var oldToken = await service.CompleteEnrollmentAsync(
                payload.InitialAdminUserName, applied.EnrollmentToken, "Strong-Passphrase-2026",
                null, TestContext.Current.CancellationToken);
            Assert.False(oldToken.Success);

            var enrolled = await service.CompleteEnrollmentAsync(
                payload.InitialAdminUserName, reissued.EnrollmentToken, "Strong-Passphrase-2026",
                null, TestContext.Current.CancellationToken);
            Assert.True(enrolled.Success);

            var admin = await database.Users.SingleAsync(
                item => item.Username == payload.InitialAdminUserName, TestContext.Current.CancellationToken);
            Assert.Equal(PasswordVerificationOutcome.Success,
                passwordHasher.Verify(admin.Password, "Strong-Passphrase-2026"));
            var consumedClaim = await database.ProvisioningRequestClaims.SingleAsync(
                item => item.RequestId == Guid.Parse(payload.RequestId), TestContext.Current.CancellationToken);
            Assert.False(consumedClaim.MustChangePassword);
            Assert.NotNull(consumedClaim.EnrollmentConsumedAtUtc);

            var replayed = await service.ApplyAsync(package, TestContext.Current.CancellationToken);
            Assert.True(replayed.Success);
            Assert.True(replayed.Replayed);
            Assert.Null(replayed.EnrollmentToken);
            Assert.Equal(1, await database.Companies.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await database.Users.CountAsync(TestContext.Current.CancellationToken));
        });
    }

    private sealed class TestAcknowledgementSigner(ECDsa key)
        : ICustomerProvisioningAcknowledgementSigningProvider
    {
        public Task<CustomerProvisioningAcknowledgementSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalAcknowledgementUtf8,
            CancellationToken cancellationToken)
        {
            var signature = key.SignData(
                canonicalAcknowledgementUtf8.Span,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return Task.FromResult(new CustomerProvisioningAcknowledgementSigningResult(
                true, "customer-ack-test-key", Convert.ToBase64String(signature), null));
        }
    }

    private static ProvisioningRequestClaim NewClaim(
        Guid requestId,
        string nonceHash,
        Guid companyId,
        Guid fiscalPeriodId,
        string? enrollmentTokenHash = null,
        Guid? parentCompanyId = null)
    {
        var now = DateTime.UtcNow;
        return new ProvisioningRequestClaim
        {
            RequestId = requestId,
            NonceHash = nonceHash,
            PayloadSha256 = Hash("payload-" + requestId.ToString("N")),
            CompanyType = parentCompanyId is null ? "independent" : "subsidiary",
            ParentCompanyId = parentCompanyId,
            SubsystemsJson = "[\"finance\"]",
            CustomerReference = "customer-test",
            InstallationId = "installation-test",
            CompanyId = companyId,
            FiscalPeriodId = fiscalPeriodId,
            AdminUserId = Guid.NewGuid(),
            EnrollmentTokenHash = enrollmentTokenHash ?? Hash("token-" + requestId.ToString("N")),
            EnrollmentExpiresAtUtc = now.AddHours(1),
            AppliedAtUtc = now,
            MustChangePassword = true,
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
