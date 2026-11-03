// A one-shot, in-memory message handed from a role form to the page it navigates to (same idea as
// `userFlash`). Nothing is written to storage or the URL; a refresh drops it.

export type RoleFlashKind = 'created' | 'updated';

export const roleFlashTtlMs = 30_000;

let pending: { roleId: string; kind: RoleFlashKind; at: number } | null = null;

export function setRoleFlash(roleId: string, kind: RoleFlashKind): void {
  pending = { roleId, kind, at: Date.now() };
}

/** Reads the message for this role without consuming it (safe under StrictMode double render). */
export function peekRoleFlash(roleId: string): RoleFlashKind | null {
  if (!pending || Date.now() - pending.at > roleFlashTtlMs) return null;
  return pending.roleId === roleId ? pending.kind : null;
}

export function clearRoleFlash(): void {
  pending = null;
}

export const roleFlashText: Record<RoleFlashKind, string> = {
  created: 'گروه دسترسی با موفقیت ایجاد شد.',
  updated: 'تغییرات گروه دسترسی ذخیره شد.',
};
