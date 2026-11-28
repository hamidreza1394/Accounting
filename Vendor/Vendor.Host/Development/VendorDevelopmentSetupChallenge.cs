namespace Faranou.Vendor.Host;

/// <summary>
/// Session challenge for the Development setup surface in setup-only mode, where no Vendor
/// identity exists yet and the HTTP surface is deliberately anonymous-but-local. The Host
/// process itself generates a per-process code at startup and prints it on its own console:
/// the code is never reachable through any endpoint, so only an operator who can read the
/// local process console can pass the mutating endpoints. Remote browser callers are already
/// blocked by loopback + same-origin + antiforgery; the challenge closes the remaining hole
/// where ANY local process (or a drive-by script) could fetch a token from a public GET.
/// The value is stored only as a SHA-256 hash in memory, compared in constant time, never
/// logged, and dies with the process (a restart prints a fresh code).
/// </summary>
internal sealed class VendorDevelopmentSetupChallenge(TimeProvider timeProvider)
{
    internal const string HeaderName = "X-Faranou-Setup-Challenge";
    internal const string ConsoleHeader = "FARANO VENDOR DEVELOPMENT SETUP";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly object _lock = new();
    private string? _challengeHash;
    private DateTimeOffset _issuedAtUtc;
    private string? _lastPrintedPlainText;

    /// <summary>
    /// Returns the current challenge plaintext, re-printing it on the console every time it
    /// rotates. Called once at startup by the setup-only host; the operator copies the code
    /// from the console output into the setup page.
    /// </summary>
    public string Issue()
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            if (_challengeHash is null || _issuedAtUtc == default || now - _issuedAtUtc > Lifetime)
            {
                var plain = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
                    .TrimEnd('=');
                _challengeHash = Hash(plain);
                _issuedAtUtc = now;
                _lastPrintedPlainText = plain;
            }

            return _lastPrintedPlainText!;
        }
    }

    /// <summary>Constant-time comparison against the in-memory hash; never logs the input.</summary>
    public bool TryValidate(string? presented)
    {
        if (string.IsNullOrWhiteSpace(presented))
        {
            return false;
        }

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            if (_challengeHash is null || now - _issuedAtUtc > Lifetime)
            {
                return false;
            }

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(_challengeHash),
                System.Text.Encoding.UTF8.GetBytes(Hash(presented)));
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
