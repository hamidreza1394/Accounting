using Microsoft.Data.SqlClient;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure-unit coverage of the Development provisioning flow helpers (Vendor DB full setup):
/// database/login identifier validation, runtime password generation, the fixed seven-stage
/// contract and the least-privilege runtime connection string shape.
/// </summary>
public sealed class VendorDevelopmentDatabaseProvisioningTests
{
    [Theory]
    [InlineData("VendorDb")]
    [InlineData("My Vendor DB-2")]
    [InlineData("db.2026_local")]
    [InlineData("فارنو دیتابیس")]
    public void DatabaseNameAcceptsOrdinaryIdentifiers(string name)
    {
        var valid = VendorDevelopmentDatabaseProvisioning.VendorDatabaseName.TryValidate(
            name, out var validated, out var error);
        Assert.True(valid, error);
        Assert.Equal(name, validated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" VendorDb")]
    [InlineData("VendorDb ")]
    [InlineData(".db")]
    [InlineData("db.")]
    [InlineData("a..b")]
    [InlineData("master")]
    [InlineData("Master")]
    [InlineData("model")]
    [InlineData("msdb")]
    [InlineData("tempdb")]
    [InlineData("ｍａｓｔｅｒ")]
    [InlineData("a]b")]
    [InlineData("a'b")]
    [InlineData("a\\b")]
    [InlineData("a;b")]
    [InlineData("a\u0001b")]
    [InlineData("a\u200Cb")]
    public void DatabaseNameRejectsUnsafeOrSystemNames(string? name)
    {
        var valid = VendorDevelopmentDatabaseProvisioning.VendorDatabaseName.TryValidate(
            name, out _, out var error);
        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DatabaseNameRejectsNamesLongerThanTheFileSafeLimit()
    {
        var tooLong = new string('a', 124);
        Assert.False(VendorDevelopmentDatabaseProvisioning.VendorDatabaseName.TryValidate(
            tooLong, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));

        var maximum = new string('a', 123);
        Assert.True(
            VendorDevelopmentDatabaseProvisioning.VendorDatabaseName.TryValidate(maximum, out _, out error),
            error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("FaranouVendorRuntime")]
    [InlineData("Vendor_Develop_9")]
    public void RuntimeLoginNameAcceptsOrdinaryNames(string name)
    {
        Assert.True(VendorDevelopmentDatabaseProvisioning.VendorRuntimeLoginName.TryValidate(name, out var error), error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("1abc")]
    [InlineData("abc-def")]
    [InlineData("abc def")]
    [InlineData("x]y")]
    [InlineData("sa")]
    [InlineData("SA")]
    [InlineData("dbo")]
    [InlineData("guest")]
    [InlineData("sys")]
    [InlineData("public")]
    [InlineData("information_schema")]
    public void RuntimeLoginNameRejectsInvalidOrReservedNames(string? name)
    {
        var valid = VendorDevelopmentDatabaseProvisioning.VendorRuntimeLoginName.TryValidate(name!, out var error);
        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RuntimeLoginNameRejectsNamesBeyondTheServerLimit()
    {
        Assert.False(VendorDevelopmentDatabaseProvisioning.VendorRuntimeLoginName.TryValidate(new string('a', 129), out _));
    }

    [Fact]
    public void GeneratedRuntimePasswordMeetsComplexityAndExcludesUnsafeCharacters()
    {
        var generated = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < 50; index++)
        {
            var password = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
            Assert.Equal(32, password.Length);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, character => "!@#$%^&*()-_=+[]{}:,.?/~".Contains(character, StringComparison.Ordinal));
            Assert.DoesNotContain(password, character => character is '\'' or '"' or '\\' or ';' or ' ' or '<' or '>' or '`');
            generated.Add(password);
        }

        Assert.True(generated.Count > 40, "the generator must not repeat values in a small sample");
    }

    [Fact]
    public void StagesKeepTheDocumentedSevenStageOrder()
    {
        var stages = VendorDevelopmentDatabaseProvisioning.Stages;
        Assert.Equal(7, stages.Length);
        Assert.Equal(
            ["connection", "test", "inspect", "createDatabase", "migrate", "runtimeIdentity", "verify"],
            stages.Select(stage => stage.Key).ToArray());
        Assert.All(stages, stage => Assert.False(string.IsNullOrWhiteSpace(stage.Title)));
        Assert.Equal(stages.Select(stage => stage.Title).Distinct().Count(), stages.Length);
    }

    [Fact]
    public void RuntimeConnectionStringIsLeastPrivilegeAndCatalogPinned()
    {
        var setupBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "localhost\\SQL2022",
            InitialCatalog = "master",
            IntegratedSecurity = false,
            UserID = "setup-user",
            Password = "setup-pass",
            Encrypt = true,
            TrustServerCertificate = true
        };
        var input = new VendorDevelopmentDatabaseProvisioning.ProvisionInput(
            setupBuilder, "VendorDb", "FaranouVendorRuntime", "runtime-pass", GeneratedRuntimePassword: false);

        var runtime = new SqlConnectionStringBuilder(
            VendorDevelopmentDatabaseProvisioning.BuildRuntimeConnectionString(input));

        Assert.Equal("VendorDb", runtime.InitialCatalog);
        Assert.Equal("FaranouVendorRuntime", runtime.UserID);
        Assert.Equal("runtime-pass", runtime.Password);
        Assert.False(runtime.IntegratedSecurity);
        Assert.True(runtime.Encrypt);
        Assert.True(runtime.TrustServerCertificate);
        Assert.False(runtime.PersistSecurityInfo);
        Assert.Equal("Faranou Vendor Development Runtime", runtime.ApplicationName);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost\\SQL2022")]
    [InlineData("localhost\\SQL2022,1433")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1,1444")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData(".")]
    [InlineData(".\\SQLEXPRESS")]
    [InlineData("(local)")]
    [InlineData("(localdb)\\MSSQLLocalDB")]
    [InlineData("tcp:localhost")]
    [InlineData("tcp:localhost,1433")]
    public void SqlTargetAllowsLocalTargets(string server)
    {
        Assert.True(VendorDevelopmentDatabaseProvisioning.VendorSqlServerTarget.IsAllowed(
            server, [], out var error), error);
    }

    [Theory]
    [InlineData("sql01.corp.example")]
    [InlineData("10.1.2.3")]
    [InlineData("10.1.2.3,1433")]
    [InlineData("tcp:sql01.corp.example")]
    [InlineData("sql01.corp.example\\INST")]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlTargetRejectsNonLocalTargetsByDefault(string server)
    {
        Assert.False(VendorDevelopmentDatabaseProvisioning.VendorSqlServerTarget.IsAllowed(
            server, [], out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void SqlTargetAcceptsExplicitlyAllowedHostsAndTheLocalMachine()
    {
        var machine = Environment.MachineName;
        Assert.True(VendorDevelopmentDatabaseProvisioning.VendorSqlServerTarget.IsAllowed(
            "approved-staging.corp.example", ["approved-staging.corp.example"], out _));
        Assert.True(VendorDevelopmentDatabaseProvisioning.VendorSqlServerTarget.IsAllowed(
            machine, [], out _), $"the local machine '{machine}' must stay a valid target");
        Assert.True(VendorDevelopmentDatabaseProvisioning.VendorSqlServerTarget.IsAllowed(
            $"{machine}\\SQL2022", [], out _));
    }

    [Theory]
    [InlineData("localhost;--")]
    [InlineData("localhost'")]
    [InlineData("localhost\\Instance\\Extra")]
    [InlineData("localhost,0")]
    [InlineData("localhost,70000")]
    [InlineData("localhost,abc")]
    [InlineData("localhost\\\\")]
    public void SqlTargetRejectsMalformedOrInjectionShapedTargets(string server)
    {
        Assert.False(VendorDevelopmentDatabaseProvisioning.VendorSqlServerTarget.IsAllowed(
            server, [], out _));
    }

    [Theory]
    [InlineData(true, false, 0, 0, 0, true)]   // marker alone proves ownership
    [InlineData(false, true, 3, 0, 3, true)]   // applied Faranou history proves ownership (legacy, pre-marker)
    [InlineData(false, false, 0, 0, 0, false)] // empty database without any proof is never adopted
    [InlineData(false, false, 0, 0, 5, false)] // tables without Vendor history are foreign
    [InlineData(false, true, 0, 0, 0, false)]  // empty history table alone proves nothing
    [InlineData(false, true, 2, 1, 3, false)]  // unknown migration ids are refused (newer schema)
    public void OwnershipDecisionFailsSafelyWithoutProof(
        bool markerPresent,
        bool historyTableExists,
        int appliedCount,
        int unknownCount,
        int userTableCount,
        bool expectedAllowed)
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseProvisioning.VendorOwnershipMarker.EvaluateOwnership(
            markerPresent, historyTableExists, appliedCount, unknownCount, userTableCount);

        Assert.Equal(expectedAllowed, allowed);
        if (!expectedAllowed)
        {
            Assert.False(string.IsNullOrWhiteSpace(refusal));
        }
    }

    [Theory]
    [InlineData(new[] { "sysadmin" }, new string[0], true)]
    [InlineData(new[] { "securityadmin" }, new string[0], true)]
    [InlineData(new[] { "dbcreator" }, new string[0], true)]
    [InlineData(new string[0], new[] { "VIEW ANY DATABASE" }, true)]
    [InlineData(new string[0], new[] { "ALTER ANY LOGIN" }, true)]
    [InlineData(new string[0], new[] { "CONNECT SQL" }, false)]
    [InlineData(new string[0], new string[0], false)]
    public void RuntimeIdentityRejectsServerLevelPowerBeyondConnectSql(
        string[] roles,
        string[] grants,
        bool expectedExcessive)
    {
        var excessive = VendorDevelopmentDatabaseProvisioning.VendorRuntimeIdentity.HasExcessiveServerAccess(
            roles, grants, out var detail);
        Assert.Equal(expectedExcessive, excessive);
        if (expectedExcessive)
        {
            Assert.False(string.IsNullOrWhiteSpace(detail));
        }
    }

    [Fact]
    public void InputBuildingRejectsPasswordsAndUsernamesBeyondTheSqlServerLimit()
    {
        var stages = VendorDevelopmentDatabaseProvisioning.Stages
            .Select(stage => new VendorDevelopmentDatabaseProvisioning.StageResult(stage.Key, "pending", string.Empty))
            .ToList();
        var request = ValidRequest();
        request = request with { Username = new string('u', 129) };

        Assert.False(VendorDevelopmentDatabaseProvisioning.TryBuildInput(
            request, [], stages, requireRuntimeIdentity: true, out _, out var failure));
        Assert.Contains("۱۲۸", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void InputBuildingRejectsRuntimePasswordsShorterThanEightCharacters()
    {
        var stages = VendorDevelopmentDatabaseProvisioning.Stages
            .Select(stage => new VendorDevelopmentDatabaseProvisioning.StageResult(stage.Key, "pending", string.Empty))
            .ToList();

        Assert.False(VendorDevelopmentDatabaseProvisioning.TryBuildInput(
            ValidRequest() with { RuntimePassword = "short12" }, [], stages,
            requireRuntimeIdentity: true, out _, out var failure));
        Assert.Contains("۸", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void InputBuildingRejectsProvisioningAgainstNonLocalTargets()
    {
        var stages = VendorDevelopmentDatabaseProvisioning.Stages
            .Select(stage => new VendorDevelopmentDatabaseProvisioning.StageResult(stage.Key, "pending", string.Empty))
            .ToList();

        Assert.False(VendorDevelopmentDatabaseProvisioning.TryBuildInput(
            ValidRequest() with { Server = "remote.example.com" }, [], stages,
            requireRuntimeIdentity: true, out _, out var failure));
        Assert.Contains("مجاز نیست", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void InputBuildingAcceptsALocalTargetWithConsistentLimits()
    {
        var stages = VendorDevelopmentDatabaseProvisioning.Stages
            .Select(stage => new VendorDevelopmentDatabaseProvisioning.StageResult(stage.Key, "pending", string.Empty))
            .ToList();

        Assert.True(VendorDevelopmentDatabaseProvisioning.TryBuildInput(
            ValidRequest() with { Password = new string('p', 128), RuntimePassword = new string('r', 128) },
            [], stages, requireRuntimeIdentity: true, out var input, out var failure),
            failure);
        Assert.Equal("VendorDb", input!.Database);
        Assert.Equal("localhost\\SQL2022", input.SetupBuilder.DataSource);
    }

    private static ProvisionRequest ValidRequest() => new(
        Server: "localhost\\SQL2022",
        Database: "VendorDb",
        Authentication: "sql",
        Username: "setup-user",
        Password: "setup-pass",
        TrustServerCertificate: true,
        RuntimeUsername: "FaranouVendorRuntime",
        RuntimePassword: "runtime-pass-123");
}
