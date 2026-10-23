using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests;

public sealed class DatabaseDependencyInjectionTests
{
    [Fact]
    public void DatabaseSchemaMigrator_CanBeResolved_WhenDatabaseContextIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DatabaseContext>(options =>
            options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Farano_DiValidation;Trusted_Connection=True;"));
        services.AddScoped<IDatabaseSchemaMigrator, DatabaseSchemaMigrator>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>();

        Assert.IsType<DatabaseSchemaMigrator>(migrator);
    }
}
