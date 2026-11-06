import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { json, listBody, stubApi, userItem, type ApiCall } from '../../../test/adminUsers';
import { adminCompanyItem, adminCompanyPage, auditEventTypesBody, auditItem, auditPage, csvBytes } from '../../../test/securityAudit';
import { SecurityAuditPage } from './SecurityAuditPage';

const onUnauthenticated = vi.fn((): boolean => false);
const auditPath = '/api/v1/admin/security/audit-events';
const exportPath = '/api/v1/admin/security/audit-events/export';
const eventTypesPath = '/api/v1/admin/security/audit-event-types';
const companiesPath = '/api/v1/admin/companies';

const defaultAudit = () =>
  json(
    auditPage([
      auditItem(1),
      auditItem(2, { eventType: 'BrandNewEvent', userId: null, actorUsername: null, actorDisplayName: null, companyId: null, companyName: null, entityType: null, entityId: null, correlationId: null }),
    ]),
  );

type Handlers = {
  audit?: (url: URL) => Response | Promise<Response>;
  export?: () => Response | Promise<Response>;
  eventTypes?: () => Response | Promise<Response>;
  companies?: (url: URL) => Response | Promise<Response>;
  users?: (url: URL) => Response;
};

function api({ audit = defaultAudit, export: exportHandler, eventTypes, companies, users }: Handlers = {}) {
  const auditUrls: URL[] = [];
  const userUrls: URL[] = [];
  const companyUrls: URL[] = [];
  const eventTypesUrls: URL[] = [];
  const exportCalls: ApiCall[] = [];
  const stub = stubApi((call: ApiCall) => {
    // `call.path` has no query string; the filters are read from the request URL of this very call.
    const url = new URL(String(stub.mock.mock.calls.at(-1)?.[0]), 'http://localhost');
    if (call.path === auditPath) {
      auditUrls.push(url);
      return audit(url);
    }
    if (call.path === exportPath) {
      exportCalls.push(call);
      return exportHandler ? exportHandler() : csvBytes('security-audit-events-20261103-010203.csv');
    }
    if (call.path === eventTypesPath) {
      eventTypesUrls.push(url);
      return eventTypes ? eventTypes() : json(auditEventTypesBody);
    }
    if (call.path === companiesPath) {
      companyUrls.push(url);
      return companies ? companies(url) : json(adminCompanyPage([adminCompanyItem('a', 'شرکت الف'), adminCompanyItem('b', 'شرکت ب'), adminCompanyItem('c', 'شرکت قدیمی', false)]));
    }
    if (call.path === '/api/v1/admin/users') {
      userUrls.push(url);
      return users ? users(url) : json(listBody([userItem(1), userItem(2)]));
    }
    return new Response(null, { status: 404 });
  });
  return { ...stub, auditUrls, userUrls, companyUrls, eventTypesUrls, exportCalls };
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

/** The `option[value]` of the event-type suggestions (datalist options have no accessible role). */
const datalistOption = (container: HTMLElement, value: string) =>
  container.querySelector(`datalist option[value="${value}"]`);

describe('SecurityAuditPage list', () => {
  it('lists the events with Persian labels, the raw code, actor, company and entity, and no IP or payload column', async () => {
    const { auditUrls } = api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'گزارش امنیت و سوابق' })).toBeInTheDocument();
    const row = (await screen.findByText('ویرایش نقش شرکت')).closest('tr') as HTMLElement;
    expect(within(row).getByText('CompanyRoleUpdated')).toHaveAttribute('dir', 'ltr');
    expect(within(row).getByRole('button', { name: /^کاربر 1.*فقط رویدادهای این کاربر$/ })).toBeInTheDocument();
    expect(within(row).getByText('role-1')).toHaveAttribute('dir', 'ltr');
    // An event unknown to the client is still shown, by its code, with no actor or company.
    const unknown = screen.getByText('BrandNewEvent').closest('tr') as HTMLElement;
    expect(within(unknown).getByText('سیستم / ناشناس')).toBeInTheDocument();
    expect(auditUrls[0]?.search).toBe('?sort=occurredAt&descending=true&page=1&pageSize=25');
    expect(screen.queryByRole('columnheader', { name: /IP|نشانی|جزئیات/ })).toBeNull();
    expect(screen.getByText(/نبودِ یک رویداد/)).toBeInTheDocument();
  });

  it('shows real login and logout events with their Persian labels from the event-type list', async () => {
    api({
      audit: () =>
        json(
          auditPage([
            auditItem(1, { eventType: 'LoginSucceeded' }),
            auditItem(2, { eventType: 'LoginFailed' }),
            auditItem(3, { eventType: 'LogoutSucceeded' }),
            auditItem(4, { eventType: 'LoginDeniedInactive' }),
          ]),
        ),
    });
    setup();
    expect(await screen.findByText('ورود موفق')).toBeInTheDocument();
    expect(screen.getByText('ورود ناموفق')).toBeInTheDocument();
    expect(screen.getByText('خروج موفق')).toBeInTheDocument();
    expect(screen.getByText('رد ورود حساب غیرفعال')).toBeInTheDocument();
    expect(screen.getByText('LoginSucceeded')).toHaveAttribute('dir', 'ltr');
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
    expect(await screen.findByText('ویرایش نقش شرکت')).toBeInTheDocument();
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
    await screen.findByText('ویرایش نقش شرکت');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(auditUrls).toHaveLength(3));
    expect(auditUrls.map((url) => url.searchParams.get('page'))).toEqual(['1', '2', '1']);
    expect(await screen.findByText('ویرایش نقش شرکت')).toBeInTheDocument();
  });

  it('pages with the chosen page size and goes back to page 1 when it changes', async () => {
    const user = userEvent.setup();
    const { auditUrls } = api({ audit: () => json(auditPage([auditItem(1)], { totalCount: 300 })) });
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('page')).toBe('2'));
    await user.selectOptions(screen.getByLabelText(/تعداد/), '50');
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('pageSize')).toBe('50'));
    expect(auditUrls.at(-1)?.searchParams.get('page')).toBe('1');
  });
});

