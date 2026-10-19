namespace Infrastructure.Security
{
	/// <summary>
	/// The result of verifying a supplied password against a stored credential.
	/// </summary>
	public enum PasswordVerificationOutcome
	{
		/// <summary>The supplied password does not match the stored credential.</summary>
		Failed = 0,

		/// <summary>The supplied password matches a credential in the current format.</summary>
		Success = 1,

		/// <summary>
		/// The supplied password matches, but the stored credential uses an older
		/// (weaker) format and must be replaced by <see cref="PasswordHashService.Hash" />
		/// after a successful sign-in.
		/// </summary>
		SuccessRehashNeeded = 2,
	}

	/// <summary>
	/// Hashes and verifies the passwords of the application.
	/// </summary>
	/// <remarks>
	/// <para>
	/// New credentials are produced by <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}" />
	/// (PBKDF2-HMAC-SHA512, per-password salt, versioned format), which is the standard
	/// ASP.NET Core mechanism. The plain SHA-256 digest that older releases of this
	/// repository stored is still accepted so that existing accounts keep working; such a
	/// credential is reported as <see cref="PasswordVerificationOutcome.SuccessRehashNeeded" />
	/// and is upgraded by the sign-in flow after a successful authentication.
	/// </para>
	/// <para>
	/// The service is stateless and thread-safe, so it is registered as a singleton.
	/// </para>
	/// </remarks>
	public sealed class PasswordHashService : object
	{
		#region Constants
		/// <summary>
		/// Length of a Base64 encoded SHA-256 digest: the format written by the legacy
		/// sign-in implementation of this repository.
		/// </summary>
		public const int LegacySha256HashLength = 44;

		/// <summary>
		/// Length of the raw SHA-256 digest that a legacy credential decodes to.
		/// </summary>
		public const int LegacySha256DigestLength = 32;
		#endregion /Constants

		#region Constructor
		public PasswordHashService() : base()
		{
			Hasher =
				new Microsoft.AspNetCore.Identity.PasswordHasher<Domain.User>();
		}
		#endregion /Constructor

		#region Properties
		private Microsoft.AspNetCore.Identity.PasswordHasher<Domain.User> Hasher { get; }

		/// <summary>
		/// A credential in the current format that no supplied password can match. It is
		/// verified for an unknown username so that a missing account and a wrong password
		/// take the same amount of work and cannot be told apart by timing.
		/// </summary>
		private static readonly System.Lazy<string> DummyHash =
			new(valueFactory: () =>
			{
				var hasher =
					new Microsoft.AspNetCore.Identity.PasswordHasher<Domain.User>();

				return hasher.HashPassword(user: null!, password: "faranou-dummy-password");
			},
			mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
		#endregion /Properties

		#region Methods

		#region Hash
		/// <summary>
		/// Produces a new credential in the current format. Every call uses a fresh random
		/// salt, therefore two calls with the same password return different values.
		/// </summary>
		public string Hash(string password)
		{
			// The hasher ignores the user instance; it is only a type parameter.
			return Hasher.HashPassword(user: null!, password: password);
		}
		#endregion /Hash

		#region Verify
		/// <summary>
		/// Verifies a supplied password against a stored credential of either the current
		/// or the legacy format.
		/// </summary>
		public PasswordVerificationOutcome Verify(string? storedHash, string? password)
		{
			if (string.IsNullOrEmpty(value: storedHash) ||
				string.IsNullOrEmpty(value: password))
			{
				return PasswordVerificationOutcome.Failed;
			}

			if (IsLegacySha256Hash(value: storedHash))
			{
				var suppliedDigest =
					Dtat.Security.Hashing.GetSha256(text: password);

				var isMatch =
					IsFixedTimeEqual(left: suppliedDigest, right: storedHash);

				// A legacy digest is verified by a single SHA-256, which is orders of
				// magnitude cheaper than the PBKDF2 of the current format. Without the
				// extra work below, a request for an existing account that still has a
				// legacy digest would answer faster than a request for an unknown
				// username (which pays for the dummy PBKDF2), and that difference would
				// reveal which accounts are not migrated yet.
				//
				// The result is ignored on purpose: only the cost is wanted, and it is
				// paid for the matching and for the non-matching case alike.
				SimulateVerification(password: password);

				return isMatch
					? PasswordVerificationOutcome.SuccessRehashNeeded
					: PasswordVerificationOutcome.Failed;
			}

			// A stored value that is not a credential of this format (a truncated column,
			// a manual edit, a value of a future format) must fail the sign-in instead of
			// throwing: the caller turns every failure into the same generic message.
			try
			{
				var result =
					Hasher.VerifyHashedPassword
					(user: null!, hashedPassword: storedHash,
					providedPassword: password);

				return result switch
				{
					Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success =>
						PasswordVerificationOutcome.Success,

					Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded =>
						PasswordVerificationOutcome.SuccessRehashNeeded,

					_ => PasswordVerificationOutcome.Failed,
				};
			}
			catch (System.FormatException)
			{
				return PasswordVerificationOutcome.Failed;
			}
			catch (System.ArgumentException)
			{
				return PasswordVerificationOutcome.Failed;
			}
		}
		#endregion /Verify

		#region SimulateVerification
		/// <summary>
		/// Performs the same work as <see cref="Verify" /> without accepting any password.
		/// It is used when the username does not exist, so that the response time does not
		/// disclose whether an account is present.
		/// </summary>
		public void SimulateVerification(string? password)
		{
			Hasher.VerifyHashedPassword
				(user: null!, hashedPassword: DummyHash.Value,
				providedPassword: password ?? string.Empty);
		}
		#endregion /SimulateVerification

		#region IsLegacySha256Hash
		/// <summary>
		/// Detects the credential format of older releases: exactly 44 Base64 characters
		/// that decode to a 32-byte SHA-256 digest. The current format never has that
		/// shape, so the detection is unambiguous.
		/// </summary>
		public static bool IsLegacySha256Hash(string? value)
		{
			if (string.IsNullOrEmpty(value: value) ||
				value!.Length != LegacySha256HashLength)
			{
				return false;
			}

			var buffer =
				new byte[LegacySha256DigestLength];

			return System.Convert.TryFromBase64String
				(s: value, bytes: buffer, bytesWritten: out var bytesWritten)
				&& bytesWritten == LegacySha256DigestLength;
		}
		#endregion /IsLegacySha256Hash

		#region IsFixedTimeEqual
		/// <summary>
		/// Compares two Base64 digests without an early exit, so that the length of the
		/// shared prefix is not measurable.
		/// </summary>
		public static bool IsFixedTimeEqual(string? left, string? right)
		{
			if (left == null || right == null)
			{
				return false;
			}

			var leftBytes =
				System.Text.Encoding.UTF8.GetBytes(s: left);

			var rightBytes =
				System.Text.Encoding.UTF8.GetBytes(s: right);

			return System.Security.Cryptography.CryptographicOperations
				.FixedTimeEquals(left: leftBytes, right: rightBytes);
		}
		#endregion /IsFixedTimeEqual

		#endregion /Methods
	}
}
