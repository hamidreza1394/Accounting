import { useCallback, useEffect, useState } from 'react';
import { ApiError, getPermissionCatalog } from '../../../api/client';
import type { PermissionCatalogItem } from '../../../api/types';

export type CatalogState =
  | { kind: 'loading' }
  | { kind: 'ready'; catalog: PermissionCatalogItem[] }
  | { kind: 'error'; status: number | null; message: string };

/** Loads the permission catalog once per page; a role form cannot be used without it. */
export function usePermissionCatalog(onUnauthenticated: (error: unknown) => boolean): { state: CatalogState; retry: () => void } {
  const [state, setState] = useState<CatalogState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    getPermissionCatalog(controller.signal)
      .then((catalog) => {
        if (!controller.signal.aborted) setState({ kind: 'ready', catalog });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setState({
          kind: 'error',
          status: cause instanceof ApiError ? cause.status : null,
          message: cause instanceof ApiError ? cause.message : 'دریافت کاتالوگ مجوزها انجام نشد. لطفاً دوباره تلاش کنید.',
        });
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);
  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { state, retry };
}
