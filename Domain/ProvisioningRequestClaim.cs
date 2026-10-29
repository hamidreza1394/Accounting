namespace Domain;

/// <summary>
/// Durable idempotency, replay protection and one-time-enrollment state for a signed customer provisioning package.
/// The request ID, nonce digest and enrollment-token digest are uniquely indexed by the persistence model.
/// </summary>
public sealed class ProvisioningRequestClaim : Seedwork.Entity
{
    public Guid RequestId { get; set; }
    /// <summary>Original protocol nonce used for exact protocol correlation; unique index prevents replay.</summary>
    public string Nonce { get; set; } = string.Empty;
    public string NonceHash { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public Guid? ParentCompanyId { get; set; }
    public string SubsystemsJson { get; set; } = "[]";
    public string CustomerReference { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    /// <summary>UserId intentionally remains a scalar, matching the legacy user store model.</summary>
    public Guid AdminUserId { get; set; }

    /// <summary>SHA-256 digest only; the one-time enrollment token is never persisted in plaintext.</summary>
    public string EnrollmentTokenHash { get; set; } = string.Empty;
    /// <summary>UTC deadline after which the one-time enrollment token is rejected.</summary>
    public DateTime EnrollmentExpiresAtUtc { get; set; }
    public DateTime? EnrollmentConsumedAtUtc { get; set; }
    public DateTime AppliedAtUtc { get; set; }

    /// <summary>When true, the first authenticated session must be restricted to password change.</summary>
    public bool MustChangePassword { get; set; } = true;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
