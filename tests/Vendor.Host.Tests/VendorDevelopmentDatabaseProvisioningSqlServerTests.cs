using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Real SQL Server cycle for the Development full provisioning flow: create the dedicated
/// Vendor database, apply the existing EF migrations, create a least-privilege SQL runtime
/// identity, verify readiness, and stay idempotent and safe on re-runs and foreign databases.
/// The critical regression guarantees: an existing runtime login is never password-reset or
/// auto-enabled, and reuse requires the operator to prove the login with its own credentials.
/// Runs only when FARANO_VENDOR_TEST_SQLSERVER points at a disposable local SQL Server;
/// the pipeline itself never drops anything, so the fixture cleans up its own objects.
/// </summary>
/// <summary>
/// The Development database provisioning SQL tests share one server-wide pipeline lock and
/// one provisioning intent slot, so they run sequentially (also relative to the safety tests).
/// </summary>
[CollectionDefinition("VendorDevelopmentDatabaseSequential", DisableParallelization = true)]
public sealed class VendorDevelopmentDatabaseSequentialCollection
{
}

[Collection("VendorDevelopmentDatabaseSequential")]
public sealed class VendorDevelopmentDatabaseProvisioningSqlServerTests
{
    /// <summary>
    /// Records the pipeline's logged warnings, including the underlying SQL exception, so a
    /// failing assertion can show the real error instead of only the sanitized stage message.
    /// </summary>
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly object _lock = new();
        private readonly List<string> _entries = [];

        public string Diagnostics
        {
            get
            {
                lock (_lock)
                {
                    return string.Join(Environment.NewLine + "---" + Environment.NewLine, _entries);
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lock)
            {
                _entries.Add($"{formatter(state, exception)}{Environment.NewLine}{exception}");
            }
        }
    }

    private static string DescribeOutcome(
        VendorDevelopmentDatabaseProvisioning.ProvisionOutcome outcome,
        RecordingLogger logger)
    {
        var stages = string.Join(" | ", outcome.StageResults.Select(stage => $"{stage.Key}={stage.State}: {stage.Message}"));
        return $"{outcome.Message}{Environment.NewLine}stages: {stages}{Environment.NewLine}log:{Environment.NewLine}{logger.Diagnostics}";
    }

