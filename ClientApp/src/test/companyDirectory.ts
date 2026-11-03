export function directoryUser(n: number, overrides: Record<string, unknown> = {}) {
  return {
    userId: `user-${n}`,
    username: `member${n}`,
    firstName: `نام${n}`,
    lastName: `خانوادگی${n}`,
    userIsActive: true,
    membershipIsActive: true,
    roleId: 'role-1',
    roleCode: 'ACCOUNTANT',
    roleName: 'حسابدار',
    roleIsActive: true,
    membershipCreatedAtUtc: '2026-10-01T09:00:00Z',
    ...overrides,
  };
}

export const directoryPage = (items: unknown[], overrides: Record<string, unknown> = {}) => ({
  companyId: 'a',
  companyName: 'شرکت الف',
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 25,
  ...overrides,
});
