using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorPersistenceIntegrationTests
{
    [Fact]
    public async Task InitialMigrationCreatesOnlyVendorIdentityAndAuditTables_AndAdminIsDisabledByDefault()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANOU_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            var message = "Set FARANOU_VENDOR_TEST_SQLSERVER to a disposable SQL Server connection. " +
                          "Use FARANOU_REQUIRE_VENDOR_SQLSERVER=1 to require this integration test in CI."; 
            if (string.Equals(Environment.GetEnvironmentVariable("FARANOU_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
            {
                Assert.Fail(message);
            }

            Assert.Skip(message);
            return;
        }

        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            // Every test gets a unique, clearly disposable database. Never point the suite at a customer database.
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        await using var db = new VendorDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.NotNull(db.Model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUserPasskey<Guid>)));

            var sqlConnection = db.Database.GetDbConnection();
            await sqlConnection.OpenAsync();
            await using (var command = sqlConnection.CreateCommand())
            {
                command.CommandText = """
                    SELECT COUNT_BIG(1)
                    FROM sys.tables
                    WHERE name IN (
                        N'VendorAdminUsers', N'VendorAdminRoles', N'VendorAdminUserClaims',
                        N'VendorAdminUserLogins', N'VendorAdminUserRoles', N'VendorAdminUserTokens',
                        N'VendorAdminRoleClaims', N'VendorAdminUserPasskeys', N'VendorAuditEvents');
                    """;
                var tableCount = Convert.ToInt64(await command.ExecuteScalarAsync());
                Assert.Equal(9L, tableCount);
            }

            var adminId = Guid.NewGuid();
            db.Users.Add(new VendorAdminUser
            {
                Id = adminId,
                UserName = "disabled-admin-test",
                NormalizedUserName = "DISABLED-ADMIN-TEST",
                Email = "disabled-admin-test@example.invalid",
                NormalizedEmail = "DISABLED-ADMIN-TEST@EXAMPLE.INVALID",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            var persistedAdmin = await db.Users.SingleAsync(user => user.Id == adminId);
            Assert.False(persistedAdmin.IsEnabled);
            Assert.True(persistedAdmin.MustChangePassword);

            // An audit reference must remain valid even if the actor row is later deleted or unavailable.
            var nonExistingActor = Guid.NewGuid();
            var auditId = Guid.NewGuid();
            db.AuditEvents.Add(new VendorAuditEvent
            {
                Id = auditId,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                ActorUserId = nonExistingActor,
                ActionCode = "test.persistence-created",
                SubjectType = "VendorAdminUser",
                SubjectId = adminId.ToString("D"),
                MetadataJson = """{"source":"integration-test"}""",
                PreviousHash = new string('0', 64),
                EventHash = new string('a', 64)
            });
            await db.SaveChangesAsync();

            var persistedAudit = await db.AuditEvents.SingleAsync(item => item.Id == auditId);
            Assert.Equal(nonExistingActor, persistedAudit.ActorUserId);
            Assert.Equal(64, persistedAudit.EventHash.Length);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
