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

/** One company of `GET /admin/companies` (the installation-wide list of the security report). */
export const adminCompanyItem = (id: string, name: string, isActive = true) => ({ id, name, isActive });

export const adminCompanyPage = (items: unknown[], overrides: Record<string, unknown> = {}) => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 20,
  ...overrides,
});

/**
 * The server's event-type registry shape (`GET /admin/security/audit-event-types`): registry codes
 * with their Persian label, unioned with observed codes whose label falls back to the code itself.
 */
export const auditEventTypesBody = [
  { code: 'CompanyRoleUpdated', label: 'ویرایش نقش شرکت' },
  { code: 'LoginSucceeded', label: 'ورود موفق' },
  { code: 'LogoutSucceeded', label: 'خروج موفق' },
  { code: 'BrandNewEvent', label: 'BrandNewEvent' },
];

export const csvBytes = (name: string, status = 200) =>
  new Response(new Blob(['a,b\r\n1,2'], { type: 'text/csv' }), {
    status,
    headers: { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': `attachment; filename="${name}"` },
  });
