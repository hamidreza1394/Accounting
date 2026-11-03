export function auditItem(n: number, overrides: Record<string, unknown> = {}) {
  return {
    id: `evt-${n}`,
    userId: `user-${n}`,
    actorUsername: `actor${n}`,
    actorDisplayName: `کاربر ${n}`,
    companyId: `company-${n}`,
    companyName: `شرکت ${n}`,
    eventType: 'CompanyRoleUpdated',
    entityType: 'CompanyRole',
    entityId: `role-${n}`,
    occurredAtUtc: '2026-11-03T10:00:00Z',
    correlationId: `00-corr-${n}`,
    ...overrides,
  };
}

export const auditPage = (items: unknown[], overrides: Record<string, unknown> = {}) => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 25,
  ...overrides,
});
