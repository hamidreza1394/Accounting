using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

public sealed class MembershipUtcWireFormatTests
{
    private static readonly DateTime Stored = new(2026, 11, 3, 10, 15, 30, DateTimeKind.Unspecified);

    [Fact]
    public async Task MembershipResponsesHaveUtcKindAndPreserveTicks()
    {
        var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new UnconvertedMembershipContext(options);
        await VerifyResponses(database, sqlServer: false);
    }

    [Fact]
    public async Task MembershipResponsesFromSqlServerSerializeWithZAndLeaveLegacyFieldsUnchanged()
    {
        if (!IntegrationDatabase.UsesSqlServer && Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        Assert.SkipUnless(IntegrationDatabase.UsesSqlServer, "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = "FaranouTests_MUTC_" + Guid.NewGuid().ToString("N")[..12],
        };
        var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options;
        try
        {
            await using var database = new Data.DatabaseContext(options);
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await VerifyResponses(database, sqlServer: true);
        }
        finally
        {
            await using var cleanup = new Data.DatabaseContext(options);
            await cleanup.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    // Simulate SQL datetime2 materialization without relying on a provider converter.
    private sealed class UnconvertedMembershipContext(DbContextOptions<Data.DatabaseContext> options)
        : Data.DatabaseContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Domain.UserCompanyMembership>()
                .Property(item => item.CreatedAtUtc).Metadata.SetValueConverter((Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter?)null);
        }
    }

    private static async Task VerifyResponses(Data.DatabaseContext database, bool sqlServer)
    {
        var company = new Domain.Company { Name = "UTC company", IsActive = true };
        var role = new Domain.CompanyRole { Company = company, TenantId = company.Id, Code = "UTC", Name = "UTC", IsActive = true };
        var adminRole = new Domain.Role(Constants.Role.Admin) { IsActive = true };
        var admin = new Domain.User("utc-admin@example.test") { Username = "utc-admin", IsActive = true, Role = adminRole };
        var target = new Domain.User("utc-member@example.test")
        {
            Username = "utc-member", IsActive = true,
            LastLoginDateTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
        };
        var permission = sqlServer
            ? await database.PermissionDefinitions.SingleAsync(item => item.Code == "users.list")
            : new Domain.PermissionDefinition { Code = "users.list", Name = "Users" };
        database.AddRange(company, role, adminRole, admin, target);
        if (!sqlServer) database.PermissionDefinitions.Add(permission);
        database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = role, RoleId = role.Id, Permission = permission, PermissionId = permission.Id,
        });
        database.UserCompanyMemberships.AddRange(
            new Domain.UserCompanyMembership { UserId = admin.Id, TenantId = company.Id, Company = company, RoleId = role.Id, Role = role, CreatedAtUtc = Stored },
            new Domain.UserCompanyMembership { UserId = target.Id, TenantId = company.Id, Company = company, RoleId = role.Id, Role = role, CreatedAtUtc = Stored });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        if (!sqlServer)
        {
            var raw = await database.UserCompanyMemberships.AsNoTracking().SingleAsync(item => item.UserId == target.Id);
            Assert.Equal(DateTimeKind.Unspecified, raw.CreatedAtUtc.Kind);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim("AccountKind", "User")], "test"));
        var legacy = await database.Users.AsNoTracking().SingleAsync(user => user.Id == target.Id);
        var details = Assert.IsType<Ok<Server.Api.UserDetailsResponse>>(await Server.Api.UserAdministrationEndpoints.GetUserAsync(
            target.Id, new DefaultHttpContext(), principal, database, default)).Value!;
        var directory = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            company.Id, new DefaultHttpContext(), principal, database,
            new Infrastructure.Tenancy.CompanyAccessService(database), search: "utc-member")).Value!;
        var members = Assert.IsType<Ok<Server.Api.CompanyRoleMemberListResponse>>(await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
            company.Id, role.Id, null, null, new DefaultHttpContext(), principal, database, default)).Value!;

        var timestamps = new[] { Assert.Single(details.Memberships).CreatedAtUtc,
            Assert.Single(directory.Items).MembershipCreatedAtUtc,
            Assert.Single(members.Items, item => item.UserId == target.Id).MemberSinceUtc };
        foreach (var timestamp in timestamps)
        {
            Assert.Equal(DateTimeKind.Utc, timestamp.Kind);
            Assert.Equal(Stored.Ticks, timestamp.Ticks);
        }
        Assert.Equal(legacy.InsertDateTime, details.InsertDateTime);
        Assert.Equal(legacy.InsertDateTime.Kind, details.InsertDateTime.Kind);
        Assert.Equal(legacy.UpdateDateTime, details.UpdateDateTime);
        Assert.Equal(legacy.UpdateDateTime.Kind, details.UpdateDateTime.Kind);
        Assert.Equal(legacy.LastLoginDateTime, details.LastLoginDateTime);
        Assert.Equal(legacy.LastLoginDateTime!.Value.Kind, details.LastLoginDateTime!.Value.Kind);

        if (!sqlServer) return;
        using var detailsJson = JsonDocument.Parse(JsonSerializer.Serialize(details, JsonSerializerOptions.Web));
        using var directoryJson = JsonDocument.Parse(JsonSerializer.Serialize(directory, JsonSerializerOptions.Web));
        using var membersJson = JsonDocument.Parse(JsonSerializer.Serialize(members, JsonSerializerOptions.Web));
        Assert.Equal("2026-11-03T10:15:30Z", detailsJson.RootElement.GetProperty("memberships")[0].GetProperty("createdAtUtc").GetString());
        Assert.Equal("2026-11-03T10:15:30Z", directoryJson.RootElement.GetProperty("items")[0].GetProperty("membershipCreatedAtUtc").GetString());
        foreach (var member in membersJson.RootElement.GetProperty("items").EnumerateArray())
            Assert.Equal("2026-11-03T10:15:30Z", member.GetProperty("memberSinceUtc").GetString());
        foreach (var name in new[] { "insertDateTime", "updateDateTime", "lastLoginDateTime" })
        {
            var value = detailsJson.RootElement.GetProperty(name).GetString()!;
            Assert.False(value.EndsWith("Z", StringComparison.Ordinal));
            Assert.DoesNotContain("+", value);
        }
        Assert.Equal("2026-01-02T03:04:05", detailsJson.RootElement.GetProperty("lastLoginDateTime").GetString());
    }
}
