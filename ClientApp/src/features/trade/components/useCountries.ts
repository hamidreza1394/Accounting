import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ComboboxOption } from '@/components/ui/SearchableCombobox';
import { isAbortError } from '@/lib/staleGuard';
import { listCountries } from '../api/tradeClient';
import type { CountryRef } from '../api/tradeTypes';
import { countryOptions } from '../lib/tradeLabels';

export type CountriesState = {
  status: 'loading' | 'ready' | 'error';
  countries: readonly CountryRef[];
  options: readonly ComboboxOption[];
  /** The codes of the loaded list, or `null` until it is loaded (the server then validates alone). */
  codes: ReadonlySet<string> | null;
  retry: () => void;
};

/**
 * The reference country list (`GET /trade/reference/countries`, 249 entries) for the form's country field
 * and for naming a country code on the detail page. A failure leaves the pages usable: the code is shown
 * as is and the server validates the value.
 */
export function useCountries(onUnauthenticated: (error: unknown) => boolean): CountriesState {
  const [state, setState] = useState<{ status: CountriesState['status']; countries: readonly CountryRef[] }>({ status: 'loading', countries: [] });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setState((previous) => ({ status: 'loading', countries: previous.countries }));
    listCountries(controller.signal)
      .then((countries) => {
        if (!controller.signal.aborted) setState({ status: 'ready', countries });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || isAbortError(cause) || onUnauthenticated(cause)) return;
        setState({ status: 'error', countries: [] });
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  const options = useMemo(() => countryOptions(state.countries), [state.countries]);
  const codes = useMemo(() => (state.status === 'ready' ? new Set(state.countries.map((country) => country.code)) : null), [state]);
  return { status: state.status, countries: state.countries, options, codes, retry };
}
