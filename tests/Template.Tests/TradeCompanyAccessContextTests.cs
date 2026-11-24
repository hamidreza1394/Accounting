using System.Reflection;
using Faranou.Modules.Trade.Application.Authorization;
using Xunit;

namespace Template.Tests;

/// <summary>
/// N-5a-type phase 1: the company-scoped context carries exactly the user, the tenant and the company. It has no
/// fiscal period and no write flag, and the period-scoped contexts stay as they are.
/// </summary>
public sealed class TradeCompanyAccessContextTests
{
    [Fact]
    public void AValidContextKeepsTheThreeIndependentIdentifiers()
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();

        var context = new TradeCompanyAccessContext(user, tenant, company);

        Assert.Equal(user, context.UserId);
        Assert.Equal(tenant, context.TenantId);
        Assert.Equal(company, context.CompanyId);
        Assert.NotEqual(context.TenantId, context.CompanyId);
    }

    [Fact]
    public void TheSameValueForTenantAndCompanyIsAllowedButNeverRequiredOrAssumed()
    {
        var same = Guid.NewGuid();

        var context = new TradeCompanyAccessContext(Guid.NewGuid(), same, same);

        Assert.Equal(same, context.TenantId);
        Assert.Equal(same, context.CompanyId);

        // The identifiers are kept apart: no value is derived from the other one.
        var other = new TradeCompanyAccessContext(context.UserId, Guid.NewGuid(), same);
        Assert.Equal(same, other.CompanyId);
        Assert.NotEqual(same, other.TenantId);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("tenant")]
    [InlineData("company")]
    public void AnEmptyIdentifierIsRefused(string which)
    {
        var user = which == "user" ? Guid.Empty : Guid.NewGuid();
        var tenant = which == "tenant" ? Guid.Empty : Guid.NewGuid();
        var company = which == "company" ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new TradeCompanyAccessContext(user, tenant, company));
    }

    [Fact]
    public void TheContextHasNoFiscalPeriodAndNoWriteFlag()
    {
        var names = typeof(TradeCompanyAccessContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(["CompanyId", "TenantId", "UserId"], names);
    }

    [Fact]
    public void TheContextIsImmutable()
    {
        Assert.All(
            typeof(TradeCompanyAccessContext)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name != "EqualityContract"),
            property => Assert.True(property.SetMethod is null, $"{property.Name} must be get-only."));
    }

    [Fact]
    public void ThePeriodScopedContextsKeepTheirCurrentShape()
    {
        // Phase 1 must not touch the period-scoped contracts (O-3 changes them only inside T1).
        Assert.Equal(
            [("UserId", typeof(Guid)), ("TenantId", typeof(Guid)), ("FiscalPeriodId", typeof(Guid)), ("AllowsWrites", typeof(bool))],
            Shape(typeof(TradeAccessContext)));
        Assert.Equal(
            [("TenantId", typeof(Guid)), ("FiscalPeriodId", typeof(Guid))],
            Shape(typeof(TradePeriodScope)));
    }

    private static (string Name, Type Type)[] Shape(Type type) =>
        type.GetConstructors().Single().GetParameters()
            .Select(parameter => (parameter.Name!, parameter.ParameterType)).ToArray()
            .Select(item => (char.ToUpperInvariant(item.Item1[0]) + item.Item1[1..], item.Item2)).ToArray();
}
