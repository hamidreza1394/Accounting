import { useSyncExternalStore } from 'react';

// A list tab stays mounted (hidden) while a form tab is open, so a saved create/update must tell it to read
// again. A module-level version counter per slice, nothing more: no data is kept here.

export function createListVersion() {
  let version = 0;
  const listeners = new Set<() => void>();
  const subscribe = (listener: () => void): (() => void) => {
    listeners.add(listener);
    return () => listeners.delete(listener);
  };
  return {
    invalidate: (): void => {
      version += 1;
      for (const listener of listeners) listener();
    },
    /** Changes whenever a record was created or updated; put it in the list's load effect dependencies. */
    useVersion: (): number => useSyncExternalStore(subscribe, () => version, () => version),
  };
}
