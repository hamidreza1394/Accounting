// A one-shot, in-memory message handed from a form page to the detail page it navigates to. Shared by the
// Trade slices (Party, Product). It lives only in memory: nothing is written to storage or the URL, a refresh
// drops it, and it never carries record data, only a message kind and the record id it is for.

/** A message nobody consumed (the detail page never opened) must not show up much later. */
export const flashTtlMs = 30_000;

export type FlashStore<K extends string> = {
  set: (recordId: string, kind: K) => void;
  /** Reads the message for this record without consuming it (safe under StrictMode double render). */
  peek: (recordId: string) => K | null;
  clear: () => void;
};

export function createFlashStore<K extends string>(): FlashStore<K> {
  let pending: { recordId: string; kind: K; at: number } | null = null;
  return {
    set: (recordId, kind) => {
      pending = { recordId, kind, at: Date.now() };
    },
    peek: (recordId) => {
      if (!pending || Date.now() - pending.at > flashTtlMs) return null;
      return pending.recordId === recordId ? pending.kind : null;
    },
    clear: () => {
      pending = null;
    },
  };
}
