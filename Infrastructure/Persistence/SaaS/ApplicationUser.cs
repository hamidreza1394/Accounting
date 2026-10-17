using Microsoft.AspNetCore.Identity;

namespace Data.SaaS;

/// <summary>Central login identity. Company membership and company roles are separate records.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
