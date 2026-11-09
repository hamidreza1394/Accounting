import { useSyncExternalStore } from 'react';

/**
 * The "the users changed, load the list again" signal. The multi-tab workspace keeps every opened
 * page mounted, so a list that is open in a background tab would otherwise keep showing the old
 * rows after a user was created. The form calls {@link invalidateUserList}; the list page depends
 * on {@link useUserListVersion} and fetches again. Memory only, no storage, no user data.
 */
let version = 0;
const listeners = new Set<() => void>();

export function invalidateUserList(): void {
  version += 1;
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useUserListVersion(): number {
  return useSyncExternalStore(subscribe, () => version, () => 0);
}
