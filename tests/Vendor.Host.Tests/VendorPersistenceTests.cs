using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorPersistenceTests
{
    private const string InitialMigration = "20261025180755_VendorInitialIdentityAndAudit";

    [Fact]
    public void Model_uses_vendor_specific_tables_and_disables_admins_by_default()
    {
        using var context = CreateModelOnlyContext();
        var model = context.Model;

        Assert.Equal("VendorAdminUsers", model.FindEntityType(typeof(VendorAdminUser))!.GetTableName());
        Assert.Equal("VendorAuditEvents", model.FindEntityType(typeof(VendorAuditEvent))!.GetTableName());

        var enabled = model.FindEntityType(typeof(VendorAdminUser))!.FindProperty(nameof(VendorAdminUser.IsEnabled));
        Assert.NotNull(enabled);
        Assert.Equal(false, enabled!.GetDefaultValue());

        var audit = model.FindEntityType(typeof(VendorAuditEvent));
        Assert.NotNull(audit);
        Assert.DoesNotContain(
            audit!.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(VendorAdminUser));
    }

    [Fact]
    public void Persistence_fails_closed_when_vendor_connection_string_is_missing()
    {
        var services = new ServiceCollection();
        services.AddVendorPersistence(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<VendorDbContext>());

        Assert.Contains("FARANO_VENDOR_ConnectionStrings__VendorDb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Persistence_registers_Identity_stores_without_opening_a_connection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VendorDb"] =
                    "Server=localhost;Database=VendorStoreRegistration;User Id=sa;Password=CiOnly_NotASecret_123!;TrustServerCertificate=True"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVendorPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IUserStore<VendorAdminUser>>());
        Assert.NotNull(provider.GetRequiredService<IRoleStore<IdentityRole<Guid>>>());
    }

    [Fact]
    public async Task Migration_creates_vendor_identity_and_audit_tables_on_sql_server()
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

        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        var dataSource = baseBuilder.DataSource.Trim();
        if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            dataSource = dataSource[4..];
        }

        var serverName = dataSource.Split(',', StringSplitOptions.TrimEntries)[0]
            .Split('\\', StringSplitOptions.TrimEntries)[0];
        if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(serverName, StringComparer.OrdinalIgnoreCase)
            || !string.Equals(baseBuilder.InitialCatalog, "FaranouVendorTests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Vendor integration tests only accept a loopback SQL Server and the dedicated FaranouVendorTests catalog. A unique test database is created and deleted.");
        }

        var testConnectionString = new SqlConnectionStringBuilder(baseBuilder.ConnectionString)
        {
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(testConnectionString, sql =>
                sql.MigrationsAssembly(typeof(VendorDbContext).Assembly.FullName))
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
                FROM sys.tables
                WHERE name IN
                (
                    N'VendorAdminUsers', N'VendorAdminRoles', N'VendorAdminUserClaims',
                    N'VendorAdminRoleClaims', N'VendorAdminUserLogins', N'VendorAdminUserRoles',
                    N'VendorAdminUserTokens', N'VendorAuditEvents'
                );
                """;

            Assert.Equal(8, Convert.ToInt32(await command.ExecuteScalarAsync()));
            Assert.Contains(InitialMigration, await context.Database.GetAppliedMigrationsAsync());

            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.columns AS c
                INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                WHERE t.name = N'VendorAdminUsers' AND c.name = N'IsEnabled';
                """;
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
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
