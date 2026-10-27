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
    private const string PasskeyMigration = "20261026045412_VendorAddPasskeySupport";
    private const string IdentitySchemaV3Migration = "20261026084500_VendorAlignIdentitySchemaV3";

    [Fact]
    public void Model_uses_vendor_specific_tables_and_disables_admins_by_default()
    {
        using var provider = VendorTestServiceProviderFactory.Create(
            "Server=localhost;Database=VendorModelOnly;Integrated Security=True;TrustServerCertificate=True");
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var model = context.Model;

        Assert.Equal("VendorAdminUsers", model.FindEntityType(typeof(VendorAdminUser))!.GetTableName());
        Assert.Equal("VendorAuditEvents", model.FindEntityType(typeof(VendorAuditEvent))!.GetTableName());
        Assert.Equal("VendorApprovalRequests", model.FindEntityType(typeof(Faranou.Vendor.Persistence.Domain.VendorApprovalRequest))!.GetTableName());

        var approvalModel = model.FindEntityType(typeof(Faranou.Vendor.Persistence.Domain.VendorApprovalRequest));
        Assert.NotNull(approvalModel);
        Assert.Contains(approvalModel!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["ResourceType", "ResourceId"]) &&
            index.IsUnique && index.GetFilter() == "[Status] = N'Pending'");
        Assert.True(approvalModel.FindProperty("RowVersion")!.IsConcurrencyToken);

        var enabled = model.FindEntityType(typeof(VendorAdminUser))!.FindProperty(nameof(VendorAdminUser.IsEnabled));
        Assert.NotNull(enabled);
        Assert.Equal(false, enabled!.GetDefaultValue());

        var audit = model.FindEntityType(typeof(VendorAuditEvent));
        Assert.NotNull(audit);
        Assert.Contains(audit!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorAuditEvent.PreviousHash)]) &&
            !index.IsUnique);
        Assert.Contains(audit.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorAuditEvent.EventHash)]) &&
            index.IsUnique);
        Assert.DoesNotContain(
            audit.GetForeignKeys(),
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
        await using var provider = VendorTestServiceProviderFactory.Create(testConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
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
                    N'VendorAdminUserTokens', N'VendorAuditEvents', N'VendorAdminUserPasskeys'
                );
                """;

            Assert.Equal(9, Convert.ToInt32(await command.ExecuteScalarAsync()));
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            Assert.Contains(InitialMigration, appliedMigrations);
            Assert.Contains(PasskeyMigration, appliedMigrations);
            Assert.Contains(IdentitySchemaV3Migration, appliedMigrations);
            Assert.Contains("20261027190000_VendorAuditChainIndexes", appliedMigrations);
            Assert.Contains("20261027193000_VendorApprovalRequests", appliedMigrations);

            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.tables
                WHERE name = N'VendorApprovalRequests';
                """;
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));

            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.indexes AS i
                INNER JOIN sys.tables AS t ON t.object_id = i.object_id
                WHERE t.name = N'VendorApprovalRequests'
                  AND i.name = N'IX_VendorApprovalRequests_ResourceType_ResourceId'
                  AND i.is_unique = 1
                  AND i.has_filter = 1;
                """;
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));

            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.indexes AS i
                INNER JOIN sys.tables AS t ON t.object_id = i.object_id
                WHERE t.name = N'VendorAuditEvents'
                  AND i.name IN (N'IX_VendorAuditEvents_EventHash', N'IX_VendorAuditEvents_PreviousHash');
                """;
            Assert.Equal(2, Convert.ToInt32(await command.ExecuteScalarAsync()));

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
            await context.Database.CloseConnectionAsync();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public void Business_records_use_sql_server_rowversion_concurrency_tokens()
    {
        using var provider = VendorTestServiceProviderFactory.Create(
            "Server=localhost;Database=VendorModelOnly;Integrated Security=True;TrustServerCertificate=True");
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VendorDbContext>();

        foreach (var entityType in new[]
        {
            typeof(Faranou.Vendor.Persistence.Domain.VendorCustomer),
            typeof(Faranou.Vendor.Persistence.Domain.VendorInstallation),
            typeof(Faranou.Vendor.Persistence.Domain.VendorContract),
            typeof(Faranou.Vendor.Persistence.Domain.VendorLicense)
        })
        {
            var property = context.Model.FindEntityType(entityType)!.FindProperty("RowVersion");
            Assert.NotNull(property);
            Assert.True(property!.IsConcurrencyToken);
            Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
            Assert.Equal("rowversion", property.GetColumnType());
        }
    }

}
