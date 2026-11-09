using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// The database side of the organizational structure (D-072) on the real provider: the
/// migration applies, the unique constraints and the filtered indexes reject what the
/// application rules reject, the check constraints guard the state machines and the
/// composite foreign keys keep every HR row inside its company.
/// </summary>
public sealed class HrOrganizationalStructureSqlServerTests
{
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
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        };
        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    [Fact]
    public async Task TheMigrationAppliesAndSeedsEveryHrPermissionExactlyOnce()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_HrStructure_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var seeded = await database.PermissionDefinitions.AsNoTracking()
                .Where(permission => permission.Code.StartsWith("hr."))
                .ToListAsync(cancellationToken);

            var expectedCodes = new[]
            {
                "hr.employees.list", "hr.employees.view", "hr.employees.create", "hr.employees.update",
                "hr.employees.user-link.manage", "hr.organizational-units.list", "hr.organizational-units.create",
                "hr.organizational-units.update", "hr.positions.list", "hr.positions.create",
                "hr.positions.update", "hr.assignments.manage",
            };
            Assert.Equal(expectedCodes.Length, seeded.Count);
            Assert.All(seeded, permission =>
            {
                Assert.False(string.IsNullOrWhiteSpace(permission.Name));
                Assert.False(string.IsNullOrWhiteSpace(permission.Description));
            });
            // The catalog query has no stable order of its own; both sides are compared in
            // ordinal order, so the expected list is sorted the same way here.
            Assert.Equal(expectedCodes.Order().ToArray(), seeded.Select(permission => permission.Code).Order().ToArray());
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task DuplicateEmployeeNumberInTheSameCompany_IsRejectedByTheUniqueIndex()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_HrStructure_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            database.Companies.Add(company);
            database.CompanyEmployees.Add(new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = "EMP-1",
                FirstName = "علی",
                LastName = "محمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            });
            await database.SaveChangesAsync(cancellationToken);

            database.CompanyEmployees.Add(new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = "EMP-1",
                FirstName = "رضا",
                LastName = "احمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task AssignmentReferencingAnotherCompany_IsRejectedByTheCompositeForeignKey()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_HrStructure_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
            database.Companies.Add(company);
            database.Companies.Add(otherCompany);
            var employee = new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = "EMP-1",
                FirstName = "علی",
                LastName = "محمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            };
            database.CompanyEmployees.Add(employee);
            var foreignUnit = new Domain.CompanyOrganizationalUnit
            {
                TenantId = otherCompany.Id,
                Code = "UNIT-X",
                Name = "واحد شرکت دیگر",
                IsActive = true,
            };
            database.CompanyOrganizationalUnits.Add(foreignUnit);
            await database.SaveChangesAsync(cancellationToken);

            database.CompanyEmployeeAssignments.Add(new Domain.CompanyEmployeeAssignment
            {
                TenantId = company.Id,
                EmployeeId = employee.Id,
                OrganizationalUnitId = foreignUnit.Id,
                StartOnUtc = DateTime.UtcNow,
                Status = Domain.CompanyEmployeeAssignmentStatus.Active,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task TerminatedEmployeeWithoutATerminationDate_IsRejectedByTheCheckConstraint()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_HrStructure_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            database.Companies.Add(company);
            database.CompanyEmployees.Add(new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = "EMP-1",
                FirstName = "علی",
                LastName = "محمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Terminated,
                TerminatedOnUtc = null,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task TwoConcurrentActiveAssignmentsForOneEmployee_TheFilteredIndexRejectsTheSecond()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_HrStructure_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            database.Companies.Add(company);
            var employee = new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = "EMP-1",
                FirstName = "علی",
                LastName = "محمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            };
            database.CompanyEmployees.Add(employee);
            database.CompanyEmployeeAssignments.Add(new Domain.CompanyEmployeeAssignment
            {
                TenantId = company.Id,
                EmployeeId = employee.Id,
                StartOnUtc = DateTime.UtcNow.AddDays(-30),
                Status = Domain.CompanyEmployeeAssignmentStatus.Active,
            });
            await database.SaveChangesAsync(cancellationToken);

            database.ChangeTracker.Clear();

            database.CompanyEmployeeAssignments.Add(new Domain.CompanyEmployeeAssignment
            {
                TenantId = company.Id,
                EmployeeId = employee.Id,
                StartOnUtc = DateTime.UtcNow,
                Status = Domain.CompanyEmployeeAssignmentStatus.Active,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task NextEmployeeNumber_OnSqlServer_UsesTheLargestNumericNumber_AndTheUniqueIndexRejectsARacedDuplicate()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_HrNextNumber_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
            var adminRole = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User($"admin-{Guid.NewGuid():N}@example.test")
            {
                Username = "admin",
                FirstName = "مدیر",
                IsActive = true,
                Password = "x",
                RoleId = adminRole.Id,
                Role = adminRole,
            };
            database.Companies.AddRange(company, otherCompany);
            database.Roles.Add(adminRole);
            database.Users.Add(admin);
            foreach (var (tenantId, number) in new[]
                     {
                         (company.Id, "EMP-9999"), (company.Id, "12"), (company.Id, "0099"),
                         (company.Id, "99999999999999999999"), (otherCompany.Id, "9999999999999999999999"),
                     })
            {
                database.CompanyEmployees.Add(new Domain.CompanyEmployee
                {
                    TenantId = tenantId,
                    EmployeeNumber = number,
                    FirstName = "علی",
                    LastName = "محمدی",
                    EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
                });
            }
            await database.SaveChangesAsync(cancellationToken);

            var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new System.Security.Claims.Claim("AccountKind", "User"),
            ], "test"));
            var result = await Server.Api.HrOrganizationalStructureEndpoints.GetNextEmployeeNumberAsync(
                company.Id, new Microsoft.AspNetCore.Http.DefaultHttpContext(), principal, database,
                new Infrastructure.Tenancy.CompanyAccessService(database), cancellationToken);

            var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<Server.Api.HrNextEmployeeNumberResponse>>(result);
            Assert.Equal("100000000000000000000", ok.Value!.SuggestedEmployeeNumber);

            // The suggestion reserves nothing: a raced second insert of the same number is
            // rejected by the unique index, which is the real uniqueness guarantee.
            database.CompanyEmployees.Add(new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = ok.Value.SuggestedEmployeeNumber,
                FirstName = "رضا",
                LastName = "احمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            });
            await database.SaveChangesAsync(cancellationToken);
            database.CompanyEmployees.Add(new Domain.CompanyEmployee
            {
                TenantId = company.Id,
                EmployeeNumber = ok.Value.SuggestedEmployeeNumber,
                FirstName = "سارا",
                LastName = "کریمی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
