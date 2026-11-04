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
 * Installation-wide admin routes use the server session administrator flag; company-scoped
 * menu hints are loaded separately from `/me/permissions` for the current work context.
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
