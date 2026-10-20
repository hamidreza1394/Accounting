using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Security
{
	/// <summary>
	/// The outcome of a sign-in attempt. Every failure of a credential is reported
	/// with the same generic <see cref="InvalidCredentials" /> result, so that the
	/// existence of an account is never disclosed by the login flow.
	/// </summary>
	public enum LoginOutcome
	{
		InvalidCredentials,
		TemporarilyLocked,
		AccountIsNotActive,
		Succeeded,
	}

	public sealed class LoginResult : object
	{
		public LoginOutcome Outcome { get; init; }

		public Domain.User? User { get; init; }

		/// <summary>
		/// The infrastructure (master) account of the deployment. It is not a
		/// company user and therefore has no company memberships.
		/// </summary>
		public bool IsInfrastructureMaster { get; init; }

		/// <summary>
		/// The identifiers of the companies the user is an active member of. The
		/// list is derived from the validated database memberships only - a
		/// company identifier is never taken from client input.
		/// </summary>
		public System.Collections.Generic.List<System.Guid> ActiveCompanyIds { get; init; } =
			new();

		/// <summary>
		/// The stamp of the credential that was verified (see <see cref="SessionValidator" />).
		/// A later change of the stored credential invalidates the session.
		/// </summary>
		public string? CredentialStamp { get; init; }
	}

	/// <summary>
	/// Verifies sign-in credentials against the real user store (the Users table)
	/// and, as the documented bootstrap path of the deployment, against the
	/// configured infrastructure (master) account. The service records security
	/// audit events (never a password or a token), throttles failed attempts and
	/// keeps the LoginLogs and the LastLoginDateTime of a user up to date.
	/// </summary>
	public sealed class AuthenticationService : object
	{
		#region Constants
		/// <summary>
		/// The number of failed sign-in attempts for one username that is allowed
		/// inside <see cref="FailureWindow" /> before sign-in is temporarily locked.
		/// </summary>
		public const int MaxFailedAttempts = 5;

		public static readonly System.TimeSpan FailureWindow =
			System.TimeSpan.FromMinutes(15);

		private const string LoginSucceededEvent = "LoginSucceeded";
		private const string LoginFailedEvent = "LoginFailed";
		private const string LoginDeniedInactiveEvent = "LoginDeniedInactive";
		private const string UserEntityType = "User";
		private const string MasterEntityType = "MasterAccount";
		private const string UnknownUserIP = "0.0.0.0";
		#endregion /Constants

		#region Constructor
		public AuthenticationService
			(Data.DatabaseContext databaseContext,
			Microsoft.Extensions.Logging.ILogger logger,
			Infrastructure.Settings.ApplicationSettings applicationSettings,
			PasswordHashService passwordHashService) : base()
		{
			DatabaseContext = databaseContext;
			Logger = logger;
			ApplicationSettings = applicationSettings;
			PasswordHashService = passwordHashService;
		}
		#endregion /Constructor

		#region Properties
		private Data.DatabaseContext DatabaseContext { get; }
		private Microsoft.Extensions.Logging.ILogger Logger { get; }
		private Infrastructure.Settings.ApplicationSettings ApplicationSettings { get; }
		private PasswordHashService PasswordHashService { get; }
		#endregion /Properties

		#region Methods

		#region GetLockoutKey
		/// <summary>
		/// Failed attempts are counted per submitted username. The key is stored in
		/// the audit trail, so the lockout behaves the same for an existing and for
		/// an unknown username and reveals nothing about the account.
		/// </summary>
		public static string GetLockoutKey(string username)
		{
			return username.Trim().ToLowerInvariant();
		}
		#endregion /GetLockoutKey

		#region IsTemporarilyLockedAsync
		public async Task<bool> IsTemporarilyLockedAsync(string lockoutKey)
		{
			var windowStartUtc =
				System.DateTime.UtcNow.Subtract(value: FailureWindow);

			var failedAttempts =
				await DatabaseContext.SecurityAuditEvents
				.Where(current => current.EventType == LoginFailedEvent)
				.Where(current => current.EntityId == lockoutKey)
				.Where(current => current.OccurredAtUtc >= windowStartUtc)
				.CountAsync();

			return failedAttempts >= MaxFailedAttempts;
		}
		#endregion /IsTemporarilyLockedAsync

		#region AuthenticateAsync
		public async Task<LoginResult> AuthenticateAsync
			(string? username, string? password, string? userIP)
		{
			if (string.IsNullOrWhiteSpace(value: username) ||
				string.IsNullOrWhiteSpace(value: password))
			{
				return new LoginResult { Outcome = LoginOutcome.InvalidCredentials, };
			}

			var fixedUsername = username.Trim();
			var lockoutKey = GetLockoutKey(username: fixedUsername);
			var auditIP = GetAuditIP(userIP: userIP);

			if (await IsTemporarilyLockedAsync(lockoutKey: lockoutKey))
			{
				return new LoginResult { Outcome = LoginOutcome.TemporarilyLocked, };
			}

			// **************************************************
			// The infrastructure (master) account of the deployment, configured in
			// ApplicationSettings. It is kept as the bootstrap/administrator path
			// and is distinct from any company user.
			// **************************************************
			var isMasterUsernameConfigured =
				!string.IsNullOrWhiteSpace(value: ApplicationSettings.MasterUsername) &&
				!string.IsNullOrWhiteSpace(value: ApplicationSettings.MasterPassword);

			if (isMasterUsernameConfigured &&
				string.Equals(fixedUsername, ApplicationSettings.MasterUsername,
				comparisonType: System.StringComparison.OrdinalIgnoreCase))
			{
				// The configured value may be a current-format credential (produced by
				// PasswordHashService) or the legacy Base64 SHA-256 digest that older
				// deployments used. Both are accepted; the configuration itself is never
				// rewritten by the application.
				var masterPasswordOutcome =
					PasswordHashService.Verify
					(storedHash: ApplicationSettings.MasterPassword,
					password: password);

				var isMasterPasswordCorrect =
					masterPasswordOutcome != PasswordVerificationOutcome.Failed;

				if (isMasterPasswordCorrect)
				{
					if (masterPasswordOutcome ==
						PasswordVerificationOutcome.SuccessRehashNeeded)
					{
						// Only the fact of the weak format is logged, never the value.
						Logger.LogWarning(
							"The configured infrastructure account password uses the legacy SHA-256 format. Replace it with a credential produced by the password hasher before production.");
					}

					await WriteAuditEventAsync
						(eventType: LoginSucceededEvent, entityType: MasterEntityType,
						entityId: lockoutKey, userId: null, auditIP: auditIP);

					return new LoginResult
					{
						Outcome = LoginOutcome.Succeeded,
						IsInfrastructureMaster = true,
						CredentialStamp =
							SessionValidator.ComputeCredentialStamp(ApplicationSettings.MasterPassword),
					};
				}

				await RegisterFailedAttemptAsync(lockoutKey: lockoutKey, auditIP: auditIP);

				return new LoginResult { Outcome = LoginOutcome.InvalidCredentials, };
			}
			// **************************************************

			// **************************************************
			var user =
				await DatabaseContext.Users
				.Include(current => current.Role)
				.Where(current => current.Username != null)
				.Where(current => current.Username!.ToLower() == fixedUsername.ToLower())
				.FirstOrDefaultAsync();
			// **************************************************

			if (user == null)
			{
				// Same work as a wrong password, so a missing account is not faster.
				PasswordHashService.SimulateVerification(password: password);

				await RegisterFailedAttemptAsync(lockoutKey: lockoutKey, auditIP: auditIP);

				return new LoginResult { Outcome = LoginOutcome.InvalidCredentials, };
			}

			var passwordOutcome =
				PasswordHashService.Verify
				(storedHash: user.Password, password: password);

			var isPasswordCorrect =
				passwordOutcome != PasswordVerificationOutcome.Failed;

			if (!isPasswordCorrect)
			{
				await RegisterFailedAttemptAsync
					(lockoutKey: lockoutKey, auditIP: auditIP, userId: user.Id);

				return new LoginResult { Outcome = LoginOutcome.InvalidCredentials, };
			}

			if (user.IsActive == false)
			{
				await WriteAuditEventAsync
					(eventType: LoginDeniedInactiveEvent, entityType: UserEntityType,
					entityId: lockoutKey, userId: user.Id, auditIP: auditIP);

				return new LoginResult
				{
					Outcome = LoginOutcome.AccountIsNotActive,
					User = user,
				};
			}

			// **************************************************
			// Successful sign-in: the login is recorded without any credential.
			// **************************************************
			user.LastLoginDateTime = Dtat.Utility.Now;

			// A credential in the legacy SHA-256 format is upgraded in place after a
			// successful sign-in. The password itself is known only here, so this is the
			// only point where the upgrade can happen without asking the user again.
			if (passwordOutcome == PasswordVerificationOutcome.SuccessRehashNeeded)
			{
				user.Password =
					PasswordHashService.Hash(password: password!);
			}

			var loginLog = new Domain.LoginLog
				(userId: user.Id, userIP: NormalizeLoginLogIP(userIP: userIP));

			await DatabaseContext.LoginLogs.AddAsync(entity: loginLog);

			var activeCompanyIds =
				await DatabaseContext.UserCompanyMemberships
				.Where(current => current.UserId == user.Id)
				.Where(current => current.IsActive)
				.Where(current => current.Company.IsActive)
				.Select(current => current.TenantId)
				.Distinct()
				.ToListAsync();

			await WriteAuditEventAsync
				(eventType: LoginSucceededEvent, entityType: UserEntityType,
				entityId: lockoutKey, userId: user.Id, auditIP: auditIP);

			await DatabaseContext.SaveChangesAsync();
			// **************************************************

			return new LoginResult
			{
				Outcome = LoginOutcome.Succeeded,
				User = user,
				ActiveCompanyIds = activeCompanyIds,
				// Computed after a possible rehash, so the stamp matches the stored value.
				CredentialStamp = SessionValidator.ComputeCredentialStamp(user.Password),
			};
		}
		#endregion /AuthenticateAsync

		#region RegisterFailedAttemptAsync
		private async Task RegisterFailedAttemptAsync
			(string lockoutKey, string auditIP, System.Guid? userId = null)
		{
			await WriteAuditEventAsync
				(eventType: LoginFailedEvent, entityType: UserEntityType,
				entityId: lockoutKey, userId: userId, auditIP: auditIP);

			await DatabaseContext.SaveChangesAsync();
		}
		#endregion /RegisterFailedAttemptAsync

		#region WriteAuditEventAsync
		private async Task WriteAuditEventAsync
			(string eventType, string entityType, string entityId,
			System.Guid? userId, string auditIP)
		{
			// The audit trail never contains a password, a hash or a token.
			var auditEvent = new Domain.SecurityAuditEvent
			{
				EventType = eventType,
				EntityType = entityType,
				EntityId = entityId,
				UserId = userId,
				IpAddress = auditIP,
				OccurredAtUtc = System.DateTime.UtcNow,
				CorrelationId = System.Diagnostics.Activity.Current?.Id,
			};

			await DatabaseContext.SecurityAuditEvents.AddAsync(entity: auditEvent);

			Logger.LogInformation
				("Sign-in audit event {EventType} for {EntityType} '{EntityId}' from {IP}.",
				eventType, entityType, entityId, auditIP);
		}
		#endregion /WriteAuditEventAsync

		#region IsFixedTimeEqual
		/// <summary>
		/// Compares two Base64 digests without an early exit, so that the length of
		/// the shared prefix is not measurable. The implementation lives in
		/// <see cref="PasswordHashService" /> and is kept here for callers and tests
		/// that use the authentication service directly.
		/// </summary>
		public static bool IsFixedTimeEqual(string? left, string? right)
		{
			return PasswordHashService.IsFixedTimeEqual(left: left, right: right);
		}
		#endregion /IsFixedTimeEqual

		#region GetAuditIP
		public static string GetAuditIP(string? userIP)
		{
			if (string.IsNullOrWhiteSpace(value: userIP))
			{
				return UnknownUserIP;
			}

			return userIP.Length <= 50 ? userIP : userIP.Substring(startIndex: 0, length: 50);
		}
		#endregion /GetAuditIP

		#region NormalizeLoginLogIP
		/// <summary>
		/// The UserIP column of LoginLogs is an IPv4 varchar(15); an IPv6 address is
		/// reduced to the IPv4 form when possible and recorded as-is in the audit
		/// trail instead.
		/// </summary>
		public static string NormalizeLoginLogIP(string? userIP)
		{
			if (System.Net.IPAddress.TryParse(ipString: userIP, out var parsed))
			{
				if (parsed.IsIPv4MappedToIPv6)
				{
					parsed = parsed.MapToIPv4();
				}

				if (parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					return parsed.ToString();
				}
			}

			return UnknownUserIP;
		}
		#endregion /NormalizeLoginLogIP

		#endregion /Methods
	}
}
