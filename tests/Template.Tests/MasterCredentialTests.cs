using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Migration of the infrastructure (Master) credential away from the legacy SHA-256 format.
/// Every password here is generated at run time; no fixed secret or real hash is stored.
/// </summary>
public sealed class MasterCredentialTests
{
	private static string NewPassword() => $"Pass-{Guid.NewGuid():N}";

	private static Infrastructure.Security.AuthenticationService CreateService(
		Infrastructure.Settings.ApplicationSettings settings, out Data.DatabaseContext database)
	{
		database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options);

		return new Infrastructure.Security.AuthenticationService(
			databaseContext: database,
			logger: NullLogger.Instance,
			applicationSettings: settings,
			passwordHashService: new Infrastructure.Security.PasswordHashService());
	}

	[Fact]
	public async Task ALegacyMasterCredential_IsAccepted_WhileTheLegacyFormatIsAllowed()
	{
		var password = NewPassword();
		var settings = new Infrastructure.Settings.ApplicationSettings
		{
			MasterUsername = "master",
			MasterPassword = Dtat.Security.Hashing.GetSha256(text: password),
		};

		var service = CreateService(settings, out var database);
		using (database)
		{
			var result = await service.AuthenticateAsync("master", password, "127.0.0.1");

			Assert.Equal(Infrastructure.Security.LoginOutcome.Succeeded, result.Outcome);
			Assert.True(result.IsInfrastructureMaster);
		}
	}

	[Fact]
	public async Task ALegacyMasterCredential_IsRefused_OnceTheLegacyFormatIsSwitchedOff()
	{
		var password = NewPassword();
		var settings = new Infrastructure.Settings.ApplicationSettings
		{
			MasterUsername = "master",
			MasterPassword = Dtat.Security.Hashing.GetSha256(text: password),
			AllowLegacyMasterPasswordHash = false,
		};

		var service = CreateService(settings, out var database);
		using (database)
		{
			var result = await service.AuthenticateAsync("master", password, "127.0.0.1");

			Assert.Equal(Infrastructure.Security.LoginOutcome.InvalidCredentials, result.Outcome);
			// Counted as a failed attempt (lockout applies), like a wrong password.
			Assert.Equal(1, await database.SecurityAuditEvents.CountAsync(current => current.EventType == "LoginFailed"));
		}
	}

	[Fact]
	public async Task ACurrentFormatMasterCredential_IsAccepted_WithTheLegacyFormatSwitchedOff()
	{
		var password = NewPassword();
		var settings = new Infrastructure.Settings.ApplicationSettings
		{
			MasterUsername = "master",
			MasterPassword = new Infrastructure.Security.PasswordHashService().Hash(password),
			AllowLegacyMasterPasswordHash = false,
		};

		var service = CreateService(settings, out var database);
		using (database)
		{
			var result = await service.AuthenticateAsync("master", password, "127.0.0.1");

			Assert.Equal(Infrastructure.Security.LoginOutcome.Succeeded, result.Outcome);
		}
	}

	[Fact]
	public void TheLegacyFormatIsAllowedByDefault_ForCompatibility()
	{
		Assert.True(new Infrastructure.Settings.ApplicationSettings().AllowLegacyMasterPasswordHash);
	}

	private static (int ExitCode, string Output, string Error) RunCommand(params string?[] inputs)
	{
		var queue = new Queue<string?>(inputs);
		var output = new StringWriter();
		var error = new StringWriter();

		var exitCode = Infrastructure.DatabaseSetup.MasterCredentialCommand.Run(
			readLine: () => queue.Count > 0 ? queue.Dequeue() : null,
			readSecret: () => queue.Count > 0 ? queue.Dequeue() : null,
			output: output,
			error: error);

		return (exitCode, output.ToString(), error.ToString());
	}

	[Fact]
	public void TheCommand_PrintsAVerifiableCurrentFormatHash_AndNeverThePassword()
	{
		var password = $"{NewPassword()}-long-enough";

		var (exitCode, output, _) = RunCommand("master", password, password);

		Assert.Equal(0, exitCode);
		Assert.DoesNotContain(password, output, StringComparison.Ordinal);

		var line = Array.Find(output.Split('\n'), current => current.StartsWith("ApplicationSettings__MasterPassword=", StringComparison.Ordinal));
		Assert.NotNull(line);
		var hash = line!["ApplicationSettings__MasterPassword=".Length..].Trim();

		Assert.False(Infrastructure.Security.PasswordHashService.IsLegacySha256Hash(hash));
		Assert.Equal(
			Infrastructure.Security.PasswordVerificationOutcome.Success,
			new Infrastructure.Security.PasswordHashService().Verify(hash, password));
	}

	[Theory]
	[InlineData("short", "short")]
	[InlineData("a-sufficiently-long-password-1", "a-different-long-password-2")]
	public void TheCommand_RefusesAWeakOrUnconfirmedPassword_WithoutPrintingAHash(string password, string confirmation)
	{
		var (exitCode, output, error) = RunCommand("master", password, confirmation);

		Assert.Equal(1, exitCode);
		Assert.False(string.IsNullOrWhiteSpace(error));
		Assert.DoesNotContain("ApplicationSettings__MasterPassword=", output, StringComparison.Ordinal);
	}

	private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
	{
		public string EnvironmentName { get; set; } = "Development";
		public string ApplicationName { get; set; } = "Server";
		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
		public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
			new Microsoft.Extensions.FileProviders.NullFileProvider();
	}

	[Fact]
	public void ALegacyMasterCredential_IsReportedAtStartUp_EvenInDevelopment()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ApplicationSettings:MasterPassword"] = Dtat.Security.Hashing.GetSha256(text: NewPassword()),
			})
			.Build();

		var warnings = Infrastructure.Hosting.DeploymentConfigurationChecks.GetWarnings(configuration, new TestHostEnvironment());

		Assert.Contains(warnings, warning => warning.Contains("--create-master-credential", StringComparison.Ordinal));
	}
}
