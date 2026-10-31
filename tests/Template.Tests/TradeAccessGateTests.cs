using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Licensing;
using Xunit;

namespace Template.Tests;

public sealed class TradeAccessGateTests
{
    private static readonly string ReadCode = TradePermissionCodes.FormView("import-order");
    private static readonly string WriteCode = TradePermissionCodes.FormUpdate("import-order");

    private readonly List<string> calls = [];

    private static TradeAccessContext Context(bool allowsWrites = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), allowsWrites);

    private TradeAccessGate Gate(
        TradeAccessContext? context,
        bool entitled = true,
        bool permission = true,
        Exception? entitlementError = null)
    {
        return new TradeAccessGate(
            new RecordingAuthorization(calls, context, permission),
            new RecordingEntitlement(calls, entitled, entitlementError));
    }

    [Fact]
    public async Task EmptyUserIsUnauthenticatedAndNothingIsCalled()
    {
        var gate = Gate(Context());

        var decision = await gate.AuthorizeAsync(Guid.Empty, ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Unauthenticated, decision.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task MissingContextRequiresContextAndShortCircuits()
    {
        var gate = Gate(null);

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ContextRequired, decision.Outcome);
        Assert.Equal(["resolve"], calls);
    }

    [Fact]
    public async Task ReadOnlyPeriodDeniesWriteBeforeEntitlementAndPermission()
    {
        var gate = Gate(Context(allowsWrites: false));

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), WriteCode, TradeAccessIntent.Write, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.PeriodReadOnly, decision.Outcome);
        Assert.Equal(["resolve"], calls);
    }

    [Fact]
    public async Task ReadIntentInReadOnlyPeriodContinuesToEntitlementAndPermission()
    {
        var gate = Gate(Context(allowsWrites: false));

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Allowed, decision.Outcome);
        Assert.Equal(["resolve", "entitlement", "permission"], calls);
    }

    [Fact]
    public async Task DisabledEntitlementIsModuleUnavailableAndPermissionIsNeverCalled()
    {
        var gate = Gate(Context(), entitled: false);

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ModuleUnavailable, decision.Outcome);
        Assert.Null(decision.Context);
        Assert.DoesNotContain("permission", calls);
    }

    [Fact]
    public async Task ThrowingEntitlementIsModuleUnavailableAndDoesNotEscape()
    {
        var error = new InvalidOperationException("boom");
        var gate = Gate(Context(), entitlementError: error);

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ModuleUnavailable, decision.Outcome);
        Assert.Same(error, decision.Fault);
        Assert.DoesNotContain("permission", calls);
    }

    [Fact]
    public async Task MissingPermissionIsForbidden()
    {
        var gate = Gate(Context(), permission: false);

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Forbidden, decision.Outcome);
        Assert.Null(decision.Context);
    }

    [Fact]
    public async Task IntentMismatchIsForbiddenWithRealPermissionRules()
    {
        // The fake authorization applies the real compatibility rule, like the adapter does.
        var gate = Gate(Context());

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), ReadCode, TradeAccessIntent.Write, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Forbidden, decision.Outcome);
    }

    [Fact]
    public async Task SuccessReturnsTheSameContextInstance()
    {
        var context = Context();
        var gate = Gate(context);

        var decision = await gate.AuthorizeAsync(Guid.NewGuid(), WriteCode, TradeAccessIntent.Write, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Allowed, decision.Outcome);
        Assert.Same(context, decision.Context);
    }

    [Fact]
    public async Task CapabilitiesFetchPermissionsOnlyWhenEntitled()
    {
        var context = Context();
        var denied = Gate(context, entitled: false);
        var deniedResult = await denied.GetCapabilitiesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ModuleUnavailable, deniedResult.Outcome);
        Assert.Empty(deniedResult.Permissions);
        Assert.DoesNotContain("list", calls);
        Assert.DoesNotContain("permission", calls);

        calls.Clear();
        var allowed = Gate(context);
        var allowedResult = await allowed.GetCapabilitiesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(allowedResult.Available);
        Assert.Equal(TradeAccessMode.ReadWrite, allowedResult.AccessMode);
        Assert.Equal([ReadCode], allowedResult.Permissions);
        Assert.Equal(["resolve", "entitlement", "list"], calls);
    }

    [Fact]
    public async Task CapabilitiesReportReadOnlyAndRequireContext()
    {
        var readOnly = Gate(Context(allowsWrites: false));
        Assert.Equal(TradeAccessMode.ReadOnly,
            (await readOnly.GetCapabilitiesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).AccessMode);

        calls.Clear();
        var none = Gate(null);
        var result = await none.GetCapabilitiesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Equal(TradeAccessOutcome.ContextRequired, result.Outcome);
        Assert.Equal(["resolve"], calls);
    }

    private sealed class RecordingAuthorization(List<string> calls, TradeAccessContext? context, bool permission)
        : ITradeAuthorization
    {
        public Task<TradeAccessContext?> ResolveCurrentContextAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            calls.Add("resolve");
            return Task.FromResult(context);
        }

        public Task<bool> HasPermissionAsync(
            TradeAccessContext context, string permissionCode, TradeAccessIntent accessIntent,
            CancellationToken cancellationToken = default)
        {
            calls.Add("permission");
            return Task.FromResult(permission && TradePermissionCodes.IsAccessIntentCompatible(permissionCode, accessIntent));
        }

        public Task<IReadOnlyList<string>> GetGrantedTradePermissionsAsync(
            TradeAccessContext context, CancellationToken cancellationToken = default)
        {
            calls.Add("list");
            return Task.FromResult<IReadOnlyList<string>>([TradePermissionCodes.FormView("import-order")]);
        }

        public Task<bool> HasPermissionForAllCompanyPeriodsAsync(
            Guid userId, IReadOnlyCollection<TradePeriodScope> scopes, string permissionCode,
            TradeAccessIntent accessIntent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingEntitlement(List<string> calls, bool enabled, Exception? error) : ITradeModuleEntitlement
    {
        public Task<bool> IsEnabledForCompanyAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            calls.Add("entitlement");
            return error is null ? Task.FromResult(enabled) : throw error;
        }
    }
}
