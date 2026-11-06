import { useMemo } from 'react';

/**
 * UI permissions. IMPORTANT: this is a presentation hint only (to hide or disable a control the
 * user could not use anyway). The server is authoritative and checks every request again
 * (D-013, D-064, D-071); never rely on this for security.
 *
 * The hints come from `GET /me/permissions` for the ACTIVE company (see {@link AccessSnapshot}),
 * never from `session.isAdministrator` (a display flag, not company-level authorization).
 */
export type Permission =
  // Installation administrator only (no company permission code exists for these).
  | 'admin.users.view'
  | 'admin.users.create'
  | 'admin.users.update'
  | 'admin.security.view'
  // Installation administrator, or a company member holding the matching company permission code.
  | 'admin.roles.view'
  | 'admin.roles.create'
  | 'admin.roles.update'
  | 'admin.roles.assign'
  | 'admin.permissions.view'
  // Any signed-in user.
  | 'account.view';

/** Where the permission answer stands. Anything but `ready` grants nothing (fail-closed). */
export type AccessStatus = 'loading' | 'ready' | 'error';

/**
 * What `/me/permissions` said for the active company. `companyId` names the company the codes
 * belong to, so a snapshot that outlives a company change can never authorize anything for the
 * new company.
 */
export type AccessSnapshot = {
  status: AccessStatus;
  isInstallationAdministrator: boolean;
  companyId: string | null;
  companyCodes: readonly string[];
};

/** The state before the server answered, after a failure and for a signed-out user: nothing is granted. */
export const deniedAccess: AccessSnapshot = {
  status: 'loading',
  isInstallationAdministrator: false,
  companyId: null,
  companyCodes: [],
};

/** Company permission codes (server-owned, D-071) that unlock each role hint. */
const roleCodes = {
  view: 'roles.view',
  create: 'roles.create',
  update: 'roles.update',
  assign: 'roles.assign',
} as const;

const sameCompany = (left: string | null, right: string): boolean => left !== null && left.toLowerCase() === right.toLowerCase();

/**
 * Answers a UI hint. `companyId` is the company the caller is about to act on; when it is given
 * and differs from the company the snapshot was loaded for, company-level codes grant nothing.
 */
export function can(access: AccessSnapshot | null | undefined, permission: Permission, companyId?: string): boolean {
  if (!access) return false;
  if (permission === 'account.view') return true;
  if (access.status !== 'ready') return false;

  if (access.isInstallationAdministrator) return true;

  switch (permission) {
    case 'admin.users.view':
    case 'admin.users.create':
    case 'admin.users.update':
    case 'admin.security.view':
      return false;
    default: {
      if (companyId !== undefined && !sameCompany(access.companyId, companyId)) return false;
      const has = (code: string) => access.companyCodes.includes(code);
      switch (permission) {
        case 'admin.roles.view':
          return has(roleCodes.view);
        case 'admin.roles.create':
          return has(roleCodes.create);
        case 'admin.roles.update':
          return has(roleCodes.update);
        case 'admin.roles.assign':
          return has(roleCodes.assign);
        case 'admin.permissions.view':
          return Object.values(roleCodes).some(has);
        default:
          return false;
      }
    }
  }
}

/** A hint function bound to a snapshot; `companyId` is the company the page is about to act on. */
export type CanHint = (permission: Permission, companyId?: string) => boolean;

/** True only while a ready answer names the signed-in user an installation administrator. */
export function isInstallationAdministrator(access: AccessSnapshot | null | undefined): boolean {
  return access?.status === 'ready' && access.isInstallationAdministrator;
}

/** Returns a stable `can(permission)` function for the given snapshot (see {@link can}). */
export function usePermissions(access: AccessSnapshot | null | undefined): { can: CanHint } {
  return useMemo(
    () => ({ can: (permission: Permission, companyId?: string) => can(access, permission, companyId) }),
    [access],
  );
}
