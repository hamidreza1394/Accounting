using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure-unit coverage of the Development provisioning safety primitives: SQL identifier
/// quoting, database and runtime login name validation, the local SQL target allow-list, the
/// ownership decision matrix (including a fabricated marker value) and the crash-window
/// recovery decision. No SQL access is required; the gated SQL integration tests cover the
/// real marker and intent roundtrips.
/// </summary>
public sealed class VendorDevelopmentDatabaseSafetyTests
{
    // ── SQL identifier quoting ──────────────────────────────────────────────────────────

    [Fact]
    public void IdentifierQuotingWrapsInBrackets()
    {
        Assert.Equal("[VendorDb]", VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote("VendorDb"));
    }

    [Fact]
    public void IdentifierQuotingEscapesClosingBracketsAsDefenseInDepth()
    {
        Assert.Equal("[a]]b]", VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote("a]b"));
        Assert.Equal("[My ]]]] DB]", VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote("My ]] DB"));
    }

    // ── Database name validation ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("VendorDb")]
    [InlineData("My Vendor DB-2")]
    [InlineData("db.2026_local")]
    [InlineData("فارنو دیتابیس")]
    public void DatabaseNameAcceptsOrdinaryIdentifiers(string name)
    {
        var valid = VendorDevelopmentDatabaseSafety.VendorDatabaseName.TryValidate(
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
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a/b")]
    [InlineData("a\u0001b")]
    [InlineData("a\u200Cb")]
    public void DatabaseNameRejectsUnsafeOrSystemNames(string? name)
    {
        var valid = VendorDevelopmentDatabaseSafety.VendorDatabaseName.TryValidate(
            name, out _, out var error);
        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DatabaseNameRejectsNamesLongerThanTheFileSafeLimit()
    {
        var tooLong = new string('a', 124);
        Assert.False(VendorDevelopmentDatabaseSafety.VendorDatabaseName.TryValidate(
            tooLong, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));

        var maximum = new string('a', 123);
        Assert.True(VendorDevelopmentDatabaseSafety.VendorDatabaseName.TryValidate(
            maximum, out _, out error));
    }

    // ── Runtime login name validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("FaranouVendorRuntime")]
    [InlineData("Vendor_Runtime_2")]
    public void LoginNameAcceptsStrictAsciiIdentifiers(string name)
    {
        var valid = VendorDevelopmentDatabaseSafety.VendorRuntimeLoginName.TryValidate(name, out var error);
        Assert.True(valid, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("2nd")]
    [InlineData("log in")]
    [InlineData("login-2")]
    [InlineData("sa")]
    [InlineData("dbo")]
    [InlineData("guest")]
    [InlineData("sys")]
    [InlineData("public")]
    [InlineData("information_schema")]
    [InlineData("ログイン")]
    public void LoginNameRejectsReservedOrUnsafeNames(string name)
    {
        var valid = VendorDevelopmentDatabaseSafety.VendorRuntimeLoginName.TryValidate(name, out var error);
        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    // ── SQL target allow-list ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost,1433")]
    [InlineData("localhost\\SQLEXPRESS")]
    [InlineData("localhost\\SQLEXPRESS,1433")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("tcp:localhost,1433")]
    [InlineData("(local)")]
    [InlineData("(localdb)\\MSSQLLocalDB")]
    [InlineData(".")]
    [InlineData(".\\SQLEXPRESS")]
    public void TargetAllowsLocalShapesOnly(string server)
    {
        var allowed = VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            server, [], out var error, machineName: "OWNER-MACHINE");
        Assert.True(allowed, error);
    }

    [Fact]
    public void TargetAllowsTheActualMachineName()
    {
        var allowed = VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            "owner-machine\\SQL2022", [], out var error, machineName: "OWNER-MACHINE");
        Assert.True(allowed, error);
    }

    [Theory]
    [InlineData("staging.internal")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.10,1433")]
    [InlineData("0.0.0.0")]
    [InlineData("169.254.169.254")]
    [InlineData("host.example.com\\instance")]
    [InlineData("local.localhost")]
    [InlineData("*.internal")]
    [InlineData("localhost*")]
    [InlineData("localhost\\instance\\extra")]
    [InlineData("localhost\\instance,0")]
    [InlineData("localhost\\instance,70000")]
    [InlineData("localhost,")]
    [InlineData("localhost\\")]
    [InlineData("")]
    [InlineData("  ")]
    public void TargetRejectsRemoteWildcardAndMalformedShapes(string server)
    {
        var allowed = VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            server, [], out _, machineName: "OWNER-MACHINE");
        Assert.False(allowed);
    }

    [Fact]
    public void TargetAllowsExplicitlyAllowListedRemoteHostsOnlyByExactName()
    {
        var allowList = new[] { "staging-db.internal", " 10.20.30.40 " };
        Assert.True(VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            "STAGING-DB.INTERNAL", allowList, out _, machineName: "OWNER-MACHINE"));
        Assert.True(VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            "10.20.30.40,1433", allowList, out _, machineName: "OWNER-MACHINE"));
        Assert.False(VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            "staging-db2.internal", allowList, out _, machineName: "OWNER-MACHINE"));
        Assert.False(VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            "sub.staging-db.internal", allowList, out _, machineName: "OWNER-MACHINE"));
    }

    [Fact]
    public void TargetAllowListEntriesCannotActAsWildcards()
    {
        var allowList = new[] { "*" };
        Assert.False(VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(
            "anything.remote", allowList, out _, machineName: "OWNER-MACHINE"));
    }

    // ── Ownership decision matrix ───────────────────────────────────────────────────────

    [Fact]
    public void OwnershipAcceptsAValidMarker()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid,
            historyTableExists: false, appliedCount: 0, unknownCount: 0, userTableCount: 0);
        Assert.True(allowed);
        Assert.Null(refusal);
    }

    [Fact]
    public void OwnershipAcceptsALegacyDatabaseProvenByAppliedVendorHistory()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent,
            historyTableExists: true, appliedCount: 7, unknownCount: 0, userTableCount: 7);
        Assert.True(allowed);
        Assert.Null(refusal);
    }

    [Fact]
    public void OwnershipRefusesAMigratedDatabaseWithUnknownMigrationIds()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid,
            historyTableExists: true, appliedCount: 9, unknownCount: 2, userTableCount: 7);
        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    [Fact]
    public void OwnershipRefusesAnUnmarkedDatabaseWithUserTables()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent,
            historyTableExists: false, appliedCount: 0, unknownCount: 0, userTableCount: 12);
        Assert.False(allowed);
        Assert.Contains("جدول", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipRefusesAnEmptyUnmarkedDatabase()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent,
            historyTableExists: false, appliedCount: 0, unknownCount: 0, userTableCount: 0);
        Assert.False(allowed);
        Assert.Contains("تصاحب", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipRefusesAnEmptyHistoryTableWithoutMarker()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent,
            historyTableExists: true, appliedCount: 0, unknownCount: 0, userTableCount: 0);
        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    [Fact]
    public void OwnershipRefusesAFabricatedMarkerValue()
    {
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.InvalidValue,
            historyTableExists: false, appliedCount: 0, unknownCount: 0, userTableCount: 0);
        Assert.False(allowed);
        Assert.Contains("جعلی", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipRefusesAFabricatedMarkerEvenWithVendorHistory()
    {
        // History alone proves ownership only when no conflicting marker exists; a foreign
        // marker value means someone else owns (or tampered with) this database.
        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.InvalidValue,
            historyTableExists: true, appliedCount: 9, unknownCount: 0, userTableCount: 7);
        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    // ── Crash-window recovery decision ─────────────────────────────────────────────────

    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IntentValueRoundTrips()
    {
        var value = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.FormatValue("VendorDb", Now);
        var parsed = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.TryParse(
            value, out var name, out var issuedUtc);
        Assert.True(parsed);
        Assert.Equal("VendorDb", name);
        Assert.Equal(Now, issuedUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("junk")]
    [InlineData("VendorDb=;IssuedUtc=x")]
    [InlineData("IssuedUtc=2026-11-27T12:00:00.000Z;VendorDb=VendorDb")]
    [InlineData("VendorDb=VendorDb;IssuedUtc=not-a-date")]
    public void IntentParsingRejectsMalformedValues(string? value)
    {
        var parsed = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.TryParse(
            value, out _, out _);
        Assert.False(parsed);
    }

    [Fact]
    public void RecoveryIsAllowedForAnEmptyUnmarkedDatabaseWithAFreshMatchingIntent()
    {
        var value = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.FormatValue("VendorDb", Now.AddMinutes(-2));
        var state = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(value, "VendorDb", Now);
        Assert.Equal(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Matching, state);

        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.EvaluateRecovery(
            state, historyTableExists: false, userTableCount: 0);
        Assert.True(allowed);
        Assert.Null(refusal);
    }

    [Fact]
    public void RecoveryIsRefusedWhenTheDatabaseHasContent()
    {
        var value = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.FormatValue("VendorDb", Now.AddMinutes(-2));
        var state = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(value, "VendorDb", Now);

        var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.EvaluateRecovery(
            state, historyTableExists: false, userTableCount: 3);
        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    [Fact]
    public void RecoveryIsRefusedWithoutIntentOrForForeignOrExpiredIntents()
    {
        var evaluate = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.EvaluateRecovery;

        Assert.False(evaluate(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Absent,
            false, 0).Allowed);

        var stale = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.FormatValue("VendorDb", Now.AddHours(-25));
        Assert.Equal(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Expired,
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(stale, "VendorDb", Now));
        Assert.False(evaluate(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Expired,
            false, 0).Allowed);

        var foreign = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.FormatValue("OtherDb", Now.AddMinutes(-1));
        Assert.Equal(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.ForOtherDatabase,
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(foreign, "VendorDb", Now));
        Assert.False(evaluate(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.ForOtherDatabase,
            false, 0).Allowed);
    }

    [Fact]
    public void ClassifyHandlesAbsentAndMalformedValues()
    {
        var classify = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify;
        Assert.Equal(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Absent, classify(null, "VendorDb", Now));
        Assert.Equal(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Absent, classify("", "VendorDb", Now));
        Assert.Equal(VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.NotMatching, classify("junk", "VendorDb", Now));
    }

    [Fact]
    public void RecoveryMessageNamesTheDatabaseAndNeverMentionsDroppingIt()
    {
        var message = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.RecoveryMessage("VendorDb");
        Assert.Contains("VendorDb", message, StringComparison.Ordinal);
        Assert.Contains("حذف نمی‌شود", message, StringComparison.Ordinal);
    }
}
