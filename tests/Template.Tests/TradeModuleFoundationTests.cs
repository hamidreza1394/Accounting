using System.Xml.Linq;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class TradeDbContextModelTests
{
    [Fact]
    public void TradeContextDoesNotOwnFiscalPeriodEntitiesOrTables()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseSqlServer("Server=localhost;Database=TradeModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new TradeDbContext(options);

        var entityTypes = context.Model.GetEntityTypes().ToArray();

        Assert.DoesNotContain(entityTypes, entity =>
            entity.ClrType.Name is "FiscalPeriod" or "TradeOperationalPeriod");
        Assert.DoesNotContain(entityTypes, entity =>
            entity.GetTableName() is "FiscalPeriods" or "TradeOperationalPeriods");
    }

    [Fact]
    public void TradePeriodScopeReferencesTheSharedCoreFiscalPeriod()
    {
        var tenantId = Guid.NewGuid();
        var fiscalPeriodId = Guid.NewGuid();

        var scope = new TradePeriodScope(tenantId, fiscalPeriodId);

        Assert.Equal(tenantId, scope.TenantId);
        Assert.Equal(fiscalPeriodId, scope.FiscalPeriodId);
    }

    [Fact]
    public void TradeProjectsHaveOnlyApprovedIntraModuleDependencies()
    {
        var root = FindRepositoryRoot();
        AssertProjectReferences(
            Path.Combine(root, "Modules", "Trade", "Trade.Domain", "Trade.Domain.csproj"),
            []);

        AssertProjectReferences(
            Path.Combine(root, "Modules", "Trade", "Trade.Application", "Trade.Application.csproj"),
            [@"..\Trade.Domain\Trade.Domain.csproj"]);

        AssertProjectReferences(
            Path.Combine(root, "Modules", "Trade", "Trade.Infrastructure", "Trade.Infrastructure.csproj"),
            [@"..\Trade.Application\Trade.Application.csproj", @"..\Trade.Domain\Trade.Domain.csproj"]);
    }

    private static void AssertProjectReferences(string projectPath, IReadOnlyCollection<string> expected)
    {
        Assert.True(File.Exists(projectPath), $"Expected Trade project does not exist: {projectPath}");
        var document = XDocument.Load(projectPath);
        var actual = document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => ((string?)element.Attribute("Include"))?.Replace('/', '\\'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expectedSorted = expected.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(expectedSorted, actual);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Template.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate Template.sln from the test output directory.");
    }
}
