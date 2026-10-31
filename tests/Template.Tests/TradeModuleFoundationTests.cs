using System.Xml.Linq;
using Faranou.Modules.Trade.Domain;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class TradeOperationalPeriodTests
{
    [Fact]
    public void CreateRequiresAValidTenant()
    {
        Assert.Throws<ArgumentException>(() => TradeOperationalPeriod.Create(
            Guid.Empty, "2026", "سال عملیاتی ۲۰۲۶",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void CreateNormalizesCodeAndStartsInDraft()
    {
        var tenantId = Guid.NewGuid();
        var period = TradeOperationalPeriod.Create(
            tenantId, "  fy-2026 ", "  سال عملیاتی ۲۰۲۶  ",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(tenantId, period.TenantId);
        Assert.Equal("FY-2026", period.Code);
        Assert.Equal("سال عملیاتی ۲۰۲۶", period.Title);
        Assert.Equal(TradeOperationalPeriodStatus.Draft, period.Status);
    }


    [Fact]
    public void CreateRejectsNonAsciiPeriodCodes()
    {
        Assert.Throws<ArgumentException>(() => TradeOperationalPeriod.Create(
            Guid.NewGuid(), "سال-۱۴۰۵", "سال عملیاتی",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void CreateRejectsAnInvalidDateRange()
    {
        Assert.Throws<ArgumentException>(() => TradeOperationalPeriod.Create(
            Guid.NewGuid(), "FY-2026", "سال عملیاتی ۲۰۲۶",
            new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1)));
    }
}

public sealed class TradeDbContextModelTests
{
    [Fact]
    public void OperationalPeriodIsOwnedByTradeSchemaAndHasNoCoreForeignKey()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseSqlServer("Server=localhost;Database=TradeModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new TradeDbContext(options);
        var entity = context.Model.FindEntityType(typeof(TradeOperationalPeriod));

        Assert.NotNull(entity);
        Assert.Equal("OperationalPeriods", entity!.GetTableName());
        Assert.Equal("trade", entity.GetSchema());
        Assert.Empty(entity.GetForeignKeys());
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
