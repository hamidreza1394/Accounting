import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import { Search, X } from 'lucide-react';
import { ApiError, getAdminUsers, type AdminUserQuery } from '../../../api/client';
import type { AdminUserListItem, AdminUserListResponse } from '../../../api/types';
import { formatDate, formatDateTime } from '../../../lib/format';
import { Link, useSearchParams } from '../../../lib/router';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { DataTable, type DataTableColumn, type SortState } from '../../ui/DataTable';
import { Input, Select, FormField } from '../../ui/FormField';
import { StatusBadge } from '../../ui/StatusBadge';
import { Button, buttonVariants } from '../../ui/button';
import { fullName } from './userDisplay';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** UI hint only (the server decides): shows the create button. */
  canCreate?: boolean;
};

const pageSizes = [10, 20, 50] as const;
const sortKeys = ['name', 'username', 'lastLogin', 'created'] as const;
const statuses = ['all', 'active', 'inactive'] as const;
const searchDebounceMs = 300;
const number = new Intl.NumberFormat('fa-IR');

type Query = {
  search: string;
  status: AdminUserQuery['status'];
  sort: AdminUserQuery['sort'];
  descending: boolean;
  page: number;
  pageSize: number;
};

const defaults: Query = { search: '', status: 'all', sort: 'name', descending: false, page: 1, pageSize: 20 };

/** Reads the list state from the URL; any invalid value falls back to its default. */
function parseQuery(params: URLSearchParams): Query {
  const status = params.get('status');
  const sort = params.get('sort');
  const page = Number(params.get('page'));
  const pageSize = Number(params.get('pageSize'));
  return {
    search: (params.get('q') ?? '').trim(),
    status: statuses.find((value) => value === status) ?? defaults.status,
    sort: sortKeys.find((value) => value === sort) ?? defaults.sort,
    descending: params.get('dir') === 'desc',
    page: Number.isInteger(page) && page >= 1 ? page : defaults.page,
    pageSize: pageSizes.find((value) => value === pageSize) ?? defaults.pageSize,
  };
}

/** Writes only non-default values, so the common URL stays clean. */
function toParams(query: Query): URLSearchParams {
  const params = new URLSearchParams();
  if (query.search) params.set('q', query.search);
  if (query.status !== defaults.status) params.set('status', query.status);
  if (query.sort !== defaults.sort) params.set('sort', query.sort);
  if (query.descending) params.set('dir', 'desc');
  if (query.page !== defaults.page) params.set('page', String(query.page));
  if (query.pageSize !== defaults.pageSize) params.set('pageSize', String(query.pageSize));
  return params;
}

type Failure = { message: string; forbidden: boolean };

function toFailure(cause: unknown): Failure {
  if (cause instanceof ApiError) {
    return cause.status === 403
      ? { message: 'شما مجوز مشاهدهٔ فهرست کاربران را ندارید.', forbidden: true }
      : { message: cause.message, forbidden: false };
  }
  return { message: 'دریافت فهرست کاربران انجام نشد. لطفاً دوباره تلاش کنید.', forbidden: false };
}

const columns: readonly DataTableColumn<AdminUserListItem>[] = [
  { key: 'name', header: 'نام و نام خانوادگی', sortable: true, cell: (user) => fullName(user) },
  {
    key: 'username',
    header: 'نام کاربری',
    sortable: true,
    cell: (user) => <span dir="ltr">{user.username ?? '—'}</span>,
  },
  { key: 'email', header: 'ایمیل', cell: (user) => <span dir="ltr">{user.emailAddress}</span> },
  { key: 'mobile', header: 'موبایل', cell: (user) => (user.cellPhoneNumber ? <span dir="ltr">{user.cellPhoneNumber}</span> : '—') },
  { key: 'status', header: 'وضعیت', cell: (user) => <StatusBadge status={user.isActive ? 'active' : 'inactive'} /> },
  { key: 'companies', header: 'شرکت‌های فعال', align: 'center', cell: (user) => number.format(user.activeCompanyCount) },
  { key: 'lastLogin', header: 'آخرین ورود', sortable: true, cell: (user) => formatDateTime(user.lastLoginDateTime, 'هرگز') },
  { key: 'created', header: 'تاریخ ایجاد', sortable: true, cell: (user) => formatDate(user.insertDateTime) },
];

