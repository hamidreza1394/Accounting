// A one-shot, in-memory message handed from a form page to the detail page it navigates to.
// It lives only in this module's memory: nothing is written to storage or the URL, and a page
// refresh drops it. It never carries user data, only a message kind and the user id it is for.

export type UserFlashKind = 'created' | 'updated';

/** A message nobody consumed (e.g. the detail page never opened) must not show up much later. */
export const userFlashTtlMs = 30_000;

let pending: { userId: string; kind: UserFlashKind; at: number } | null = null;

export function setUserFlash(userId: string, kind: UserFlashKind): void {
  pending = { userId, kind, at: Date.now() };
}

/** Reads the message for this user without consuming it (safe under StrictMode double render). */
export function peekUserFlash(userId: string): UserFlashKind | null {
  if (!pending || Date.now() - pending.at > userFlashTtlMs) return null;
  return pending.userId === userId ? pending.kind : null;
}

export function clearUserFlash(): void {
  pending = null;
}

export const userFlashText: Record<UserFlashKind, string> = {
  created: 'کاربر با موفقیت ایجاد شد.',
  updated: 'تغییرات کاربر ذخیره شد.',
};
