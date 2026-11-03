import type { PermissionCatalogItem } from '../api/types';

export function perm(code: string, name: string, description: string | null = null): PermissionCatalogItem {
  return { id: `id-${code}`, code, name, description, subsystemCode: code.split('.')[0] ?? code };
}

export const catalogItems: PermissionCatalogItem[] = [
  perm('admin.roles.manage', 'مدیریت گروه‌های دسترسی', 'ایجاد و ویرایش گروه‌ها'),
  perm('admin.roles.view', 'مشاهدهٔ گروه‌های دسترسی'),
  perm('admin.users.create', 'ایجاد کاربر'),
  perm('admin.users.update', 'ویرایش کاربر'),
  perm('admin.users.view', 'مشاهدهٔ کاربران', 'فهرست و جزئیات کاربران'),
  perm('trade.orders.approve', 'تأیید سفارش'),
  perm('trade.orders.create', 'ایجاد سفارش'),
  perm('trade.orders.delete', 'حذف سفارش', 'حذف سفارش پیش‌نویس'),
  perm('trade.orders.view', 'مشاهدهٔ سفارش‌ها'),
  perm('trade.reports.company.view', 'گزارش شرکتی'),
];

export const templates = [
  { code: 'trade.viewer', name: 'بازرگانی — مشاهده', permissionCodes: ['trade.orders.view', 'trade.reports.company.view'] },
  { code: 'trade.manager', name: 'بازرگانی — مدیر', permissionCodes: ['trade.orders.approve', 'trade.orders.create', 'trade.orders.delete', 'trade.orders.view'] },
];

export function roleItem(overrides: Record<string, unknown> = {}) {
  return {
    id: 'role-1',
    companyId: 'a',
    code: 'SALES',
    name: 'کارشناس فروش',
    isSystemRole: false,
    isActive: true,
    permissionCount: 2,
    activeMemberCount: 0,
    permissionCodes: ['trade.orders.view', 'trade.orders.create'],
    ...overrides,
  };
}

export const systemRole = roleItem({
  id: 'role-sys',
  code: 'ADMIN',
  name: 'مدیر شرکت',
  isSystemRole: true,
  permissionCount: 1,
  activeMemberCount: 3,
  permissionCodes: ['admin.users.view'],
});

export const companiesBody = [
  { id: 'a', name: 'شرکت الف', nationalId: '10101010101', roleName: 'مدیر' },
  { id: 'b', name: 'شرکت ب', nationalId: null, roleName: 'مدیر' },
];
