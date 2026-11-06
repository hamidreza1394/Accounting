import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ApiError, getCompanyUsers } from '../../api/client';
import type { CompanyDirectoryPage, CompanyDirectoryUser } from '../../api/types';
import { formatDate } from '../../lib/format';
import { Link } from '../../lib/router';
import { Alert } from '../ui/Alert';
import { Breadcrumb } from '../ui/Breadcrumb';
import { DataTable, type DataTableColumn } from '../ui/DataTable';
import { Input, Select } from '../ui/FormField';
import { FormField } from '../ui/FormField';
import { StatusBadge } from '../ui/StatusBadge';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** The active work-context company: the directory always belongs to it. */
  companyId: string;
  companyName?: string;
  /** UI hint only (the server decides): shows the link to the role of the selected user. */
  canViewRoles?: boolean;
};

const pageSizes = [10, 25, 50, 100] as const;
const searchDelayMs = 350;

type MembershipFilter = 'all' | 'active' | 'inactive';

function displayName(user: CompanyDirectoryUser): string {
  return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || user.username || 'کاربر بدون نام';
}

function membershipValue(filter: MembershipFilter): boolean | undefined {
  return filter === 'all' ? undefined : filter === 'active';
}

/**
 * The directory of the people who belong to the active company (read-only). It needs the explicit
 * `users.list` permission of THIS company; the server answers 403 without it and 404 for a company the
 * user cannot access, and the page shows exactly that. Nothing of another company is requested or shown.
 * It never grants or changes anything: membership and group changes live in the administration pages.
 */
