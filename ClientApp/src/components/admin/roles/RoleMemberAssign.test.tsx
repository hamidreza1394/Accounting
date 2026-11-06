import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CompanyRole } from '../../../api/types';
import { directoryPage, directoryUser } from '../../../test/companyDirectory';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { roleItem } from '../../../test/roles';
import { RoleMemberAssign } from './RoleMemberAssign';

const onUnauthenticated = vi.fn((): boolean => false);
const onForbidden = vi.fn();
const onAssigned = vi.fn();
const usersPath = '/api/v1/companies/a/users';
const assignPath = (userId: string) => `/api/v1/admin/companies/a/roles/role-1/members/${userId}`;
const role = roleItem() as unknown as CompanyRole;

function setup(target: CompanyRole = role) {
  return render(
    <RoleMemberAssign role={target} companyId="a" onUnauthenticated={onUnauthenticated} onForbidden={onForbidden} onAssigned={onAssigned} />,
  );
}

type Handlers = { users?: (call: ApiCall) => Response; assign?: (call: ApiCall) => Response };

function api({ users, assign }: Handlers = {}) {
  return stubApi((call) => {
    if (call.path === usersPath) {
      return users
        ? users(call)
        : json(
            directoryPage([
              directoryUser(1, { roleId: 'role-9', roleName: 'دیگر' }),
              directoryUser(2, { roleId: 'role-1', roleName: 'همین گروه' }),
              directoryUser(3, { userIsActive: false }),
              directoryUser(4, { roleId: 'role-9', roleName: 'دیگر' }),
            ]),
          );
    }
    if (call.method === 'POST' && call.path.startsWith('/api/v1/admin/companies/a/roles/role-1/members/')) {
      return assign ? assign(call) : new Response(null, { status: 204 });
    }
    return new Response(null, { status: 404 });
  });
}

const posts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'POST');

async function search(text = 'نام') {
  await userEvent.type(screen.getByRole('searchbox', { name: 'جستجوی کاربر' }), text);
  await userEvent.click(screen.getByRole('button', { name: 'جستجو' }));
}

describe('RoleMemberAssign', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    onForbidden.mockReset();
    onAssigned.mockReset();
  });

  it('does not call the server until the administrator searches', () => {
    const { calls } = api();
    setup();
    expect(calls).toHaveLength(0);
  });

  it('searches only active memberships and lists active users who are not already in the group', async () => {
    const { calls } = api();
    setup();
    await search('نام');
    const list = await screen.findByRole('list', { name: 'نتایج جستجوی کاربر' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(2);
    expect(screen.queryByText('نام2 خانوادگی2')).not.toBeInTheDocument(); // already in this group
    expect(screen.queryByText('نام3 خانوادگی3')).not.toBeInTheDocument(); // inactive account
    const query = calls.find((call) => call.path === usersPath);
    expect(query).toBeDefined();
  });

  it('asks for confirmation and then assigns the chosen member, reporting success', async () => {
    const { calls } = api();
    setup();
    await search();
    const item = (await screen.findByText('نام1 خانوادگی1')).closest('li') as HTMLElement;
    await userEvent.click(within(item).getByRole('button', { name: /اختصاص/ }));
    expect(posts(calls)).toHaveLength(0);
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'اختصاص به گروه' }));
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]!.path).toBe(assignPath('user-1'));
    expect(await screen.findByText(/به گروه «.+» اختصاص یافت/)).toBeInTheDocument();
    expect(onAssigned).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('نام1 خانوادگی1')).not.toBeInTheDocument();
  });

  it('cancelling the confirmation does not assign', async () => {
    const { calls } = api();
    setup();
    await search();
    const item = (await screen.findByText('نام1 خانوادگی1')).closest('li') as HTMLElement;
    await userEvent.click(within(item).getByRole('button', { name: /اختصاص/ }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'انصراف' }));
    expect(posts(calls)).toHaveLength(0);
  });

  it.each([
    [403, 'فراتر از سقف مجوزهای شما است.'],
    [409, 'عضویت فعال نیست.'],
    [404, 'اطلاعات درخواست‌شده پیدا نشد یا به آن دسترسی ندارید.'],
  ])('shows the server refusal for %s and does not report success', async (status, message) => {
    api({ assign: () => json({ error: message }, status) });
    setup();
    await search();
    const item = (await screen.findByText('نام1 خانوادگی1')).closest('li') as HTMLElement;
    await userEvent.click(within(item).getByRole('button', { name: /اختصاص/ }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'اختصاص به گروه' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(onAssigned).not.toHaveBeenCalled();
    if (status === 403) expect(onForbidden).toHaveBeenCalledTimes(1);
  });

  it('explains that the directory needs the user-list permission when the search is forbidden', async () => {
    api({ users: () => json({ error: 'no' }, 403) });
    setup();
    await search();
    expect(await screen.findByRole('alert')).toHaveTextContent('مجوز مشاهدهٔ فهرست کاربران');
    expect(screen.queryByRole('list', { name: 'نتایج جستجوی کاربر' })).not.toBeInTheDocument();
  });

  it('says so when nobody matches', async () => {
    api({ users: () => json(directoryPage([])) });
    setup();
    await search('zzz');
    expect(await screen.findByText('کاربر قابل اختصاصی پیدا نشد.')).toBeInTheDocument();
  });

  it('hands a 401 to the session handler without an error message', async () => {
    onUnauthenticated.mockReturnValue(true);
    api({ assign: () => json({ error: 'x' }, 401) });
    setup();
    await search();
    const item = (await screen.findByText('نام1 خانوادگی1')).closest('li') as HTMLElement;
    await userEvent.click(within(item).getByRole('button', { name: /اختصاص/ }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'اختصاص به گروه' }));
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('never assigns twice on a double click', async () => {
    const { calls } = stubApi((call) => {
      if (call.path === usersPath) return json(directoryPage([directoryUser(1, { roleId: 'role-9' })]));
      return new Response(null, { status: 204 });
    });
    setup();
    await search();
    const item = (await screen.findByText('نام1 خانوادگی1')).closest('li') as HTMLElement;
    await userEvent.click(within(item).getByRole('button', { name: /اختصاص/ }));
    const confirm = within(await screen.findByRole('dialog')).getByRole('button', { name: 'اختصاص به گروه' });
    await userEvent.dblClick(confirm);
    await waitFor(() => expect(posts(calls).length).toBeGreaterThanOrEqual(1));
    expect(posts(calls)).toHaveLength(1);
  });

  it('tells the administrator to narrow the search when the directory has more matches than were returned', async () => {
    api({ users: () => json(directoryPage([directoryUser(1, { roleId: 'role-9' })], { totalCount: 80 })) });
    setup();
    await search();
    expect(await screen.findByText(/نتایج بیشتری وجود دارد/)).toBeInTheDocument();
  });
});
