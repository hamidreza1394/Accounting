import { describe, expect, it } from 'vitest';
import { catalogItems } from '../test/roles';
import {
  buildPermissionGroups,
  checkStateOf,
  diffCodes,
  filterPermissions,
  operationOf,
  resourceOf,
  subsystemOf,
} from './permissionTree';

describe('permissionTree helpers (UI-derived)', () => {
  it('derives subsystem, resource and operation from the dotted code', () => {
    expect(subsystemOf('trade.reports.company.view')).toBe('trade');
    expect(resourceOf('trade.reports.company.view')).toBe('reports.company');
    expect(resourceOf('admin.users.view')).toBe('users');
    expect(resourceOf('trade.view')).toBe('');
    expect(operationOf('admin.users.view')).toBe('view');
    expect(operationOf('trade.orders.delete')).toBe('delete');
    expect(operationOf('trade.orders.approve')).toBeNull();
    expect(operationOf('view')).toBeNull();
  });

  it('groups by subsystem then resource in code order', () => {
    const groups = buildPermissionGroups(catalogItems);
    expect(groups.map((group) => [group.key, group.count])).toEqual([['admin', 5], ['trade', 5]]);
    expect(groups[1]?.resources.map((resource) => resource.resource)).toEqual(['orders', 'reports.company']);
    expect(groups[1]?.resources[0]?.items.map((item) => item.code)).toEqual([
      'trade.orders.approve',
      'trade.orders.create',
      'trade.orders.delete',
      'trade.orders.view',
    ]);
  });

  it('filters by code, name and description, with every word required', () => {
    expect(filterPermissions(catalogItems, 'orders delete').map((item) => item.code)).toEqual(['trade.orders.delete']);
    expect(filterPermissions(catalogItems, 'پیش‌نویس').map((item) => item.code)).toEqual(['trade.orders.delete']);
    expect(filterPermissions(catalogItems, '  ')).toBe(catalogItems);
    expect(filterPermissions(catalogItems, 'zzz')).toEqual([]);
  });

  it('computes the tri-state and the diff', () => {
    expect(checkStateOf(0, 3)).toBe('unchecked');
    expect(checkStateOf(1, 3)).toBe('mixed');
    expect(checkStateOf(3, 3)).toBe('checked');
    expect(checkStateOf(0, 0)).toBe('unchecked');
    expect(diffCodes(['b', 'a'], ['c', 'a'])).toEqual({ added: ['c'], removed: ['b'] });
  });
});
