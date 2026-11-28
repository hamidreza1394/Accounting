using System.Text.Json;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

// Issue #814 regression coverage for Visual Studio startup and console operator ergonomics.
public sealed class VendorLocalStartupConfigurationTests
{
    [Fact]
    public void VisualStudioLaunchProfileUsesTheApplicationRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsPath = Path.Combine(
            repositoryRoot,
            "Vendor",
            "Vendor.Host",
            "Properties",
            "launchSettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var launchUrl = document.RootElement
            .GetProperty("profiles")
            .GetProperty("Vendor.Host")
            .GetProperty("launchUrl")
            .GetString();

        Assert.Equal("/", launchUrl);
    }

    [Fact]
    public void VendorConsolePromptsAndMessagesContainAsciiOnly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceFiles = new[]
        {
            Path.Combine(repositoryRoot, "Vendor", "Vendor.Host", "Program.cs"),
            Path.Combine(repositoryRoot, "Vendor", "Vendor.Host", "Bootstrap", "VendorBootstrapCommand.cs")
        };

        var consoleLines = sourceFiles
            .SelectMany(File.ReadAllLines)
            .Where(line =>
                line.Contains("Console.Write", StringComparison.Ordinal) ||
                line.Contains("Console.Error.Write", StringComparison.Ordinal) ||
                line.Contains("prompt.ReadLine(", StringComparison.Ordinal) ||
                line.Contains("prompt.ReadPassword(", StringComparison.Ordinal) ||
                line.Contains("prompt.WriteLine(", StringComparison.Ordinal) ||
                line.Contains("prompt.WriteSensitiveLine(", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(consoleLines);
        Assert.All(consoleLines, line =>
            Assert.All(line, character => Assert.True(character <= 0x7F, $"Non-ASCII console text found: {line}")));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vendor.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Vendor", "Vendor.Host")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
