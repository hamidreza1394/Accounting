import { useEffect, useState } from 'react';
import { getCurrentPermissions } from '../api/client';
import type { CurrentPermissions } from '../api/types';

/** Presentation hint only; the server still authorizes every operation. */
export function useCompanyPermissions(companyId: string | undefined, onUnauthenticated: (error: unknown) => boolean) {
  const [state, setState] = useState<{ companyId: string; value: CurrentPermissions | null; error: boolean } | null>(null);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!companyId) { setState(null); return undefined; }
    const controller = new AbortController();
    setState(null);
    getCurrentPermissions(controller.signal).then((value) => {
      if (controller.signal.aborted) return;
      if (value.company?.id.toLowerCase() !== companyId.toLowerCase()) {
        setState({ companyId, value: null, error: true });
      } else setState({ companyId, value, error: false });
    }).catch((error: unknown) => {
      if (controller.signal.aborted || onUnauthenticated(error)) return;
      setState({ companyId, value: null, error: true });
    });
    return () => controller.abort();
  }, [companyId, attempt, onUnauthenticated]);
  const current = state && state.companyId === companyId ? state : null;
  return {
    codes: current?.value?.company?.permissionCodes ?? [],
    status: current ? (current.error ? 'error' as const : 'ready' as const) : 'loading' as const,
    retry: () => setAttempt((value) => value + 1),
  };
}