describe('SecurityAuditPage sorting', () => {
  it('sorts on the server with the allowlisted key, marks the direction and returns to page 1', async () => {
    const user = userEvent.setup();
    const { auditUrls } = api({ audit: () => json(auditPage([auditItem(1)], { totalCount: 60 })) });
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    expect(auditUrls[0]?.search).toBe('?sort=occurredAt&descending=true&page=1&pageSize=25');
    expect(screen.getByRole('columnheader', { name: /زمان/ })).toHaveAttribute('aria-sort', 'descending');

    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('page')).toBe('2'));

    // Newest first is the default; clicking the time header asks the server for ascending order.
    await user.click(screen.getByRole('button', { name: 'زمان' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('descending')).toBe('false'));
    expect(auditUrls.at(-1)?.searchParams.get('sort')).toBe('occurredAt');
    expect(auditUrls.at(-1)?.searchParams.get('page')).toBe('1');
    expect(screen.getByRole('columnheader', { name: /زمان/ })).toHaveAttribute('aria-sort', 'ascending');

    await user.click(screen.getByRole('button', { name: 'رویداد' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('sort')).toBe('eventType'));
    expect(auditUrls.at(-1)?.searchParams.get('descending')).toBe('false');
    expect(screen.getByRole('columnheader', { name: /رویداد/ })).toHaveAttribute('aria-sort', 'ascending');

    await user.click(screen.getByRole('button', { name: 'رویداد' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('descending')).toBe('true'));
  });

  it('never lets the late answer of an older request overwrite the result of a new sort', async () => {
    const user = userEvent.setup();
    const release: ((response: Response) => void)[] = [];
    api({
      audit: () => new Promise<Response>((resolve) => release.push(resolve)),
    });
    setup();
    await waitFor(() => expect(release).toHaveLength(1));
    release[0]!(json(auditPage([auditItem(1)])));
    await screen.findByText('ویرایش نقش شرکت');

    // The sort change aborts the request of the previous order; its answer may arrive late.
    await user.click(screen.getByRole('button', { name: 'رویداد' }));
    await waitFor(() => expect(release).toHaveLength(2));
    release[1]!(json(auditPage([auditItem(7, { eventType: 'NewSortEvent' })], { totalCount: 1 })));
    expect(await screen.findByText('NewSortEvent')).toBeInTheDocument();
    release[0]!(json(auditPage([auditItem(1, { eventType: 'StaleSortEvent' })], { totalCount: 1 })));
    expect(screen.queryByText('StaleSortEvent')).not.toBeInTheDocument();
    expect(screen.getByText('NewSortEvent')).toBeInTheDocument();
  });
});

describe('SecurityAuditPage event types', () => {
  it('spends no sensitive-endpoint budget on page load; both registries load only on interaction', async () => {
    const user = userEvent.setup();
    const { companyUrls, eventTypesUrls } = api();
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    // The audit page shares the sensitive rate-limit budget with the rest of the installation;
    // an untouched page must not spend it (regression: the e2e run hit a 429 on page load).
    expect(eventTypesUrls).toHaveLength(0);
    expect(companyUrls).toHaveLength(0);

    await user.click(screen.getByLabelText('نوع رویداد'));
    await waitFor(() => expect(eventTypesUrls).toHaveLength(1));
    await user.click(screen.getByRole('combobox', { name: 'شرکت' }));
    await waitFor(() => expect(companyUrls).toHaveLength(1));
    expect(eventTypesUrls).toHaveLength(1);
  });

  it('fills the suggestions from the server list, unknown codes included, static-only codes not', async () => {
    const user = userEvent.setup();
    api();
    const { container } = setup();
    await screen.findByText('ویرایش نقش شرکت');
    await user.click(screen.getByLabelText('نوع رویداد'));
    await waitFor(() => expect(datalistOption(container, 'BrandNewEvent')).not.toBeNull());
    // A code that only the static fallback knows disappears once the server list has arrived.
    expect(datalistOption(container, 'WorkContextSelected')).toBeNull();
  });

  it('keeps the report usable when the event-type list fails and retries into the server list', async () => {
    const user = userEvent.setup();
    let fail = true;
    api({ eventTypes: () => (fail ? new Response(null, { status: 500 }) : json(auditEventTypesBody)) });
    const { container } = setup();
    // The report itself still loads and shows rows; only the suggestions fall back.
    expect(await screen.findByText('ویرایش نقش شرکت')).toBeInTheDocument();
    await user.click(screen.getByLabelText('نوع رویداد'));
    expect(await screen.findByText('دریافت انواع رویداد انجام نشد؛ می‌توانید کد رویداد را دستی وارد کنید.')).toBeInTheDocument();
    expect(datalistOption(container, 'WorkContextSelected')).not.toBeNull();
    expect(datalistOption(container, 'BrandNewEvent')).toBeNull();

    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره برای انواع رویداد' }));
    await waitFor(() => expect(screen.queryByText(/دریافت انواع رویداد انجام نشد/)).not.toBeInTheDocument());
    expect(datalistOption(container, 'BrandNewEvent')).not.toBeNull();
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
    await screen.findByText('ویرایش نقش شرکت');

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
    await screen.findByText('ویرایش نقش شرکت');
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
    await screen.findByText('ویرایش نقش شرکت');
    await user.type(screen.getByRole('combobox', { name: /کاربر/ }), 'user');
    await user.click(await screen.findByRole('option', { name: /نام2 خانوادگی2/ }));
    expect(userUrls.at(-1)?.searchParams.get('search')).toBe('user');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('userId')).toBe('user-2'));
    const chips = screen.getByRole('list', { name: 'فیلترهای فعال' });
    await user.click(within(chips).getByRole('button', { name: 'حذف فیلتر کاربر نام2 خانوادگی2' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.has('userId')).toBe(false));
  });

  it('filters by any company of the installation through the server-searched picker, inactive ones included', async () => {
    const user = userEvent.setup();
    const { auditUrls, companyUrls } = api();
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    expect(screen.getByText(/همهٔ شرکت‌های نصب/)).toBeInTheDocument();

    const combo = screen.getByRole('combobox', { name: 'شرکت' });
    await user.click(combo);
    await screen.findByRole('option', { name: 'شرکت ب' });
    // The list comes from the installation-wide admin endpoint, not from the user's memberships.
    expect(companyUrls.at(-1)?.pathname).toBe(companiesPath);
    expect(companyUrls.at(-1)?.searchParams.get('pageSize')).toBe('20');
    expect(screen.getByRole('option', { name: /شرکت قدیمی/ })).toHaveTextContent('(غیرفعال)');

    await user.type(combo, 'ب');
    await waitFor(() => expect(companyUrls.at(-1)?.searchParams.get('search')).toBe('ب'));
    await user.click(screen.getByRole('option', { name: 'شرکت ب' }));
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('companyId')).toBe('b'));

    const chips = screen.getByRole('list', { name: 'فیلترهای فعال' });
    await user.click(within(chips).getByRole('button', { name: 'حذف فیلتر شرکت شرکت ب' }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.has('companyId')).toBe(false));
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

  it('shows a company search error inside the picker and keeps the report usable', async () => {
    const user = userEvent.setup();
    let fail = true;
    api({ companies: () => (fail ? new Response(null, { status: 500 }) : json(adminCompanyPage([adminCompanyItem('b', 'شرکت ب')]))) });
    setup();
    // The report itself is unaffected by a failing company list.
    expect(await screen.findByText('ویرایش نقش شرکت')).toBeInTheDocument();
    const combo = screen.getByRole('combobox', { name: 'شرکت' });
    await user.click(combo);
    expect(await screen.findByText('جستجوی شرکت‌ها انجام نشد.')).toBeInTheDocument();
    fail = false;
    await user.type(combo, 'ب');
    expect(await screen.findByRole('option', { name: 'شرکت ب' })).toBeInTheDocument();
  });
});

