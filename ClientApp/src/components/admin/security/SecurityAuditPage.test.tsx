import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { json, listBody, stubApi, userItem, type ApiCall } from '../../../test/adminUsers';
import { auditItem, auditPage } from '../../../test/securityAudit';
import { companiesBody } from '../../../test/roles';
import { SecurityAuditPage } from './SecurityAuditPage';

const onUnauthenticated = vi.fn((): boolean => false);
const auditPath = '/api/v1/admin/security/audit-events';

const defaultAudit = () =>
  json(
    auditPage([
      auditItem(1),
      auditItem(2, { eventType: 'BrandNewEvent', userId: null, actorUsername: null, actorDisplayName: null, companyId: null, companyName: null, entityType: null, entityId: null, correlationId: null }),
    ]),
  );

type Handlers = {
  audit?: (url: URL) => Response;
  companies?: () => Response;
  users?: (url: URL) => Response;
};

function api({ audit = defaultAudit, companies, users }: Handlers = {}) {
  const auditUrls: URL[] = [];
  const userUrls: URL[] = [];
  const stub = stubApi((call: ApiCall) => {
    // `call.path` has no query string; the filters are read from the request URL of this very call.
    const url = new URL(String(stub.mock.mock.calls.at(-1)?.[0]), 'http://localhost');
    if (call.path === auditPath) {
      auditUrls.push(url);
      return audit(url);
    }
    if (call.path === '/api/v1/companies') return companies ? companies() : json(companiesBody);
    if (call.path === '/api/v1/admin/users') {
      userUrls.push(url);
      return users ? users(url) : json(listBody([userItem(1), userItem(2)]));
    }
    return new Response(null, { status: 404 });
  });
  return { ...stub, auditUrls, userUrls };
}

const setup = () => render(<SecurityAuditPage onUnauthenticated={onUnauthenticated} />);

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
});
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllEnvs();
});

/** A day button of the open calendar by Jalali day and month of 1405 (anchored: ۱ never matches ۲۱). */
const dayButton = (n: string, month = 'آبان') => screen.findByRole('button', { name: new RegExp(`\\s${n}-ام ${month} ۱۴۰۵`) });

describe('SecurityAuditPage list', () => {
  it('lists the events with Persian labels, the raw code, actor, company and entity, and no IP or payload column', async () => {
    const { auditUrls } = api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'گزارش امنیت و سوابق' })).toBeInTheDocument();
    const row = (await screen.findByText('ویرایش گروه دسترسی')).closest('tr') as HTMLElement;
    expect(within(row).getByText('CompanyRoleUpdated')).toHaveAttribute('dir', 'ltr');
    expect(within(row).getByRole('button', { name: /^کاربر 1.*فقط رویدادهای این کاربر$/ })).toBeInTheDocument();
    expect(within(row).getByText('role-1')).toHaveAttribute('dir', 'ltr');
    // An event unknown to the client is still shown, by its code, with no actor or company.
    const unknown = screen.getByText('BrandNewEvent').closest('tr') as HTMLElement;
    expect(within(unknown).getByText('سیستم / ناشناس')).toBeInTheDocument();
    expect(auditUrls[0]?.search).toBe('?page=1&pageSize=25');
    expect(screen.queryByRole('columnheader', { name: /IP|نشانی|جزئیات/ })).toBeNull();
    expect(screen.getByText(/نبودِ یک رویداد/)).toBeInTheDocument();
  });

  it('shows a no-access panel for a non-administrator (403) without a table', async () => {
    api({ audit: () => new Response(null, { status: 403 }) });
    setup();
    expect(await screen.findByText('گزارش امنیتی فقط برای مدیر نصب در دسترس است.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('explains an empty result differently with and without an active filter', async () => {
    const user = userEvent.setup();
    api({ audit: () => json(auditPage([])) });
    setup();
    expect(await screen.findByText('هنوز رویداد امنیتی‌ای ثبت نشده است.')).toBeInTheDocument();
    await user.type(screen.getByLabelText('نوع رویداد'), 'UserCreated');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    expect(await screen.findByText('با فیلترهای فعلی رویدادی ثبت نشده است.')).toBeInTheDocument();
  });

  it('shows an error with a retry, and hands a 401 to the unauthenticated flow', async () => {
    const user = userEvent.setup();
    let fail = true;
    api({ audit: () => (fail ? new Response(null, { status: 500 }) : defaultAudit()) });
    const first = setup();
    const retry = await screen.findByRole('button', { name: /تلاش/ });
    fail = false;
    await user.click(retry);
    expect(await screen.findByText('ویرایش گروه دسترسی')).toBeInTheDocument();
    first.unmount();

    onUnauthenticated.mockReturnValue(true);
    api({ audit: () => new Response(null, { status: 401 }) });
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });

  it('asks for page 1 after a stale page 2 came back empty, and finishes loading', async () => {
    const user = userEvent.setup();
    let call = 0;
    const bodies = [auditPage([auditItem(1)], { totalCount: 60 }), auditPage([], { page: 2, totalCount: 1 }), auditPage([auditItem(1)], { totalCount: 1 })];
    const { auditUrls } = api({ audit: () => json(bodies[Math.min(call++, bodies.length - 1)]) });
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(auditUrls).toHaveLength(3));
    expect(auditUrls.map((url) => url.searchParams.get('page'))).toEqual(['1', '2', '1']);
    expect(await screen.findByText('ویرایش گروه دسترسی')).toBeInTheDocument();
  });

  it('pages with the chosen page size and goes back to page 1 when it changes', async () => {
    const user = userEvent.setup();
    const { auditUrls } = api({ audit: () => json(auditPage([auditItem(1)], { totalCount: 300 })) });
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('page')).toBe('2'));
    await user.selectOptions(screen.getByLabelText(/تعداد/), '50');
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('pageSize')).toBe('50'));
    expect(auditUrls.at(-1)?.searchParams.get('page')).toBe('1');
  });
});

