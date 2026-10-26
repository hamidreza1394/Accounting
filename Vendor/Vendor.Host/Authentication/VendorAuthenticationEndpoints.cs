using System.Security.Claims;
using System.Text.Encodings.Web;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Host.Authentication;

public static class VendorAuthenticationEndpoints
{
    private const string GenericSignInFailure = "Sign-in failed. Check your credentials or authentication method.";
    private const string OtpAmr = "otp";
    private const string PasskeyAmr = "passkey";

    public static IEndpointRouteBuilder MapVendorAuthentication(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth");

        auth.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { requestToken = tokens.RequestToken });
        }).AllowAnonymous();

        auth.MapPost("/password", async (
            PasswordSignInRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            SignInManager<VendorAdminUser> signInManager,
            CancellationToken cancellationToken) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            if (string.IsNullOrWhiteSpace(request.UserName) ||
                string.IsNullOrEmpty(request.Password) ||
                request.UserName.Length > 256 ||
                request.Password.Length > 1024)
            {
                return Results.Unauthorized();
            }

            var user = await users.FindByNameAsync(request.UserName);
            if (user is null)
            {
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            // Always perform a password check for disabled or MFA-unenrolled accounts so
            // this endpoint never creates an application cookie for a password-only login.
            if (!user.IsEnabled || !user.TwoFactorEnabled)
            {
                _ = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await signInManager.PasswordSignInAsync(
                user,
                request.Password,
                isPersistent: false,
                lockoutOnFailure: true);

            if (result.RequiresTwoFactor)
            {
                return Results.Ok(new { requiresSecondFactor = true, method = "totp" });
            }

            // A password-only result is never acceptable for this control plane.
            if (result.Succeeded)
            {
                await signInManager.SignOutAsync();
            }

            return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .AllowAnonymous()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/totp/verify", async (
            TotpSignInRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            SignInManager<VendorAdminUser> signInManager) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 32)
            {
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user is null || !user.IsEnabled || !user.TwoFactorEnabled)
            {
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
                request.Code,
                isPersistent: false,
                rememberClient: false);

            if (!result.Succeeded)
            {
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            // Replace Identity's final cookie with a principal that records the verified factor.
            await signInManager.SignInWithClaimsAsync(
                user,
                isPersistent: false,
                additionalClaims: [new Claim("amr", OtpAmr)]);

            return Results.Ok(new { authenticated = true });
        })
        .AllowAnonymous()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/passkey/options", async (
            PasskeyOptionsRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            SignInManager<VendorAdminUser> signInManager) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            VendorAdminUser? user = null;
            if (!string.IsNullOrWhiteSpace(request.UserName) && request.UserName.Length <= 256)
            {
                var candidate = await users.FindByNameAsync(request.UserName);
                if (candidate is { IsEnabled: true })
                {
                    var passkeys = await users.GetPasskeysAsync(candidate);
                    if (passkeys.Count > 0)
                    {
                        user = candidate;
                    }
                }
            }

            // A null user intentionally enables discoverable-credential UX and avoids
            // revealing whether a username exists or has a registered passkey.
            var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
            return Results.Content(optionsJson, "application/json");
        })
        .AllowAnonymous()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/passkey/sign-in", async (
            PasskeySignInRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            SignInManager<VendorAdminUser> signInManager) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            if (string.IsNullOrWhiteSpace(request.CredentialJson) || request.CredentialJson.Length > 64 * 1024)
            {
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await signInManager.PasskeySignInAsync(request.CredentialJson);
            if (!result.Succeeded)
            {
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            var user = authentication.Principal is null
                ? null
                : await users.GetUserAsync(authentication.Principal);

            if (user is null || !user.IsEnabled)
            {
                await signInManager.SignOutAsync();
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var passkeys = await users.GetPasskeysAsync(user);
            if (passkeys.Count == 0)
            {
                await signInManager.SignOutAsync();
                return Results.Json(new { error = GenericSignInFailure }, statusCode: StatusCodes.Status401Unauthorized);
            }

            await signInManager.SignInWithClaimsAsync(
                user,
                isPersistent: false,
                additionalClaims: [new Claim("amr", PasskeyAmr)]);

            return Results.Ok(new { authenticated = true });
        })
        .AllowAnonymous()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/passkey/creation-options", async (
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            SignInManager<VendorAdminUser> signInManager) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled)
            {
                return Results.Unauthorized();
            }

            var userName = await users.GetUserNameAsync(user) ?? user.Email ?? "Vendor administrator";
            var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
            {
                Id = user.Id.ToString("D"),
                Name = userName,
                DisplayName = userName
            });

            return Results.Content(optionsJson, "application/json");
        })
        .RequireAuthorization()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/passkey/register", async (
            PasskeyRegistrationRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users,
            SignInManager<VendorAdminUser> signInManager) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled ||
                string.IsNullOrWhiteSpace(request.CredentialJson) ||
                request.CredentialJson.Length > 64 * 1024)
            {
                return Results.BadRequest(new { error = "Passkey registration failed." });
            }

            var attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
            if (!attestation.Succeeded ||
                !string.Equals(attestation.UserEntity.Id, user.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Passkey registration failed." });
            }

            var passkey = attestation.Passkey;
            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                passkey.Name = request.DisplayName.Trim()[..Math.Min(request.DisplayName.Trim().Length, 100)];
            }

            var result = await users.AddOrUpdatePasskeyAsync(user, passkey);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = "Passkey registration failed." });
            }

            return Results.Ok(new { registered = true });
        })
        .RequireAuthorization()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/totp/setup", async (
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled || user.TwoFactorEnabled)
            {
                return Results.Conflict(new { error = "Authenticator setup is unavailable." });
            }

            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key))
            {
                var reset = await users.ResetAuthenticatorKeyAsync(user);
                if (!reset.Succeeded)
                {
                    return Results.Problem(detail: "Authenticator setup could not be started.");
                }

                key = await users.GetAuthenticatorKeyAsync(user);
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return Results.Problem(detail: "Authenticator setup could not be started.");
            }

            var label = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id.ToString("D"));
            var issuer = Uri.EscapeDataString("Faranou Vendor");
            var uri = $"otpauth://totp/{issuer}:{label}?secret={UrlEncoder.Default.Encode(key)}&issuer={issuer}&digits=6";

            // The shared key is returned only to an already MFA-authenticated administrator.
            return Results.Ok(new { sharedKey = key, authenticatorUri = uri });
        })
        .RequireAuthorization()
        .RequireRateLimiting("vendor-auth");

        auth.MapPost("/totp/confirm", async (
            TotpSetupConfirmationRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<VendorAdminUser> users) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled || user.TwoFactorEnabled ||
                string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 32)
            {
                return Results.BadRequest(new { error = "Authenticator confirmation failed." });
            }

            var valid = await users.VerifyTwoFactorTokenAsync(
                user,
                users.Options.Tokens.AuthenticatorTokenProvider,
                request.Code);

            if (!valid)
            {
                return Results.BadRequest(new { error = "Authenticator confirmation failed." });
            }

            var result = await users.SetTwoFactorEnabledAsync(user, true);
            return result.Succeeded
                ? Results.Ok(new { enabled = true })
                : Results.BadRequest(new { error = "Authenticator confirmation failed." });
        })
        .RequireAuthorization()
        .RequireRateLimiting("vendor-auth");

        auth.MapGet("/me", async (
            HttpContext context,
            UserManager<VendorAdminUser> users) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled)
            {
                return Results.Unauthorized();
            }

            var passkeys = await users.GetPasskeysAsync(user);
            return Results.Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                email = user.Email,
                totpEnabled = user.TwoFactorEnabled,
                passkeyCount = passkeys.Count,
                mustChangePassword = user.MustChangePassword
            });
        }).RequireAuthorization();

        auth.MapPost("/logout", async (
            HttpContext context,
            IAntiforgery antiforgery,
            SignInManager<VendorAdminUser> signInManager) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest(new { error = "Invalid anti-forgery token." });
            }

            await signInManager.SignOutAsync();
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            await context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
            await context.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
            return Results.Ok(new { signedOut = true });
        }).AllowAnonymous();

        return endpoints;
    }

    public sealed record PasswordSignInRequest(string UserName, string Password);
    public sealed record TotpSignInRequest(string Code);
    public sealed record PasskeyOptionsRequest(string? UserName);
    public sealed record PasskeySignInRequest(string CredentialJson);
    public sealed record PasskeyRegistrationRequest(string CredentialJson, string? DisplayName);
    public sealed record TotpSetupConfirmationRequest(string Code);
}
