import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { auditItem, auditPage } from '../../../test/securityAudit';
import { SecurityAuditPage, dayEndUtc, dayStartUtc } from './SecurityAuditPage';

const onUnauthenticated = vi.fn((): boolean => false);
const path = '/api/v1/admin/security/audit-events';

function api(handler: (url: URL) => Response = () => json(auditPage([auditItem(1), auditItem(2, { eventType: 'BrandNewEvent', userId: null, actorUsername: null, actorDisplayName: null, companyId: null, companyName: null, entityType: null, entityId: null, correlationId: null })]))) {
  const urls: URL[] = [];
  const stub = stubApi((call: ApiCall) => {
    if (call.path !== path) return new Response(null, { status: 404 });
    // `call.path` has no query string; the filters are read from the request URL of this call.
    const url = new URL(String(stub.mock.mock.calls.at(-1)?.[0]), 'http://localhost');
    urls.push(url);
    return handler(url);
  });
  return { ...stub, urls };
}

const setup = () => render(<SecurityAuditPage onUnauthenticated={onUnauthenticated} />);

describe('SecurityAuditPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists the events with Persian labels, the raw code, actor, company and entity', async () => {
    const { urls } = api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'گزارش امنیت و سوابق' })).toBeInTheDocument();
    const row = (await screen.findByText('ویرایش گروه دسترسی')).closest('tr') as HTMLElement;
    expect(within(row).getByText('CompanyRoleUpdated')).toHaveAttribute('dir', 'ltr');
    expect(within(row).getByRole('button', { name: 'فقط رویدادهای کاربر 1' })).toBeInTheDocument();
    expect(within(row).getByText('role-1')).toHaveAttribute('dir', 'ltr');
    // An event unknown to the client is still shown, by its code, with no actor or company.
    const unknown = screen.getByText('BrandNewEvent').closest('tr') as HTMLElement;
    expect(within(unknown).getByText('سیستم / ناشناس')).toBeInTheDocument();
    expect(urls[0]?.search).toBe('?page=1&pageSize=25');
    // The page never offers an IP address or payload column.
    expect(screen.queryByRole('columnheader', { name: /IP|نشانی|جزئیات/ })).toBeNull();
  });

  it('says that a missing event proves nothing about a session', async () => {
    api();
    setup();
    expect(await screen.findByText(/نبودِ یک رویداد/)).toBeInTheDocument();
  });

  it('applies the event type and date filters from page 1 and converts days to UTC instants', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.type(screen.getByLabelText('نوع رویداد'), 'PasswordChanged');
    await user.type(screen.getByLabelText('از تاریخ'), '2026-11-01');
    await user.type(screen.getByLabelText('تا تاریخ'), '2026-11-03');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(urls.length).toBeGreaterThan(1));
    const last = urls.at(-1) as URL;
    expect(last.searchParams.get('eventType')).toBe('PasswordChanged');
    expect(last.searchParams.get('fromUtc')).toBe(dayStartUtc('2026-11-01'));
    expect(last.searchParams.get('toUtc')).toBe(dayEndUtc('2026-11-03'));
    expect(last.searchParams.get('page')).toBe('1');
  });

  it('refuses a start date after the end date without calling the server', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.type(screen.getByLabelText('از تاریخ'), '2026-11-05');
    await user.type(screen.getByLabelText('تا تاریخ'), '2026-11-01');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    expect(await screen.findByText('تاریخ شروع نباید بعد از تاریخ پایان باشد.')).toBeInTheDocument();
    expect(urls).toHaveLength(1);
  });

  it('filters by a user or company chosen in the table, shows a removable chip, and clears it', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    setup();
    await user.click(await screen.findByRole('button', { name: 'فقط رویدادهای کاربر 1' }));
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('userId')).toBe('user-1'));
    const chips = screen.getByRole('list', { name: 'فیلترهای فعال' });
    await user.click(within(chips).getByRole('button', { name: 'حذف فیلتر کاربر کاربر 1' }));
    await waitFor(() => expect(urls.at(-1)?.searchParams.has('userId')).toBe(false));

    await user.click(await screen.findByRole('button', { name: 'فقط رویدادهای شرکت شرکت 1' }));
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('companyId')).toBe('company-1'));
  });

  it('resets every filter', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    setup();
    await user.type(await screen.findByLabelText('نوع رویداد'), 'UserCreated');
    await user.click(screen.getByRole('button', { name: 'اعمال فیلتر' }));
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('eventType')).toBe('UserCreated'));
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن فیلترها' }));
    await waitFor(() => expect(urls.at(-1)?.searchParams.has('eventType')).toBe(false));
    expect(screen.getByLabelText('نوع رویداد')).toHaveValue('');
  });

  it('shows a no-access panel for a non-administrator (403)', async () => {
    api(() => new Response(null, { status: 403 }));
    setup();
    expect(await screen.findByText('گزارش امنیتی فقط برای مدیر نصب در دسترس است.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('shows the empty state differently with and without filters', async () => {
    api(() => json(auditPage([])));
    setup();
    expect(await screen.findByText('هنوز رویداد امنیتی‌ای ثبت نشده است.')).toBeInTheDocument();
  });

  it('shows an error with a retry, and hands a 401 to the unauthenticated flow', async () => {
    const user = userEvent.setup();
    let fail = true;
    api(() => (fail ? new Response(null, { status: 500 }) : json(auditPage([auditItem(1)]))));
    setup();
    const retry = await screen.findByRole('button', { name: /تلاش/ });
    fail = false;
    await user.click(retry);
    expect(await screen.findByText('ویرایش گروه دسترسی')).toBeInTheDocument();

    onUnauthenticated.mockReturnValue(true);
    api(() => new Response(null, { status: 401 }));
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });

  it('moves to the last existing page when the current one became empty', async () => {
    const user = userEvent.setup();
    let call = 0;
    const bodies = [
      auditPage([auditItem(1)], { totalCount: 60 }),
      auditPage([], { page: 2, totalCount: 1 }),
      auditPage([auditItem(1)], { totalCount: 1 }),
    ];
    api(() => json(bodies[Math.min(call++, bodies.length - 1)]));
    setup();
    await screen.findByText('ویرایش گروه دسترسی');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(call).toBe(3));
    expect(await screen.findByText('ویرایش گروه دسترسی')).toBeInTheDocument();
  });
});
