import { useEffect, useState } from 'react';
import { getNextEmployeeNumber } from '@/features/hr/api/hrClient';

export type SuggestionState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; value: string }
  | { status: 'error' };

/**
 * Asks the server once for the next personnel number of the work-context company. It is only a
 * suggestion — nothing is reserved — so the create call stays the authority (409). It runs only
 * when `enabled` (the user holds `hr.employees.create` there); a failure leaves the field empty
 * and editable instead of blocking the form.
 */
export function useSuggestedEmployeeNumber(
  companyId: string | null,
  enabled: boolean,
  onUnauthenticated: (error: unknown) => boolean,
): SuggestionState {
  const [state, setState] = useState<SuggestionState>({ status: 'idle' });
  useEffect(() => {
    if (!enabled || companyId === null) {
      setState({ status: 'idle' });
      return undefined;
    }
    const controller = new AbortController();
    setState({ status: 'loading' });
    getNextEmployeeNumber(companyId, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setState({ status: 'ready', value: result.suggestedEmployeeNumber });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setState({ status: 'error' });
      });
    return () => controller.abort();
  }, [companyId, enabled, onUnauthenticated]);
  return state;
}
