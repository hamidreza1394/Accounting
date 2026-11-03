import { renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { can, usePermissions } from './permissions';

describe('permissions (UI hint)', () => {
  it('grants admin.* only to administrators', () => {
    expect(can({ isAdministrator: true }, 'admin.users.view')).toBe(true);
    expect(can({ isAdministrator: true }, 'admin.users.update')).toBe(true);
    expect(can({ isAdministrator: false }, 'admin.users.view')).toBe(false);
    expect(can({ isAdministrator: false }, 'admin.users.create')).toBe(false);
  });

  it('always grants account.view to a signed-in user, never without a session', () => {
    expect(can({ isAdministrator: false }, 'account.view')).toBe(true);
    expect(can(null, 'account.view')).toBe(false);
    expect(can(undefined, 'admin.users.view')).toBe(false);
  });

  it('exposes a stable can() through the hook and follows the session', () => {
    const { result, rerender } = renderHook(({ admin }) => usePermissions({ isAdministrator: admin }), {
      initialProps: { admin: false },
    });
    const first = result.current;
    expect(first.can('admin.users.view')).toBe(false);
    rerender({ admin: false });
    expect(result.current).toBe(first);
    rerender({ admin: true });
    expect(result.current.can('admin.users.view')).toBe(true);
  });
});
