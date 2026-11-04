import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { json, stubApi, type ApiCall } from '../../test/adminUsers';
import { pageBody, pendingItem } from '../../test/membership';
import { roleItem } from '../../test/roles';
import { PendingMembershipRequestsPage } from './PendingMembershipRequestsPage';

const onUnauthenticated = vi.fn((): boolean => false);
const queuePath = '/api/v1/admin/companies/c1/membership-requests/pending';
const rolesPath = '/api/v1/admin/companies/c1/membership-requests/assignable-roles';

function setup() {
  return render(<PendingMembershipRequestsPage onUnauthenticated={onUnauthenticated} companyId="c1" companyName="شرکت نمونه" />);
}

type Handlers = {
  queue?: (call: ApiCall) => Response;
  roles?: () => Response;
  decide?: (call: ApiCall) => Response;
};

function api({ queue, roles, decide }: Handlers = {}) {
  return stubApi((call) => {
    if (call.path === queuePath) return queue ? queue(call) : json(pageBody([pendingItem(1), pendingItem(2)]));
    if (call.path === rolesPath) return roles ? roles() : json(pageBody([roleItem(), { ...roleItem({ id: 'system', name: 'سیستمی' }), isSystemRole: true }]));
    if (call.method === 'POST') return decide ? decide(call) : new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
}

const posts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'POST');

describe('PendingMembershipRequestsPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists the pending requests of the active company', async () => {
    const { calls } = api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'درخواست‌های عضویت در انتظار تأیید' })).toBeInTheDocument();
    expect(await screen.findByText('کاربر 1')).toBeInTheDocument();
    expect(screen.getByText('user2')).toHaveAttribute('dir', 'ltr');
    expect(screen.getByText(/شرکت «شرکت نمونه»/)).toBeInTheDocument();
    expect(calls.map((call) => call.path)).toEqual([queuePath]);
  });

  it('approves into an active role of the company and reloads the queue', async () => {
    const user = userEvent.setup();
    let queueCalls = 0;
    const { calls } = api({ queue: () => json(pageBody(queueCalls++ === 0 ? [pendingItem(1)] : [])) });
    setup();
    await user.click(await screen.findByRole('button', { name: 'تأیید درخواست کاربر 1 (user1)' }));

    const dialog = await screen.findByRole('dialog', { name: 'تأیید درخواست عضویت' });
    const select = await within(dialog).findByRole('combobox', { name: /گروه دسترسی/ });
    // Only server-assignable roles are offered, and a malformed system role is excluded.
    await waitFor(() => expect(within(select).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'انتخاب کنید',
      'کارشناس فروش',
    ]));
    await user.selectOptions(select, 'role-1');
    await user.click(within(dialog).getByRole('button', { name: 'تأیید و ایجاد عضویت' }));

    expect(await screen.findByText('درخواست «کاربر 1» تأیید شد.')).toBeInTheDocument();
    const [post] = posts(calls);
    expect(post?.path).toBe('/api/v1/admin/companies/c1/membership-requests/req-1/approve');
    expect(post?.body).toEqual({ roleId: 'role-1' });
    expect(post?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(await screen.findByText('درخواستی در انتظار تأیید نیست')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('does not send an approval without a chosen role', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.click(await screen.findByRole('button', { name: 'تأیید درخواست کاربر 1 (user1)' }));
    const dialog = await screen.findByRole('dialog');
    await within(dialog).findByRole('option', { name: 'کارشناس فروش' });
    await user.click(within(dialog).getByRole('button', { name: 'تأیید و ایجاد عضویت' }));
    expect(await within(dialog).findByText('یک گروه دسترسی برای کاربر انتخاب کنید.')).toBeInTheDocument();
    expect(posts(calls)).toHaveLength(0);
  });

  it('rejects with a trimmed reason, or without one', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.click(await screen.findByRole('button', { name: 'رد درخواست کاربر 1 (user1)' }));
    let dialog = await screen.findByRole('alertdialog', { name: 'رد درخواست عضویت' });
    await user.type(within(dialog).getByRole('textbox'), '  سمت نامرتبط  ');
    await user.click(within(dialog).getByRole('button', { name: 'رد درخواست' }));
    expect(await screen.findByText('درخواست «کاربر 1» رد شد.')).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: 'رد درخواست کاربر 2 (user2)' }));
    dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'رد درخواست' }));
    await waitFor(() => expect(posts(calls)).toHaveLength(2));
    expect(posts(calls).map((call) => call.body)).toEqual([{ reason: 'سمت نامرتبط' }, { reason: null }]);
    expect(posts(calls)[0]?.path).toBe('/api/v1/admin/companies/c1/membership-requests/req-1/reject');
  });

  it('shows the server message for self-approval and keeps the dialog open', async () => {
    const user = userEvent.setup();
    const message = 'درخواست‌کننده نمی‌تواند درخواست عضویت خودش را تأیید کند.';
    api({ decide: () => json({ error: message }, 403) });
    setup();
    await user.click(await screen.findByRole('button', { name: 'تأیید درخواست کاربر 1 (user1)' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(await within(dialog).findByRole('combobox', { name: /گروه دسترسی/ }), 'role-1');
    await user.click(within(dialog).getByRole('button', { name: 'تأیید و ایجاد عضویت' }));
    expect(await within(dialog).findByText(message)).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('reloads the queue when someone else decided the request first (409)', async () => {
    const user = userEvent.setup();
    let queueCalls = 0;
    api({
      queue: () => json(pageBody(queueCalls++ === 0 ? [pendingItem(1)] : [])),
      decide: () => json({ error: 'درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است.' }, 409),
    });
    setup();
    await user.click(await screen.findByRole('button', { name: 'رد درخواست کاربر 1 (user1)' }));
    const dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'رد درخواست' }));
    expect(await within(dialog).findByText('درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است.')).toBeInTheDocument();
    await waitFor(() => expect(queueCalls).toBe(2));
  });

  it('explains the missing role list for a non-installation approver and does not approve', async () => {
    const user = userEvent.setup();
    const { calls } = api({ roles: () => new Response(null, { status: 403 }) });
    setup();
    await user.click(await screen.findByRole('button', { name: 'تأیید درخواست کاربر 1 (user1)' }));
    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText(/دسترسی ندارد، پس تأیید از این صفحه ممکن نیست/)).toBeInTheDocument();
    expect(within(dialog).queryByRole('combobox')).toBeNull();
    await user.click(within(dialog).getByRole('button', { name: 'تأیید و ایجاد عضویت' }));
    expect(await within(dialog).findByText('بدون فهرست گروه‌های دسترسی، تأیید ممکن نیست.')).toBeInTheDocument();
    expect(posts(calls)).toHaveLength(0);
  });

  it('cannot approve a request whose account is inactive, but can reject it', async () => {
    api({ queue: () => json(pageBody([pendingItem(1, { isRequesterActive: false })])) });
    setup();
    expect(await screen.findByRole('button', { name: 'تأیید درخواست کاربر 1 (user1)' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'رد درخواست کاربر 1 (user1)' })).toBeEnabled();
  });

  it('shows a no-access page when the server answers 404 and loads nothing else', async () => {
    const { calls } = api({ queue: () => new Response(null, { status: 404 }) });
    setup();
    expect(await screen.findByText(/مجوز بررسی درخواست‌های عضویت این شرکت را ندارید/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
    expect(calls.map((call) => call.path)).toEqual([queuePath]);
  });

  it('moves to the last existing page when the current one became empty, and never hangs loading', async () => {
    const user = userEvent.setup();
    let call = 0;
    // 1st: page 1 of 2 pages; 2nd: page 2 turned empty (requests were decided meanwhile); 3rd: page 1 again.
    const bodies = [
      pageBody([pendingItem(1)], { totalCount: 25 }),
      pageBody([], { page: 2, totalCount: 1 }),
      pageBody([pendingItem(1)], { totalCount: 1 }),
    ];
    api({ queue: () => json(bodies[Math.min(call++, bodies.length - 1)]) });
    setup();
    expect(await screen.findByText('کاربر 1')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(call).toBe(3));
    expect(await screen.findByText('کاربر 1')).toBeInTheDocument();
    expect(screen.queryByRole('status', { name: /در حال/ })).toBeNull();
  });

  it('shows the server message and reloads when an approval hits a request that no longer exists (404)', async () => {
    const user = userEvent.setup();
    let queueCalls = 0;
    api({
      queue: () => json(pageBody(queueCalls++ === 0 ? [pendingItem(1)] : [])),
      decide: () => json({}, 404),
    });
    setup();
    await user.click(await screen.findByRole('button', { name: 'رد درخواست کاربر 1 (user1)' }));
    const dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'رد درخواست' }));
    expect(await within(dialog).findByRole('alert')).toBeInTheDocument();
    await waitFor(() => expect(queueCalls).toBe(2));
  });

  it('moves focus to the confirmation after a decision', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await user.click(await screen.findByRole('button', { name: 'رد درخواست کاربر 1 (user1)' }));
    const dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'رد درخواست' }));
    const flash = await screen.findByText('درخواست «کاربر 1» رد شد.');
    await waitFor(() => expect(flash.closest('[role="status"]')).toHaveFocus());
  });

  it('hands a 401 to the unauthenticated flow', async () => {
    onUnauthenticated.mockReturnValue(true);
    api({ queue: () => new Response(null, { status: 401 }) });
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });
});
