import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ComboboxOption } from '@/components/ui/SearchableCombobox';
import { isAbortError } from '@/lib/staleGuard';
import { listUnits } from '../api/tradeClient';
import type { UnitRef } from '../api/tradeTypes';
import { unitOptions } from '../lib/tradeLabels';

export type UnitsState = {
  status: 'loading' | 'ready' | 'error';
  units: readonly UnitRef[];
  options: readonly ComboboxOption[];
  /** The codes of the loaded list, or `null` until it is loaded (the server then validates alone). */
  codes: ReadonlySet<string> | null;
  retry: () => void;
};

/**
 * The reference unit list (`GET /trade/reference/units`) for the product form's base-unit field and for
 * naming a unit code on the detail page and in the list. A failure leaves the pages usable: the code is shown
 * as is and the server validates the value.
 */
export function useUnits(onUnauthenticated: (error: unknown) => boolean): UnitsState {
  const [state, setState] = useState<{ status: UnitsState['status']; units: readonly UnitRef[] }>({ status: 'loading', units: [] });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setState((previous) => ({ status: 'loading', units: previous.units }));
    listUnits(controller.signal)
      .then((units) => {
        if (!controller.signal.aborted) setState({ status: 'ready', units });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || isAbortError(cause) || onUnauthenticated(cause)) return;
        setState({ status: 'error', units: [] });
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  const options = useMemo(() => unitOptions(state.units), [state.units]);
  const codes = useMemo(() => (state.status === 'ready' ? new Set(state.units.map((unit) => unit.code)) : null), [state]);
  return { status: state.status, units: state.units, options, codes, retry };
}
