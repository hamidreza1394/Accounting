using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
