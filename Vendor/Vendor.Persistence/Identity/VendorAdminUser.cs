using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Persistence.Identity;

/// <summary>
/// Identity for a human operator of the separate Vendor control plane.
/// IsEnabled defaults to false so persistence alone cannot activate an admin.
/// </summary>
public sealed class VendorAdminUser : IdentityUser<Guid>
{
    public bool IsEnabled { get; set; }

    public bool MustChangePassword { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastSuccessfulSignInAtUtc { get; set; }
}
