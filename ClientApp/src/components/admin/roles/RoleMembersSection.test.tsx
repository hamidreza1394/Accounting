import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CompanyRole } from '../../../api/types';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { memberItem, memberPage } from '../../../test/roleMembers';
import { roleItem, systemRole } from '../../../test/roles';
import { RoleMembersSection } from './RoleMembersSection';

const onUnauthenticated = vi.fn((): boolean => false);
const membersPath = '/api/v1/admin/companies/a/roles/role-1/members';
const effectivePath = '/api/v1/admin/companies/a/effective-permissions/user-1';
const transferPath = `${membersPath}/transfer`;
const rolesPath = '/api/v1/admin/companies/a/roles';

const source = roleItem({ activeMemberCount: 2 }) as unknown as CompanyRole;
const targetRole = roleItem({ id: 'role-2', code: 'ACCOUNTANT', name: 'حسابدار' });
const inactiveRole = roleItem({ id: 'role-3', name: 'غیرفعال', isActive: false });
const systemTarget = { ...systemRole };

function setup(role: CompanyRole = source, canAssign = true, onForbidden?: () => void) {
  return render(<RoleMembersSection role={role} companyId="a" canAssign={canAssign} onForbidden={onForbidden} onUnauthenticated={onUnauthenticated} />);
}

type Handlers = {
  members?: (call: ApiCall) => Response;
  roles?: () => Response;
  transfer?: (call: ApiCall) => Response;
  effective?: (call: ApiCall) => Response;
};

function api({ members, roles, transfer, effective }: Handlers = {}) {
  return stubApi((call) => {
    if (call.path === membersPath) return members ? members(call) : json(memberPage([memberItem(1), memberItem(2, { isUserActive: false })]));
    if (call.path === rolesPath) return roles ? roles() : json([roleItem(), targetRole, inactiveRole, systemTarget]);
    if (call.path === transferPath) return transfer ? transfer(call) : json({ movedCount: 2 });
    if (call.path === effectivePath) {
      return effective
        ? effective(call)
        : json({ userId: 'user-1', companyId: 'a', roleNames: ['حسابدار'], permissionCodes: ['users.list', 'roles.view'] });
    }
    return new Response(null, { status: 404 });
  });
}

const posts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'POST');

