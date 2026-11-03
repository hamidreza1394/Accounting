import { vi } from 'vitest';

export function userItem(n: number, overrides: Record<string, unknown> = {}) {
  return {
    id: `user-${n}`,
    username: `user${n}`,
    firstName: `نام${n}`,
    lastName: `خانوادگی${n}`,
    emailAddress: `user${n}@example.test`,
    cellPhoneNumber: n % 2 === 0 ? `0912000000${n}` : null,
    isActive: n % 3 !== 0,
    lastLoginDateTime: n === 1 ? null : '2026-11-02T08:00:00Z',
    insertDateTime: '2026-10-01T09:00:00Z',
    activeCompanyCount: n,
    ...overrides,
  };
}

export function listBody(items: unknown[], overrides: Record<string, unknown> = {}) {
  return { items, totalCount: items.length, page: 1, pageSize: 20, ...overrides };
}

export function userDetails(n: number, overrides: Record<string, unknown> = {}) {
  return {
    ...userItem(n),
    isEmailAddressVerified: true,
    isCellPhoneNumberVerified: false,
    updateDateTime: '2026-11-01T09:00:00Z',
    legacyRoleName: 'Accountant',
    memberships: [
      { companyId: 'c1', companyName: 'شرکت نمونه', roleCode: 'ACCOUNTANT', roleName: 'حسابدار', isActive: true, createdAtUtc: '2026-10-01T09:00:00Z' },
      { companyId: 'c2', companyName: 'شرکت دوم', roleCode: 'VIEWER', roleName: 'مشاهده‌گر', isActive: false, createdAtUtc: '2026-10-05T09:00:00Z' },
    ],
    ...overrides,
  };
}

export const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

/** Stubs fetch with a handler that receives the parsed URL; the calls are returned for assertions. */
export function stubFetch(handler: (url: URL) => Response | Promise<Response>) {
  const mock = vi.fn((input: string) => Promise.resolve(handler(new URL(input, 'http://localhost'))));
  vi.stubGlobal('fetch', mock);
  return mock;
}

export const listCalls = (mock: ReturnType<typeof stubFetch>): URL[] =>
  mock.mock.calls.map((call) => new URL(String(call[0]), 'http://localhost')).filter((url) => url.pathname === '/api/v1/admin/users');
