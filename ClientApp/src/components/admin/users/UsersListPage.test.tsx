import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, listBody, listCalls, stubFetch, userItem } from '../../../test/adminUsers';
import { UsersListPage } from './UsersListPage';

const onUnauthenticated = vi.fn((): boolean => false);
let canCreate = false;
function ListRoute() {
  return <UsersListPage onUnauthenticated={onUnauthenticated} canCreate={canCreate} />;
}
function DetailRoute() {
  return <h1>صفحهٔ جزئیات</h1>;
}
const routes: Route[] = [
  { path: '/admin/users', element: ListRoute, title: 'مدیریت کاربران' },
  { path: '/admin/users/:id', element: DetailRoute, title: 'جزئیات' },
];

function setup(path = '/app/admin/users') {
  window.history.pushState(null, '', path);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

const three = [userItem(1), userItem(2), userItem(3)];

describe('UsersListPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    canCreate = false;
  });

  it('shows no create button without the create permission', async () => {
    stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    expect(screen.queryByRole('link', { name: 'ایجاد کاربر' })).toBeNull();
  });

  it('shows a primary create link to /admin/users/new with the create permission', async () => {
    const user = userEvent.setup();
    canCreate = true;
    stubFetch(() => json(listBody(three)));
    const createRoutes = [{ path: '/admin/users/new', element: () => <h1>فرم ایجاد</h1>, title: 'ایجاد' }, ...routes];
    window.history.pushState(null, '', '/app/admin/users');
    render(
      <RouterProvider routes={createRoutes}>
        <RouteView />
      </RouterProvider>,
    );
    const link = await screen.findByRole('link', { name: 'ایجاد کاربر' });
    expect(link).toHaveAttribute('href', '/app/admin/users/new');
    link.focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('heading', { name: 'فرم ایجاد' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users/new');
  });

  it('requests the server-side default query and renders every column', async () => {
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup();
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
    const [first] = listCalls(fetchMock);
    expect(Object.fromEntries(first!.searchParams)).toEqual({
      status: 'all', page: '1', pageSize: '20', sort: 'created', descending: 'true',
    });
    for (const header of ['نام و نام خانوادگی', 'نام کاربری', 'ایمیل', 'موبایل', 'وضعیت', 'شرکت‌های فعال', 'آخرین ورود', 'تاریخ ایجاد']) {
      expect(screen.getByRole('columnheader', { name: new RegExp(header) })).toBeInTheDocument();
    }
    const rows = screen.getAllByRole('row');
    const row = within(rows[2]!); // user2: has a mobile, active
    expect(row.getByText('user2')).toHaveAttribute('dir', 'ltr');
    expect(row.getByText('user2@example.test')).toBeInTheDocument();
    expect(row.getByText('09120000002')).toBeInTheDocument();
    expect(row.getByText('فعال')).toBeInTheDocument();
    expect(row.getByText(/^۱۴۰۵\/۰۸\/۱۱ /)).toBeInTheDocument();
    expect(row.getByText('۱۴۰۵/۰۷/۰۹')).toBeInTheDocument();
    // user3 is inactive; user1 never signed in.
    expect(within(rows[3]!).getByText('غیرفعال')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('هرگز')).toBeInTheDocument();
    expect(screen.getByText(/نمایش ۱ تا ۳ از ۳/)).toBeInTheDocument();
  });

  it('shows a loading skeleton first', async () => {
    stubFetch(() => new Promise<Response>(() => undefined));
    setup();
    expect(await screen.findByText('در حال بارگذاری…', { selector: 'p' })).toBeInTheDocument();
    expect(screen.getByRole('table')).toHaveAttribute('aria-busy', 'true');
  });

  it('keeps the search icon and the clear button inside the full-width field', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    const input = screen.getByRole('searchbox', { name: 'جستجوی کاربران' });
    const field = input.parentElement as HTMLElement;
    expect(field.querySelector('svg')).not.toBeNull();
    expect(input).toHaveClass('ps-9');
    expect(screen.queryByRole('button', { name: 'پاک‌کردن جستجو' })).toBeNull();
    await user.type(input, 'ع');
    expect(within(field).getByRole('button', { name: 'پاک‌کردن جستجو' })).toBeInTheDocument();
  });

  it('debounces the search, sends one request, keeps it in the URL and clears it', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی کاربران' }), 'علی');
    expect(listCalls(fetchMock)).toHaveLength(1); // nothing sent before the pause
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(2), { timeout: 2000 });
    expect(listCalls(fetchMock)[1]!.searchParams.get('search')).toBe('علی');
    expect(new URLSearchParams(window.location.search).get('q')).toBe('علی');

    await user.click(screen.getByRole('button', { name: 'پاک‌کردن جستجو' }));
    expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('');
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(3));
    expect(listCalls(fetchMock)[2]!.searchParams.has('search')).toBe(false);
    expect(window.location.search).toBe('');
  });

  it('filters by status and resets to the first page', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => json(listBody(three, { totalCount: 45 })));
    setup('/app/admin/users?page=2');
    await screen.findByText('نام1 خانوادگی1');
    await user.selectOptions(screen.getByRole('combobox', { name: 'وضعیت' }), 'inactive');
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(2));
    const params = listCalls(fetchMock)[1]!.searchParams;
    expect(params.get('status')).toBe('inactive');
    expect(params.get('page')).toBe('1');
    expect(window.location.search).toBe('?status=inactive');
  });

  it('maps sortable headers to API sort values and direction', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    await user.click(screen.getByRole('button', { name: 'آخرین ورود' }));
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(2));
    expect(listCalls(fetchMock)[1]!.searchParams.get('sort')).toBe('lastLogin');
    expect(listCalls(fetchMock)[1]!.searchParams.get('descending')).toBe('false');
    await user.click(screen.getByRole('button', { name: 'آخرین ورود' }));
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(3));
    expect(listCalls(fetchMock)[2]!.searchParams.get('descending')).toBe('true');
    expect(screen.getByRole('columnheader', { name: /آخرین ورود/ })).toHaveAttribute('aria-sort', 'descending');
    await user.click(screen.getByRole('button', { name: 'تاریخ ایجاد' }));
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(4));
    expect(listCalls(fetchMock)[3]!.searchParams.get('sort')).toBe('created');
    await user.click(screen.getByRole('button', { name: 'نام کاربری' }));
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(5));
    expect(listCalls(fetchMock)[4]!.searchParams.get('sort')).toBe('username');
  });

  it('pages through results and changes the page size', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch((url) => json(listBody(three, { totalCount: 45, page: Number(url.searchParams.get('page')) })));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    expect(screen.getByText(/صفحه ۱ از ۳/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /بعدی/ }));
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(2));
    expect(listCalls(fetchMock)[1]!.searchParams.get('page')).toBe('2');
    expect(window.location.search).toBe('?page=2');
    await user.selectOptions(screen.getByRole('combobox', { name: /تعداد در صفحه/ }), '50');
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(3));
    expect(listCalls(fetchMock)[2]!.searchParams.get('pageSize')).toBe('50');
    expect(listCalls(fetchMock)[2]!.searchParams.get('page')).toBe('1');
  });

  it('restores the whole state from the URL and ignores invalid values', async () => {
    const fetchMock = stubFetch(() => json(listBody(three, { totalCount: 100 })));
    setup('/app/admin/users?q=ali&status=active&sort=created&dir=desc&page=3&pageSize=10');
    await screen.findByText('نام1 خانوادگی1');
    expect(Object.fromEntries(listCalls(fetchMock)[0]!.searchParams)).toEqual({
      search: 'ali', status: 'active', page: '3', pageSize: '10', sort: 'created', descending: 'true',
    });
    expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('ali');
    expect(screen.getByRole('combobox', { name: 'وضعیت' })).toHaveValue('active');
  });

  it('falls back to defaults for invalid URL values', async () => {
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup('/app/admin/users?status=x&sort=password&page=-4&pageSize=999');
    await screen.findByText('نام1 خانوادگی1');
    expect(Object.fromEntries(listCalls(fetchMock)[0]!.searchParams)).toEqual({
      status: 'all', page: '1', pageSize: '20', sort: 'created', descending: 'true',
    });
  });

  it('follows back and forward through the filter history', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    await user.selectOptions(screen.getByRole('combobox', { name: 'وضعیت' }), 'active');
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(2));
    window.history.back();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'وضعیت' })).toHaveValue('all'));
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(3));
    expect(listCalls(fetchMock)[2]!.searchParams.get('status')).toBe('all');
  });

  it('shows the search of the restored URL in the box after back', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(listBody(three)));
    setup('/app/admin/users?q=ali');
    await screen.findByText('نام1 خانوادگی1');
    await user.selectOptions(screen.getByRole('combobox', { name: 'وضعیت' }), 'active');
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن جستجو' }));
    expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('');
    window.history.back();
    await waitFor(() => expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('ali'));
  });

  it('links each row to its detail page and opens it', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(listBody(three)));
    setup();
    const link = await screen.findByRole('link', { name: 'نام2 خانوادگی2' });
    expect(link).toHaveAttribute('href', '/app/admin/users/user-2');
    await user.click(link);
    expect(await screen.findByRole('heading', { name: 'صفحهٔ جزئیات' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users/user-2');
  });

  it('opens a row with the keyboard (Enter on the link)', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(listBody(three)));
    setup();
    const link = await screen.findByRole('link', { name: 'نام1 خانوادگی1' });
    link.focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('heading', { name: 'صفحهٔ جزئیات' })).toBeInTheDocument();
  });

  it('shows an empty state without filters, and clears the filters from the empty state', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch((url) => json(listBody(url.searchParams.has('search') ? [] : three)));
    setup('/app/admin/users?q=zzz&status=active');
    expect(await screen.findByText('کاربری پیدا نشد')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن فیلترها' }));
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
    expect(window.location.search).toBe('');
    expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('');
    expect(listCalls(fetchMock).at(-1)!.searchParams.get('status')).toBe('all');
  });

  it('shows the plain empty message when nothing is filtered', async () => {
    stubFetch(() => json(listBody([])));
    setup();
    expect(await screen.findByText('هنوز کاربری ثبت نشده است.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'پاک‌کردن فیلترها' })).toBeNull();
  });

  it('shows an error with retry that refetches', async () => {
    const user = userEvent.setup();
    let fail = true;
    const fetchMock = stubFetch(() => (fail ? json({}, 500) : json(listBody(three))));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
    expect(listCalls(fetchMock)).toHaveLength(2);
  });

  it('shows a forbidden message for 403 without a retry button', async () => {
    stubFetch(() => json({}, 403));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('مجوز مشاهدهٔ فهرست کاربران را ندارید');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
  });

  it('hands a 401 to the existing unauthenticated flow and shows no error', async () => {
    onUnauthenticated.mockReturnValue(true);
    stubFetch(() => new Response(null, { status: 401 }));
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('ignores a stale response that arrives after a newer request', async () => {
    const user = userEvent.setup();
    const resolvers: Array<(response: Response) => void> = [];
    stubFetch(() => new Promise<Response>((resolve) => resolvers.push(resolve)));
    setup();
    await waitFor(() => expect(resolvers).toHaveLength(1));
    await user.selectOptions(screen.getByRole('combobox', { name: 'وضعیت' }), 'active');
    await waitFor(() => expect(resolvers).toHaveLength(2));
    resolvers[1]!(json(listBody([userItem(7)])));
    expect(await screen.findByText('نام7 خانوادگی7')).toBeInTheDocument();
    resolvers[0]!(json(listBody([userItem(8)])));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(screen.queryByText('نام8 خانوادگی8')).toBeNull();
    expect(screen.getByText('نام7 خانوادگی7')).toBeInTheDocument();
  });

  it('moves to the last page when the requested page is past the end', async () => {
    const fetchMock = stubFetch((url) =>
      url.searchParams.get('page') === '9' ? json(listBody([], { totalCount: 25, page: 9 })) : json(listBody(three, { totalCount: 25 })),
    );
    setup('/app/admin/users?page=9');
    await waitFor(() => expect(listCalls(fetchMock).map((url) => url.searchParams.get('page'))).toEqual(['9', '2']));
    expect(await screen.findByText('نام1 خانوادگی1')).toBeInTheDocument();
  });

  it('cancels a pending debounced search when back/forward changes the location', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    await user.selectOptions(screen.getByRole('combobox', { name: 'وضعیت' }), 'active');
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(2));
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی کاربران' }), 'zz');
    act(() => {
      window.history.back();
    });
    await waitFor(() => expect(listCalls(fetchMock)).toHaveLength(3));
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(listCalls(fetchMock).some((url) => url.searchParams.get('search') === 'zz')).toBe(false);
    expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('');
    expect(window.location.search).toBe('');
  });

  it('keeps unsent typing when the page itself changes another filter', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => json(listBody(three)));
    setup();
    await screen.findByText('نام1 خانوادگی1');
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی کاربران' }), 'qq');
    await user.selectOptions(screen.getByRole('combobox', { name: 'وضعیت' }), 'active');
    await waitFor(() => expect(listCalls(fetchMock).some((url) => url.searchParams.get('search') === 'qq')).toBe(true), { timeout: 2000 });
    expect(screen.getByRole('searchbox', { name: 'جستجوی کاربران' })).toHaveValue('qq');
  });
});