export function UsersListPage({ onUnauthenticated, canCreate = false }: Props) {
  const [params, setParams] = useSearchParams();
  const query = useMemo(() => parseQuery(params), [params]);
  const searchId = useId();
  const [searchInput, setSearchInput] = useState(query.search);
  const [data, setData] = useState<AdminUserListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [attempt, setAttempt] = useState(0);

  const queryRef = useRef(query);
  queryRef.current = query;

  // True while a URL change was made by this page itself (as opposed to back/forward).
  const ownNavigation = useRef(false);
  const navigateTo = useCallback(
    (next: Query, options?: { replace?: boolean }) => {
      const nextParams = toParams(next);
      ownNavigation.current = nextParams.toString() !== toParams(queryRef.current).toString();
      setParams(nextParams, options);
    },
    [setParams],
  );

  const update = useCallback(
    (patch: Partial<Query>, options?: { replace?: boolean }) => {
      const next = { ...queryRef.current, ...patch };
      if (!('page' in patch)) next.page = 1;
      navigateTo(next, options);
    },
    [navigateTo],
  );

  // Debounced server-side search; typing replaces the history entry instead of flooding it.
  // The timer is cancelled whenever the location changes (the effect depends on `params`).
  useEffect(() => {
    const trimmed = searchInput.trim();
    if (trimmed === queryRef.current.search) return;
    const timer = window.setTimeout(() => update({ search: trimmed }, { replace: true }), searchDebounceMs);
  return () => window.clearTimeout(timer);
  }, [searchInput, update, params]);

  // Back/forward: the box shows the restored search and unsent typing is dropped. Changes made by
  // this page keep what the user is typing.
  useEffect(() => {
    if (ownNavigation.current) {
      ownNavigation.current = false;
      return;
    }
    setSearchInput(query.search);
  }, [params, query.search]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    const request: AdminUserQuery = {
      status: query.status,
      page: query.page,
      pageSize: query.pageSize,
      sort: query.sort,
      descending: query.descending,
    };
    if (query.search) request.search = query.search;
    getAdminUsers(request, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setData(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        // A superseded request is ignored; it must not clear the loading state of the newer one.
        if (controller.signal.aborted) return;
        if (onUnauthenticated(cause)) return;
        setFailure(toFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [query, attempt, onUnauthenticated]);

  // A stale page number (e.g. a shared URL after users were removed) moves to the last page.
  useEffect(() => {
    if (!loading && data && data.items.length === 0 && data.totalCount > 0 && query.page > 1) {
      update({ page: Math.max(1, Math.ceil(data.totalCount / query.pageSize)) }, { replace: true });
    }
  }, [loading, data, query.page, query.pageSize, update]);

  const hasFilters = query.search !== '' || query.status !== 'all' || searchInput.trim() !== '';
  const clearFilters = () => {
    setSearchInput('');
    navigateTo({ ...queryRef.current, search: '', status: 'all', page: 1 });
  };
  const sort: SortState = { key: query.sort, direction: query.descending ? 'desc' : 'asc' };

  usePageBreadcrumb([{ label: 'مدیریت سیستم' }, { label: 'کاربران' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 space-y-2">
            <h1 className="text-2xl font-extrabold tracking-tight text-title">مدیریت کاربران</h1>
            <p className="text-sm text-sub">فهرست حساب‌های این نصب؛ برای مشاهدهٔ جزئیات و عضویت‌ها یک کاربر را انتخاب کنید.</p>
          </div>
          {canCreate ? (
            <Link to="/admin/users/new" className={buttonVariants({ size: 'lg' })}>
              ایجاد کاربر
            </Link>
          ) : null}
        </div>
      </header>

      <form role="search" aria-label="جستجو و فیلتر کاربران" onSubmit={(event) => event.preventDefault()} className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-60 flex-1 flex-col gap-1.5">
          <label htmlFor={searchId} className="text-sm font-semibold text-ink">جستجوی کاربران</label>
          {/* The icon and the clear button sit inside the field; the input spans the whole row. */}
          <div className="relative">
            <Search aria-hidden="true" size={16} className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-muted" />
            <Input
              id={searchId}
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="جستجو…"
              autoComplete="off"
              aria-describedby={`${searchId}-hint`}
              className="h-11 ps-9 pe-11 sm:h-10 [&::-webkit-search-cancel-button]:appearance-none"
            />
            {searchInput ? (
              <button
                type="button"
                aria-label="پاک‌کردن جستجو"
                onClick={() => {
                  setSearchInput('');
                  update({ search: '' }, { replace: true });
                }}
                className="absolute inset-y-0 end-0 flex w-11 items-center justify-center rounded-e-lg text-sub outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-gold"
              >
                <X aria-hidden="true" size={16} />
              </button>
            ) : null}
          </div>
          <p id={`${searchId}-hint`} className="text-xs leading-5 text-sub">نام، نام کاربری، ایمیل یا موبایل</p>
        </div>
        <FormField label="وضعیت" className="w-40">
          <Select value={query.status} onChange={(event) => update({ status: statuses.find((value) => value === event.target.value) ?? defaults.status })}>
            <option value="all">همه</option>
            <option value="active">فعال</option>
            <option value="inactive">غیرفعال</option>
          </Select>
        </FormField>
      </form>

      <DataTable
        caption="فهرست کاربران"
        columns={columns}
        rows={data?.items ?? []}
        getRowKey={(user) => user.id}
        loading={loading}
        error={failure?.message ?? null}
        {...(failure && !failure.forbidden ? { onRetry: () => setAttempt((value) => value + 1) } : {})}
        emptyTitle="کاربری پیدا نشد"
        emptyDescription={
          hasFilters ? (
            <Button type="button" variant="outline" size="lg" onClick={clearFilters}>
              پاک‌کردن فیلترها
            </Button>
          ) : (
            'هنوز کاربری ثبت نشده است.'
          )
        }
        sort={sort}
        onSortChange={(next) => {
          const key = sortKeys.find((value) => value === next.key);
          if (key) update({ sort: key, descending: next.direction === 'desc' });
        }}
        rowHref={(user) => `/admin/users/${encodeURIComponent(user.id)}`}
        {...(data && !failure
          ? {
              pagination: {
                page: query.page,
                pageSize: query.pageSize,
                total: data.totalCount,
                pageSizeOptions: pageSizes,
                onPageChange: (page: number) => update({ page }),
                onPageSizeChange: (pageSize: number) =>
                  update({ pageSize: pageSizes.find((value) => value === pageSize) ?? defaults.pageSize }),
              },
            }
          : {})}
      />
    </div>
  );
}