    /// <summary>Runs the pipeline and fails the test with full diagnostics when not successful.</summary>
    private static async Task<VendorDevelopmentDatabaseProvisioning.ProvisionOutcome> RunPipelineAsync(ProvisionRequest request)
    {
        var logger = new RecordingLogger();
        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            request, logger, canSaveConnection: false, [], TestContext.Current.CancellationToken);
        Assert.True(outcome.Success, DescribeOutcome(outcome, logger));
        return outcome;
    }

    /// <summary>Runs the pipeline expecting a controlled failure; the outcome carries the refusal.</summary>
    private static async Task<(VendorDevelopmentDatabaseProvisioning.ProvisionOutcome Outcome, string StagesAndLog)>
        RunPipelineExpectingFailureAsync(ProvisionRequest request)
    {
        var logger = new RecordingLogger();
        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            request, logger, canSaveConnection: false, [], TestContext.Current.CancellationToken);
        var stages = string.Join(" | ", outcome.StageResults.Select(stage => $"{stage.Key}={stage.State}: {stage.Message}"));
        return (outcome, $"{outcome.Message}{Environment.NewLine}stages: {stages}{Environment.NewLine}log:{Environment.NewLine}{logger.Diagnostics}");
    }

    private static Dictionary<string, string> States(VendorDevelopmentDatabaseProvisioning.ProvisionOutcome outcome) =>
        outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

    [Fact]
    public async Task FullPipelineProvisionsDatabaseMigrationsAndRuntimeIdentity()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var request = fixture.CreateRequest(runtimePassword: runtimePassword);

        var outcome = await RunPipelineAsync(request);
        var states = States(outcome);

        Assert.Equal("success", states["verify"]);
        Assert.Equal("success", states["createDatabase"]);
        Assert.Null(outcome.GeneratedPassword);
        Assert.Equal(fixture.RuntimeLoginName, outcome.RuntimeUsername);

        // Schema exists through migrations and the runtime identity holds exactly reader/writer.
        Assert.True(await fixture.DatabaseExistsAsync());
        Assert.True(await fixture.HasMigrationsHistoryAsync());
        Assert.True(await fixture.HasOwnershipMarkerAsync(), await fixture.DescribeStateAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.LoginExistsAsync(fixture.RuntimeLoginName), await fixture.DescribeStateAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.IsMemberAsync(fixture.RuntimeLoginName, "db_datareader"),
            await fixture.DescribeStateAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.IsMemberAsync(fixture.RuntimeLoginName, "db_datawriter"),
            await fixture.DescribeStateAsync(fixture.RuntimeLoginName));
        Assert.False(await fixture.IsMemberAsync(fixture.RuntimeLoginName, "db_owner"));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword),
            await fixture.DescribeRuntimeLoginConnectAsync(fixture.RuntimeLoginName, runtimePassword));

        // Re-run is idempotent: the database is kept, migrations stay applied, the existing
        // login is reused through its own credentials (proof, not reset) and readiness verifies.
        var rerun = await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));
        var rerunStates = States(rerun);

        Assert.Equal("skipped", rerunStates["createDatabase"]);
        Assert.Equal("success", rerunStates["verify"]);
        Assert.Null(rerun.GeneratedPassword);
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
    }

    [Fact]
    public async Task FreshLoginCanBeCreatedWithAGeneratedPassword()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        // No runtime password supplied: the flow generates one for the login it creates.
        var outcome = await RunPipelineAsync(fixture.CreateRequest());
        var states = States(outcome);

        Assert.NotNull(outcome.GeneratedPassword);
        Assert.True(await fixture.LoginExistsAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, outcome.GeneratedPassword!));
    }

    [Fact]
    public async Task ExistingRuntimeLoginPasswordIsNeverResetAndReuseRequiresItsOwnCredentials()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var originalPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: originalPassword));

        // Regression guard (3.1): a re-run with a DIFFERENT password must NOT reset the
        // existing login; it fails closed instead and the original password keeps working.
        var attackerPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var (rerunWithDifferentPassword, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(runtimePassword: attackerPassword));
        var rerunStates = States(rerunWithDifferentPassword);

        Assert.False(rerunWithDifferentPassword.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("تغییر نمی", rerunStates["runtimeIdentity"] + rerunWithDifferentPassword.Message, StringComparison.Ordinal);
        Assert.False(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, attackerPassword));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
        Assert.Null(rerunWithDifferentPassword.GeneratedPassword);
        GC.KeepAlive(diagnostics);

        // A re-run with NO password (generation requested) must also fail closed.
        var (rerunWithGeneration, _) = await RunPipelineExpectingFailureAsync(fixture.CreateRequest());
        Assert.False(rerunWithGeneration.Success);
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
    }

    [Fact]
    public async Task PipelineFailsFastForSystemDatabaseNames()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var request = fixture.CreateRequest(databaseName: "master");

        var (outcome, _) = await RunPipelineExpectingFailureAsync(request);
        var states = States(outcome);

        Assert.False(outcome.Success);
        // Identifier validation is fail-fast at stage 1; nothing reaches the inspect stage.
        Assert.True(states["connection"] == "failed" || states["inspect"] == "failed",
            $"connection={states["connection"]}, inspect={states["inspect"]}");
        Assert.Equal("pending", states["createDatabase"]);
        Assert.Contains("سیستمی",
            outcome.StageResults.First(stage => stage.Key == "connection").Message +
            outcome.StageResults.First(stage => stage.Key == "inspect").Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PipelineRefusesAnExistingEmptyDatabaseWithoutOwnershipProof()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        // An empty pre-created database may have been made for another purpose (for example
        // an empty Customer database); without ownership proof it is never adopted.
        await fixture.CreateEmptyForeignDatabaseAsync();

        var (outcome, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName));
        var states = States(outcome);
        var createMessage = outcome.StageResults.First(stage => stage.Key == "createDatabase").Message;

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["createDatabase"]);
        Assert.Equal("pending", states["migrate"]);
        Assert.Contains("مالکیت", createMessage + outcome.Message, StringComparison.Ordinal);
        Assert.False(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        Assert.Equal(0, await fixture.UserTableCountAsync(fixture.ForeignDatabaseName));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRecoversACrashWindowOrphanOnlyWithMatchingIntentEvidence()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        await fixture.CreateEmptyForeignDatabaseAsync();

        // No intent record: the orphaned database stays anonymous and fail-closed.
        var (withoutIntent, _) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()));
        Assert.False(withoutIntent.Success);
        Assert.False(await fixture.HasOwnershipMarkerAsync(fixture.ForeignDatabaseName));

        // With a fresh intent recorded before the create and the create-date binding
        // satisfied, the controlled recovery writes the marker and continues the flow.
        var createdUtc = await fixture.ReadDatabaseCreatedUtcAsync(fixture.ForeignDatabaseName);
        Assert.NotNull(createdUtc);
        var intentIssuedUtc = createdUtc.Value.AddMinutes(-2);
        // Simulate the real crash order: master-side intent, then the in-database record —
        // a crash right after the in-database write leaves both records and no marker.
        await fixture.WriteProvisioningIntentAsync(fixture.ForeignDatabaseName, intentIssuedUtc);
        await fixture.WriteInDatabaseIntentAsync(fixture.ForeignDatabaseName, intentIssuedUtc);

        var withIntent = await RunPipelineAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()));
        var recoveredStates = States(withIntent);

        Assert.Equal("skipped", recoveredStates["createDatabase"]);
        Assert.Equal("success", recoveredStates["migrate"]);
        Assert.True(await fixture.HasOwnershipMarkerAsync(fixture.ForeignDatabaseName));
        Assert.True(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        Assert.Null(await fixture.ReadProvisioningIntentAsync());
    }

    [Fact]
    public async Task PipelineKeepsAcceptingLegacyVendorDatabasesByHistoryAndBackfillsMarker()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();

        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // Simulate a pre-marker Vendor database: history proves ownership, marker removed.
        await fixture.DropOwnershipMarkerAsync();
        Assert.False(await fixture.HasOwnershipMarkerAsync());

        // The existing login is reused through its own credentials; a different password
        // must never be able to adopt it (covered by the dedicated regression test).
        var rerun = await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));
        var rerunStates = States(rerun);

        Assert.Equal("skipped", rerunStates["createDatabase"]);
        Assert.Equal("success", rerunStates["migrate"]);
        Assert.True(await fixture.HasOwnershipMarkerAsync());
    }

    [Fact]
    public async Task RuntimeIdentityIsLeastPrivilegedOnTheRealServer()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var outcome = await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // No server role of any kind, no server grant beyond CONNECT SQL, visibility deny.
        Assert.False(await fixture.IsServerRoleMemberAsync(fixture.RuntimeLoginName, "sysadmin"));
        Assert.False(await fixture.IsServerRoleMemberAsync(fixture.RuntimeLoginName, "dbcreator"));
        Assert.False(await fixture.IsServerRoleMemberAsync(fixture.RuntimeLoginName, "securityadmin"));
        Assert.Equal(["CONNECT SQL"], await fixture.ServerGrantedPermissionsAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.HasViewAnyDatabaseDenyAsync(fixture.RuntimeLoginName));

        // The runtime identity cannot read another database, cannot create a database or a
        // login, cannot run migrations (no DDL), and cannot read other users' catalogs —
        // while the normal data path stays usable.
        await fixture.CreateForeignDatabaseAsync();
        Assert.False(await fixture.RuntimeCanSelectFromOtherDatabaseAsync(fixture.RuntimeLoginName, runtimePassword, fixture.ForeignDatabaseName));
        Assert.False(await fixture.RuntimeCanCreateDatabaseAsync(fixture.RuntimeLoginName, runtimePassword));
        Assert.False(await fixture.RuntimeCanCreateLoginAsync(fixture.RuntimeLoginName, runtimePassword));
        Assert.False(await fixture.RuntimeCanRunDdlAsync(fixture.RuntimeLoginName, runtimePassword));
        Assert.True(await fixture.RuntimeCanDeleteZeroRowsAsync(fixture.RuntimeLoginName, runtimePassword));
    }

    [Fact]
    public async Task PipelineRefusesADisabledRuntimeLoginWithoutEnablingIt()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var originalPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: originalPassword));

        await fixture.DisableRuntimeLoginAsync();

        var (rerun, _) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(runtimePassword: originalPassword));
        var rerunStates = States(rerun);

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;
        Assert.Contains("غیرفعال", identityMessage, StringComparison.Ordinal);
        Assert.True(await fixture.IsRuntimeLoginDisabledAsync());
    }

    [Fact]
    public async Task PipelineRefusesARuntimeLoginMappedIntoAnotherDatabase()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var originalPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: originalPassword));

        // Simulate cross-database drift: a user mapping in a database this flow never owns.
        await fixture.CreateForeignDatabaseAsync();
        await fixture.MapRuntimeLoginIntoForeignDatabaseAsync();

        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(runtimePassword: originalPassword));
        var rerunStates = States(rerun);

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;
        Assert.Contains("دیتابیس‌های دیگری", identityMessage, StringComparison.Ordinal);
        // The password was never touched by the refused run.
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesAnOverPrivilegedExistingRuntimeLogin()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();

        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // Simulate server-level drift: the existing login must be refused, not silently reused.
        await fixture.GrantViewAnyDatabaseAsync();
        var rerunPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(runtimePassword: rerunPassword));
        var rerunStates = States(rerun);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("سطح سرور", identityMessage, StringComparison.Ordinal);
        // The privileged login's password must not have been reset by the refused run.
        Assert.False(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, rerunPassword));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesProvisioningWhenTheSetupIdentityCannotReadServerPrincipals()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // A setup identity that sees only itself in sys.server_principals cannot tell an
        // existing login from a missing one: the whole provisioning must fail closed
        // instead of silently misrouting into CREATE LOGIN.
        var setupPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await fixture.CreateLimitedSetupLoginAsync(setupPassword);
        await fixture.MapLimitedSetupUserIntoTargetAsDbOwnerAsync();

        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(fixture.CreateRequest(
            runtimePassword: runtimePassword,
            setupUsername: fixture.LimitedSetupLoginName,
            setupPassword: setupPassword));
        var rerunStates = States(rerun);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("VIEW ANY DEFINITION", identityMessage, StringComparison.Ordinal);
        // The refused run neither created nor altered the runtime login.
        Assert.True(await fixture.LoginExistsAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesAnExistingRuntimeLoginWhenTheCrossDatabaseAuditIsIncomplete()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // A setup identity that is not sysadmin cannot open every non-target database
        // (model/msdb keep guest disabled, and transient databases have no mapping), so the
        // cross-database audit is never complete and reuse must fail closed. VIEW ANY
        // DEFINITION makes the principal catalog readable (and implies database enumeration),
        // so it is the unauditable-database rule — not principal readiness — that fires.
        var setupPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await fixture.CreateLimitedSetupLoginAsync(setupPassword);
        await fixture.GrantViewAnyDefinitionToLimitedSetupLoginAsync();
        await fixture.MapLimitedSetupUserIntoTargetAsDbOwnerAsync();

        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(fixture.CreateRequest(
            runtimePassword: runtimePassword,
            setupUsername: fixture.LimitedSetupLoginName,
            setupPassword: setupPassword));
        var rerunStates = States(rerun);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("ممیزی", identityMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("VIEW ANY DATABASE", identityMessage, StringComparison.Ordinal);
        // The refusal never touched the login or its password.
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesAnExistingRuntimeLoginWhenDatabaseEnumerationIsDenied()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // VIEW ANY DATABASE is granted to PUBLIC by default, so the enumeration rule is only
        // reachable through an explicit DENY. With the DENY, database metadata visibility
        // shrinks to databases the setup identity OWNS (a mere user mapping is not enough),
        // so this test makes the limited identity the target database owner: an explicitly
        // denied owner-operator that cannot list every database and therefore cannot prove
        // the absence of cross-database mappings.
        var setupPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await fixture.CreateLimitedSetupLoginAsync(setupPassword);
        await fixture.GrantViewAnyDefinitionToLimitedSetupLoginAsync();
        await fixture.DenyViewAnyDatabaseToLimitedSetupLoginAsync();
        await fixture.TransferTargetOwnershipToLimitedSetupLoginAsync();

        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(fixture.CreateRequest(
            runtimePassword: runtimePassword,
            setupUsername: fixture.LimitedSetupLoginName,
            setupPassword: setupPassword));
        var rerunStates = States(rerun);

        Assert.False(rerun.Success);
        Assert.True(rerunStates.TryGetValue("runtimeIdentity", out var identityState) && identityState == "failed",
            diagnostics);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;
        Assert.Contains("VIEW ANY DATABASE", identityMessage, StringComparison.Ordinal);
        // The refusal never touched the login or its password.
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesAnExistingRuntimeLoginWhenANonTargetDatabaseCannotBeAudited()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // The runtime login drifted into a database the setup identity cannot open. A
        // HAS_DBACCESS = 0 result describes the setup identity, not the absence of a runtime
        // mapping, so the reuse must fail closed while that database is un-auditable.
        await fixture.CreateForeignDatabaseAsync();
        await fixture.MapRuntimeLoginIntoForeignDatabaseAsync();

        var setupPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await fixture.CreateLimitedSetupLoginAsync(setupPassword);
        await fixture.GrantViewAnyDatabaseToLimitedSetupLoginAsync();
        await fixture.GrantViewAnyDefinitionToLimitedSetupLoginAsync();
        await fixture.MapLimitedSetupUserIntoTargetAsDbOwnerAsync();

        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(fixture.CreateRequest(
            runtimePassword: runtimePassword,
            setupUsername: fixture.LimitedSetupLoginName,
            setupPassword: setupPassword));
        var rerunStates = States(rerun);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("ممیزی", identityMessage, StringComparison.Ordinal);
        Assert.Contains(fixture.ForeignDatabaseName, identityMessage, StringComparison.Ordinal);
        // The hidden foreign mapping survived the refused run untouched.
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesToMigrateAUnknownDatabaseWithUserTables()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        await fixture.CreateForeignDatabaseAsync();

        var (outcome, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()));
        var states = States(outcome);
        var createMessage = outcome.StageResults.First(stage => stage.Key == "createDatabase").Message;

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["createDatabase"]);
        Assert.Contains("جدول", createMessage + outcome.Message, StringComparison.Ordinal);
        Assert.False(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        GC.KeepAlive(diagnostics);
    }

    private sealed class ProvisioningFixture : IAsyncDisposable
    {
        private readonly string _baseConnection;

        public string DatabaseName { get; } = $"FaranouVendorDevSetup_{Guid.NewGuid():N}"[..44];
        public string ForeignDatabaseName { get; } = $"FaranouVendorDevSetup_{Guid.NewGuid():N}"[..44];
        public string RuntimeLoginName { get; } = $"FaranouVendorRuntime_{Guid.NewGuid():N}"[..40];
        public string LimitedSetupLoginName { get; } = $"FaranouVendorSetupLimited_{Guid.NewGuid():N}"[..44];

        private ProvisioningFixture(string baseConnection) => _baseConnection = baseConnection;

        public static async Task<ProvisioningFixture?> OpenAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(baseConnection))
            {
                const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server.";
                if (Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER") == "1")
                {
                    Assert.Fail(message);
                }

                Assert.Skip(message);
                return null;
            }

            return new ProvisioningFixture(baseConnection);
        }

        public ProvisionRequest CreateRequest(
            string? databaseName = null,
            string? runtimePassword = null,
            string? runtimeUsername = null,
            string? setupUsername = null,
            string? setupPassword = null)
        {
            var builder = new SqlConnectionStringBuilder(_baseConnection);
            var username = setupUsername ?? builder.UserID;
            var password = setupPassword ?? builder.Password;
            return new ProvisionRequest(
                Server: builder.DataSource,
                Database: databaseName ?? DatabaseName,
                Authentication: string.IsNullOrWhiteSpace(username) ? "windows" : "sql",
                Username: username,
                Password: password,
                TrustServerCertificate: builder.TrustServerCertificate,
                RuntimeUsername: runtimeUsername ?? RuntimeLoginName,
                RuntimePassword: runtimePassword);
        }

        public async Task<bool> DatabaseExistsAsync(string? databaseName = null) =>
            await ScalarAsync("master", "SELECT COUNT(*) FROM sys.databases WHERE name = @name",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName ?? DatabaseName }) is > 0;

        public async Task<bool> HasMigrationsHistoryAsync(string? databaseName = null) =>
            await ScalarAsync(databaseName ?? DatabaseName,
                "SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory'") is > 0;

        public async Task<bool> HasOwnershipMarkerAsync(string? databaseName = null) =>
            await ScalarAsync(databaseName ?? DatabaseName,
                "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorOwnership'") is > 0;

        public async Task<int> UserTableCountAsync(string? databaseName = null) =>
            await ScalarAsync(databaseName ?? DatabaseName,
                "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0") is int count ? count : 0;

        public async Task<DateTimeOffset?> ReadDatabaseCreatedUtcAsync(string databaseName) =>
            await ScalarAsync("master", "SELECT create_date FROM sys.databases WHERE name = @name",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName }) is DateTime created
                ? new DateTimeOffset(created, TimeSpan.Zero)
                : null;

        public async Task WriteProvisioningIntentAsync(string databaseName, DateTimeOffset issuedUtc)
        {
            await using var connection = new SqlConnection(TargetBuilder("master").ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent')
                    EXEC sys.sp_updateextendedproperty @name = N'FaranouVendorProvisioningIntent', @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = N'FaranouVendorProvisioningIntent', @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@value", SqlDbType.NVarChar, 512)
            {
                Value = FormattableString.Invariant($"VendorDb={databaseName};IssuedUtc={issuedUtc:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}")
            });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task WriteInDatabaseIntentAsync(string databaseName, DateTimeOffset issuedUtc)
        {
            // The pipeline writes the in-database intent right after CREATE DATABASE and the
            // master-side one before it; a crash between the two leaves the in-DB record too,
            // so the recovery evidence set is master + in-database + create-date binding.
            await using var connection = new SqlConnection(TargetBuilder(databaseName).ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent')
                    EXEC sys.sp_updateextendedproperty @name = N'FaranouVendorProvisioningIntent', @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = N'FaranouVendorProvisioningIntent', @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@value", SqlDbType.NVarChar, 512)
            {
                Value = FormattableString.Invariant($"VendorDb={databaseName};IssuedUtc={issuedUtc:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}")
            });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<string?> ReadProvisioningIntentAsync()
        {
            var result = await ScalarAsync("master",
                "SELECT CAST(value AS nvarchar(512)) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent'");
            return result as string;
        }

        public async Task DropOwnershipMarkerAsync(string? databaseName = null) =>
            await ExecuteAsync(databaseName ?? DatabaseName,
                "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorOwnership') EXEC sys.sp_dropextendedproperty @name = 'FaranouVendorOwnership';");

        public async Task<bool> IsServerRoleMemberAsync(string loginName, string role)
        {
            // IS_SRVROLEMEMBER returns int (1 = member, 0 = not a member, NULL = invalid
            // argument). Read the integer and treat NULL or any unexpected value as
            // inconclusive: the negative assertions must fail loudly instead of passing
            // because a cast turned both 0 and 1 into null.
            var membership = await ScalarAsync("master", "SELECT IS_SRVROLEMEMBER(@role, @login)",
                new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = role },
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName });
            return membership switch
            {
                1 => true,
                0 => false,
                _ => throw new InvalidOperationException($"IS_SRVROLEMEMBER('{role}', '{loginName}') returned an inconclusive result: {membership?.ToString() ?? "NULL"}"),
            };
        }

        public async Task<List<string>> ServerGrantedPermissionsAsync(string loginName)
        {
            var grants = new List<string>();
            await using var connection = new SqlConnection(TargetBuilder("master").ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT pe.permission_name
                FROM sys.server_permissions pe
                JOIN sys.server_principals g ON g.principal_id = pe.grantee_principal_id
                WHERE g.name = @login AND pe.state IN ('G', 'W')
                ORDER BY pe.permission_name
                """;
            command.Parameters.Add(new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName });
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                grants.Add(reader.GetString(0));
            }

            return grants;
        }

        public async Task<bool> HasViewAnyDatabaseDenyAsync(string loginName) =>
            await ScalarAsync("master", """
                SELECT COUNT(*)
                FROM sys.server_permissions pe
                JOIN sys.server_principals g ON g.principal_id = pe.grantee_principal_id
                WHERE g.name = @login AND pe.state = 'D' AND pe.permission_name = 'VIEW ANY DATABASE'
                """,
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName }) is > 0;

        public async Task<bool> RuntimeCanSelectFromOtherDatabaseAsync(string loginName, string password, string otherDatabase)
        {
            var builder = new SqlConnectionStringBuilder(TargetBuilder(otherDatabase).ConnectionString)
            {
                // Explicit SQL authentication: UserID/Password are ignored when the fixture
                // string carries Integrated Security=true, and the probe would then run as the
                // setup administrator instead of the runtime identity.
                UserID = loginName,
                Password = password,
                IntegratedSecurity = false,
                Pooling = false
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM dbo.ForeignMarker";
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

        public async Task<bool> RuntimeCanCreateDatabaseAsync(string loginName, string password)
        {
            var builder = new SqlConnectionStringBuilder(TargetBuilder("master").ConnectionString)
            {
                // Explicit SQL authentication: UserID/Password are ignored when the fixture
                // string carries Integrated Security=true, and the probe would then run as the
                // setup administrator instead of the runtime identity.
                UserID = loginName,
                Password = password,
                IntegratedSecurity = false,
                Pooling = false
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [FaranouMustNotBeCreatable_{Guid.NewGuid():N}]";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

        public async Task<bool> RuntimeCanCreateLoginAsync(string loginName, string password)
        {
            var builder = new SqlConnectionStringBuilder(TargetBuilder("master").ConnectionString)
            {
                // Explicit SQL authentication: UserID/Password are ignored when the fixture
                // string carries Integrated Security=true, and the probe would then run as the
                // setup administrator instead of the runtime identity.
                UserID = loginName,
                Password = password,
                IntegratedSecurity = false,
                Pooling = false
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE LOGIN [FaranouMustNotBeCreatable_{Guid.NewGuid():N}] WITH PASSWORD = N'Xy9!aaaa'";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

        public async Task<bool> RuntimeCanRunDdlAsync(string loginName, string password)
        {
            var builder = new SqlConnectionStringBuilder(TargetBuilder(DatabaseName).ConnectionString)
            {
                // Explicit SQL authentication: UserID/Password are ignored when the fixture
                // string carries Integrated Security=true, and the probe would then run as the
                // setup administrator instead of the runtime identity.
                UserID = loginName,
                Password = password,
                IntegratedSecurity = false,
                Pooling = false
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE dbo.FaranouRuntimeMustNotCreateTables (Id int NOT NULL PRIMARY KEY)";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

        public async Task<bool> RuntimeCanDeleteZeroRowsAsync(string loginName, string password)
        {
            var builder = new SqlConnectionStringBuilder(TargetBuilder(DatabaseName).ConnectionString)
            {
                // Explicit SQL authentication: UserID/Password are ignored when the fixture
                // string carries Integrated Security=true, and the probe would then run as the
                // setup administrator instead of the runtime identity.
                UserID = loginName,
                Password = password,
                IntegratedSecurity = false,
                Pooling = false
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM VendorAdminUsers WHERE 1 = 0";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

        public async Task DisableRuntimeLoginAsync() =>
            await ExecuteAsync("master", $"ALTER LOGIN [{RuntimeLoginName.Replace("]", "]]", StringComparison.Ordinal)}] DISABLE");

        public async Task<bool> IsRuntimeLoginDisabledAsync() =>
            await ScalarAsync("master", "SELECT is_disabled FROM sys.sql_logins WHERE name = @login",
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = RuntimeLoginName }) is true;

        public async Task MapRuntimeLoginIntoForeignDatabaseAsync()
        {
            var quotedLogin = $"[{RuntimeLoginName.Replace("]", "]]", StringComparison.Ordinal)}]";
            await ExecuteAsync(ForeignDatabaseName, $"CREATE USER {quotedLogin} FOR LOGIN {quotedLogin}");
        }

        public async Task GrantViewAnyDatabaseAsync() =>
            await ExecuteAsync("master", $"GRANT VIEW ANY DATABASE TO [{RuntimeLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");

        // A deliberately limited setup identity for the fail-closed audit-coverage tests: it
        // owns the target database through db_owner but has no server-level enumeration power
        // unless the test explicitly grants VIEW ANY DATABASE.
        public async Task CreateLimitedSetupLoginAsync(string password) =>
            await ExecuteAsync("master",
                $"CREATE LOGIN [{LimitedSetupLoginName.Replace("]", "]]", StringComparison.Ordinal)}] WITH PASSWORD = N'{password.Replace("'", "''", StringComparison.Ordinal)}', CHECK_POLICY = ON");

        public async Task GrantViewAnyDatabaseToLimitedSetupLoginAsync() =>
            await ExecuteAsync("master", $"GRANT VIEW ANY DATABASE TO [{LimitedSetupLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task GrantViewAnyDefinitionToLimitedSetupLoginAsync() =>
            await ExecuteAsync("master", $"GRANT VIEW ANY DEFINITION TO [{LimitedSetupLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task DenyViewAnyDatabaseToLimitedSetupLoginAsync() =>
            await ExecuteAsync("master", $"DENY VIEW ANY DATABASE TO [{LimitedSetupLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task TransferTargetOwnershipToLimitedSetupLoginAsync() =>
            await ExecuteAsync("master",
                $"ALTER AUTHORIZATION ON DATABASE::[{DatabaseName.Replace("]", "]]", StringComparison.Ordinal)}] TO [{LimitedSetupLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task MapLimitedSetupUserIntoTargetAsDbOwnerAsync()
        {
            var quoted = $"[{LimitedSetupLoginName.Replace("]", "]]", StringComparison.Ordinal)}]";
            await ExecuteAsync(DatabaseName, $"CREATE USER {quoted} FOR LOGIN {quoted}");
            await ExecuteAsync(DatabaseName,
                "EXEC sp_addrolemember @rolename = 'db_owner', @membername = @member",
                new SqlParameter("@member", SqlDbType.NVarChar, 128) { Value = LimitedSetupLoginName });
        }

        public async Task CreateEmptyForeignDatabaseAsync() =>
            await ExecuteAsync("master", $"CREATE DATABASE [{ForeignDatabaseName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task<bool> LoginExistsAsync(string loginName) =>
            await ScalarAsync("master", "SELECT COUNT(*) FROM sys.server_principals WHERE name = @name AND type = 'S'",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = loginName }) is > 0;

        public async Task<bool> IsMemberAsync(string loginName, string role)
        {
            // Catalog-based membership check (visible to the admin connection immediately);
            // a short retry absorbs any catalog-freshness delay right after the pipeline run.
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var member = await ScalarAsync(DatabaseName, """
                    SELECT COUNT(*)
                    FROM sys.database_role_members drm
                    JOIN sys.database_principals r ON r.principal_id = drm.role_principal_id
                    JOIN sys.database_principals m ON m.principal_id = drm.member_principal_id
                    WHERE r.name = @role AND m.name = @login
                    """,
                    new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = role },
                    new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName }) is > 0;
                if (member)
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            }

            return false;
        }

        public async Task<string> DescribeStateAsync(string loginName, string? databaseName = null)
        {
            var db = databaseName ?? DatabaseName;
            var marker = await ScalarAsync(db,
                "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorOwnership'");
            var userExists = await ScalarAsync(db,
                "SELECT COUNT(*) FROM sys.database_principals WHERE name = @name AND type IN ('S', 'U')",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = loginName });
            var readerRole = await ScalarAsync(db, "SELECT IS_ROLEMEMBER(@role, @login)",
                new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = "db_datareader" },
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName });
            var writerRole = await ScalarAsync(db, "SELECT IS_ROLEMEMBER(@role, @login)",
                new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = "db_datawriter" },
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName });
            var markerInMaster = await ScalarAsync("master",
                "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorOwnership'");
            var userInMaster = await ScalarAsync("master",
                "SELECT COUNT(*) FROM sys.database_principals WHERE name = @name AND type IN ('S', 'U')",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = loginName });
            var readerInMaster = await ScalarAsync("master", "SELECT IS_ROLEMEMBER(@role, @login)",
                new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = "db_datareader" },
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName });
            return $"target[{db}]: marker={marker}, userExists={userExists}, reader={readerRole}, writer={writerRole} | master: marker={markerInMaster}, user={userInMaster}, reader={readerInMaster}";
        }

        public async Task<string> DescribeRuntimeLoginConnectAsync(string loginName, string runtimePassword)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = new SqlConnectionStringBuilder(_baseConnection).DataSource,
                InitialCatalog = DatabaseName,
                UserID = loginName,
                Password = runtimePassword,
                IntegratedSecurity = false,
                Encrypt = true,
                TrustServerCertificate = new SqlConnectionStringBuilder(_baseConnection).TrustServerCertificate,
                ConnectTimeout = 15,
                Pooling = false
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM VendorAdminUsers";
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
                return "connected";
            }
            catch (SqlException exception)
            {
                // A refused login (bad password, denied access) is a valid negative answer.
                return $"refused: {exception.Number} {exception.Message}";
            }
        }

        public async Task<bool> RuntimeLoginCanConnectAsync(string loginName, string runtimePassword) =>
            (await DescribeRuntimeLoginConnectAsync(loginName, runtimePassword)) == "connected";

        public async Task CreateForeignDatabaseAsync()
        {
            await ExecuteAsync("master", $"CREATE DATABASE [{ForeignDatabaseName.Replace("]", "]]", StringComparison.Ordinal)}]");
            await ExecuteAsync(ForeignDatabaseName, "CREATE TABLE dbo.ForeignMarker (Id int NOT NULL PRIMARY KEY)");
        }

        // Pooling is disabled for fixture-owned connections so DROP DATABASE never fights an
        // idle pooled connection, and a fixture can never disturb other classes' pools.
        private static SqlConnectionStringBuilder TargetBuilder(string database) =>
            new(Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER")!)
            {
                InitialCatalog = database,
                ConnectTimeout = 15,
                Pooling = false
            };

        private static async Task<object?> ScalarAsync(string database, string commandText, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(TargetBuilder(database).ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        private static async Task ExecuteAsync(string database, string commandText)
        {
            await using var connection = new SqlConnection(TargetBuilder(database).ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        private static async Task ExecuteAsync(string database, string commandText, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(TargetBuilder(database).ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            // Each cleanup step runs independently (one failure never skips the rest) and
            // without the test token, so a cancelled test still cleans up. SINGLE_USER ends
            // sessions the pipeline left pooled on the fixture databases before the drop.
            foreach (var database in new[] { DatabaseName, ForeignDatabaseName })
            {
                var quoted = $"[{database.Replace("]", "]]", StringComparison.Ordinal)}]";
                try
                {
                    await ExecuteAsync("master", $"ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
                }
                catch (SqlException)
                {
                    // Best effort: the CI server is ephemeral and discarded after the run.
                }

                try
                {
                    await ExecuteAsync("master", $"DROP DATABASE IF EXISTS {quoted}");
                }
                catch (SqlException)
                {
                    // Best effort.
                }
            }

            foreach (var loginName in new[] { RuntimeLoginName, LimitedSetupLoginName })
            {
                try
                {
                    // DROP LOGIN has no IF EXISTS form: guard through the catalog, then drop.
                    var loginExists = await ScalarAsync("master",
                        "SELECT COUNT(*) FROM sys.server_principals WHERE name = @name AND type = 'S'",
                        new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = loginName }) is > 0;
                    if (loginExists)
                    {
                        await ExecuteAsync("master", $"DROP LOGIN [{loginName.Replace("]", "]]", StringComparison.Ordinal)}]");
                    }
                }
                catch (SqlException)
                {
                    // Best effort.
                }
            }

            try
            {
                await ExecuteAsync("master",
                    "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent') EXEC sys.sp_dropextendedproperty @name = N'FaranouVendorProvisioningIntent';");
            }
            catch (SqlException)
            {
                // Best effort.
            }
        }
    }
}
