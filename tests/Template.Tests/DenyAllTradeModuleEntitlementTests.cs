using Infrastructure.Trade;
using Xunit;

namespace Template.Tests;

public sealed class DenyAllTradeModuleEntitlementTests
{
    [Fact]
    public async Task AlwaysFalseForAnyTenant()
    {
        var entitlement = new DenyAllTradeModuleEntitlement();

        Assert.False(await entitlement.IsEnabledForCompanyAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.False(await entitlement.IsEnabledForCompanyAsync(Guid.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HasNoDependenciesAndIsSealed()
    {
        var type = typeof(DenyAllTradeModuleEntitlement);

        Assert.True(type.IsSealed);
        var constructor = Assert.Single(type.GetConstructors());
        Assert.Empty(constructor.GetParameters());
    }
}
