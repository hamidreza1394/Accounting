import { useSyncExternalStore } from 'react';

// The party list tab stays mounted (hidden) while a form tab is open, so a saved create/update must tell
// it to read again. A module-level version counter, nothing more: no data is kept here.

let version = 0;
const listeners = new Set<() => void>();

export function invalidatePartyList(): void {
  version += 1;
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Changes whenever a party was created or updated; put it in the list's load effect dependencies. */
export function usePartyListVersion(): number {
  return useSyncExternalStore(subscribe, () => version, () => version);
}