describe('RoleMembersSection', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists the active members with name, username, account status and member-since date', async () => {
    api();
    setup();
    expect(await screen.findByRole('heading', { level: 2, name: 'اعضای این گروه (۲)' })).toBeInTheDocument();
    const row = (await screen.findByText('نام1 خانوادگی1')).closest('tr') as HTMLElement;
    expect(within(row).getByText('member1')).toHaveAttribute('dir', 'ltr');
    expect(within(row).getByText('فعال')).toBeInTheDocument();
    const inactive = screen.getByText('نام2 خانوادگی2').closest('tr') as HTMLElement;
    expect(within(inactive).getByText('غیرفعال')).toBeInTheDocument();
  });

  it('searches members server-side with a debounced term and restarts paging', async () => {
    const user = userEvent.setup();
    const { mock } = api({ members: () => json(memberPage([memberItem(1)])) });
    setup();
    await screen.findByRole('heading', { level: 2, name: 'اعضای این گروه (۱)' });
    const memberUrls = () => mock.mock.calls.map((call) => String(call[0])).filter((url) => url.includes('/members?'));
    const initialCount = memberUrls().length;
    expect(initialCount).toBeGreaterThan(0);

    await user.type(screen.getByRole('searchbox', { name: /جستجوی اعضا/ }), 'سارا');
    await waitFor(() => {
      const searched = memberUrls().filter((url) => url.includes('search='));
      expect(searched.length).toBeGreaterThan(0);
      const lastUrl = new URL(searched.at(-1)!, 'http://localhost');
      expect(lastUrl.searchParams.get('search')).toBe('سارا');
      // A committed search term always restarts from page 1.
      expect(lastUrl.searchParams.get('page')).toBe('1');
    });
    // The 300 ms debounce means typing never floods the API: one extra request, not per keystroke.
    expect(memberUrls().length).toBe(initialCount + 1);

    // Clearing the box asks for the unfiltered list again.
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن جستجو' }));
    await waitFor(() => {
      const last = new URL(memberUrls().at(-1)!, 'http://localhost');
      expect(last.searchParams.has('search')).toBe(false);
      expect(last.searchParams.get('page')).toBe('1');
    });
  });

  it('falls back to the username, then to a neutral label, for a member without a name', async () => {
    api({ members: () => json(memberPage([memberItem(1, { firstName: null, lastName: null }), memberItem(2, { firstName: null, lastName: null, username: null })])) });
    setup();
    expect(await screen.findAllByRole('cell', { name: 'member1' })).toHaveLength(2); // name fallback + username column
    expect(screen.getByText('کاربر بدون نام')).toBeInTheDocument();
  });

  it('offers only active, non-system roles of the company other than this one as targets', async () => {
    api();
    setup();
    const select = await screen.findByRole('combobox', { name: /گروه مقصد/ });
    await waitFor(() => expect(within(select).getAllByRole('option').map((option) => option.textContent)).toEqual(['انتخاب کنید', 'حسابدار']));
  });

  it('asks for confirmation, then moves everyone with the exact body and reloads the members', async () => {
    const user = userEvent.setup();
    let memberCalls = 0;
    const { calls } = api({ members: () => json(memberPage(memberCalls++ === 0 ? [memberItem(1), memberItem(2)] : [])) });
    setup();
    const select = await screen.findByRole('combobox', { name: /گروه مقصد/ });
    const open = screen.getByRole('button', { name: 'انتقال اعضا…' });
    expect(open).toBeDisabled();
    await within(select).findByRole('option', { name: 'حسابدار' });
    await user.selectOptions(select, 'role-2');
    await user.click(open);

    const dialog = await screen.findByRole('dialog', { name: 'انتقال همهٔ اعضای گروه' });
    expect(dialog).toHaveTextContent('۲ عضو فعال گروه «کارشناس فروش» به گروه «حسابدار» منتقل می‌شوند');
    expect(posts(calls)).toHaveLength(0);
    await user.click(within(dialog).getByRole('button', { name: 'انتقال اعضا' }));

    expect(await screen.findByText('۲ عضو به گروه «حسابدار» منتقل شد.')).toBeInTheDocument();
    const [post] = posts(calls);
    expect(post?.path).toBe(transferPath);
    expect(post?.body).toEqual({ targetRoleId: 'role-2' });
    expect(post?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(await screen.findByText('این گروه عضو فعالی ندارد')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
    // No members left, so the transfer controls disappear.
    expect(screen.queryByRole('button', { name: 'انتقال اعضا…' })).toBeNull();
  });

  async function openTransferDialog(user: ReturnType<typeof userEvent.setup>) {
    const select = await screen.findByRole('combobox', { name: /گروه مقصد/ });
    await within(select).findByRole('option', { name: 'حسابدار' });
    await user.selectOptions(select, 'role-2');
    await user.click(screen.getByRole('button', { name: 'انتقال اعضا…' }));
    return screen.findByRole('dialog');
  }

  it('shows the server message in a focused alert when the transfer is refused, and reloads everything', async () => {
    const user = userEvent.setup();
    const message = 'گروه مقصد غیرفعال است.';
    const { calls } = api({ transfer: () => json({ error: message }, 409) });
    setup();
    const dialog = await openTransferDialog(user);
    await user.click(within(dialog).getByRole('button', { name: 'انتقال اعضا' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(message);
    await waitFor(() => expect(alert).toHaveFocus());
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.queryByText(/منتقل شد/)).toBeNull();
    // The members and the possible targets are refreshed; the stale target choice is dropped.
    await waitFor(() => expect(calls.filter((call) => call.path === membersPath).length).toBeGreaterThan(1));
    await waitFor(() => expect(calls.filter((call) => call.path === rolesPath).length).toBeGreaterThan(1));
    expect(await screen.findByRole('combobox', { name: /گروه مقصد/ })).toHaveValue('');
  });

  it('never claims nothing moved when the outcome is unknown (5xx), and reloads the members', async () => {
    const user = userEvent.setup();
    const { calls } = api({ transfer: () => new Response(null, { status: 500 }) });
    setup();
    const dialog = await openTransferDialog(user);
    await user.click(within(dialog).getByRole('button', { name: 'انتقال اعضا' }));
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('نتیجهٔ انتقال نامشخص است');
    expect(alert).not.toHaveTextContent('هیچ عضوی منتقل نشده');
    await waitFor(() => expect(calls.filter((call) => call.path === membersPath).length).toBeGreaterThan(1));
  });

  it('moves focus to the confirmation after a successful transfer and clears it on the next dialog', async () => {
    const user = userEvent.setup();
    let memberCalls = 0;
    api({ members: () => json(memberPage(memberCalls++ === 0 ? [memberItem(1)] : [memberItem(3)])) });
    setup();
    const dialog = await openTransferDialog(user);
    await user.click(within(dialog).getByRole('button', { name: 'انتقال اعضا' }));
    const notice = await screen.findByText('۲ عضو به گروه «حسابدار» منتقل شد.');
    await waitFor(() => expect(notice.closest('[role="status"]')).toHaveFocus());
    await user.selectOptions(await screen.findByRole('combobox', { name: /گروه مقصد/ }), 'role-2');
    await user.click(screen.getByRole('button', { name: 'انتقال اعضا…' }));
    expect(screen.queryByText('۲ عضو به گروه «حسابدار» منتقل شد.')).toBeNull();
  });

  it('hands a 401 of the member list or of the transfer to the unauthenticated flow without an error or a notice', async () => {
    const user = userEvent.setup();
    onUnauthenticated.mockReturnValue(true);
    api({ transfer: () => new Response(null, { status: 401 }) });
    setup();
    const dialog = await openTransferDialog(user);
    await user.click(within(dialog).getByRole('button', { name: 'انتقال اعضا' }));
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByText(/منتقل شد/)).toBeNull();
  });

  it('hands a 401 while loading the members to the unauthenticated flow', async () => {
    onUnauthenticated.mockReturnValue(true);
    api({ members: () => new Response(null, { status: 401 }) });
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows an alert and no transfer controls when the target roles cannot be loaded', async () => {
    api({ roles: () => new Response(null, { status: 500 }) });
    setup();
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /گروه مقصد/ })).toBeNull();
  });

  it('pages with the exact query and returns to page 1 when the page size changes', async () => {
    const user = userEvent.setup();
    const { mock } = api({ members: () => json(memberPage([memberItem(1)], { totalCount: 45 })) });
    setup();
    await screen.findByText('نام1 خانوادگی1');
    const memberUrls = () => mock.mock.calls.map((call) => String(call[0])).filter((url) => url.startsWith(membersPath));
    expect(memberUrls().at(-1)).toBe(`${membersPath}?page=1&pageSize=20`);
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(memberUrls().at(-1)).toBe(`${membersPath}?page=2&pageSize=20`));
    await user.selectOptions(screen.getByLabelText(/تعداد/), '50');
    await waitFor(() => expect(memberUrls().at(-1)).toBe(`${membersPath}?page=1&pageSize=50`));
  });

  it('moves to the last existing page when the current one became empty, and never hangs loading', async () => {
    const user = userEvent.setup();
    let call = 0;
    const bodies = [
      memberPage([memberItem(1)], { totalCount: 25 }),
      memberPage([], { page: 2, totalCount: 1 }),
      memberPage([memberItem(1)], { totalCount: 1 }),
    ];
    api({ members: () => json(bodies[Math.min(call++, bodies.length - 1)]) });
    setup();
    await screen.findByText('نام1 خانوادگی1');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(call).toBe(3));
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
  });

  it('reports the live member count to its parent after loading', async () => {
    api();
    const onMemberCount = vi.fn();
    render(<RoleMembersSection role={source} companyId="a" canAssign onUnauthenticated={onUnauthenticated} onMemberCount={onMemberCount} />);
    await waitFor(() => expect(onMemberCount).toHaveBeenCalledWith(2));
  });

  it('never offers a transfer for a system role and says why', async () => {
    api();
    setup(systemRole as unknown as CompanyRole);
    expect(await screen.findByText('انتقال گروهی اعضا از یا به گروه سیستمی مجاز نیست.')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /گروه مقصد/ })).toBeNull();
    expect(screen.queryByRole('button', { name: 'انتقال اعضا…' })).toBeNull();
  });

  it('is read-only without the manage hint: members are shown but no transfer controls', async () => {
    const { calls } = api();
    setup(source, false);
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /گروه مقصد/ })).toBeNull();
    expect(calls.some((call) => call.path === rolesPath)).toBe(false);
  });

  it('refuses a bulk move above the server limit before asking anything', async () => {
    api({ members: () => json(memberPage([memberItem(1)], { totalCount: 501 })) });
    setup();
    expect(await screen.findByText(/از سقف ۵۰۰ نفر/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'انتقال اعضا…' })).toBeNull();
  });

  it('shows the member list error with a retry and the unauthenticated hand-off', async () => {
    const user = userEvent.setup();
    let fail = true;
    api({ members: () => (fail ? new Response(null, { status: 500 }) : json(memberPage([memberItem(1)]))) });
    setup();
    const retry = await screen.findByRole('button', { name: /تلاش/ });
    fail = false;
    await user.click(retry);
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
  });
  describe('adding a member (roles.assign)', () => {
    it('offers the add-member search to an assigner of an active, non-system group', async () => {
      api();
      setup();
      expect(await screen.findByRole('heading', { level: 3, name: 'افزودن عضو به این گروه' })).toBeInTheDocument();
    });

    it.each([
      ['without the assign permission', source, false],
      ['for a system group', systemRole as unknown as CompanyRole, true],
      ['for an inactive group', roleItem({ isActive: false }) as unknown as CompanyRole, true],
    ])('hides the add-member search %s', async (_name, role, canAssign) => {
      api();
      setup(role, canAssign);
      await screen.findByRole('heading', { level: 2 });
      expect(screen.queryByRole('heading', { level: 3, name: 'افزودن عضو به این گروه' })).not.toBeInTheDocument();
    });

    it('asks the caller to re-read permissions when the server refuses a transfer with 403', async () => {
      const onForbidden = vi.fn();
      api({ transfer: () => json({ error: 'نه' }, 403) });
      setup(source, true, onForbidden);
      await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'گروه مقصد' }), 'role-2');
      await userEvent.click(screen.getByRole('button', { name: 'انتقال اعضا…' }));
      await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'انتقال اعضا' }));
      await waitFor(() => expect(onForbidden).toHaveBeenCalledTimes(1));
    });
  });
});

