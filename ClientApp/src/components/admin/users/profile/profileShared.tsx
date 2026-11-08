import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { ApiError } from '@/api/client';
import { ErrorState, LoadingState } from '@/components/Feedback';

/**
 * Shared pieces of the user-profile workspace tabs (stage 3, Issue #444):
 * - `useProfileTabData` loads each tab's data only when the tab mounts (lazy) and keeps the
 *   result in a per-user in-memory cache held by the page, so revisiting a tab never issues
 *   a duplicate request and switching away never discards loaded data.
 * - `Info` is the labeled read-only field used across the profile tabs.
 */

export type ProfileTabState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'error'; status: number | null; message: string };

/**
 * Lazy tab data loader. Runs `loader` on the first mount (or after `retry`), caches the
 * result under `cacheKey` in the page-level cache and serves later mounts from it. A tab
 * that stays inactive never fetches; an unmounted tab keeps its cache entry.
 */
export function useProfileTabData<T>(
  cache: Map<string, unknown>,
  cacheKey: string,
  loader: (signal: AbortSignal) => Promise<T>,
  onUnauthenticated: (error: unknown) => boolean,
): ProfileTabState<T> & { retry: () => void } {
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<ProfileTabState<T>>(() => {
    const cached = cache.get(cacheKey);
    return cached !== undefined ? { kind: 'ready', data: cached as T } : { kind: 'loading' };
  });
  const loaderRef = useRef(loader);
  loaderRef.current = loader;

  useEffect(() => {
    const cached = cache.get(cacheKey);
    if (cached !== undefined) {
      setState({ kind: 'ready', data: cached as T });
      return;
    }
    const controller = new AbortController();
    let cancelled = false;
    setState({ kind: 'loading' });
    loaderRef
      .current(controller.signal)
      .then((data) => {
        if (cancelled || controller.signal.aborted) return;
        cache.set(cacheKey, data);
        setState({ kind: 'ready', data });
      })
      .catch((cause: unknown) => {
        if (cancelled || controller.signal.aborted) return;
        if (onUnauthenticated(cause)) return;
        setState({
          kind: 'error',
          status: cause instanceof ApiError ? cause.status : null,
          message:
            cause instanceof ApiError ? cause.message : 'دریافت اطلاعات این بخش انجام نشد. لطفاً دوباره تلاش کنید.',
        });
      });
    return () => {
      cancelled = true;
      controller.abort();
    };
    // The cache and the key fully determine the load; `loader` is read through a ref so an
    // inline closure never re-triggers a fetch for a tab that is already satisfied.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cache, cacheKey, attempt, onUnauthenticated]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { ...state, retry };
}

export function Info({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0 rounded-xl bg-cool px-3.5 py-3">
      <dt className="text-xs font-medium text-sub">{label}</dt>
      <dd className="mt-1 break-words text-sm font-semibold text-ink">{children}</dd>
    </div>
  );
}

/** The standard loading / error (with retry) wrapper of every profile tab. */
export function TabDataBoundary<T>({
  state,
  label,
  children,
}: {
  state: ProfileTabState<T> & { retry: () => void };
  label: string;
  children: (data: T) => ReactNode;
}) {
  if (state.kind === 'loading') return <LoadingState label={label} />;
  if (state.kind === 'error') {
    const retry = state.status === 401 || state.status === 403 || state.status === 404 ? null : state.retry;
    return <ErrorState message={state.message} {...(retry ? { onRetry: retry } : {})} />;
  }
  return <>{children(state.data)}</>;
}
