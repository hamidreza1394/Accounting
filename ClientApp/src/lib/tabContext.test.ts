import { describe, expect, it } from 'vitest';
import { tabContextScope, tabDependsOnContext, tabKeyPathname } from './tabContext';

describe('workspace tab context dependency (Issue #444 stage 1 completion)', () => {
  it('classifies the real routes of the app', () => {
    expect(tabContextScope('/')).toBe('company-period');
    expect(tabContextScope('/admin/users')).toBe('global');
    expect(tabContextScope('/admin/users/u-1')).toBe('global');
    expect(tabContextScope('/admin/users/new')).toBe('global');
    expect(tabContextScope('/admin/permissions')).toBe('global');
    expect(tabContextScope('/admin/security/audit')).toBe('global');
    expect(tabContextScope('/account/membership-requests')).toBe('global');
    expect(tabContextScope('/admin/roles')).toBe('company');
    expect(tabContextScope('/admin/roles/r-1')).toBe('company');
    expect(tabContextScope('/admin/membership-requests')).toBe('company');
    expect(tabContextScope('/company/users')).toBe('company');
    expect(tabContextScope('/hr/org/units')).toBe('company');
    expect(tabContextScope('/hr/org/employees/e-1/edit')).toBe('company');
  });

  it('is fail-safe: an unknown page is context-dependent, never global', () => {
    expect(tabContextScope('/some/future/page')).toBe('company');
    expect(tabDependsOnContext('/some/future/page')).toBe(true);
  });

  it('ignores the query string of a tab key', () => {
    expect(tabKeyPathname('/admin/users?q=sara&page=2')).toBe('/admin/users');
    expect(tabDependsOnContext('/admin/users?q=sara&page=2')).toBe(false);
    expect(tabDependsOnContext('/hr/org/employees?search=علی')).toBe(true);
    expect(tabDependsOnContext('/')).toBe(true);
  });
});
