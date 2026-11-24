using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Licensing;
using Xunit;

namespace Template.Tests;

/// <summary>
/// N-5a-type phase 1: the company-scoped overload of <see cref="TradeAccessGate" /> keeps every control of the
/// period-scoped path (identity, context, commercial entitlement, user permission) and drops only the period.
/// </summary>
public sealed class TradeCompanyAccessGateTests
{
    private static readonly string ReadCode = TradePermissionCodes.FormView("import-order");
    private static readonly string WriteCode = TradePermissionCodes.FormUpdate("import-order");

    private readonly List<string> calls = [];
    private readonly List<Guid> entitlementChecks = [];
    private readonly List<(string Code, TradeAccessIntent Intent)> permissionChecks = [];

    private static TradeCompanyAccessContext Context() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private TradeAccessGate Gate(
        TradeCompanyAccessContext? context,
        bool entitled = true,
        bool permission = true,
        Exception? entitlementError = null) =>
        new(
            new CompanyOnlyAuthorization(calls, context, permission, permissionChecks),
            new RecordingEntitlement(calls, entitlementChecks, entitled, entitlementError));

    [Fact]
    public async Task EmptyUserIsUnauthenticatedAndNothingIsCalled()
    {
        var decision = await Gate(Context()).AuthorizeCompanyScopedAsync(
            Guid.Empty, ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Unauthenticated, decision.Outcome);
        Assert.Null(decision.Context);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task MissingContextRequiresContextAndShortCircuits()
    {
        var decision = await Gate(null).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ContextRequired, decision.Outcome);
        Assert.Equal(["resolve-company"], calls);
    }

    [Fact]
    public async Task EntitlementIsCheckedForTheCompanyAndDeniesBeforeAnyPermissionIsRead()
    {
        var context = Context();

        var decision = await Gate(context, entitled: false).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ModuleUnavailable, decision.Outcome);
        Assert.Equal(["resolve-company", "entitlement"], calls);
        Assert.Equal([context.CompanyId], entitlementChecks);
        Assert.DoesNotContain(context.TenantId, entitlementChecks);
    }

    [Fact]
    public async Task AFailingEntitlementDeniesAndNeverFallsBackToUserPermission()
    {
        var failure = new InvalidOperationException("entitlement store down");

        var decision = await Gate(Context(), entitlementError: failure).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.ModuleUnavailable, decision.Outcome);
        Assert.Same(failure, decision.Fault);
        Assert.Null(decision.Context);
        Assert.DoesNotContain("permission", calls);
    }

    [Fact]
    public async Task ACancelledEntitlementCheckIsNotTurnedIntoADenial()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var gate = Gate(Context(), entitlementError: new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => gate.AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, cancellation.Token));
    }

    [Fact]
    public async Task AMissingPermissionIsForbiddenAndExposesNoContext()
    {
        var decision = await Gate(Context(), permission: false).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Forbidden, decision.Outcome);
        Assert.Null(decision.Context);
        Assert.Equal(["resolve-company", "entitlement", "permission"], calls);
    }

    [Fact]
    public async Task AllControlsInOrderAllowTheRequestAndReturnTheResolvedContext()
    {
        var context = Context();

        var decision = await Gate(context).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), WriteCode, TradeAccessIntent.Write, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Allowed, decision.Outcome);
        Assert.Same(context, decision.Context);
        Assert.Equal(["resolve-company", "entitlement", "permission"], calls);
        Assert.Equal([(WriteCode, TradeAccessIntent.Write)], permissionChecks);
    }

    [Fact]
    public async Task AReadPermissionCanNotAuthorizeAWriteIntent()
    {
        var decision = await Gate(Context()).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Write, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Forbidden, decision.Outcome);
    }

    [Fact]
    public async Task TheCompanyPathNeverTouchesAnythingPeriodScoped()
    {
        // CompanyOnlyAuthorization throws NotSupportedException from every period-scoped member, so any call
        // would fail this test: the path needs no fiscal period (real or made up).
        var decision = await Gate(Context()).AuthorizeCompanyScopedAsync(
            Guid.NewGuid(), ReadCode, TradeAccessIntent.Read, TestContext.Current.CancellationToken);

        Assert.Equal(TradeAccessOutcome.Allowed, decision.Outcome);
        Assert.DoesNotContain("resolve", calls);
    }

    [Fact]
    public async Task TheCompanyPathNeverReportsAPeriodDecision()
    {
        foreach (var intent in new[] { TradeAccessIntent.Read, TradeAccessIntent.Write })
        {
            var decision = await Gate(Context()).AuthorizeCompanyScopedAsync(
                Guid.NewGuid(), intent == TradeAccessIntent.Read ? ReadCode : WriteCode, intent,
                TestContext.Current.CancellationToken);

            Assert.NotEqual(TradeAccessOutcome.PeriodReadOnly, decision.Outcome);
        }
    }

    private sealed class CompanyOnlyAuthorization(
        List<string> calls,
        TradeCompanyAccessContext? context,
        bool permission,
        List<(string Code, TradeAccessIntent Intent)> checks) : ITradeAuthorization
    {
        public Task<TradeCompanyAccessContext?> ResolveCurrentCompanyContextAsync(
            Guid userId, CancellationToken cancellationToken = default)
        {
            calls.Add("resolve-company");
            return Task.FromResult(context);
        }

        public Task<bool> HasCompanyPermissionAsync(
            TradeCompanyAccessContext context, string permissionCode, TradeAccessIntent accessIntent,
            CancellationToken cancellationToken = default)
        {
            calls.Add("permission");
            checks.Add((permissionCode, accessIntent));
            return Task.FromResult(
                permission && TradePermissionCodes.IsAccessIntentCompatible(permissionCode, accessIntent));
        }

        public Task<TradeAccessContext?> ResolveCurrentContextAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            calls.Add("resolve");
            throw new NotSupportedException("The company-scoped path must not resolve a period context.");
        }

        public Task<bool> HasPermissionAsync(
            TradeAccessContext context, string permissionCode, TradeAccessIntent accessIntent,
            CancellationToken cancellationToken = default, bool periodIndependent = false)
            => throw new NotSupportedException("The company-scoped path must not use the period-scoped permission check.");

        public Task<IReadOnlyList<string>> GetGrantedTradePermissionsAsync(
            TradeAccessContext context, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HasPermissionForAllCompanyPeriodsAsync(
            Guid userId, IReadOnlyCollection<TradePeriodScope> scopes, string permissionCode,
            TradeAccessIntent accessIntent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingEntitlement(
        List<string> calls, List<Guid> checks, bool enabled, Exception? error) : ITradeModuleEntitlement
    {
        public Task<bool> IsEnabledForCompanyAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            calls.Add("entitlement");
            checks.Add(tenantId);
            return error is null ? Task.FromResult(enabled) : throw error;
        }
    }
}
