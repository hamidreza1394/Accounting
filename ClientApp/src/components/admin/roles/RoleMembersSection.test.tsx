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
const transferPath = `${membersPath}/transfer`;
const rolesPath = '/api/v1/admin/companies/a/roles';

const source = roleItem({ activeMemberCount: 2 }) as unknown as CompanyRole;
const targetRole = roleItem({ id: 'role-2', code: 'ACCOUNTANT', name: 'حسابدار' });
const inactiveRole = roleItem({ id: 'role-3', name: 'غیرفعال', isActive: false });
const systemTarget = { ...systemRole };

function setup(role: CompanyRole = source, canManage = true) {
  return render(<RoleMembersSection role={role} companyId="a" canManage={canManage} onUnauthenticated={onUnauthenticated} />);
}

type Handlers = {
  members?: (call: ApiCall) => Response;
  roles?: () => Response;
  transfer?: (call: ApiCall) => Response;
};

function api({ members, roles, transfer }: Handlers = {}) {
  return stubApi((call) => {
    if (call.path === membersPath) return members ? members(call) : json(memberPage([memberItem(1), memberItem(2, { isUserActive: false })]));
    if (call.path === rolesPath) return roles ? roles() : json([roleItem(), targetRole, inactiveRole, systemTarget]);
    if (call.path === transferPath) return transfer ? transfer(call) : json({ movedCount: 2 });
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

  it('keeps the dialog open and shows the server message when the transfer is refused', async () => {
    const user = userEvent.setup();
    const message = 'گروه مقصد غیرفعال است.';
    const { calls } = api({ transfer: () => json({ error: message }, 409) });
    setup();
    const select = await screen.findByRole('combobox', { name: /گروه مقصد/ });
    await within(select).findByRole('option', { name: 'حسابدار' });
    await user.selectOptions(select, 'role-2');
    await user.click(screen.getByRole('button', { name: 'انتقال اعضا…' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'انتقال اعضا' }));
    expect(await within(dialog).findByText(message)).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.queryByText(/منتقل شد/)).toBeNull();
    // The members are reloaded so the page shows the current state.
    await waitFor(() => expect(calls.filter((call) => call.path === membersPath).length).toBeGreaterThan(1));
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
});
