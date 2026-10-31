using Faranou.Modules.Trade.Application.Licensing;

namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Fail-closed Trade access gate. Order: identity, context, period state, commercial entitlement,
/// then user permission. A failing or throwing entitlement never falls back to user permission.
/// </summary>
public sealed class TradeAccessGate(
    ITradeAuthorization authorization,
    ITradeModuleEntitlement entitlement)
{
    public async Task<TradeAccessDecision> AuthorizeAsync(
        Guid userId,
        string permissionCode,
        TradeAccessIntent intent,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAsync(userId, cancellationToken);
        if (context.Denied is { } denied)
        {
            return denied;
        }

        var current = context.Context!;
        if (intent == TradeAccessIntent.Write && !current.AllowsWrites)
        {
            return new TradeAccessDecision(TradeAccessOutcome.PeriodReadOnly, null);
        }

        var (enabled, fault) = await IsEntitledAsync(current.TenantId, cancellationToken);
        if (!enabled)
        {
            return new TradeAccessDecision(TradeAccessOutcome.ModuleUnavailable, null, fault);
        }

        return await authorization.HasPermissionAsync(current, permissionCode, intent, cancellationToken)
            ? new TradeAccessDecision(TradeAccessOutcome.Allowed, current)
            : new TradeAccessDecision(TradeAccessOutcome.Forbidden, null);
    }

    /// <summary>Stages 1 and 3 only (no permission check); permissions are read only when entitled.</summary>
    public async Task<TradeCapabilities> GetCapabilitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(userId, cancellationToken);
        if (resolved.Denied is { } denied)
        {
            return new TradeCapabilities(denied.Outcome, TradeAccessMode.ReadOnly, []);
        }

        var current = resolved.Context!;
        var mode = current.AllowsWrites ? TradeAccessMode.ReadWrite : TradeAccessMode.ReadOnly;
        var (enabled, fault) = await IsEntitledAsync(current.TenantId, cancellationToken);
        if (!enabled)
        {
            return new TradeCapabilities(TradeAccessOutcome.ModuleUnavailable, mode, [], fault);
        }

        var permissions = await authorization.GetGrantedTradePermissionsAsync(current, cancellationToken);
        return new TradeCapabilities(TradeAccessOutcome.Allowed, mode, permissions);
    }

    private async Task<(TradeAccessContext? Context, TradeAccessDecision? Denied)> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return (null, new TradeAccessDecision(TradeAccessOutcome.Unauthenticated, null));
        }

        var context = await authorization.ResolveCurrentContextAsync(userId, cancellationToken);
        return context is null
            ? (null, new TradeAccessDecision(TradeAccessOutcome.ContextRequired, null))
            : (context, null);
    }

    private async Task<(bool Enabled, Exception? Fault)> IsEntitledAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await entitlement.IsEnabledForCompanyAsync(tenantId, cancellationToken), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}
