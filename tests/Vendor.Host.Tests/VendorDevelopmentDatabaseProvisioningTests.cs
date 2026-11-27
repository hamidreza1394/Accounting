using Microsoft.Data.SqlClient;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure-unit coverage of the Development provisioning pipeline contract: the fixed seven-stage
/// order, input validation (target allow-list, database name, SQL length limits, runtime
/// password length), the generated runtime password shape and the least-privilege runtime
/// connection string. The gated SQL integration tests cover the real pipeline behavior.
/// </summary>
public sealed class VendorDevelopmentDatabaseProvisioningTests
{
    [Fact]
    public void StageContractIsFixedAndSevenStage()
    {
        var expected = new[]
        {
            "connection", "test", "inspect", "createDatabase", "migrate", "runtimeIdentity", "verify"
        };
        Assert.Equal(expected, VendorDevelopmentDatabaseProvisioning.Stages.Select(stage => stage.Key));
        Assert.All(VendorDevelopmentDatabaseProvisioning.Stages, stage =>
            Assert.False(string.IsNullOrWhiteSpace(stage.Title)));
    }

    [Fact]
    public void BuildInputRejectsDisallowedSqlTargets()
    {
        var result = TryBuild("10.1.2.3,1433", authentication: "windows");
        Assert.NotNull(result.Failure);
        Assert.Equal("failed", result.Stages.First(stage => stage.Key == "connection").State);
    }

    [Fact]
    public void BuildInputRejectsSystemDatabaseNames()
    {
        var result = TryBuild("localhost", authentication: "windows", database: "master");
        Assert.NotNull(result.Failure);
        Assert.Contains("سیستمی", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("sql")]
    public void BuildInputAcceptsValidRequests(string authentication)
    {
        var result = TryBuild(
            "localhost", authentication,
            username: authentication == "sql" ? "setup_user" : null,
            password: authentication == "sql" ? "setup-passw0rd!" : null);
        Assert.Null(result.Failure);
        Assert.NotEqual("failed", result.Stages.First(stage => stage.Key == "connection").State);
        var input = result.Input!;
        Assert.Equal("FaranouVendorRuntime", input.RuntimeUsername);
        Assert.Equal("VendorDb", input.Database);
        Assert.True(input.SetupBuilder.Encrypt);
        Assert.False(input.SetupBuilder.TrustServerCertificate);
        Assert.False(input.SetupBuilder.PersistSecurityInfo);
    }

    [Fact]
    public void BuildInputRequiresSetupCredentialsForSqlAuthentication()
    {
        var result = TryBuild("localhost", "sql", username: null, password: null);
        Assert.NotNull(result.Failure);
        Assert.Contains("نام کاربری", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInputRejectsShortManualRuntimePasswords()
    {
        var result = TryBuild("localhost", "windows", runtimePassword: "short12");
        Assert.NotNull(result.Failure);
        Assert.Contains("۸", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInputRejectsOverLongSqlCredentials()
    {
        var result = TryBuild(
            "localhost", "sql",
            username: new string('u', 129),
            password: "setup-passw0rd!");
        Assert.NotNull(result.Failure);
        Assert.Contains("۱۲۸", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedRuntimePasswordIsStrongAndUnambiguous()
    {
        var password = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        Assert.Equal(32, password.Length);
        Assert.All(password, character => Assert.True(
            char.IsUpper(character) || char.IsLower(character) || char.IsDigit(character) ||
            "!@#$%^&*()-_=+[]{}:,.?/~".Contains(character, StringComparison.Ordinal),
            $"unexpected character {character}"));
        Assert.DoesNotContain('\'', password);
        Assert.DoesNotContain('"', password);
        Assert.DoesNotContain('\\', password);
        Assert.DoesNotContain(' ', password);
        Assert.DoesNotContain(';', password);
        Assert.True(password.Any(char.IsUpper), "missing upper-case class");
        Assert.True(password.Any(char.IsLower), "missing lower-case class");
        Assert.True(password.Any(char.IsDigit), "missing digit class");
        Assert.True(password.Any(character => "!@#$%^&*()-_=+[]{}:,.?/~".Contains(character, StringComparison.Ordinal)),
            "missing special class");
    }

    [Fact]
    public void RuntimeConnectionStringIsLeastPrivilegeAndEncrypted()
    {
        var setupBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "localhost,1433",
            InitialCatalog = "master",
            IntegratedSecurity = false,
            UserID = "setup_user",
            Password = "setup-passw0rd!",
            Encrypt = true,
            TrustServerCertificate = false,
            PersistSecurityInfo = false
        };
        var input = new VendorDevelopmentDatabaseProvisioning.ProvisionInput(
            setupBuilder, "VendorDb", "FaranouVendorRuntime", "runtime-passw0rd!", GeneratedRuntimePassword: false);

        var runtime = new SqlConnectionStringBuilder(
            VendorDevelopmentDatabaseProvisioning.BuildRuntimeConnectionString(input));

        Assert.Equal("localhost,1433", runtime.DataSource);
        Assert.Equal("VendorDb", runtime.InitialCatalog);
        Assert.False(runtime.IntegratedSecurity);
        Assert.Equal("FaranouVendorRuntime", runtime.UserID);
        Assert.Equal("runtime-passw0rd!", runtime.Password);
        Assert.True(runtime.Encrypt);
        Assert.False(runtime.TrustServerCertificate);
        Assert.False(runtime.PersistSecurityInfo);
    }

    [Fact]
    public void ProvisionOutcomeFailedHasNoRuntimeFields()
    {
        var outcome = VendorDevelopmentDatabaseProvisioning.ProvisionOutcome.Failed(
            [new VendorDevelopmentDatabaseProvisioning.StageResult("connection", "failed", "خطا")], "خطا");
        Assert.False(outcome.Success);
        Assert.Null(outcome.RuntimeUsername);
        Assert.Null(outcome.GeneratedPassword);
    }

    private sealed record BuildOutcome(
        string? Failure,
        VendorDevelopmentDatabaseProvisioning.ProvisionInput? Input,
        List<VendorDevelopmentDatabaseProvisioning.StageResult> Stages);

    private static BuildOutcome TryBuild(
        string server,
        string authentication,
        string? username = null,
        string? password = null,
        string? database = "VendorDb",
        string? runtimePassword = null)
    {
        var stages = VendorDevelopmentDatabaseProvisioning.Stages
            .Select(stage => new VendorDevelopmentDatabaseProvisioning.StageResult(stage.Key, "pending", string.Empty))
            .ToList();
        var request = new ProvisionRequest(
            Server: server,
            Database: database ?? "VendorDb",
            Authentication: authentication,
            Username: username,
            Password: password,
            TrustServerCertificate: false,
            RuntimeUsername: "FaranouVendorRuntime",
            RuntimePassword: runtimePassword);
        var succeeded = VendorDevelopmentDatabaseProvisioning.TryBuildInput(
            request, [], stages, requireRuntimeIdentity: true, out var input, out var failureMessage);
        return new BuildOutcome(succeeded ? null : failureMessage, input, stages);
    }
}
