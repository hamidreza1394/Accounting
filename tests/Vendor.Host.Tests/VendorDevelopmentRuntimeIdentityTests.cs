using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure-unit coverage of the runtime identity least-privilege rules (fixed server roles,
/// server grants/denies, cross-database user mappings) and the credential-safety guarantees
/// (sanitized SQL error mapping, encrypted runtime connection strings without persisted
/// security info). The gated SQL integration tests prove the same rules against a real server.
/// </summary>
public sealed class VendorDevelopmentRuntimeIdentityTests
{
    private static bool Evaluate(
        string[] roles,
        string[] grants,
        string[] denies,
        out string detail) =>
        VendorDevelopmentRuntimeIdentity.VendorRuntimeIdentity.HasExcessiveServerAccess(roles, grants, denies, out detail);

    [Fact]
    public void UnprivilegedLoginIsAccepted()
    {
        var excessive = Evaluate([], ["CONNECT SQL"], [], out var detail);
        Assert.False(excessive);
        Assert.Equal(string.Empty, detail);
    }

    [Fact]
    public void EmptyServerStateIsAccepted()
    {
        var excessive = Evaluate([], [], [], out _);
        Assert.False(excessive);
    }

    [Theory]
    [InlineData("sysadmin")]
    [InlineData("serveradmin")]
    [InlineData("securityadmin")]
    [InlineData("processadmin")]
    [InlineData("setupadmin")]
    [InlineData("bulkadmin")]
    [InlineData("diskadmin")]
    [InlineData("dbcreator")]
    public void AnyFixedServerRoleIsExcessive(string role)
    {
        var excessive = Evaluate([role], [], [], out var detail);
        Assert.True(excessive);
        Assert.Contains(role, detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("VIEW ANY DATABASE")]
    [InlineData("ALTER ANY LOGIN")]
    [InlineData("IMPERSONATE ANY LOGIN")]
    [InlineData("CONTROL SERVER")]
    public void AnyServerGrantBeyondConnectSqlIsExcessive(string permission)
    {
        var excessive = Evaluate([], ["CONNECT SQL", permission], [], out var detail);
        Assert.True(excessive);
        Assert.Contains(permission, detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectSqlGrantAloneIsNotExcessive()
    {
        var excessive = Evaluate([], ["CONNECT SQL"], [], out _);
        Assert.False(excessive);
    }

    [Fact]
    public void ConnectSqlDenyMakesTheLoginUnusable()
    {
        var excessive = Evaluate([], ["CONNECT SQL"], ["CONNECT SQL"], out var detail);
        Assert.True(excessive);
        Assert.Contains("CONNECT SQL", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherServerDeniesAreRestrictiveNotExcessive()
    {
        // The flow itself denies VIEW ANY DATABASE on fresh logins; denies other than
        // CONNECT SQL must not disqualify the login.
        var excessive = Evaluate([], ["CONNECT SQL"], ["VIEW ANY DATABASE"], out _);
        Assert.False(excessive);
    }

    [Fact]
    public void DetailListsDistinctFindings()
    {
        var excessive = Evaluate(["sysadmin", "SYSADMIN"], ["CONTROL SERVER", "CONTROL SERVER"], [], out var detail);
        Assert.True(excessive);
        Assert.Equal("sysadmin، CONTROL SERVER", detail);
    }

    // ── Cross-database user mappings ────────────────────────────────────────────────────

    [Fact]
    public void NoOtherUserMappingsAreAccepted()
    {
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateUserMappings(
            "FaranouVendorRuntime", []);
        Assert.True(allowed);
        Assert.Null(refusal);
    }

    [Fact]
    public void AnyOtherUserMappingIsRefusedWithTheDatabaseName()
    {
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateUserMappings(
            "FaranouVendorRuntime", [" CustomerDb "]);
        Assert.False(allowed);
        Assert.NotNull(refusal);
        Assert.Contains("FaranouVendorRuntime", refusal, StringComparison.Ordinal);
        Assert.Contains("CustomerDb", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleOtherMappingsAreAllListedOnce()
    {
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateUserMappings(
            "FaranouVendorRuntime", ["Db1", "db1 ", "Db2"]);
        Assert.False(allowed);
        Assert.Contains("Db1، Db2", refusal, StringComparison.Ordinal);
    }

    // ── Audit coverage: reuse only after a complete cross-database audit ────────────────

    [Fact]
    public void IncompleteDatabaseEnumerationRefusesTheExistingLogin()
    {
        // Without sysadmin/CONTROL SERVER/VIEW ANY DATABASE the setup identity cannot even
        // list every database, so "no mapping found" would be an incomplete proof.
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateAuditCoverage(
            "FaranouVendorRuntime", canEnumerateAllDatabases: false, []);
        Assert.False(allowed);
        Assert.NotNull(refusal);
        Assert.Contains("FaranouVendorRuntime", refusal, StringComparison.Ordinal);
        Assert.Contains("VIEW ANY DATABASE", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteAuditOfEveryDatabaseWithoutExtraMappingIsAccepted()
    {
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateAuditCoverage(
            "FaranouVendorRuntime", canEnumerateAllDatabases: true, []);
        Assert.True(allowed);
        Assert.Null(refusal);
    }

    [Fact]
    public void UnauditableDatabasesRefuseTheExistingLoginAndAreListed()
    {
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateAuditCoverage(
            "FaranouVendorRuntime", canEnumerateAllDatabases: true, [" OfflineDb ", "NoAccessDb", "NoAccessDb"]);
        Assert.False(allowed);
        Assert.NotNull(refusal);
        Assert.Contains("FaranouVendorRuntime", refusal, StringComparison.Ordinal);
        Assert.Contains("OfflineDb، NoAccessDb", refusal, StringComparison.Ordinal);
        Assert.Contains("ممیزی", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void WhitespaceOnlyUnauditableNamesAreIgnored()
    {
        var (allowed, refusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateAuditCoverage(
            "FaranouVendorRuntime", canEnumerateAllDatabases: true, [" ", ""]);
        Assert.True(allowed);
        Assert.Null(refusal);
    }

    // ── Credential safety: sanitized error mapping ──────────────────────────────────────

    [Fact]
    public void SqlErrorNumberMappingAnswersWithFixedGuidanceOnly()
    {
        var message = VendorDevelopmentDatabaseProvisioning.DescribeSqlError(18456);
        Assert.Contains("ورود به SQL Server رد شد", message, StringComparison.Ordinal);
        Assert.DoesNotContain("connection", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownSqlErrorNumbersAnswerWithTheGenericMessage()
    {
        var message = VendorDevelopmentDatabaseProvisioning.DescribeSqlError(99999);
        Assert.Contains("SQL Server", message, StringComparison.Ordinal);
        // The generic answer must never include anything exception-specific, because the
        // exception text can carry host names, user names and connection details.
        Assert.DoesNotContain("Server=", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenericExceptionMessagesNeverReachTheAnswer()
    {
        var sensitive = new InvalidOperationException(
            "Connection string leak: Server=db.internal,1433;Password=S3cret!Value;");
        var message = VendorDevelopmentDatabaseProvisioning.DescribeSqlError(sensitive);
        Assert.DoesNotContain("S3cret", message, StringComparison.Ordinal);
        Assert.DoesNotContain("db.internal", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeoutExceptionsAnswerWithTheTimeoutMessage()
    {
        var message = VendorDevelopmentDatabaseProvisioning.DescribeSqlError(new TimeoutException("host=secret-host"));
        Assert.Contains("مهلت مجاز", message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-host", message, StringComparison.Ordinal);
    }
}
