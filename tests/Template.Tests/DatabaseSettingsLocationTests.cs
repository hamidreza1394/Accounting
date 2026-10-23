using System;
using System.Collections.Generic;
using System.IO;
using Infrastructure.DatabaseSetup;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Template.Tests;

public sealed class DatabaseSettingsLocationTests
{
	private static IConfiguration Config(string? directory) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(directory is null
				? new Dictionary<string, string?>()
				: new Dictionary<string, string?> { ["Farano:Settings:Directory"] = directory })
			.Build();

	[Fact]
	public void Default_IsLocalApplicationDataFaranoSettings_WithLegacyRoot()
	{
		using var folder = new TempFolder();

		var location = DatabaseSettingsLocation.Resolve(Config(null), folder.Path);

		Assert.Equal(Path.Combine(folder.Path, "Farano", "Settings"), location.Root);
		Assert.Equal(DatabaseSettingsLocation.DefaultSource, location.Source);
		Assert.Equal(Path.Combine(folder.Path, "TarazAva", "Database"), location.LegacyRoot);
		Assert.False(Directory.Exists(location.Root), "Resolve must not create folders.");
	}

	[Fact]
	public void NullConfiguration_UsesDefault_AndRealFolderIsNotHardCoded()
	{
		var location = DatabaseSettingsLocation.Resolve(null, "/some/lad");

		Assert.StartsWith(Path.GetFullPath("/some/lad"), location.Root);
	}

	[Fact]
	public void ConfiguredDirectory_Wins()
	{
		using var folder = new TempFolder();
		var custom = folder.Combine("custom", "farano");

		var location = DatabaseSettingsLocation.Resolve(Config(custom), folder.Combine("lad"));

		Assert.Equal(custom, location.Root);
		Assert.Equal(DatabaseSettingsLocation.ConfigurationSource, location.Source);
	}

	[Fact]
	public void EnvironmentVariableFormIsHonoured()
	{
		using var folder = new TempFolder();
		var custom = folder.Combine("fromenv");
		// The environment provider maps "__" to ":" (variable Farano__Settings__Directory).
		Environment.SetEnvironmentVariable("Farano__Settings__Directory", custom);
		try
		{
			var fromEnvironment = new ConfigurationBuilder().AddEnvironmentVariables().Build();
			Assert.Equal(custom, DatabaseSettingsLocation.Resolve(fromEnvironment, folder.Combine("lad")).Root);
		}
		finally
		{
			Environment.SetEnvironmentVariable("Farano__Settings__Directory", null);
		}

	}

	[Fact]
	public void RelativeConfiguredPath_IsRejected_NamingTheKey()
	{
		var exception = Assert.Throws<InvalidOperationException>(
			() => DatabaseSettingsLocation.Resolve(Config("relative/path"), "/lad"));

		Assert.Contains("Farano:Settings:Directory", exception.Message);
	}

	[Fact]
	public void ConfiguredPathInsideApplicationOrContentRoot_IsRejected()
	{
		using var folder = new TempFolder();
		var app = folder.Combine("app");

		Assert.Throws<InvalidOperationException>(
			() => DatabaseSettingsLocation.Resolve(Config(Path.Combine(app, "settings")), folder.Combine("lad"), app));
		Assert.Throws<InvalidOperationException>(
			() => DatabaseSettingsLocation.Resolve(Config(app), folder.Combine("lad"), app));
		Assert.Throws<InvalidOperationException>(
			() => DatabaseSettingsLocation.Resolve(Config(Path.Combine(app, "x")), folder.Combine("lad"), folder.Combine("other"), app));

		// A sibling with the same prefix is not "inside".
		var sibling = DatabaseSettingsLocation.Resolve(Config(app + "-data"), folder.Combine("lad"), app);
		Assert.Equal(app + "-data", sibling.Root);
	}

	[Fact]
	public void EmptyLocalApplicationData_WithoutConfiguration_Throws_NamingTheKey()
	{
		var exception = Assert.Throws<InvalidOperationException>(() => DatabaseSettingsLocation.Resolve(Config(null), string.Empty));

		Assert.Contains("Farano:Settings:Directory", exception.Message);
		Assert.Contains("Farano__Settings__Directory", exception.Message);
	}

	[Fact]
	public void EmptyLocalApplicationData_WithConfiguredPath_Works_WithoutLegacy()
	{
		using var folder = new TempFolder();

		var location = DatabaseSettingsLocation.Resolve(Config(folder.Combine("c")), string.Empty);

		Assert.Null(location.LegacyRoot);
	}

	[Fact]
	public void BrandConstants_AreInOneClass()
	{
		Assert.Equal("Farano", FaranoSettingsNames.Name);
		Assert.Equal("Farano.DatabaseConnectionSettings.v2", FaranoSettingsNames.ProtectorPurpose);
		Assert.Equal("TarazAva.DatabaseConnectionSettings.v1", LegacySettingsNames.ProtectorPurpose);
		Assert.Equal("TarazAva", LegacySettingsNames.ApplicationName);
	}

	[Fact]
	public void WebRootIsForbiddenToo()
	{
		using var folder = new TempFolder();
		var webRoot = folder.Combine("app", "wwwroot");

		Assert.Throws<InvalidOperationException>(() => DatabaseSettingsLocation.Resolve(
			Config(Path.Combine(webRoot, "settings")), folder.Combine("lad"), folder.Combine("elsewhere"), folder.Combine("elsewhere"), webRoot));
	}

	private static bool TryLink(string link, string target)
	{
		try
		{
			Directory.CreateSymbolicLink(link, target);
			return true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
		{
			return false;
		}
	}

	[Fact]
	public void SymlinkIntoTheApplicationFolder_IsRejected_EvenForANotYetExistingTail()
	{
		using var folder = new TempFolder();
		var app = folder.Combine("app");
		Directory.CreateDirectory(app);
		var link = folder.Combine("innocent-link");
		Assert.SkipUnless(TryLink(link, app), "Symbolic links are not allowed here.");

		Assert.Throws<InvalidOperationException>(() => DatabaseSettingsLocation.Resolve(
			Config(Path.Combine(link, "settings", "deeper")), folder.Combine("lad"), app));
		Assert.Throws<InvalidOperationException>(() => DatabaseSettingsLocation.Resolve(Config(link), folder.Combine("lad"), app));
	}

	[Fact]
	public void ApplicationFolderReachedThroughASymlink_StillCountsAsTheApplicationFolder()
	{
		using var folder = new TempFolder();
		var realApp = folder.Combine("real-app");
		Directory.CreateDirectory(realApp);
		var appLink = folder.Combine("app-link");
		Assert.SkipUnless(TryLink(appLink, realApp), "Symbolic links are not allowed here.");

		Assert.Throws<InvalidOperationException>(() => DatabaseSettingsLocation.Resolve(
			Config(Path.Combine(realApp, "settings")), folder.Combine("lad"), appLink));
	}

	[Fact]
	public void SymlinkToAnUnrelatedFolder_IsAccepted()
	{
		using var folder = new TempFolder();
		var data = folder.Combine("data");
		Directory.CreateDirectory(data);
		var link = folder.Combine("data-link");
		Assert.SkipUnless(TryLink(link, data), "Symbolic links are not allowed here.");

		var location = DatabaseSettingsLocation.Resolve(Config(Path.Combine(link, "farano")), folder.Combine("lad"), folder.Combine("app"));

		Assert.Equal(Path.Combine(link, "farano"), location.Root);
	}
}
