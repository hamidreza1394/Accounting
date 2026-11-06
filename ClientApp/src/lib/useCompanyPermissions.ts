import { useEffect, useMemo, useRef, useState } from 'react';
import { getCurrentPermissions } from '../api/client';
import type { CurrentPermissions } from '../api/types';
import { deniedAccess, type AccessSnapshot } from './permissions';

/** A returning tab revalidates the hints only if the last answer is older than this. */
export const permissionRevalidateAfterMs = 60_000;

type State = { companyId: string; value: CurrentPermissions | null; error: boolean };

/**
 * Loads `/me/permissions` for the active company and exposes it as an {@link AccessSnapshot}.
 *
 * Presentation hint only; the server still authorizes every operation (D-013, D-071). The hook is
 * fail-closed: while loading, after any failure, for a malformed answer, or when the answer names
 * another company than the active one, nothing is granted. A company change drops the previous
 * answer in the same render and reloads, and a late answer for an old company is ignored.
 */
export function useCompanyPermissions(companyId: string | undefined, onUnauthenticated: (error: unknown) => boolean) {
  const [state, setState] = useState<State | null>(null);
  const [attempt, setAttempt] = useState(0);
  // A soft reload (tab shown again) keeps the current answer until the fresh one arrives; every other
  // reload drops it first.
  const soft = useRef(false);
  const loadedAt = useRef(0);
  const loadedFor = useRef<string | undefined>(undefined);

  useEffect(() => {
    if (!companyId) { setState(null); return undefined; }
    const controller = new AbortController();
    // A different company is never a soft reload: the old answer must not survive it.
    if (!soft.current || loadedFor.current !== companyId) setState(null);
    soft.current = false;
    loadedFor.current = companyId;
    getCurrentPermissions(controller.signal).then((value) => {
      if (controller.signal.aborted) return;
      loadedAt.current = Date.now();
      if (value.company?.id.toLowerCase() !== companyId.toLowerCase()) {
        setState({ companyId, value: null, error: true });
      } else setState({ companyId, value, error: false });
    }).catch((error: unknown) => {
      if (controller.signal.aborted || onUnauthenticated(error)) return;
      setState({ companyId, value: null, error: true });
    });
    return () => controller.abort();
  }, [companyId, attempt, onUnauthenticated]);

  // Rights can change while the tab is in the background: revalidate when it is shown again.
  useEffect(() => {
    if (!companyId) return undefined;
    const revalidate = () => {
      if (document.visibilityState !== 'visible') return;
      if (Date.now() - loadedAt.current < permissionRevalidateAfterMs) return;
      soft.current = true;
      setAttempt((value) => value + 1);
    };
    document.addEventListener('visibilitychange', revalidate);
    return () => document.removeEventListener('visibilitychange', revalidate);
  }, [companyId]);

  const current = state && state.companyId === companyId ? state : null;
  const status = current ? (current.error ? 'error' as const : 'ready' as const) : 'loading' as const;
  const isInstallationAdministrator = current?.value?.isInstallationAdministrator === true;
  const codes = useMemo(() => current?.value?.company?.permissionCodes ?? [], [current]);
  const resolvedCompanyId = current?.value?.company?.id ?? null;

  const access = useMemo<AccessSnapshot>(
    () =>
      status === 'ready'
        ? { status, isInstallationAdministrator, companyId: resolvedCompanyId, companyCodes: codes }
        : { ...deniedAccess, status },
    [status, isInstallationAdministrator, resolvedCompanyId, codes],
  );

  // `retry` (after a failure) is a hard reload. `refresh` (a server 403 says the hints may be stale) keeps the page
  // mounted until the fresh answer arrives, so a re-check can never unmount and remount the page in a loop; a
  // failed re-check still ends in `error`, which grants nothing.
  const retry = useMemo(() => () => setAttempt((value) => value + 1), []);
  const refresh = useMemo(
    () => () => {
      soft.current = true;
      setAttempt((value) => value + 1);
    },
    [],
  );
  return { access, codes, status, retry, refresh };
}
