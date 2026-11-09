using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the D-078 provisioning behavior against real SQL Server: the nullable role
/// column, the (UserId, TenantId) unique index as the final duplicate/race guard, the
/// composite role foreign key, the atomicity of the user+membership save and the
/// fail-closed access of a roleless member.
/// </summary>
public sealed class CompanyUserProvisioningSqlServerTests
{
    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static Data.DatabaseContext CreateContext(
        string databaseName,
        SaveChangesInterceptor? interceptor = null)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        };
        var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName));
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new Data.DatabaseContext(options.Options);
    }

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var name = "FaranouTests_Prov_" + Guid.NewGuid().ToString("N")[..12];
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

    private sealed record Seed(Guid CompanyId, Guid ManagerId, Guid RoleId, Guid PermissionId);

    private static async Task<Seed> SeedCompanyManagerAsync(
        Data.DatabaseContext database, string roleCode = "MANAGER")
    {
        var company = new Domain.Company { Name = "شرکت آزمون " + Guid.NewGuid().ToString("N")[..6], IsActive = true };
        // The migrated database already holds the seeded users.create permission (unique
        // code): reuse that definition and only insert one when it is genuinely absent.
        var cancellationToken = TestContext.Current.CancellationToken;
        // Tracked on purpose: an untracked (detached) permission reached from an Added
        // CompanyRolePermission is graph-discovered as a second INSERT and dies on the
        // primary key; a tracked entity stays Unchanged.
        var permission = await database.PermissionDefinitions
            .FirstOrDefaultAsync(item =>
                item.Code == Server.Api.CompanyUserProvisioningEndpoints.CreateUserPermissionCode,
                cancellationToken);
        var role = new Domain.CompanyRole
        { Company = company, TenantId = company.Id, Code = roleCode, Name = "مدیر شرکت", IsActive = true };
        var manager = new Domain.User($"manager-{Guid.NewGuid():N}"[..20].Replace('-', 'a') + "@example.test")
        {
            Username = ("m" + Guid.NewGuid().ToString("N"))[..20],
            IsActive = true,
            Password = "private-password",
        };
        if (permission is null)
        {
            permission = new Domain.PermissionDefinition
            { Code = Server.Api.CompanyUserProvisioningEndpoints.CreateUserPermissionCode, Name = "ایجاد کاربر برای شرکت" };
            database.Add(permission);
        }
        database.AddRange(company, role, manager);
        database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        { Role = role, RoleId = role.Id, Permission = permission, PermissionId = permission.Id });
        database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = manager.Id,
            TenantId = company.Id,
            Company = company,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
        });
        await database.SaveChangesAsync(cancellationToken);
        return new Seed(company.Id, manager.Id, role.Id, permission.Id);
    }

    [Fact]
    public async Task Migration_AllowsRolelessMemberships_WhileKeepingConstraints()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
        await WithDatabaseAsync(async name =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت قیدها", IsActive = true };
            var role = new Domain.CompanyRole
            { Company = company, TenantId = company.Id, Code = "R1", Name = "نقش", IsActive = true };
            var withRole = new Domain.User("with.role@example.test")
            { Username = "withrole1", IsActive = true, Password = "private-password" };
            var withoutRole = new Domain.User("no.role@example.test")
            { Username = "norole01", IsActive = true, Password = "private-password" };
            database.AddRange(company, role, withRole, withoutRole);
            database.UserCompanyMemberships.AddRange(
                new Domain.UserCompanyMembership
                { UserId = withRole.Id, TenantId = company.Id, Company = company, RoleId = role.Id, Role = role, IsActive = true },
                new Domain.UserCompanyMembership
                { UserId = withoutRole.Id, TenantId = company.Id, Company = company, RoleId = null, Role = null, IsActive = true });
            await database.SaveChangesAsync(cancellationToken);

            // The unique index is the final duplicate guard.
            var duplicate = new Domain.UserCompanyMembership
            { UserId = withRole.Id, TenantId = company.Id, Company = company, RoleId = null, Role = null, IsActive = true };
            database.UserCompanyMemberships.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(cancellationToken));
            database.Entry(duplicate).State = EntityState.Detached;

            // The composite (RoleId, TenantId) foreign key still binds the role to the company.
            var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
            database.Add(otherCompany);
            await database.SaveChangesAsync(cancellationToken);
            database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            { UserId = withoutRole.Id, TenantId = otherCompany.Id, Company = otherCompany, RoleId = role.Id, Role = role, IsActive = true });
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(cancellationToken));

            // The roleless member has no operational access (fail-closed), server-side.
            var access = new Infrastructure.Tenancy.CompanyAccessService(database);
            Assert.False(await access.HasPermissionAsync(withoutRole.Id, company.Id, "users.list"));
            Assert.Empty(await access.GetAccessibleCompaniesAsync(withoutRole.Id));
        });
    }

    [Fact]
    public async Task Provisioning_CreatesRolelessMembership_OnSqlServer()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
        await WithDatabaseAsync(async name =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);
            var seed = await SeedCompanyManagerAsync(database);

            var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
                seed.CompanyId, new DefaultHttpContext(), Principal(seed.ManagerId),
                new Server.Api.CreateUserRequest(
                    "member01", "عضو", "تازه", "member01@example.test", null,
                    "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true),
                new AllowAntiforgery(), database, new Infrastructure.Security.PasswordHashService(),
                new Infrastructure.Tenancy.CompanyAccessService(database),
                TimeProvider.System, cancellationToken);

            var created = Assert.IsType<Created<Server.Api.CompanyUserProvisionedResponse>>(result);
            Assert.True(created.Value.UserCreated);

            var membership = await database.UserCompanyMemberships
                .SingleAsync(item => item.UserId == created.Value.UserId, cancellationToken);
            Assert.True(membership.IsActive);
            Assert.True(membership.RoleId is null, "a provisioned member starts without a role (D-078).");

            var user = await database.Users.SingleAsync(item => item.Id == created.Value.UserId, cancellationToken);
            Assert.DoesNotContain("A-Long-Unique-Phrase-1406!", user.Password);

            var audits = await database.SecurityAuditEvents
                .Where(item => item.EntityId == user.Id.ToString())
                .Select(item => item.EventType)
                .ToListAsync(cancellationToken);
            Assert.Contains("UserCreated", audits);
            Assert.Contains("UserMembershipCreated", audits);
        });
    }

    [Fact]
    public async Task Provisioning_MembershipRace_IsWonExactlyOnce_AndFailsClosed()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
        await WithDatabaseAsync(async name =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);
            var seed = await SeedCompanyManagerAsync(database);

            var existing = new Domain.User("known.member@example.test")
            { Username = "knownmember01", IsActive = true, Password = "private-password" };
            database.Add(existing);
            await database.SaveChangesAsync(cancellationToken);

            // A competing request wins the unique index while the endpoint is saving.
            var interceptor = new ConcurrentMembershipInsertInterceptor(name, existing.Id, seed.CompanyId);

            await using var requestContext = CreateContext(name, interceptor);
            var conflict = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
                seed.CompanyId, new DefaultHttpContext(), Principal(seed.ManagerId),
                new Server.Api.CreateUserRequest(
                    "knownmember01", "عضو", "موجود", "another.address@example.test", null,
                    "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true),
                new AllowAntiforgery(), requestContext, new Infrastructure.Security.PasswordHashService(),
                new Infrastructure.Tenancy.CompanyAccessService(requestContext),
                TimeProvider.System, cancellationToken);
            Assert.Equal(StatusCodes.Status409Conflict, (conflict as IStatusCodeHttpResult)?.StatusCode);

            await using var check = CreateContext(name);
            Assert.Equal(1, await check.UserCompanyMemberships
                .CountAsync(item => item.UserId == existing.Id && item.TenantId == seed.CompanyId, cancellationToken));
            // The losing attempt wrote nothing: no audit row, no membership.
            Assert.DoesNotContain(await check.SecurityAuditEvents.ToListAsync(cancellationToken),
                item => item.EventType == "UserMembershipCreated" && item.EntityId == existing.Id.ToString());
        });
    }

    [Fact]
    public async Task Provisioning_NewUserAtomicity_UsernameRaceLeavesNothingBehind()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
        await WithDatabaseAsync(async name =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);
            var seed = await SeedCompanyManagerAsync(database);

            // A competing user with the same username wins while the endpoint is saving:
            // the combined user+membership write must roll back completely.
            var interceptor = new ConcurrentUserInsertInterceptor(name, "raced.user01", "raced@example.test");

            await using var requestContext = CreateContext(name, interceptor);
            var conflict = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
                seed.CompanyId, new DefaultHttpContext(), Principal(seed.ManagerId),
                new Server.Api.CreateUserRequest(
                    "raced.user01", "عضو", "مسابقه", "raced@example.test", null,
                    "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true),
                new AllowAntiforgery(), requestContext, new Infrastructure.Security.PasswordHashService(),
                new Infrastructure.Tenancy.CompanyAccessService(requestContext),
                TimeProvider.System, cancellationToken);
            Assert.Equal(StatusCodes.Status409Conflict, (conflict as IStatusCodeHttpResult)?.StatusCode);

            await using var check = CreateContext(name);
            var winner = await check.Users.SingleAsync(item => item.Username == "raced.user01", cancellationToken);
            Assert.False(await check.UserCompanyMemberships
                .AnyAsync(item => item.UserId == winner.Id && item.TenantId == seed.CompanyId, cancellationToken));
            // The endpoint attempt left no partial user with a different id.
            Assert.Equal(1, await check.Users.CountAsync(item => item.Username == "raced.user01", cancellationToken));
        });
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
            Infrastructure.Tenancy.CompanyAccessService.UserAccountKind),
    ], "sql-server-test"));

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    /// <summary>Runs one competing write on a separate context exactly once, before the
    /// endpoint's SaveChanges commits — a deterministic simulation of the concurrent
    /// request that wins the unique index.</summary>
    private sealed class ConcurrentMembershipInsertInterceptor(
        string databaseName,
        Guid userId,
        Guid companyId) : SaveChangesInterceptor
    {
        private int _triggered;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _triggered, 1) == 0)
            {
                await using var competing = CreateContext(databaseName);
                competing.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
                { UserId = userId, TenantId = companyId, IsActive = true, CreatedAtUtc = DateTime.UtcNow });
                await competing.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ConcurrentUserInsertInterceptor(
        string databaseName,
        string username,
        string emailAddress) : SaveChangesInterceptor
    {
        private int _triggered;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _triggered, 1) == 0)
            {
                await using var competing = CreateContext(databaseName);
                competing.Users.Add(new Domain.User(emailAddress)
                { Username = username, IsActive = true, Password = "private-password" });
                await competing.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }
}
