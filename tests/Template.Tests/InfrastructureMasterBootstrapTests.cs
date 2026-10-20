using System.Collections.Generic;
using Infrastructure.DatabaseSetup;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Template.Tests;

public sealed class InfrastructureMasterBootstrapTests
{
	private readonly InfrastructureMasterBootstrap _bootstrap =
		new(new PasswordHashService());

	[Fact]
	public void IsMasterConfigured_WhenBothValuesMissing_ReturnsFalse()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		Assert.False(InfrastructureMasterBootstrap.IsMasterConfigured(configuration));
	}

	[Fact]
	public void IsMasterConfigured_WhenOnlyUsernamePresent_ReturnsFalse()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ApplicationSettings:MasterUsername"] = "admin",
			})
			.Build();

		Assert.False(InfrastructureMasterBootstrap.IsMasterConfigured(configuration));
	}

	[Fact]
	public void IsMasterConfigured_WhenBothPresent_ReturnsTrue()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ApplicationSettings:MasterUsername"] = "admin",
				["ApplicationSettings:MasterPassword"] = "not-empty-hash",
			})
			.Build();

		Assert.True(InfrastructureMasterBootstrap.IsMasterConfigured(configuration));
	}

	[Fact]
	public void Prepare_RejectsPasswordShorterThanFifteenCharacters()
	{
		var result = _bootstrap.Prepare(
			username: "infra-admin",
			password: "short-password",
			passwordConfirmation: "short-password");

		Assert.False(result.Succeeded);
		Assert.Contains("۱۵", result.ErrorMessage);
		Assert.Null(result.PasswordHash);
	}

	[Fact]
	public void Prepare_RejectsMismatchedConfirmation()
	{
		var result = _bootstrap.Prepare(
			username: "infra-admin",
			password: "a-long-enough-passphrase",
			passwordConfirmation: "a-long-enough-passphrase!");

		Assert.False(result.Succeeded);
		Assert.Null(result.PasswordHash);
	}

	[Fact]
	public void Prepare_RejectsEmptyUsername()
	{
		var result = _bootstrap.Prepare(
			username: "  ",
			password: "a-long-enough-passphrase",
			passwordConfirmation: "a-long-enough-passphrase");

		Assert.False(result.Succeeded);
		Assert.Null(result.PasswordHash);
	}

	[Fact]
	public void Prepare_WithValidPassphrase_ReturnsHashAndDoesNotEchoRawPassword()
	{
		const string raw = "correct horse battery staple plan";
		var result = _bootstrap.Prepare(
			username: "infra-admin",
			password: raw,
			passwordConfirmation: raw);

		Assert.True(result.Succeeded);
		Assert.Equal("infra-admin", result.Username);
		Assert.False(string.IsNullOrWhiteSpace(result.PasswordHash));
		Assert.DoesNotContain(raw, result.PasswordHash!);
		Assert.DoesNotContain(raw, result.DevelopmentCommands!);
		Assert.Contains("ApplicationSettings:MasterUsername", result.DevelopmentCommands!);
		Assert.Contains("ApplicationSettings:MasterPassword", result.DevelopmentCommands!);
		Assert.Contains("ApplicationSettings__MasterUsername", result.ProductionEnvironmentVariables!);
		Assert.Contains(result.PasswordHash!, result.ProductionEnvironmentVariables!);

		var verify = new PasswordHashService().Verify(result.PasswordHash, raw);
		Assert.Equal(PasswordVerificationOutcome.Success, verify);
	}

	[Fact]
	public void Prepare_DoesNotCreateUsersTableSemantics_HashVerifiesOnlyAgainstPasswordHashService()
	{
		const string raw = "fifteen-chars-ok";
		var result = _bootstrap.Prepare("ops", raw, raw);
		Assert.True(result.Succeeded);

		var wrong = new PasswordHashService().Verify(result.PasswordHash, "different-password-1");
		Assert.Equal(PasswordVerificationOutcome.Failed, wrong);
	}
}
