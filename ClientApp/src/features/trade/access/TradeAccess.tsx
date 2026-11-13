import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError } from '@/api/client';
import { getTradeCapabilities } from '../api/tradeClient';
import type { TradeAccessMode } from '../api/tradeTypes';

/**
 * UI hints for the Trade module. They come ONLY from `GET /trade/capabilities` (the Trade permission
 * codes of the active company); the shell permission snapshot knows nothing about Trade. This is a
 * presentation hint — the server checks every request again (D-013, D-064) — and it is fail-closed:
 * while loading, after any failure, when the module is not available, nothing is granted.
 */

/** Short hint names mapped to the server's Trade permission codes (`trade.form.<form>.<action>`). */
const permissionCodes = {
  'party.view': 'trade.form.party.view',
  'party.create': 'trade.form.party.create',
  'party.update': 'trade.form.party.update',
  'product.view': 'trade.form.product.view',
  'product.create': 'trade.form.product.create',
  'product.update': 'trade.form.product.update',
} as const;

export type TradePermission = keyof typeof permissionCodes;

/**
 * Permission hints the server requires TOGETHER for an operation: a Party/Product write needs its write code AND
 * its `view` code (otherwise the server answers 403 `trade_forbidden`), so the UI shows a write action only when both
 * are held.
 */
const requiredHints: Record<TradePermission, readonly TradePermission[]> = {
  'party.view': ['party.view'],
  'party.create': ['party.create', 'party.view'],
  'party.update': ['party.update', 'party.view'],
  'product.view': ['product.view'],
  'product.create': ['product.create', 'product.view'],
  'product.update': ['product.update', 'product.view'],
};

/**
 * Hints that additionally need a writable fiscal period (`accessMode: ReadWrite`). EMPTY on purpose: Party and Product are
 * tenant-level master data and may be written in a closed period too (owner decision), so their hints never look
 * at `accessMode`. Keep the mechanism for a future operation that really is period-bound; add its hint here.
 */
const periodBoundHints: ReadonlySet<TradePermission> = new Set<TradePermission>();

export type TradeAccessStatus = 'loading' | 'ready' | 'unavailable' | 'error';

export type TradeAccess = {
  status: TradeAccessStatus;
  /** Why the module is not usable: not licensed/enabled for the company, or no valid work context. */
  unavailableReason: 'module' | 'context' | null;
  accessMode: TradeAccessMode | null;
  /** The raw permission codes the server granted (empty unless `ready`). */
  permissions: readonly string[];
  /** True when the user holds every code the operation needs (and, for a period-bound hint, the period is writable). */
  can: (permission: TradePermission) => boolean;
  retry: () => void;
};

const noop = () => undefined;
const denyAll = () => false;

/** The default of the context: nothing granted. Components outside a provider (tests, the sidebar alone) fail closed. */
export const deniedTradeAccess: TradeAccess = {
  status: 'loading',
  unavailableReason: null,
  accessMode: null,
  permissions: [],
  can: denyAll,
  retry: noop,
};

export const TradeAccessContext = createContext<TradeAccess>(deniedTradeAccess);

export function useTradeAccess(): TradeAccess {
  return useContext(TradeAccessContext);
}

type State =
  | { scope: string; status: 'loading' }
  | { scope: string; status: 'ready'; accessMode: TradeAccessMode; permissions: readonly string[] }
  | { scope: string; status: 'unavailable'; reason: 'module' | 'context' }
  | { scope: string; status: 'error' };

/** Builds the access value of a settled answer; exported so tests can inject exact states. */
export function createTradeAccess(
  input:
    | { status: 'loading' | 'error' }
    | { status: 'unavailable'; reason: 'module' | 'context' }
    | { status: 'ready'; accessMode: TradeAccessMode; permissions: readonly string[] },
  retry: () => void = noop,
): TradeAccess {
  if (input.status !== 'ready') {
    return {
      ...deniedTradeAccess,
      status: input.status,
      unavailableReason: input.status === 'unavailable' ? input.reason : null,
      retry,
    };
  }
  const holds = (permission: TradePermission) => input.permissions.includes(permissionCodes[permission]);
  return {
    status: 'ready',
    unavailableReason: null,
    accessMode: input.accessMode,
    permissions: input.permissions,
    can: (permission) =>
      requiredHints[permission].every(holds) && (!periodBoundHints.has(permission) || input.accessMode === 'ReadWrite'),
    retry,
  };
}

/**
 * Loads the capabilities of the active work context. `scope` names that context (company and fiscal
 * period): a change drops the previous answer in the same render and reloads, and a late answer of
 * an older scope is ignored, so one company's hints can never authorize another's pages.
 */
export function TradeAccessProvider({
  scope,
  onUnauthenticated,
  children,
}: {
  scope: string;
  onUnauthenticated?: ((error: unknown) => boolean) | undefined;
  children: ReactNode;
}) {
  const [state, setState] = useState<State>({ scope, status: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setState({ scope, status: 'loading' });
    getTradeCapabilities(controller.signal)
      .then((capabilities) => {
        if (controller.signal.aborted) return;
        setState(
          capabilities.available
            ? { scope, status: 'ready', accessMode: capabilities.accessMode, permissions: capabilities.permissions }
            : { scope, status: 'unavailable', reason: 'module' },
        );
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated?.(cause)) return;
        if (cause instanceof ApiError && cause.status === 403 && cause.problemCode === 'trade_module_unavailable') {
          setState({ scope, status: 'unavailable', reason: 'module' });
        } else if (cause instanceof ApiError && cause.status === 409 && cause.problemCode === 'trade_context_required') {
          setState({ scope, status: 'unavailable', reason: 'context' });
        } else {
          // Includes 403 trade_forbidden and every network/5xx/contract failure: nothing is granted.
          setState({ scope, status: 'error' });
        }
      });
    return () => controller.abort();
  }, [scope, attempt, onUnauthenticated]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);

  const value = useMemo<TradeAccess>(() => {
    // A state of another scope (the render right after a context change) grants nothing.
    if (state.scope !== scope) return createTradeAccess({ status: 'loading' }, retry);
    switch (state.status) {
      case 'ready':
        return createTradeAccess({ status: 'ready', accessMode: state.accessMode, permissions: state.permissions }, retry);
      case 'unavailable':
        return createTradeAccess({ status: 'unavailable', reason: state.reason }, retry);
      default:
        return createTradeAccess({ status: state.status }, retry);
    }
  }, [state, scope, retry]);

  return <TradeAccessContext.Provider value={value}>{children}</TradeAccessContext.Provider>;
}
