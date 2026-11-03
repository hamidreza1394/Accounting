export function memberItem(n: number, overrides: Record<string, unknown> = {}) {
  return {
    userId: `user-${n}`,
    username: `member${n}`,
    firstName: `نام${n}`,
    lastName: `خانوادگی${n}`,
    isUserActive: true,
    memberSinceUtc: '2026-10-01T09:00:00Z',
    ...overrides,
  };
}

export const memberPage = (items: unknown[], overrides: Record<string, unknown> = {}) => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 20,
  ...overrides,
});
