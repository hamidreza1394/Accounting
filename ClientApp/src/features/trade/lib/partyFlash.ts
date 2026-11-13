// A one-shot, in-memory message handed from a party form page to the detail page it navigates to.
// It lives only in this module's memory: nothing is written to storage or the URL, and a page
// refresh drops it. It never carries party data, only a message kind and the party id it is for.

export type PartyFlashKind = 'created' | 'updated';

/** A message nobody consumed (the detail page never opened) must not show up much later. */
export const partyFlashTtlMs = 30_000;

let pending: { partyId: string; kind: PartyFlashKind; at: number } | null = null;

export function setPartyFlash(partyId: string, kind: PartyFlashKind): void {
  pending = { partyId, kind, at: Date.now() };
}

/** Reads the message for this party without consuming it (safe under StrictMode double render). */
export function peekPartyFlash(partyId: string): PartyFlashKind | null {
  if (!pending || Date.now() - pending.at > partyFlashTtlMs) return null;
  return pending.partyId === partyId ? pending.kind : null;
}

export function clearPartyFlash(): void {
  pending = null;
}

export const partyFlashText: Record<PartyFlashKind, string> = {
  created: 'طرف‌حساب با موفقیت ثبت شد.',
  updated: 'تغییرات طرف‌حساب ذخیره شد.',
};
