export function myRequest(overrides: Record<string, unknown> = {}) {
  return {
    id: 'req-1',
    destinationCompanyId: 'c-dest',
    destinationCompanyName: 'شرکت مقصد',
    status: 'Pending',
    requestedAtUtc: '2026-11-01T08:00:00Z',
    decidedAtUtc: null,
    decisionReason: null,
    approvedRoleCode: null,
    approvedRoleName: null,
    ...overrides,
  };
}

export function pendingItem(n: number, overrides: Record<string, unknown> = {}) {
  return {
    id: `req-${n}`,
    userId: `user-${n}`,
    requesterDisplayName: `کاربر ${n}`,
    username: `user${n}`,
    requestedAtUtc: '2026-11-01T08:00:00Z',
    isRequesterActive: true,
    ...overrides,
  };
}

export const pageBody = (items: unknown[], overrides: Record<string, unknown> = {}) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
  ...overrides,
});
