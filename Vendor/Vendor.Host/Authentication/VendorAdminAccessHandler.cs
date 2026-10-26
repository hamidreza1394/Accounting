using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Host.Authentication;

public sealed class VendorAdminAccessRequirement : IAuthorizationRequirement { }

public sealed class VendorAdminAccessHandler(
    UserManager<VendorAdminUser> users)
    : AuthorizationHandler<VendorAdminAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VendorAdminAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.Claims.Any(claim =>
                claim.Type == "amr" &&
                (claim.Value == "otp" || claim.Value == "passkey")))
        {
            return;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null || !user.IsEnabled)
        {
            return;
        }

        if (context.User.HasClaim("amr", "otp") && user.TwoFactorEnabled)
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.HasClaim("amr", "passkey"))
        {
            var passkeys = await users.GetPasskeysAsync(user);
            if (passkeys.Count > 0)
            {
                context.Succeed(requirement);
            }
        }
    }
}