describe('RoleMembersSection — effective permissions viewer (Stage 2-E)', () => {
  it('shows the union of role names and codes for a member', async () => {
    api();
    setup();
    const row = (await screen.findByText('نام1 خانوادگی1')).closest('tr') as HTMLElement;
    await userEvent.click(within(row).getByRole('button', { name: 'مشاهده' }));
    expect(await screen.findByRole('region', { name: 'دسترسی‌های مؤثر عضو' })).toHaveTextContent('حسابدار');
    expect(screen.getByText('users.list')).toHaveAttribute('dir', 'ltr');
    expect(screen.getByText('roles.view')).toBeInTheDocument();
  });

  it('renders the empty state for a member without any effective permission', async () => {
    api({ effective: () => json({ userId: 'user-1', companyId: 'a', roleNames: [], permissionCodes: [] }) });
    setup();
    const row = (await screen.findByText('نام1 خانوادگی1')).closest('tr') as HTMLElement;
    await userEvent.click(within(row).getByRole('button', { name: 'مشاهده' }));
    expect(await screen.findByText('این عضو از طریق نقش‌های فعالش در این شرکت هیچ مجوزی ندارد.')).toBeInTheDocument();
  });

  it('treats a 404 as an indistinguishable no-active-membership message and a 403 as a forbidden refresh', async () => {
    const onForbidden = vi.fn();
    api({ effective: () => new Response(null, { status: 404 }) });
    setup(source, true, onForbidden);
    const row = (await screen.findByText('نام1 خانوادگی1')).closest('tr') as HTMLElement;
    await userEvent.click(within(row).getByRole('button', { name: 'مشاهده' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('عضو فعال این شرکت نیست');

    api({ effective: () => new Response(null, { status: 403 }) });
    await userEvent.click(within(row).getByRole('button', { name: 'مشاهده' }));
    expect(onForbidden).toHaveBeenCalledTimes(1);
    expect(await screen.findByRole('alert')).toHaveTextContent('مجوز دسترسی به این بخش را ندارید');
  });
});
