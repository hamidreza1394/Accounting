using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorPersistenceTests
{
    [Fact]
    public void Model_keeps_all_vendor_entities_in_vendor_schema()
    {
        using var context = CreateModelOnlyContext();

        var entityTypes = context.Model.GetEntityTypes().ToArray();

        Assert.NotEmpty(entityTypes);
        Assert.All(entityTypes, entity => Assert.Equal("vendor", entity.GetSchema()));
        Assert.Contains(entityTypes, entity => entity.ClrType == typeof(VendorAdminUser));
        Assert.Contains(entityTypes, entity => entity.ClrType == typeof(VendorAuditEvent));
    }

    [Fact]
    public void Audit_events_do_not_have_a_cascading_user_foreign_key()
    {
        using var context = CreateModelOnlyContext();

        var auditEntity = context.Model.FindEntityType(typeof(VendorAuditEvent));
        Assert.NotNull(auditEntity);
        Assert.DoesNotContain(
            auditEntity!.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(VendorAdminUser));
    }

    [Fact]
    public void Persistence_registration_fails_closed_without_its_own_connection_string()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddVendorPersistence(configuration));

        Assert.Contains("FARANO_VENDOR_ConnectionStrings__VendorDb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sql_server_migration_creates_isolated_identity_and_audit_tables()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"),
                    "1",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "CI requires FARANO_VENDOR_TEST_SQLSERVER; the SQL Server integration test must not be silently skipped.");
            }

            return;
        }

        var connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new VendorDbContext(options);
        try
        {
            await context.Database.MigrateAsync();

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.tables AS t
                INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                WHERE s.name = N'vendor'
                  AND t.name IN
                  (
                      N'AdminUsers', N'AdminRoles', N'AdminUserClaims',
                      N'AdminRoleClaims', N'AdminUserLogins', N'AdminUserRoles',
                      N'AdminUserTokens', N'AuditEvents'
                  );
                """;

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            Assert.Equal(8, count);

            var migration = await context.Database.GetAppliedMigrationsAsync();
            Assert.Contains("20261026090000_InitialVendorPersistence", migration);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static VendorDbContext CreateModelOnlyContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer("Server=localhost;Database=VendorModelOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        return new VendorDbContext(options);
    }
}
