namespace Domain;

/// <summary>
/// One server-side sign-in session of a company user (D-069). The
/// <c>SessionId</c> claim of the authentication cookie is the identifier of this
/// row and is re-validated on every request (<c>Infrastructure.Security.SessionValidator</c>),
/// so revoking a row ends that device's access on its next request while the other
/// sessions of the same user keep working.
/// </summary>
/// <remarks>
/// The session belongs to the installation account of the user, never to a company
/// or tenant: the company context is chosen after the sign-in. No IP address and no
/// user agent are stored here; connection metadata stays in the bounded audit events
/// (login, logout, revocation). The infrastructure (master) account has no session rows.
/// </remarks>
public sealed class UserSession
{
        /// <summary>The random identifier of this sign-in; carried in the cookie as <c>SessionId</c>.</summary>
        [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None)]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public User? User { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        /// <summary>Refreshed at most once per five minutes while the session is used.</summary>
        public DateTime LastSeenAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        public Guid? RevokedByUserId { get; set; }

        /// <summary>A short code such as <c>self_revoked</c>, <c>revoked_others</c> or <c>password_changed</c>.</summary>
        public string? RevocationReasonCode { get; set; }

        public byte[] RowVersion { get; set; } = [];
}
