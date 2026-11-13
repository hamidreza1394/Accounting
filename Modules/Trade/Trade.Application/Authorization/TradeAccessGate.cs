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
    public Task<TradeAccessDecision> AuthorizeAsync(
        Guid userId,
        string permissionCode,
        TradeAccessIntent intent,
        CancellationToken cancellationToken = default) =>
        AuthorizeAsync(userId, permissionCode, intent, TradeAccessOptions.Default, cancellationToken);

    public async Task<TradeAccessDecision> AuthorizeAsync(
        Guid userId,
        string permissionCode,
        TradeAccessIntent intent,
        TradeAccessOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var context = await ResolveAsync(userId, cancellationToken);
        if (context.Denied is { } denied)
        {
            return denied;
        }

        var current = context.Context!;
        // Only an operation that explicitly declares itself period-independent (tenant-level master data,
        // D-063 item 3, D-084) skips the closed-period write block; for every other operation the default stays
        // "writes need a writable period".
        var periodIndependent = options.PeriodIndependent && intent == TradeAccessIntent.Write;
        if (intent == TradeAccessIntent.Write && !current.AllowsWrites && !periodIndependent)
        {
            return new TradeAccessDecision(TradeAccessOutcome.PeriodReadOnly, null);
        }

        var (enabled, fault) = await IsEntitledAsync(current.TenantId, cancellationToken);
        if (!enabled)
        {
            return new TradeAccessDecision(TradeAccessOutcome.ModuleUnavailable, null, fault);
        }

        if (!await authorization.HasPermissionAsync(
                current, permissionCode, intent, cancellationToken, periodIndependent))
        {
            return new TradeAccessDecision(TradeAccessOutcome.Forbidden, null);
        }

        // Extra Read permissions (for example update also needs view) are checked in the gate, so a user
        // lacking them never reaches the handler and learns nothing about the record.
        foreach (var additional in options.AdditionalReadPermissions)
        {
            if (!await authorization.HasPermissionAsync(
                    current, additional, TradeAccessIntent.Read, cancellationToken))
            {
                return new TradeAccessDecision(TradeAccessOutcome.Forbidden, null);
            }
        }

        return new TradeAccessDecision(TradeAccessOutcome.Allowed, current);
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
