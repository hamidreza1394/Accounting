using Faranou.Modules.Trade.Application.Authorization;
using Xunit;

namespace Template.Tests;

public sealed class TradePermissionCatalogTests
{
    [Fact]
    public void Catalog_ContainsUniqueAsciiCodesWithinTheSharedPermissionColumnLimit()
    {
        var permissions = TradePermissionCatalog.All;

        Assert.Equal(46, permissions.Count);
        Assert.Equal(
            permissions.Count,
            permissions.Select(permission => permission.Code).Distinct(StringComparer.Ordinal).Count());

        Assert.All(permissions, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Code));
            Assert.True(permission.Code.Length <= 120, permission.Code);
            Assert.All(permission.Code, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_', permission.Code));
            Assert.False(string.IsNullOrWhiteSpace(permission.Name));
            Assert.True(permission.Name.Length <= 160, permission.Code);
            Assert.False(string.IsNullOrWhiteSpace(permission.Description));
            Assert.True(permission.Description.Length <= 500, permission.Code);
        });
    }

    [Fact]
    public void Catalog_ContainsTheApprovedInitialFormsAndActions()
    {
        var codes = TradePermissionCatalog.All
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(TradePermissionCodes.FormView("party"), codes);
        Assert.Contains(TradePermissionCodes.FormCreate("party"), codes);
        Assert.Contains(TradePermissionCodes.FormUpdate("party"), codes);
        Assert.Contains(TradePermissionCodes.FormDelete("party"), codes);
        Assert.Contains(TradePermissionCodes.FormExport("party"), codes);

        Assert.Contains(TradePermissionCodes.FormView("quotation-comparison"), codes);
        Assert.Contains(TradePermissionCodes.FormApprove("quotation-comparison"), codes);
        Assert.Contains(TradePermissionCodes.FormExport("quotation-comparison"), codes);
        Assert.DoesNotContain(TradePermissionCodes.FormDelete("quotation-comparison"), codes);

        Assert.Contains(TradePermissionCodes.FormClose("import-case"), codes);
        Assert.Contains(TradePermissionCodes.CompanyReportView, codes);
        Assert.Contains(TradePermissionCodes.MultiCompanyReportView, codes);
    }

    [Fact]
    public void Catalog_DeclaresReadAndWriteIntentConsistently()
    {
        Assert.All(TradePermissionCatalog.All, permission =>
            Assert.True(
                TradePermissionCodes.IsAccessIntentCompatible(permission.Code, permission.AccessIntent),
                $"Permission intent does not match its action: {permission.Code}"));

        Assert.All(
            TradePermissionCatalog.All.Where(permission => permission.Code.EndsWith(".view", StringComparison.Ordinal)
                || permission.Code.EndsWith(".export", StringComparison.Ordinal)),
            permission => Assert.Equal(TradeAccessIntent.Read, permission.AccessIntent));

        Assert.All(
            TradePermissionCatalog.All.Where(permission => permission.Code.EndsWith(".create", StringComparison.Ordinal)
                || permission.Code.EndsWith(".update", StringComparison.Ordinal)
                || permission.Code.EndsWith(".approve", StringComparison.Ordinal)
                || permission.Code.EndsWith(".delete", StringComparison.Ordinal)
                || permission.Code.EndsWith(".close", StringComparison.Ordinal)),
            permission => Assert.Equal(TradeAccessIntent.Write, permission.AccessIntent));
    }
}
