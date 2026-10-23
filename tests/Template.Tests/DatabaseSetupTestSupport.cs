using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infrastructure.DatabaseSetup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Template.Tests;

/// <summary>A temp folder that is removed afterwards. Tests never touch the real user profile.</summary>
internal sealed class TempFolder : IDisposable
{
	public TempFolder()
	{
		Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "farano-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path);
	}

	public string Path { get; }

	public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path, recursive: true);
		}
		catch (IOException)
		{
			// Best effort cleanup of a temp folder.
		}
	}
}

/// <summary>Creates settings of the previous product version with its real protector.</summary>
internal static class LegacyFixture
{
	public static string Create(string legacyRoot, string connectionString)
	{
		var keys = System.IO.Path.Combine(legacyRoot, "keys");
		Directory.CreateDirectory(keys);
		var provider = DataProtectionProvider.Create(new DirectoryInfo(keys), builder => builder.SetApplicationName("TarazAva"));
		var protectedValue = provider.CreateProtector("TarazAva.DatabaseConnectionSettings.v1").Protect(connectionString);
		var file = System.IO.Path.Combine(legacyRoot, "database-settings.json");
		File.WriteAllText(
			file,
			JsonSerializer.Serialize(new { ProtectedConnectionString = protectedValue, SavedAtUtc = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
		return file;
	}

	/// <summary>Hash of every file name and content below the folder (detects any change).</summary>
	public static string Snapshot(string folder)
	{
		var builder = new StringBuilder();
		foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
		{
			builder.Append(System.IO.Path.GetRelativePath(folder, file)).Append('|')
				.Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))).Append('\n');
		}

		return builder.ToString();
	}
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
	public List<string> Messages { get; } = [];

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var text = formatter(state, exception);
		if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
		{
			text += " " + string.Join(" ", pairs.Select(pair => pair.Key + "=" + pair.Value));
		}

		Messages.Add(text + (exception is null ? string.Empty : " EX:" + exception));
	}

	public string All => string.Join("\n", Messages);
}
