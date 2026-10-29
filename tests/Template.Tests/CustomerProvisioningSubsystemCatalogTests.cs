using Infrastructure.Provisioning;
using Xunit;

namespace Template.Tests;

public sealed class CustomerProvisioningSubsystemCatalogTests
{
    [Theory]
    [InlineData("finance")]
    [InlineData("sales")]
    [InlineData("inventory")]
    [InlineData("treasury")]
    public void KnownSubsystemCodes_AreExplicitlyAllowlisted(string subsystemCode)
    {
        Assert.True(CustomerProvisioningSubsystemCatalog.IsKnownSubsystem(subsystemCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Finance")]
    [InlineData("accounting")]
    [InlineData("finance;treasury")]
    public void UnknownSubsystemCodes_FailClosed(string? subsystemCode)
    {
        Assert.False(CustomerProvisioningSubsystemCatalog.IsKnownSubsystem(subsystemCode));
    }

    [Theory]
    [InlineData("finance", "finance.document.create", true)]
    [InlineData("finance", "finance.document.approve", true)]
    [InlineData("finance", "sales.invoice.create", false)]
    [InlineData("finance", "finance", false)]
    [InlineData("finance", "finance..create", false)]
    [InlineData("finance", "finance.document.", false)]
    [InlineData("unknown", "unknown.document.create", false)]
    [InlineData("sales", "sales.invoice.create\n", false)]
    public void PermissionOwnership_UsesExactModulePrefixAndValidSegments(
        string subsystemCode,
        string permissionCode,
        bool expected)
    {
        Assert.Equal(expected,
            CustomerProvisioningSubsystemCatalog.IsPermissionOwnedBySubsystem(subsystemCode, permissionCode));
    }

    [Fact]
    public void Resolution_ReturnsOnlyExistingExactPermissionCodesForPurchasedSubsystems()
    {
        var purchased = new[] { "finance", "inventory", "unknown" };
        var defined = new[]
        {
            "finance.document.create",
            "finance.document.create",
            "finance.document.approve",
            "sales.invoice.create",
            "inventory.voucher.view",
            "finance..invalid",
            "finance",
        };

        var resolved = CustomerProvisioningSubsystemCatalog.ResolveExistingPermissionCodes(purchased, defined);

        Assert.Equal(
            new[] { "finance.document.approve", "finance.document.create", "inventory.voucher.view" },
            resolved);
    }

    [Fact]
    public void Resolution_DoesNotInventPermissionDefinitionsWhenCatalogIsEmpty()
    {
        var resolved = CustomerProvisioningSubsystemCatalog.ResolveExistingPermissionCodes(
            new[] { "finance", "sales", "inventory", "treasury" },
            Array.Empty<string>());

        Assert.Empty(resolved);
    }
}
