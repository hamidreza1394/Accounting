import { useCallback, useEffect, useState } from 'react';
import { ApiError, getCompanies } from '../../../api/client';
import type { CompanySummary } from '../../../api/types';

export type CompanyChoices =
  | { kind: 'loading' }
  | { kind: 'ready'; companies: CompanySummary[] }
  | { kind: 'error'; message: string };

/**
 * The companies the signed-in user belongs to (`GET /companies`). There is no installation-wide
 * company list in the API yet, so this is the only source for the company selector.
 */
export function useCompanyChoices(onUnauthenticated: (error: unknown) => boolean): { state: CompanyChoices; retry: () => void } {
  const [state, setState] = useState<CompanyChoices>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    getCompanies(controller.signal)
      .then((companies) => {
        if (!controller.signal.aborted) setState({ kind: 'ready', companies });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setState({ kind: 'error', message: cause instanceof ApiError ? cause.message : 'دریافت فهرست شرکت‌ها انجام نشد.' });
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);
  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { state, retry };
}

/** The company named by `?company=`, else the active work-context company. */
export function resolveCompanyId(params: URLSearchParams, defaultCompanyId: string): string {
  const fromUrl = (params.get('company') ?? '').trim();
  return fromUrl !== '' ? fromUrl : defaultCompanyId;
}

export const rolesPath = (companyId: string): string => `/admin/roles?company=${encodeURIComponent(companyId)}`;
export const newRolePath = (companyId: string): string => `/admin/roles/new?company=${encodeURIComponent(companyId)}`;
export const rolePath = (companyId: string, roleId: string): string =>
  `/admin/roles/${encodeURIComponent(roleId)}?company=${encodeURIComponent(companyId)}`;
