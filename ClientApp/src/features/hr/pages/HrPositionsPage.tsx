import { useEffect, useMemo, useState } from 'react';
import { ApiError } from '@/api/client';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { FormField, Input } from '@/components/ui/FormField';
import { buttonVariants } from '@/components/ui/button';
import { Link } from '@/lib/router';
import { can } from '@/lib/permissions';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';
import { listHrPositions } from '../api/hrClient';
import type { HrPositionItem } from '../api/hrTypes';
import { HrActiveBadge } from '../components/HrBadges';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

const pageSizes = [10, 25, 50, 100] as const;
const searchDelayMs = 350;
const persianNumber = new Intl.NumberFormat('fa-IR');

/**
 * Positions of the company (`GET /org/positions`). A position is an organizational job title —
 * it is NOT a security role and never carries permissions (D-064/D-072); the page says so
 * explicitly so no one confuses the two concepts.
 */
export function HrPositionsPage({ onUnauthenticated, companyId }: Props) {
  const { access } = useCompanyPermissions(companyId, onUnauthenticated);
  // The standard hint (Issue #444 stage 1): a ready installation administrator passes without a
  // company code — the same dual-mode rule the server enforces for every `hr.*` operation.
  const canCreate = can(access, 'hr.positions.create');

  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [data, setData] = useState<{ items: readonly HrPositionItem[]; totalCount: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);

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
    listHrPositions(companyId, { search, page, pageSize }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setData({ items: result.items, totalCount: result.totalCount });
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 403
              ? 'شما مجوز مشاهدهٔ سمت‌های این شرکت (hr.positions.list) را ندارید.'
              : status === 404
                ? 'شرکت پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت سمت‌ها انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, search, page, pageSize, attempt, onUnauthenticated]);

  const columns = useMemo<readonly DataTableColumn<HrPositionItem>[]>(
    () => [
      {
        key: 'name',
        header: 'سمت',
        cell: (position) => (
          <span className="flex flex-col">
            <span className="font-medium">{position.name}</span>
            <code dir="ltr" className="text-[11px] text-sub">
              {position.code}
            </code>
          </span>
        ),
      },
      { key: 'rank', header: 'رتبه', cell: (position) => (position.rank === null ? '—' : persianNumber.format(position.rank)) },
      { key: 'status', header: 'وضعیت', cell: (position) => <HrActiveBadge isActive={position.isActive} /> },
    ],
    [],
  );

  const forbidden = failure?.status === 403 || failure?.status === 404;

  usePageBreadcrumb([{ label: 'منابع انسانی' }, { label: 'سمت‌های سازمانی' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-2xl font-extrabold tracking-tight text-title">سمت‌های سازمانی</h1>
            <p className="mt-1 text-sm text-sub">
              سمت، عنوان شغلی در ساختار سازمانی است و با «گروه دسترسی» فرق دارد: سمت هیچ مجوز دسترسی‌ای به سیستم نمی‌دهد.
            </p>
          </div>
          {canCreate ? (
            <Link to="/hr/org/positions/new" className={buttonVariants({ size: 'lg' })}>
              سمت جدید
            </Link>
          ) : null}
        </div>
      </header>

      {forbidden ? (
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure?.message}</p>
        </div>
      ) : (
        <>
          <form role="search" aria-label="جستجو در سمت‌ها" onSubmit={(event) => event.preventDefault()} className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2">
            <FormField label="جستجو" hint="کد یا عنوان سمت">
              <Input value={searchText} maxLength={120} autoComplete="off" onChange={(event) => setSearchText(event.target.value)} />
            </FormField>
          </form>

          <DataTable
            caption="فهرست سمت‌های سازمانی"
            columns={columns}
            rows={data?.items ?? []}
            getRowKey={(position) => position.id}
            loading={loading}
            error={failure?.message ?? null}
            onRetry={() => setAttempt((value) => value + 1)}
            rowHref={(position) => `/hr/org/positions/${position.id}`}
            emptyTitle="سمتی پیدا نشد"
            emptyDescription={search !== '' ? 'با جستجوی فعلی سمتی پیدا نشد.' : 'هنوز سمتی ثبت نشده است.'}
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
