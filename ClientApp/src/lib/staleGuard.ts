import { useMemo, useRef } from 'react';

/**
 * The stale-response guard of the workspace (Issue #444 stage 1 completion, PR 4).
 *
 * A tiny generation counter — deliberately NOT a cache or a state-management framework.
 * Every context-scoped read site creates its guard with the key that names the data scope
 * (the company id, or `company:period`). When the key changes, the generation increments
 * and every in-flight completion of the previous scope is stale:
 *
 * - requests keep their `AbortController` (the existing pattern of the pages);
 * - the guard adds the second line of defence for completions that cannot be aborted
 *   anymore (already-settled promises racing the context switch): the caller checks
 *   `isCurrent(token)` before `setState`, so a late answer of the OLD company/period can
 *   never enter the state of the new one.
 */

export type StaleGuard = {
  /** The generation this render/effect belongs to; capture it before starting a request. */
  token: number;
  /** True only when `candidate` is still the newest generation of this scope. */
  isCurrent: (candidate: number) => boolean;
};

export function useStaleGuard(key: string): StaleGuard {
  const generation = useRef(1);
  const lastKey = useRef(key);
  if (lastKey.current !== key) {
    lastKey.current = key;
    generation.current += 1;
  }
  const token = generation.current;
  return useMemo<StaleGuard>(
    () => ({
      token,
      isCurrent: (candidate: number) => candidate === generation.current,
    }),
    [token],
  );
}

/** True for a request cancelled through its `AbortSignal` (never a real user-facing error). */
export function isAbortError(cause: unknown): boolean {
  return cause instanceof DOMException
    ? cause.name === 'AbortError'
    : cause instanceof Error && cause.name === 'AbortError';
}
