namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// D-103 V1: a read-only tenant view derived at request time from one Vendor customer account.
/// This is display data only: it is never stored, never editable, never an authorization basis
/// and carries no identifier of its own. No VendorTenant entity, table, migration or runtime
/// exists in this stage; the architectural contract stays in D-103.
/// </summary>
public sealed record VendorTenantView(string DerivedFrom, string WorkspaceReference, string Status)
{
    /// <summary>Constant basis marker: the tenant view is derived from the customer account.</summary>
    public const string CustomerAccountBasis = "customer-account";

    /// <summary>Derives the read-only view from persisted customer fields; nothing is persisted back.</summary>
    public static VendorTenantView From(string customerNumber, string customerStatus) =>
        new(CustomerAccountBasis, customerNumber, customerStatus);
}
