using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.Provisioning;

/// <summary>
/// Fixed, version-controlled ownership map between commercial subsystem codes and
/// operational permission codes. This deliberately does not invent permissions or
/// grant wildcards: a permission is assignable only when its exact stable code is
/// present in the customer's PermissionDefinitions catalog and belongs to the module.
/// </summary>
public static class CustomerProvisioningSubsystemCatalog
{
    private static readonly HashSet<string> SubsystemCodes = new(StringComparer.Ordinal)
    {
        "finance",
        "sales",
        "inventory",
        "treasury",
    };

    /// <summary>Returns true only for a subsystem code explicitly supported by this release.</summary>
    public static bool IsKnownSubsystem(string? subsystemCode) =>
        subsystemCode is not null && SubsystemCodes.Contains(subsystemCode);

    /// <summary>
    /// Checks ownership using the stable `&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;` convention.
    /// Exact permission definitions remain the authority; this method never expands a wildcard.
    /// </summary>
    public static bool IsPermissionOwnedBySubsystem(string? subsystemCode, string? permissionCode)
    {
        if (!IsKnownSubsystem(subsystemCode) ||
            string.IsNullOrWhiteSpace(permissionCode) ||
            permissionCode.Length > 160 ||
            permissionCode.Any(char.IsControl))
        {
            return false;
        }

        var parts = permissionCode.Split('.', StringSplitOptions.None);
        return parts.Length >= 3 &&
            parts.All(part => part.Length > 0 && part.All(character =>
                character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')) &&
            string.Equals(parts[0], subsystemCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves only exact, already-defined permission codes for purchased subsystems.
    /// It never synthesizes a permission definition from a package or a module name.
    /// </summary>
    public static IReadOnlyList<string> ResolveExistingPermissionCodes(
        IEnumerable<string> purchasedSubsystems,
        IEnumerable<string> definedPermissionCodes)
    {
        ArgumentNullException.ThrowIfNull(purchasedSubsystems);
        ArgumentNullException.ThrowIfNull(definedPermissionCodes);

        var purchased = purchasedSubsystems
            .Where(IsKnownSubsystem)
            .ToHashSet(StringComparer.Ordinal);

        return definedPermissionCodes
            .Where(code => purchased.Any(module => IsPermissionOwnedBySubsystem(module, code)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
