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
        Assert.Contains("type=\"button\"", markup);
        Assert.Contains("aria-controls=\"ViewModel_Password\"", markup);
        Assert.DoesNotContain("type=\"submit\"", markup[toggleIndex..]);
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
