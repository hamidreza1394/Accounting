// A one-shot, in-memory message handed from a form page to the detail page it navigates to.
// It lives only in this module's memory: nothing is written to storage or the URL, and a page
// refresh drops it. It never carries user data, only a message kind and the user id it is for.

export type UserFlashKind =
  | 'created'
  | 'updated'
  /** The account and its personnel record were created together. */
  | 'createdWithEmployee'
  /** The account exists, but the personnel number was already taken in the company. */
  | 'createdEmployeeDuplicate'
  /** The account exists, but the personnel record could not be created. */
  | 'createdEmployeeFailed'
  /** The account and its personnel record exist, but the chosen photo could not be stored. */
  | 'createdPhotoFailed';

/** Kinds that report a half-finished creation: shown as a warning, never as plain success. */
export const userFlashWarnings: readonly UserFlashKind[] = ['createdEmployeeDuplicate', 'createdEmployeeFailed', 'createdPhotoFailed'];

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
  createdWithEmployee: 'کاربر و پرونده پرسنلی او با موفقیت ایجاد شدند. اتصال پرونده به حساب پس از عضویت فعال در شرکت از بخش «اطلاعات استخدامی» انجام می‌شود.',
  createdEmployeeDuplicate:
    'حساب کاربر ایجاد شد، اما پرونده پرسنلی ساخته نشد، چون شماره پرسنلی در این شرکت تکراری بود. پرونده را از بخش کارکنان با شمارهٔ دیگری ثبت کنید.',
  createdEmployeeFailed:
    'حساب کاربر ایجاد شد، اما ثبت پرونده پرسنلی انجام نشد. پرونده را بعداً از بخش کارکنان ثبت کنید.',
  createdPhotoFailed:
    'کاربر و پرونده پرسنلی او ایجاد شدند، اما ذخیرهٔ عکس پرسنلی انجام نشد. عکس را بعداً از ویرایش کاربر دوباره بارگذاری کنید.',
};

// --- The message the users LIST shows after a create (the form closes and returns to the list) ---

type ListFlash = { kind: UserFlashKind; at: number; id: number };
let listFlash: ListFlash | null = null;
let listFlashSeq = 0;
const listFlashListeners = new Set<() => void>();

function emitListFlash(): void {
  for (const listener of listFlashListeners) listener();
}

/** Remembers the outcome of a create for the list page (in memory only, one message at a time). */
export function setUserListFlash(kind: UserFlashKind): void {
  listFlashSeq += 1;
  listFlash = { kind, at: Date.now(), id: listFlashSeq };
  emitListFlash();
}

export function clearUserListFlash(): void {
  if (listFlash === null) return;
  listFlash = null;
  emitListFlash();
}

export function subscribeUserListFlash(listener: () => void): () => void {
  listFlashListeners.add(listener);
  return () => listFlashListeners.delete(listener);
}

/** The pending list message, or `null` when none exists or it is older than {@link userFlashTtlMs}. */
export function readUserListFlash(): ListFlash | null {
  if (!listFlash || Date.now() - listFlash.at > userFlashTtlMs) return null;
  return listFlash;
}
