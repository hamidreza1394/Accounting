using System.Text;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.DatabaseSetup;

/// <summary>
/// Validates and hashes the infrastructure (Master) account for first-run setup.
/// Does not persist secrets: operators place the hash into User Secrets (development)
/// or environment variables / a secret store (production). No Users-table seed.
/// </summary>
public sealed class InfrastructureMasterBootstrap
{
	public const int MinimumPasswordLength = 15;
	public const int MaximumPasswordLength = 256;
	public const int MaximumUsernameLength = 64;

	private readonly PasswordHashService _passwordHashService;

	public InfrastructureMasterBootstrap(PasswordHashService passwordHashService)
	{
		_passwordHashService = passwordHashService
			?? throw new ArgumentNullException(nameof(passwordHashService));
	}

	/// <summary>
	/// Returns true when both Master username and password credential are present
	/// in configuration (any provider: appsettings, User Secrets, environment).
	/// </summary>
	public static bool IsMasterConfigured(IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var username = configuration["ApplicationSettings:MasterUsername"];
		var password = configuration["ApplicationSettings:MasterPassword"];

		return !string.IsNullOrWhiteSpace(username)
			&& !string.IsNullOrWhiteSpace(password);
	}

	public MasterBootstrapResult Prepare(
		string? username,
		string? password,
		string? passwordConfirmation)
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			return MasterBootstrapResult.Invalid("نام کاربری مدیر زیرساختی الزامی است.");
		}

		var fixedUsername = username.Trim();
		if (fixedUsername.Length > MaximumUsernameLength)
		{
			return MasterBootstrapResult.Invalid(
				$"نام کاربری حداکثر {MaximumUsernameLength} نویسه می‌تواند باشد.");
		}

		if (string.IsNullOrEmpty(password))
		{
			return MasterBootstrapResult.Invalid("رمز عبور الزامی است.");
		}

		if (password.Length < MinimumPasswordLength)
		{
			return MasterBootstrapResult.Invalid(
				$"رمز عبور باید حداقل {MinimumPasswordLength} نویسه باشد. استفاده از عبارت عبور توصیه می‌شود.");
		}

		if (password.Length > MaximumPasswordLength)
		{
			return MasterBootstrapResult.Invalid(
				$"رمز عبور حداکثر {MaximumPasswordLength} نویسه می‌تواند باشد.");
		}

		if (!string.Equals(password, passwordConfirmation, StringComparison.Ordinal))
		{
			return MasterBootstrapResult.Invalid("رمز عبور و تکرار آن یکسان نیستند.");
		}

		// Hash only; never return or log the raw password.
		var passwordHash = _passwordHashService.Hash(password);

		var developmentCommands = BuildDevelopmentCommands(fixedUsername, passwordHash);
		var productionVariables = BuildProductionVariables(fixedUsername, passwordHash);

		return MasterBootstrapResult.Success(
			username: fixedUsername,
			passwordHash: passwordHash,
			developmentCommands: developmentCommands,
			productionEnvironmentVariables: productionVariables);
	}

	private static string BuildDevelopmentCommands(string username, string passwordHash)
	{
		var builder = new StringBuilder();
		builder.AppendLine("dotnet user-secrets set \"ApplicationSettings:MasterUsername\" \"" + EscapeForShell(username) + "\" --project Server/Server.csproj");
		builder.AppendLine("dotnet user-secrets set \"ApplicationSettings:MasterPassword\" \"" + EscapeForShell(passwordHash) + "\" --project Server/Server.csproj");
		return builder.ToString().TrimEnd();
	}

	private static string BuildProductionVariables(string username, string passwordHash)
	{
		var builder = new StringBuilder();
		builder.AppendLine("ApplicationSettings__MasterUsername=" + username);
		builder.AppendLine("ApplicationSettings__MasterPassword=" + passwordHash);
		return builder.ToString().TrimEnd();
	}

	private static string EscapeForShell(string value)
	{
		return value.Replace("\"", "\\\"", StringComparison.Ordinal);
	}
}

public sealed class MasterBootstrapResult
{
	private MasterBootstrapResult(
		bool succeeded,
		string? errorMessage,
		string? username,
		string? passwordHash,
		string? developmentCommands,
		string? productionEnvironmentVariables)
	{
		Succeeded = succeeded;
		ErrorMessage = errorMessage;
		Username = username;
		PasswordHash = passwordHash;
		DevelopmentCommands = developmentCommands;
		ProductionEnvironmentVariables = productionEnvironmentVariables;
	}

	public bool Succeeded { get; }
	public string? ErrorMessage { get; }
	public string? Username { get; }
	/// <summary>Hashed credential only; never a raw password.</summary>
	public string? PasswordHash { get; }
	public string? DevelopmentCommands { get; }
	public string? ProductionEnvironmentVariables { get; }

	public static MasterBootstrapResult Invalid(string message) =>
		new(false, message, null, null, null, null);

	public static MasterBootstrapResult Success(
		string username,
		string passwordHash,
		string developmentCommands,
		string productionEnvironmentVariables) =>
		new(true, null, username, passwordHash, developmentCommands, productionEnvironmentVariables);
}