describe('SecurityAuditPage filters', () => {
  it('filters by event type and a Jalali date range, converted to the user-zone day boundaries in UTC', async () => {
    vi.stubEnv('TZ', 'Asia/Tehran');
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-03T09:00:00'));
    const user = userEvent.setup();
    const { auditUrls } = api();
    setup();
    await screen.findByText('ویرایش گروه دسترسی');

    await user.type(screen.getByLabelText('نوع رویداد'), 'PasswordChanged');
    await user.click(screen.getByRole('button', { name: /از تاریخ/ }));
    await user.click(await dayButton('۱۰')); // 10 Aban 1405 = 2026-11-01
    await user.click(screen.getByRole('button', { name: /تا تاریخ/ }));
    await user.click(await dayButton('۱۵')); // 15 Aban 1405 = 2026-11-06
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));

    await waitFor(() => expect(auditUrls.length).toBeGreaterThan(1));
    const last = auditUrls.at(-1) as URL;
    expect(last.searchParams.get('eventType')).toBe('PasswordChanged');
    expect(last.searchParams.get('fromUtc')).toBe('2026-10-31T20:30:00.000Z');
    expect(last.searchParams.get('toUtc')).toBe('2026-11-06T20:29:59.999Z');
    expect(last.searchParams.get('page')).toBe('1');
    // The chosen days are shown as Jalali dates.
    expect(screen.getByRole('button', { name: /از تاریخ/ })).toHaveAccessibleDescription(/۱۴۰۵\/۰۸\/۱۰/);
    expect(screen.getByRole('button', { name: /تا تاریخ/ })).toHaveAccessibleDescription(/۱۴۰۵\/۰۸\/۱۵/);
  });

  it('does not let the end date be chosen before the start date', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-03T09:00:00'));
    const user = userEvent.setup();
    api();
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.click(screen.getByRole('button', { name: /از تاریخ/ }));
    await user.click(await dayButton('۱۵'));
    await user.click(screen.getByRole('button', { name: /تا تاریخ/ }));
    expect(await dayButton('۱۰')).toBeDisabled();
    expect(await dayButton('۱۵')).toBeEnabled();
  });

  it('filters by a user found with the picker and shows it as a removable chip', async () => {
    const user = userEvent.setup();
    const { auditUrls, userUrls } = api();
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.type(screen.getByRole('combobox', { name: /کاربر/ }), 'user');
    await user.click(await screen.findByRole('option', { name: /نام2 خانوادگی2/ }));
    expect(userUrls.at(-1)?.searchParams.get('search')).toBe('user');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('userId')).toBe('user-2'));
    const chips = screen.getByRole('list', { name: 'فیلترهای فعال' });
    await user.click(within(chips).getByRole('button', { name: 'حذف فیلتر کاربر نام2 خانوادگی2' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.has('userId')).toBe(false));
  });

  it('filters by a company of the user list, and tells that only their own companies are listed', async () => {
    const user = userEvent.setup();
    const { auditUrls } = api();
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    expect(screen.getByText(/فقط شرکت‌هایی که خودتان عضو آن‌ها هستید/)).toBeInTheDocument();
    const select = screen.getByLabelText('شرکت');
    await within(select).findByRole('option', { name: 'شرکت ب' });
    await user.selectOptions(select, 'b');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('companyId')).toBe('b'));
  });

  it('filters by a user or company chosen in the table, and clears the chip', async () => {
    const user = userEvent.setup();
    const { auditUrls } = api();
    setup();
    await user.click(await screen.findByRole('button', { name: /^کاربر 1.*فقط رویدادهای این کاربر$/ }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('userId')).toBe('user-1'));
    const chips = screen.getByRole('list', { name: 'فیلترهای فعال' });
    await user.click(within(chips).getByRole('button', { name: 'حذف فیلتر کاربر کاربر 1' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.has('userId')).toBe(false));

    await user.click(await screen.findByRole('button', { name: /^شرکت 1.*فقط رویدادهای این شرکت$/ }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('companyId')).toBe('company-1'));
    await user.click(within(screen.getByRole('list', { name: 'فیلترهای فعال' })).getByRole('button', { name: 'حذف فیلتر شرکت شرکت 1' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.has('companyId')).toBe(false));
  });

  it('resets every filter including the date fields', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-03T09:00:00'));
    const user = userEvent.setup();
    const { auditUrls } = api();
    setup();
    await user.type(await screen.findByLabelText('نوع رویداد'), 'UserCreated');
    await user.click(screen.getByRole('button', { name: /از تاریخ/ }));
    await user.click(await dayButton('۱۰'));
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('eventType')).toBe('UserCreated'));
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن فیلترها' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.has('eventType')).toBe(false));
    expect(auditUrls.at(-1)?.searchParams.has('fromUtc')).toBe(false);
    expect(screen.getByLabelText('نوع رویداد')).toHaveValue('');
    expect(screen.getByRole('button', { name: /از تاریخ/ })).toHaveTextContent('انتخاب تاریخ');
  });

  it('shows a company list error with a retry and keeps the report usable', async () => {
    const user = userEvent.setup();
    let fail = true;
    api({ companies: () => (fail ? new Response(null, { status: 500 }) : json(companiesBody)) });
    setup();
    expect(await screen.findByText('ویرایش گروه دسترسی')).toBeInTheDocument();
    const retry = await screen.findByRole('button', { name: 'تلاش دوباره برای فهرست شرکت‌ها' });
    fail = false;
    await user.click(retry);
    await within(screen.getByLabelText('شرکت')).findByRole('option', { name: 'شرکت ب' });
  });
});
