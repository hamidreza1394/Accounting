import { useEffect, useMemo, useState } from 'react';
import { ApiError } from '@/api/client';
import { Breadcrumb } from '@/components/ui/Breadcrumb';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { FormField, Input, Select } from '@/components/ui/FormField';
import { Button, buttonVariants } from '@/components/ui/button';
import { Link } from '@/lib/router';
import { formatHrInstant } from '../lib/hrFormat';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';
import { getHrUnitTree, listHrUnits } from '../api/hrClient';
import type { HrUnitItem, HrUnitTreeNode } from '../api/hrTypes';
import { HrActiveBadge } from '../components/HrBadges';
import { HrUnitTree } from '../components/HrUnitTree';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

const pageSizes = [10, 25, 50, 100] as const;
const searchDelayMs = 350;
type ViewMode = 'tree' | 'flat';

/**
 * Organizational units: the hierarchy view (`?view=tree`, includes inactive units) and the
 * searchable flat list (active units only, paged) — exactly the two read models the backend
 * exposes. Every row links to the unit's detail page; creation is gated by the UI hint of
 * `hr.organizational-units.create` (the server stays authoritative).
 */
export function HrUnitsPage({ onUnauthenticated, companyId }: Props) {
  const { codes } = useCompanyPermissions(companyId, onUnauthenticated);
  const canCreate = codes.includes('hr.organizational-units.create');

  const [view, setView] = useState<ViewMode>('tree');
  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);

  const [tree, setTree] = useState<readonly HrUnitTreeNode[] | null>(null);
  const [flat, setFlat] = useState<{ items: readonly HrUnitItem[]; totalCount: number } | null>(null);
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
    const request =
      view === 'tree'
        ? getHrUnitTree(companyId, controller.signal).then((result) => {
            setTree(result.roots);
            setFlat(null);
          })
        : listHrUnits(companyId, { view: 'flat', search, page, pageSize }, controller.signal).then((result) => {
            setFlat({ items: result.items, totalCount: result.totalCount });
            setTree(null);
          });
    request
      .then(() => {
        if (!controller.signal.aborted) setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 403
              ? 'شما مجوز مشاهدهٔ ساختار سازمانی این شرکت (hr.organizational-units.list) را ندارید.'
              : status === 404
                ? 'شرکت پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت واحدهای سازمانی انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, view, search, page, pageSize, attempt, onUnauthenticated]);

  const columns = useMemo<readonly DataTableColumn<HrUnitItem>[]>(
    () => [
      {
        key: 'name',
        header: 'واحد سازمانی',
        cell: (unit) => (
          <span className="flex flex-col">
            <span className="font-medium">{unit.name}</span>
            <code dir="ltr" className="text-[11px] text-sub">
              {unit.code}
            </code>
          </span>
        ),
      },
      { key: 'status', header: 'وضعیت', cell: (unit) => <HrActiveBadge isActive={unit.isActive} /> },
      { key: 'validFrom', header: 'شروع اعتبار', cell: (unit) => formatHrInstant(unit.validFromUtc) },
      { key: 'validTo', header: 'پایان اعتبار', cell: (unit) => formatHrInstant(unit.validToUtc) },
    ],
    [],
  );

  const forbidden = failure?.status === 403 || failure?.status === 404;
  const filtered = search !== '';

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'منابع انسانی' }, { label: 'واحدهای سازمانی' }]} />
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-2xl font-extrabold tracking-tight text-title">واحدهای سازمانی</h1>
            <p className="mt-1 text-sm text-sub">
              ساختار درختی واحدهای این شرکت. هر واحد فقط به واحدهای همین شرکت متصل می‌شود و سمت‌ها مستقل از واحدها تعریف می‌شوند.
            </p>
          </div>
          {canCreate ? (
            <Link to="/hr/org/units/new" className={buttonVariants({ size: 'lg' })}>
              واحد جدید
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
          <div className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2 lg:grid-cols-3">
            <FormField label="نحوهٔ نمایش">
              <Select value={view} onChange={(event) => setView(event.target.value as ViewMode)}>
                <option value="tree">درخت سازمانی</option>
                <option value="flat">فهرست (جستجو و صفحه‌بندی)</option>
              </Select>
            </FormField>
            {view === 'flat' ? (
              <FormField label="جستجو" hint="کد یا عنوان واحد">
                <Input value={searchText} maxLength={120} autoComplete="off" onChange={(event) => setSearchText(event.target.value)} />
              </FormField>
            ) : null}
          </div>

          {view === 'tree' ? (
            <div className="rounded-xl border border-line bg-white p-4">
              {loading ? (
                <p role="status" className="text-sm text-sub">
                  در حال بارگذاری…
                </p>
              ) : failure ? (
                <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft p-5 text-danger">
                  <p className="font-medium">{failure.message}</p>
                  <Button type="button" variant="outline" size="lg" className="mt-3" onClick={() => setAttempt((value) => value + 1)}>
                    تلاش دوباره
                  </Button>
                </div>
              ) : (
                <HrUnitTree
                  nodes={tree ?? []}
                  detailHref={(id) => `/hr/org/units/${id}`}
                  emptyLabel="هنوز واحدی ثبت نشده است"
                />
              )}
            </div>
          ) : (
            <DataTable
              caption="فهرست واحدهای سازمانی"
              columns={columns}
              rows={flat?.items ?? []}
              getRowKey={(unit) => unit.id}
              loading={loading}
              error={failure?.message ?? null}
              onRetry={() => setAttempt((value) => value + 1)}
              rowHref={(unit) => `/hr/org/units/${unit.id}`}
              emptyTitle="واحدی پیدا نشد"
              emptyDescription={filtered ? 'با جستجوی فعلی واحدی پیدا نشد.' : 'هنوز واحدی ثبت نشده است.'}
              pagination={{
                page,
                pageSize,
                total: flat?.totalCount ?? 0,
                pageSizeOptions: pageSizes,
                onPageChange: setPage,
                onPageSizeChange: (size) => {
                  setPageSize(size);
                  setPage(1);
                },
              }}
            />
          )}
        </>
      )}
    </div>
  );
}
