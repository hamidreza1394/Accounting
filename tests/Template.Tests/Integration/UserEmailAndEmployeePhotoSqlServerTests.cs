using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Real SQL Server checks for the two schema changes of the users-form fix: the optional e-mail
/// column (<c>MakeUserEmailAddressOptional</c>) and the personnel-photo table
/// (<c>AddCompanyEmployeePhotos</c>). They run on freshly migrated databases.
/// </summary>
public sealed class UserEmailAndEmployeePhotoSqlServerTests
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

    private static Domain.User NewUser(string username, string? email) => new(email)
    {
        Username = username, FirstName = "نام", LastName = "آزمون", IsActive = true,
    };

    [Fact]
    public async Task EmailIsNullable_ManyUsersMayHaveNone_ButARealAddressStaysUnique()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_EMAIL_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            database.Users.AddRange(NewUser("no-email-1", null), NewUser("no-email-2", null), NewUser("with-email", "a@example.test"));
            await database.SaveChangesAsync(cancellationToken);

            var stored = await database.Users.AsNoTracking().OrderBy(user => user.Username).ToListAsync(cancellationToken);
            Assert.Equal(3, stored.Count);
            Assert.Equal(2, stored.Count(user => user.EmailAddress is null));

            database.Users.Add(NewUser("duplicate", "a@example.test"));
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task EmployeePhoto_IsOnePerEmployee_AndCannotCrossCompanies()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_PHOTO_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت یک", IsActive = true };
            var other = new Domain.Company { Name = "شرکت دو", IsActive = true };
            database.Companies.AddRange(company, other);
            var employee = new Domain.CompanyEmployee
            {
                TenantId = company.Id, EmployeeNumber = "P-1", FirstName = "علی", LastName = "محمدی",
                EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            };
            database.CompanyEmployees.Add(employee);
            await database.SaveChangesAsync(cancellationToken);

            Domain.CompanyEmployeePhoto Photo(Guid tenantId) => new()
            {
                TenantId = tenantId, EmployeeId = employee.Id, ContentType = "image/png",
                ContentLength = 3, Content = [1, 2, 3], UpdatedByUserId = Guid.NewGuid(),
            };

            database.CompanyEmployeePhotos.Add(Photo(company.Id));
            await database.SaveChangesAsync(cancellationToken);
            Assert.Equal(3, (await database.CompanyEmployeePhotos.AsNoTracking().SingleAsync(cancellationToken)).Content.Length);

            // Second photo for the same employee: unique index.
            database.CompanyEmployeePhotos.Add(Photo(company.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(cancellationToken));
            database.ChangeTracker.Clear();

            // A photo claiming the employee under another company: composite foreign key.
            database.CompanyEmployeePhotos.Add(Photo(other.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
