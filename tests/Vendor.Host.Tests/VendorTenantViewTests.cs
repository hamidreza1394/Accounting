using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// D-103 V1: the tenant view must stay a pure, read-only derivation of the customer account.
/// It carries no identifier, no stored key and no editable state; the guard assertions document
/// the scope decision that no independent tenant runtime is introduced in this stage.
/// </summary>
public sealed class VendorTenantViewTests
{
    [Fact]
    public void TenantViewIsDerivedFromCustomerAccountFieldsOnly()
    {
        var view = VendorTenantView.From("C-100", "Active");

        Assert.Equal("customer-account", view.DerivedFrom);
        Assert.Equal(VendorTenantView.CustomerAccountBasis, view.DerivedFrom);
        Assert.Equal("C-100", view.WorkspaceReference);
        Assert.Equal("Active", view.Status);
    }

    [Fact]
    public void SuspendedCustomerYieldsSuspendedWorkspaceViewWithoutStateTransition()
    {
        var active = VendorTenantView.From("C-200", "Active");
        var suspended = VendorTenantView.From("C-200", "Suspended");

        Assert.Equal("Suspended", suspended.Status);
        Assert.NotEqual(active.Status, suspended.Status);
        // The view mirrors the customer status; it defines no independent lifecycle of its own.
        Assert.Equal(VendorTenantView.From("C-200", "Suspended"), suspended);
    }

    [Fact]
    public void ViewCarriesNoIdentifierOrManageableKey()
    {
        var view = VendorTenantView.From("C-300", "Active");

        // Scope guard: the record exposes exactly the three derivation fields.
        Assert.Equal(3, view.GetType().GetProperties().Length);
        Assert.Equal(["DerivedFrom", "Status", "WorkspaceReference"],
            view.GetType().GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray());
        Assert.Equal(typeof(string), view.GetType().GetProperty("WorkspaceReference")!.PropertyType);
        Assert.DoesNotContain(view.GetType().GetProperties(), property =>
            property.Name.Contains("Key", StringComparison.Ordinal) ||
            property.Name.Contains("Id", StringComparison.Ordinal) ||
            property.PropertyType == typeof(Guid));
    }
}
