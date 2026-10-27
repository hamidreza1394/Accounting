using System.Security.Claims;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorResourceAuthorizationIntegrationTests
{
    [Fact]
    public async Task LicenseResourceEvaluatorRejectsCrossCustomerContractAndAllowsMatchingParent()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable SQL Server connection. Use FARANO_REQUIRE_VENDOR_SQLSERVER=1 to require this integration test in CI.";
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail(message);
            Assert.Skip(message);
            return;
        }

        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        Assert.Contains("localhost", baseBuilder.DataSource, StringComparison.OrdinalIgnoreCase);
        var databaseName = $"FaranouVendorTests_{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateDatabaseAsync(baseConnectionString, databaseName, cancellationToken);
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };

        await using var provider = VendorTestServiceProviderFactory.Create(connection.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
            Assert.Contains("20261027060000_VendorBusinessDomain", await db.Database.GetAppliedMigrationsAsync(cancellationToken));

            var customerA = new VendorCustomer
            {
                Id = Guid.NewGuid(), CustomerNumber = "A-" + Guid.NewGuid().ToString("N")[..12],
                LegalName = "Customer A", DisplayName = "Customer A", Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            var customerB = new VendorCustomer
            {
                Id = Guid.NewGuid(), CustomerNumber = "B-" + Guid.NewGuid().ToString("N")[..12],
                LegalName = "Customer B", DisplayName = "Customer B", Status = "Active",
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            var contractA = new VendorContract
            {
                Id = Guid.NewGuid(), CustomerId = customerA.Id, Customer = customerA,
                ContractNumber = "C-" + Guid.NewGuid().ToString("N")[..12], Status = "Active",
                StartsAtUtc = DateTimeOffset.UtcNow, CurrencyCode = "EUR", CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var validLicense = new VendorLicense
            {
                Id = Guid.NewGuid(), CustomerId = customerA.Id, Customer = customerA,
                ContractId = contractA.Id, Contract = contractA,
                LicenseNumber = "L-" + Guid.NewGuid().ToString("N")[..12], ProductCode = "ACCOUNTING",
                Status = "Active", ValidFromUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var mismatchedLicense = new VendorLicense
            {
                Id = Guid.NewGuid(), CustomerId = customerB.Id, Customer = customerB,
                ContractId = contractA.Id, Contract = contractA,
                LicenseNumber = "L-" + Guid.NewGuid().ToString("N")[..12], ProductCode = "ACCOUNTING",
                Status = "Active", ValidFromUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            db.Customers.AddRange(customerA, customerB);
            db.Contracts.Add(contractA);
            db.Licenses.AddRange(validLicense, mismatchedLicense);
            await db.SaveChangesAsync(cancellationToken);

            var evaluator = new VendorResourceAccessEvaluator(db);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")) }, "integration-test"));

            Assert.True(await evaluator.HasAccessAsync(principal,
                new VendorResourceDescriptor("license", validLicense.Id, customerA.Id),
                VendorPermissionCatalog.LicensesRead, cancellationToken));
            Assert.False(await evaluator.HasAccessAsync(principal,
                new VendorResourceDescriptor("license", mismatchedLicense.Id, customerB.Id),
                VendorPermissionCatalog.LicensesRead, cancellationToken));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            await db.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static async Task CreateDatabaseAsync(string baseConnectionString, string databaseName, CancellationToken cancellationToken)
    {
        if (!databaseName.StartsWith("FaranouVendorTests_", StringComparison.Ordinal) ||
            databaseName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new InvalidOperationException("Refusing to create a non-test Vendor database.");

        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
