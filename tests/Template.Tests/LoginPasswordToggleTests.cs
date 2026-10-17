using System.IO;
using Xunit;

namespace Template.Tests;

public sealed class LoginPasswordToggleTests
{
    private static string ReadRepositoryFile(string relativePath)
    {
        var root = Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.Parent?.Parent?.FullName;
        Assert.False(string.IsNullOrWhiteSpace(root));

        var path = Path.Combine(root!, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
        Assert.Contains("data-password-icon", script);
        Assert.Contains("[data-password-toggle]').not('.taraz-login-form [data-password-toggle]')", commonScript);
    }
}