export function CompanyUsersPage({ onUnauthenticated, companyId, companyName, canViewRoles = false }: Props) {
  const searchId = useId();
  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [membership, setMembership] = useState<MembershipFilter>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [data, setData] = useState<CompanyDirectoryPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [selected, setSelected] = useState<CompanyDirectoryUser | null>(null);
  const detailRef = useRef<HTMLDivElement>(null);

  // Live search: one request per pause in typing, always from page 1.
  useEffect(() => {
    if (searchText === search) return undefined;
    const timer = window.setTimeout(() => {
      setPage(1);
      setSearch(searchText);
    }, searchDelayMs);
    return () => window.clearTimeout(timer);
  }, [searchText, search]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    const active = membershipValue(membership);
    getCompanyUsers(companyId, { search, ...(active === undefined ? {} : { membershipActive: active }), page, pageSize }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        if (result.items.length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (lastPage < page) {
            setPage(lastPage);
            return;
          }
        }
        setData(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setData(null);
        setFailure({
          status,
          message:
            status === 403
              ? 'شما مجوز مشاهدهٔ فهرست کاربران این شرکت (users.list) را ندارید.'
              : status === 404
                ? 'شرکت پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت فهرست کاربران انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, search, membership, page, pageSize, attempt, onUnauthenticated]);

  // The selection belongs to one company and one list: forget it when either changes.
  useEffect(() => setSelected(null), [companyId]);
  useEffect(() => {
    if (selected) detailRef.current?.focus();
  }, [selected]);

  const columns = useMemo<readonly DataTableColumn<CompanyDirectoryUser>[]>(
    () => [
      {
        key: 'name',
        header: 'نام',
        cell: (user) => (
          <button
            type="button"
            className="min-h-8 text-start text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
            aria-haspopup="false"
            onClick={() => setSelected(user)}
          >
            {displayName(user)}
            <span className="sr-only">، نمایش جزئیات</span>
          </button>
        ),
      },
      { key: 'username', header: 'نام کاربری', cell: (user) => (user.username ? <code dir="ltr" className="text-xs">{user.username}</code> : '—') },
      { key: 'account', header: 'حساب', cell: (user) => <StatusBadge status={user.userIsActive ? 'active' : 'inactive'} /> },
      {
        key: 'membership',
        header: 'عضویت در این شرکت',
        cell: (user) => <StatusBadge status={user.membershipIsActive ? 'active' : 'inactive'} />,
      },
      {
        key: 'role',
        header: 'گروه دسترسی',
        cell: (user) => (
          <span className="flex flex-col">
            <span>{user.roleName}</span>
            {!user.roleIsActive ? <span className="text-xs text-danger">گروه غیرفعال است</span> : null}
          </span>
        ),
      },
      { key: 'since', header: 'عضو از', cell: (user) => formatDate(user.membershipCreatedAtUtc) },
    ],
    [],
  );

  const forbidden = failure?.status === 403 || failure?.status === 404;
  const title = data?.companyName ?? companyName;
  const filtered = search !== '' || membership !== 'all';

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'شرکت' }, { label: 'کاربران شرکت' }]} />
        <h1 className="text-2xl font-extrabold tracking-tight text-title">کاربران شرکت</h1>
        <p className="text-sm text-sub">
          افرادی که در {title ? `شرکت «${title}»` : 'شرکت انتخاب‌شده'} عضویت دارند. این فهرست فقط‌خواندنی است و اطلاعات تماس یا ورود کاربران را نشان نمی‌دهد.
        </p>
      </header>

      {forbidden ? (
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure?.message}</p>
        </div>
      ) : (
        <>
          <form role="search" aria-label="جستجو در کاربران شرکت" onSubmit={(event) => event.preventDefault()} className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2">
            <FormField label="جستجو" hint="نام، نام خانوادگی یا نام کاربری">
              <Input id={searchId} value={searchText} maxLength={120} autoComplete="off" onChange={(event) => setSearchText(event.target.value)} />
            </FormField>
            <FormField label="وضعیت عضویت">
              <Select
                value={membership}
                onChange={(event) => {
                  setMembership(event.target.value as MembershipFilter);
                  setPage(1);
                }}
              >
                <option value="all">همه</option>
                <option value="active">فعال</option>
                <option value="inactive">غیرفعال</option>
              </Select>
            </FormField>
          </form>

          {selected ? (
            <section ref={detailRef} tabIndex={-1} aria-label={`جزئیات ${displayName(selected)}`} className="space-y-3 rounded-xl border border-line bg-white p-4 outline-none focus-visible:ring-2 focus-visible:ring-gold">
              <div className="flex items-start justify-between gap-3">
                <h2 className="text-lg font-extrabold text-title">{displayName(selected)}</h2>
                <button type="button" onClick={() => setSelected(null)} className="rounded-lg px-3 py-1 text-sm font-medium text-teal-deep outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold">
                  بستن جزئیات
                </button>
              </div>
              <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
                <div><dt className="text-sub">نام کاربری</dt><dd>{selected.username ? <code dir="ltr">{selected.username}</code> : '—'}</dd></div>
                <div><dt className="text-sub">وضعیت حساب</dt><dd><StatusBadge status={selected.userIsActive ? 'active' : 'inactive'} /></dd></div>
                <div><dt className="text-sub">عضویت در این شرکت</dt><dd><StatusBadge status={selected.membershipIsActive ? 'active' : 'inactive'} /></dd></div>
                <div><dt className="text-sub">عضو از</dt><dd>{formatDate(selected.membershipCreatedAtUtc)}</dd></div>
                <div>
                  <dt className="text-sub">گروه دسترسی</dt>
                  <dd>
                    {selected.roleName} <code dir="ltr" className="ms-1 text-xs text-sub">{selected.roleCode}</code>
                    {!selected.roleIsActive ? <span className="ms-2 text-xs text-danger">(غیرفعال)</span> : null}
                  </dd>
                </div>
              </dl>
              {!selected.membershipIsActive || !selected.userIsActive || !selected.roleIsActive ? (
                <Alert tone="info">با حساب، عضویت یا گروه غیرفعال، کاربر به داده‌های این شرکت دسترسی ندارد.</Alert>
              ) : null}
              <p className="text-xs text-sub">
                مجوزهای دقیق گروه و واحد/سمت سازمانی در این فهرست نیست.
                {canViewRoles ? (
                  <>
                    {' '}
                    <Link to={`/admin/roles/${encodeURIComponent(selected.roleId)}?company=${encodeURIComponent(companyId)}`} className="font-medium text-teal-deep underline-offset-4 hover:underline">
                      مشاهدهٔ مجوزهای این گروه
                    </Link>
                  </>
                ) : null}
              </p>
            </section>
          ) : null}

          <DataTable
            caption="فهرست کاربران شرکت"
            columns={columns}
            rows={data?.items ?? []}
            getRowKey={(user) => user.userId}
            loading={loading}
            error={failure?.message ?? null}
            onRetry={() => setAttempt((value) => value + 1)}
            emptyTitle="کاربری پیدا نشد"
            emptyDescription={filtered ? 'با جستجو یا فیلتر فعلی کاربری پیدا نشد.' : 'برای این شرکت هنوز کاربری ثبت نشده است.'}
            pagination={{
              page,
              pageSize,
              total: data?.totalCount ?? 0,
              pageSizeOptions: pageSizes,
              onPageChange: setPage,
              onPageSizeChange: (size) => {
                setPageSize(size);
                setPage(1);
              },
            }}
          />
        </>
      )}
    </div>
  );
}
