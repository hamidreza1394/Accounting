using System.Security.Cryptography;
using System.Text;

namespace Faranou.Vendor.Host;

/// <summary>
/// Single-use bootstrap token for the Development provisioning endpoints in setup-only mode,
/// where no Vendor identity exists yet and the surface is deliberately anonymous-but-local:
/// the loopback page fetches a short-lived token that is paired with a strict HttpOnly
/// cookie, and the provisioning call must present both exactly once. Values are random, are
/// stored only as SHA-256 hashes in memory, are never logged, never appear in URLs or
/// persistent browser storage, and are consumed on use (replay and reuse fail closed).
/// </summary>
internal static class VendorDevelopmentSetupBootstrapToken
{
    internal const string HeaderName = "X-Faranou-Setup-Token";
    internal const string CookieName = "FaranouSetupBootstrap";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private const int MaximumOutstanding = 16;
    private static readonly object IssuanceLock = new();
    private static readonly Dictionary<string, (string CookieHash, DateTimeOffset ExpiresAt)> Outstanding =
        new(StringComparer.Ordinal);

    internal sealed record BootstrapToken(string Token, string CookieValue);

    internal static BootstrapToken Issue(DateTimeOffset now)
    {
        var token = RandomToken();
        var cookieValue = RandomToken();
        lock (IssuanceLock)
        {
            EvictExpired(now);
            if (Outstanding.Count >= MaximumOutstanding)
            {
                // Fail-safe pressure valve: drop the oldest outstanding pair so a fresh,
                // legitimate page can always continue; every value still expires on its own.
                var oldest = Outstanding.MinBy(pair => pair.Value.ExpiresAt);
                Outstanding.Remove(oldest.Key);
            }

            Outstanding[Hash(token)] = (Hash(cookieValue), now + Lifetime);
        }

        return new BootstrapToken(token, cookieValue);
    }

    internal static bool TryConsume(string? token, string? cookieValue, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(cookieValue))
        {
            return false;
        }

        lock (IssuanceLock)
        {
            var key = Hash(token);
            if (!Outstanding.TryGetValue(key, out var entry))
            {
                return false;
            }

            // Single use: the pair is consumed even when the comparison fails, so a stolen
            // token cannot be retried against other cookie guesses.
            Outstanding.Remove(key);
            return now <= entry.ExpiresAt &&
                   string.Equals(entry.CookieHash, Hash(cookieValue), StringComparison.Ordinal);
        }
    }

    private static void EvictExpired(DateTimeOffset now)
    {
        var expired = Outstanding.Where(pair => pair.Value.ExpiresAt < now).Select(pair => pair.Key).ToList();
        foreach (var key in expired)
        {
            Outstanding.Remove(key);
        }
    }

    private static string RandomToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
