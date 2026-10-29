using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Provisioning;

/// <summary>
/// Creates and verifies one-time administrator enrollment tokens. Only the SHA-256 digest
/// belongs in the database; the raw token is returned once to the trusted local operator.
/// </summary>
public static class CustomerProvisioningEnrollmentToken
{
    public const int RandomByteLength = 32;
    public const int EncodedTokenLength = 43;
    public const int HashHexLength = 64;

    /// <summary>Generates a cryptographically random, unpadded base64url token.</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(RandomByteLength);
        return System.Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>Returns the uppercase hexadecimal SHA-256 digest for a valid token.</summary>
    public static string ComputeHash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (!IsValidToken(token))
            throw new ArgumentException("Enrollment token format is invalid.", nameof(token));

        return System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// Verifies a presented token against its stored digest using a constant-time comparison.
    /// Invalid token/hash formats are rejected without throwing.
    /// </summary>
    public static bool Verify(string? token, string? storedHash)
    {
        if (!IsValidToken(token) || !IsValidHash(storedHash))
            return false;

        try
        {
            var expected = System.Convert.FromHexString(storedHash!);
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token!));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidToken(string? token)
    {
        if (token is not { Length: EncodedTokenLength } ||
            !token.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            return false;
        }

        try
        {
            var bytes = System.Convert.FromBase64String(
                token.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != RandomByteLength)
                return false;

            var canonical = System.Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return string.Equals(canonical, token, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidHash(string? hash) =>
        hash is { Length: HashHexLength } &&
        hash.All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
}