describe('SecurityAuditPage CSV export', () => {
  const originalCreate = URL.createObjectURL;
  const originalRevoke = URL.revokeObjectURL;

  beforeEach(() => {
    (URL as unknown as Record<string, unknown>).createObjectURL = vi.fn(() => 'blob:download-1');
    (URL as unknown as Record<string, unknown>).revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  });
  afterEach(() => {
    (URL as unknown as Record<string, unknown>).createObjectURL = originalCreate;
    (URL as unknown as Record<string, unknown>).revokeObjectURL = originalRevoke;
    vi.restoreAllMocks();
  });

  /** Applies a 6-day Jalali range (۱۰..۱۵ آبان ۱۴۰۵) and waits for the filtered request. */
  const applyRange = async (user: ReturnType<typeof userEvent.setup>, auditUrls: URL[]) => {
    vi.stubEnv('TZ', 'Asia/Tehran');
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-03T09:00:00'));
    await user.click(screen.getByRole('button', { name: /از تاریخ/ }));
    await user.click(await dayButton('۱۰'));
    await user.click(screen.getByRole('button', { name: /تا تاریخ/ }));
    await user.click(await dayButton('۱۵'));
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(auditUrls.length).toBeGreaterThan(1));
  };

  it('exports with the applied filters as a POST body and downloads the server file name', async () => {
    const user = userEvent.setup();
    const { auditUrls, exportCalls } = api();
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    await applyRange(user, auditUrls);

    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    await waitFor(() => expect(exportCalls).toHaveLength(1));
    expect(exportCalls[0]?.body).toEqual({ fromUtc: '2026-10-31T20:30:00.000Z', toUtc: '2026-11-06T20:29:59.999Z' });
    expect(exportCalls[0]?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
    const anchor = (HTMLAnchorElement.prototype.click as unknown as { mock: { instances: (HTMLAnchorElement | undefined)[] } }).mock.instances[0];
    expect(anchor?.download).toBe('security-audit-events-20261103-010203.csv');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:download-1');
    expect(screen.queryByRole('alert', { name: /خروجی/ })).not.toBeInTheDocument();
  });

  it('includes the applied event type, user and company in the export body', async () => {
    const user = userEvent.setup();
    const { auditUrls, exportCalls } = api();
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    await applyRange(user, auditUrls);
    await user.click(await screen.findByRole('button', { name: /^شرکت 1.*فقط رویدادهای این شرکت$/ }));
    await waitFor(() => expect(auditUrls.at(-1)?.searchParams.get('companyId')).toBe('company-1'));

    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    await waitFor(() => expect(exportCalls).toHaveLength(1));
    expect(exportCalls[0]?.body).toEqual({ companyId: 'company-1', fromUtc: '2026-10-31T20:30:00.000Z', toUtc: '2026-11-06T20:29:59.999Z' });
  });

  it('refuses to export without an applied time range and sends nothing', async () => {
    const user = userEvent.setup();
    const { exportCalls } = api();
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    expect(await screen.findByText('برای خروجی CSV ابتدا بازهٔ زمانی (از تاریخ و تا تاریخ) را اعمال کنید.')).toBeInTheDocument();
    expect(exportCalls).toHaveLength(0);
  });

  it('shows loading and blocks a second request while the export is being prepared', async () => {
    const user = userEvent.setup();
    let release: ((response: Response) => void) | null = null;
    const { auditUrls, exportCalls } = api({
      export: () =>
        new Promise<Response>((resolve) => {
          release = (response) => resolve(response);
          return undefined as unknown as Response;
        }),
    });
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    await applyRange(user, auditUrls);

    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    const busy = await screen.findByRole('button', { name: 'در حال آماده‌سازی خروجی…' });
    expect(busy).toBeDisabled();
    expect(exportCalls).toHaveLength(1);
    release!(csvBytes('late.csv'));
    expect(await screen.findByRole('button', { name: 'خروجی CSV' })).toBeEnabled();
    expect(exportCalls).toHaveLength(1);
  });

  it('surfaces export errors (403, over the row cap, bad range) and keeps the filters', async () => {
    const user = userEvent.setup();
    let status = 403;
    const { auditUrls, exportCalls } = api({
      export: () => (status === 403 ? new Response(null, { status: 403 }) : status === 413 ? new Response(JSON.stringify({ error: 'تعداد رکوردهای خروجی بیش از حد مجاز است؛ بازه یا فیلترها را محدود کنید.' }), { status: 413, headers: { 'Content-Type': 'application/json' } }) : new Response(JSON.stringify({ error: 'برای خروجی، بازه زمانی معتبر حداکثر ۳۱ روز و فیلترهای معتبر الزامی است.' }), { status: 400, headers: { 'Content-Type': 'application/json' } })),
    });
    setup();
    await screen.findByText('ویرایش نقش شرکت');
    await applyRange(user, auditUrls);

    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    expect(await screen.findByText('خروجی گزارش امنیتی فقط برای مدیر نصب در دسترس است.')).toBeInTheDocument();
    // The filters of the report are untouched and a retry is possible.
    expect(auditUrls.at(-1)?.searchParams.get('fromUtc')).toBe('2026-10-31T20:30:00.000Z');

    status = 413;
    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    expect(await screen.findByText(/بیش از حد مجاز/)).toBeInTheDocument();

    status = 400;
    await user.click(screen.getByRole('button', { name: 'خروجی CSV' }));
    expect(await screen.findByText('برای خروجی، بازه زمانی معتبر حداکثر ۳۱ روز و فیلترهای معتبر الزامی است.')).toBeInTheDocument();
    expect(exportCalls).toHaveLength(3);
  });
});
