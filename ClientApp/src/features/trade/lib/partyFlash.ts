import { createFlashStore, flashTtlMs } from './tradeFlash';

// The one-shot message from a party form page to the detail page (see `tradeFlash.ts`).

export type PartyFlashKind = 'created' | 'updated';

export const partyFlashTtlMs = flashTtlMs;

const store = createFlashStore<PartyFlashKind>();

export const setPartyFlash = store.set;
export const peekPartyFlash = store.peek;
export const clearPartyFlash = store.clear;

export const partyFlashText: Record<PartyFlashKind, string> = {
  created: 'طرف‌حساب با موفقیت ثبت شد.',
  updated: 'تغییرات طرف‌حساب ذخیره شد.',
};
