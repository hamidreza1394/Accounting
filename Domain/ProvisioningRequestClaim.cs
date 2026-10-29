namespace Domain;

/// <summary>
/// Durable idempotency, replay protection and one-time-enrollment state for a signed customer provisioning package.
/// The request ID, nonce digest and enrollment-token digest are uniquely indexed by the persistence model.
/// </summary>
public sealed class ProvisioningRequestClaim : Seedwork.Entity
{
    public Guid RequestId { get; set; }
    public string NonceHash { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    /// <summary>UserId intentionally remains a scalar, matching the legacy user store model.</summary>
    public Guid AdminUserId { get; set; }

    /// <summary>SHA-256 digest only; the one-time enrollment token is never persisted in plaintext.</summary>
    public string EnrollmentTokenHash { get; set; } = string.Empty;
    public DateTimeOffset EnrollmentExpiresAtUtc { get; set; }
    public DateTimeOffset? EnrollmentConsumedAtUtc { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }

    /// <summary>When true, the first authenticated session must be restricted to password change.</summary>
    public bool MustChangePassword { get; set; } = true;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
