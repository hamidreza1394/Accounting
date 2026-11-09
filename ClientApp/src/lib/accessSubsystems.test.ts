import { describe, expect, it } from 'vitest';
import type { AccessSubsystem } from '../api/types';
import { perm } from '../test/roles';
import {
  buildFormTable,
  buildSubsystemEntries,
  disableSubsystem,
  enableSubsystem,
  heldCodes,
  isSubsystemEnabled,
  readCodesOf,
  subsystemCodeOfPermission,
  verbOf,
} from './accessSubsystems';

const subsystem = (code: string, title: string): AccessSubsystem => ({ code, title, entitlementScope: 'none', entitlementLinked: false, entitlementCode: null });
const permissions = [
  perm('hr.employees.list', 'فهرست کارکنان'),
  perm('hr.employees.view', 'مشاهده کارمند'),
  perm('hr.employees.create', 'ایجاد کارمند'),
  perm('hr.assignments.manage', 'مدیریت انتساب'),
  perm('roles.view', 'مشاهده نقش‌ها'),
  perm('trade.form.party.view', 'مشاهده طرف حساب'),
  perm('trade.reports.company.view', 'گزارش شرکتی'),
  perm('trade.form.party.export', 'خروجی طرف حساب'),
];

describe('accessSubsystems', () => {
  it('maps real permission prefixes to catalog subsystems and keeps an unknown prefix as itself', () => {
    expect(subsystemCodeOfPermission('hr.employees.list')).toBe('hr');
    expect(subsystemCodeOfPermission('roles.view')).toBe('access-admin');
    expect(subsystemCodeOfPermission('users.list')).toBe('membership');
    expect(subsystemCodeOfPermission('trade.form.party.view')).toBe('TRADE_IMPORT');
    expect(subsystemCodeOfPermission('consolidation.adjustments.approve')).toBe('corporate-groups');
    expect(subsystemCodeOfPermission('mystery.thing.view')).toBe('mystery');
  });

  it('lists catalog subsystems without permission codes honestly empty and never invents codes for them', () => {
    const entries = buildSubsystemEntries([subsystem('hr', 'منابع انسانی'), subsystem('finance', 'حسابداری')], permissions);
    expect(entries.find((entry) => entry.code === 'finance')?.permissions).toEqual([]);
    expect(entries.find((entry) => entry.code === 'hr')?.permissions.map((item) => item.code)).toEqual([
      'hr.assignments.manage',
      'hr.employees.create',
      'hr.employees.list',
      'hr.employees.view',
    ]);
    // Without the catalog (403) only subsystems that really have codes are listed, titled by the raw code.
    const limited = buildSubsystemEntries(null, permissions);
    expect(limited.map((entry) => entry.code).sort()).toEqual(['TRADE_IMPORT', 'access-admin', 'hr']);
    expect(limited.every((entry) => entry.title === entry.code)).toBe(true);
  });

  it('derives enabled strictly from held codes, disables by removing only that subsystem and enables with read codes', () => {
    const entries = buildSubsystemEntries([subsystem('hr', 'منابع انسانی'), subsystem('access-admin', 'دسترسی')], permissions);
    const hr = entries.find((entry) => entry.code === 'hr')!;
    const selection = ['hr.employees.create', 'roles.view', 'legacy.unknown.code'];
    expect(isSubsystemEnabled(hr, new Set(selection))).toBe(true);
    expect(isSubsystemEnabled(hr, new Set(['roles.view']))).toBe(false);
    expect(heldCodes(hr, new Set(selection))).toEqual(['hr.employees.create']);
    expect(disableSubsystem(selection, hr)).toEqual(['roles.view', 'legacy.unknown.code']);
    expect(readCodesOf(hr)).toEqual(['hr.employees.list', 'hr.employees.view']);
    expect(enableSubsystem(['roles.view'], hr).sort()).toEqual(['hr.employees.list', 'hr.employees.view', 'roles.view']);
  });

  it('builds the form table from the real codes only (rows = resources, columns = operations that exist)', () => {
    const trade = permissions.filter((item) => item.code.startsWith('trade.'));
    const table = buildFormTable(trade);
    expect(table.verbs).toEqual(['view', 'export']);
    expect(table.rows.map((row) => row.resource)).toEqual(['form.party', 'reports.company']);
    expect(table.rows[0]?.cells['view']?.[0]?.code).toBe('trade.form.party.view');
    expect(table.rows[1]?.cells['export']).toBeUndefined();
    expect(verbOf('hr.assignments.manage')).toBe('manage');
    expect(verbOf('hr.employees.user-link.manage')).toBe('manage');
    expect(verbOf('thing.frobnicate')).toBe('other');
  });
});
