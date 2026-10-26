using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Persistence.Identity;

/// <summary>
/// Identity record for a human administrator of the vendor control plane.
/// Vendor administrator access is separate from customer-application users.
/// </summary>
public sealed class VendorAdminUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DisabledAtUtc { get; set; }
}
