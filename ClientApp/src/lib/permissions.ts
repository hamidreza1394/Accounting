import { useMemo } from 'react';
import type { Session } from '../api/types';

/**
 * UI permissions. IMPORTANT: this is a presentation hint only (to hide or disable a control the
 * user could not use anyway). The server is authoritative and checks every request again (D-064);
 * never rely on this for security.
 */
export type Permission =
  | 'admin.users.view'
  | 'admin.users.create'
  | 'admin.users.update'
  // Same rule as admin.users.*: a UI hint from `isAdministrator`; the server decides every request.
  | 'admin.roles.view'
  | 'admin.roles.manage'
  | 'admin.permissions.view'
  | 'admin.security.view'
  | 'account.view';

/**
 * Today the hint is derived only from `session.isAdministrator` for `admin.*` permissions and is
 * always true for `account.view`. When `GET /me/permissions` exists, this function will switch to
 * the server-provided list and keep the same signature.
 */
export function can(session: Pick<Session, 'isAdministrator'> | null | undefined, permission: Permission): boolean {
  if (!session) return false;
  if (permission.startsWith('admin.')) return session.isAdministrator;
  return permission === 'account.view';
}

/** Returns a stable `can(permission)` function for the given session (see {@link can}). */
export function usePermissions(session: Pick<Session, 'isAdministrator'> | null | undefined): {
  can: (permission: Permission) => boolean;
} {
  const isAdministrator = session?.isAdministrator;
  const hasSession = session != null;
  return useMemo(
    () => ({
      can: (permission: Permission) => can(hasSession ? { isAdministrator: isAdministrator === true } : null, permission),
    }),
    [hasSession, isAdministrator],
  );
}
