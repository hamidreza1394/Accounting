using System.IO;
using Xunit;

namespace Template.Tests;

public sealed class LoginPasswordToggleTests
{
    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory.Parent != null && !File.Exists(Path.Combine(directory.FullName, "Template.sln")))
        {
            directory = directory.Parent;
        }

        Assert.True(File.Exists(Path.Combine(directory.FullName, "Template.sln")), "Repository root could not be located.");

        var path = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Expected repository file was not found: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void FaranouMarkup_UsesPasswordToggleButtonAndMatchingControlId()
    {
        var markup = ReadRepositoryFile("Server/Pages/Account/Login.cshtml");
        var toggleIndex = markup.IndexOf("data-password-toggle=\"ViewModel_Password\"", System.StringComparison.Ordinal);

        Assert.True(toggleIndex >= 0);

        // The toggle button is the <button> opening tag that owns data-password-toggle.
        // Slice only that opening tag (from <button to the next >) so that the separate
        // submit button further down the form is not mistaken for the toggle. The earlier
        // assertion `Assert.DoesNotContain("type=\"submit\"", markup[toggleIndex..])`
        // matched the real submit button that legitimately follows the toggle.
        var buttonStart = markup.LastIndexOf("<button", toggleIndex, System.StringComparison.Ordinal);
        var openTagEnd = markup.IndexOf('>', toggleIndex);
        Assert.True(buttonStart >= 0 && openTagEnd > toggleIndex,
            "Password toggle opening tag could not be located.");
        var toggleOpenTag = markup.Substring(buttonStart, openTagEnd - buttonStart + 1);

        Assert.Contains("type=\"button\"", toggleOpenTag);
        Assert.Contains("aria-controls=\"ViewModel_Password\"", toggleOpenTag);
        Assert.DoesNotContain("type=\"submit\"", toggleOpenTag);
    }

    [Fact]
    public void FaranouScript_ContainsSingleAccessibleStateTransition()
    {
        var script = ReadRepositoryFile("Server/wwwroot/js/taraz-login.js");
        var commonScript = ReadRepositoryFile("Server/wwwroot/js/site.js");

        Assert.Contains("password.type = visible ? 'text' : 'password';", script);
        Assert.Contains("toggle.setAttribute('aria-pressed', String(visible));", script);
        Assert.Contains("event.preventDefault();", script);
        Assert.Contains("[data-password-toggle]').not('.taraz-login-form [data-password-toggle]')", commonScript);
    }
}
