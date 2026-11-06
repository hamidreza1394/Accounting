import { renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { can, deniedAccess, type AccessSnapshot, usePermissions } from './permissions';

function snapshot(overrides: Partial<AccessSnapshot> = {}): AccessSnapshot {
  return { status: 'ready', isInstallationAdministrator: false, companyId: 'a', companyCodes: [], ...overrides };
}

const installationAdmin = snapshot({ isInstallationAdministrator: true });
const ordinaryMember = snapshot({ companyCodes: ['users.list'] });
const companyAdmin = snapshot({ companyCodes: ['roles.view', 'roles.create', 'roles.update', 'roles.assign'] });
const companyAdminViewOnly = snapshot({ companyCodes: ['roles.view'] });

const installationOnly = [
  'admin.users.view',
  'admin.users.create',
  'admin.users.update',
  'admin.security.view',
] as const;
const roleHints = [
  'admin.roles.view',
  'admin.roles.create',
  'admin.roles.update',
  'admin.roles.assign',
  'admin.permissions.view',
] as const;

describe('permissions (UI hint only, fail-closed)', () => {
  describe('installation administrator (from /me/permissions, not the session flag)', () => {
    it('is granted every admin hint', () => {
      for (const permission of [...installationOnly, ...roleHints]) expect(can(installationAdmin, permission)).toBe(true);
    });
  });

  describe('company administrator', () => {
    it('is granted the role hints their company permission codes name, and no installation-wide hint', () => {
      for (const permission of roleHints) expect(can(companyAdmin, permission)).toBe(true);
      for (const permission of installationOnly) expect(can(companyAdmin, permission)).toBe(false);
    });

    it('is granted only the hints matching each individual code', () => {
      expect(can(companyAdminViewOnly, 'admin.roles.view')).toBe(true);
      expect(can(companyAdminViewOnly, 'admin.permissions.view')).toBe(true);
      expect(can(companyAdminViewOnly, 'admin.roles.create')).toBe(false);
      expect(can(companyAdminViewOnly, 'admin.roles.update')).toBe(false);
      expect(can(companyAdminViewOnly, 'admin.roles.assign')).toBe(false);
      expect(can(snapshot({ companyCodes: ['roles.assign'] }), 'admin.roles.view')).toBe(false);
    });

    it('treats a company admin WITHOUT role permissions like an ordinary member', () => {
      const adminWithoutRoles = snapshot({ companyCodes: ['users.list', 'users.membership.approve'] });
      for (const permission of [...installationOnly, ...roleHints]) expect(can(adminWithoutRoles, permission)).toBe(false);
    });
  });

  describe('ordinary company member', () => {
    it('is granted no admin hint', () => {
      for (const permission of [...installationOnly, ...roleHints]) expect(can(ordinaryMember, permission)).toBe(false);
    });
  });

  describe('fail-closed', () => {
    it.each(['loading', 'error'] as const)('grants no admin hint while permissions are %s, even for an administrator', (status) => {
      const pending = snapshot({ status, isInstallationAdministrator: true, companyCodes: ['roles.view'] });
      for (const permission of [...installationOnly, ...roleHints]) expect(can(pending, permission)).toBe(false);
    });

    it('grants nothing without any snapshot', () => {
      for (const permission of [...installationOnly, ...roleHints, 'account.view'] as const) {
        expect(can(null, permission)).toBe(false);
        expect(can(undefined, permission)).toBe(false);
      }
    });

    it('denies everything by default', () => {
      for (const permission of [...installationOnly, ...roleHints] as const) expect(can(deniedAccess, permission)).toBe(false);
    });

    it('does not trust a stale snapshot for another company', () => {
      const stale = snapshot({ companyCodes: ['roles.view'], companyId: 'old' });
      expect(can(stale, 'admin.roles.view', 'new')).toBe(false);
      expect(can(stale, 'admin.roles.view', 'OLD')).toBe(true);
    });
  });

  it('always allows account.view to a signed-in user, even while permissions load', () => {
    expect(can(snapshot({ status: 'loading' }), 'account.view')).toBe(true);
    expect(can(snapshot({ status: 'error' }), 'account.view')).toBe(true);
    expect(can(deniedAccess, 'account.view')).toBe(true);
  });

  it('exposes a stable can() through the hook and follows the snapshot', () => {
    const { result, rerender } = renderHook(({ access }) => usePermissions(access), {
      initialProps: { access: ordinaryMember },
    });
    const first = result.current;
    expect(first.can('admin.roles.view')).toBe(false);
    rerender({ access: ordinaryMember });
    expect(result.current).toBe(first);
    rerender({ access: companyAdmin });
    expect(result.current.can('admin.roles.view')).toBe(true);
  });
});

describe('HR hints (company-scoped list codes)', () => {
  const ready = (codes: string[], overrides: Partial<AccessSnapshot> = {}): AccessSnapshot => ({
    status: 'ready',
    isInstallationAdministrator: false,
    companyId: 'a',
    companyCodes: codes,
    ...overrides,
  });

  it.each([
    ['hr.units.view', 'hr.organizational-units.list'],
    ['hr.positions.view', 'hr.positions.list'],
    ['hr.employees.view', 'hr.employees.list'],
  ] as const)('%s is granted only by %s', (permission, code) => {
    expect(can(ready([code]), permission)).toBe(true);
    expect(can(ready([]), permission)).toBe(false);
    expect(can(ready(['hr.employees.view', 'hr.positions.create'].filter((c) => c !== code)), permission)).toBe(false);
  });

  it('does not let one HR family unlock another', () => {
    const access = ready(['hr.employees.list']);
    expect(can(access, 'hr.units.view')).toBe(false);
    expect(can(access, 'hr.positions.view')).toBe(false);
  });

  it('grants every HR hint to a ready installation administrator, like the server', () => {
    const access = ready([], { isInstallationAdministrator: true });
    expect(can(access, 'hr.units.view') && can(access, 'hr.positions.view') && can(access, 'hr.employees.view')).toBe(true);
  });

  it('grants nothing while loading, after an error, or for another company', () => {
    expect(can({ ...deniedAccess }, 'hr.employees.view')).toBe(false);
    expect(can({ ...deniedAccess, status: 'error' }, 'hr.employees.view')).toBe(false);
    expect(can(ready(['hr.employees.list']), 'hr.employees.view', 'b')).toBe(false);
    expect(can(ready(['hr.employees.list']), 'hr.employees.view', 'A')).toBe(true);
  });
});
