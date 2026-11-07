import { useEffect, useMemo, useState } from 'react';
import { ApiError } from '@/api/client';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { FormField, Input, Select } from '@/components/ui/FormField';
import { buttonVariants } from '@/components/ui/button';
import { Link } from '@/lib/router';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';
import { listHrEmployees, getHrUnitTree } from '../api/hrClient';
import type { HrEmployeeListItem, HrUnitTreeNode } from '../api/hrTypes';
import { HrEmploymentStatusBadge } from '../components/HrBadges';
import { formatHrInstant } from '../lib/hrFormat';
import { hrEmploymentStatusLabel } from '../lib/hrLabels';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

const pageSizes = [10, 25, 50, 100] as const;
const searchDelayMs = 350;
type StatusFilter = 'all' | '1' | '2' | '3';

/** Active units of the tree as the unit filter options. */
function unitFilterOptions(roots: readonly HrUnitTreeNode[], depth = 0, out: { key: string; label: string }[] = []): { key: string; label: string }[] {
  for (const node of roots) {
    if (node.isActive) {
      out.push({ key: node.id, label: `${'\u2003'.repeat(depth)}${node.name} (${node.code})` });
      unitFilterOptions(node.children, depth + 1, out);
    }
  }
  return out;
}

/**
 * The employee list (`GET /org/employees`): server-side search (name, family, employee number),
 * employment-status filter and the unit filter (active assignment). The `hasLinkedUser` flag is
 * shown without ever exposing a user identity (the list DTO has none). Employees without an
 * account appear exactly like everyone else — a login is optional (D-072).
 */
export function HrEmployeesPage({ onUnauthenticated, companyId }: Props) {
  const { codes } = useCompanyPermissions(companyId, onUnauthenticated);
  const canCreate = codes.includes('hr.employees.create');

  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<StatusFilter>('all');
  const [unitId, setUnitId] = useState<string>('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [data, setData] = useState<{ items: readonly HrEmployeeListItem[]; totalCount: number } | null>(null);
  const [unitOptions, setUnitOptions] = useState<{ key: string; label: string }[]>([]);
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

  // The unit filter comes from the real tree of THIS company (active units only).
  useEffect(() => {
    const controller = new AbortController();
    getHrUnitTree(companyId, controller.signal)
      .then((tree) => {
        if (!controller.signal.aborted) setUnitOptions(unitFilterOptions(tree.roots));
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        // The list still works without the filter options; the error state stays on the table.
        setUnitOptions([]);
      });
    return () => controller.abort();
  }, [companyId, onUnauthenticated]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    listHrEmployees(
      companyId,
      {
        ...(search === '' ? {} : { search }),
        ...(status === 'all' ? {} : { status: Number(status) as 1 | 2 | 3 }),
        ...(unitId === '' ? {} : { unitId }),
        page,
        pageSize,
      },
      controller.signal,
    )
      .then((result) => {
        if (controller.signal.aborted) return;
        setData({ items: result.items, totalCount: result.totalCount });
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const statusCode = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status: statusCode,
          message:
            statusCode === 403
              ? 'شما مجوز مشاهدهٔ فهرست کارکنان این شرکت (hr.employees.list) را ندارید.'
              : statusCode === 404
                ? 'شرکت پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت فهرست کارکنان انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, search, status, unitId, page, pageSize, attempt, onUnauthenticated]);

  const columns = useMemo<readonly DataTableColumn<HrEmployeeListItem>[]>(
    () => [
      {
        key: 'name',
        header: 'کارمند',
        cell: (employee) => (
          <span className="flex flex-col">
            <span className="font-medium">
              {employee.firstName} {employee.lastName}
            </span>
            <code dir="ltr" className="text-[11px] text-sub">
              {employee.employeeNumber}
            </code>
          </span>
        ),
      },
      { key: 'status', header: 'وضعیت اشتغال', cell: (employee) => <HrEmploymentStatusBadge status={employee.employmentStatus} /> },
      {
        key: 'unit',
        header: 'واحد جاری',
        cell: (employee) => (employee.unitName ?? '—'),
      },
      {
        key: 'position',
        header: 'سمت جاری',
        cell: (employee) => (employee.positionName ?? '—'),
      },
      { key: 'hiredOn', header: 'تاریخ همکاری', cell: (employee) => formatHrInstant(employee.hiredOnUtc) },
      {
        key: 'account',
        header: 'حساب کاربری',
        cell: (employee) =>
          employee.hasLinkedUser ? (
            <span className="text-success">متصل</span>
          ) : (
            <span className="text-sub">بدون حساب</span>
          ),
      },
    ],
    [],
  );

  const forbidden = failure?.status === 403 || failure?.status === 404;
  const filtered = search !== '' || status !== 'all' || unitId !== '';

  usePageBreadcrumb([{ label: 'منابع انسانی' }, { label: 'کارکنان' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-2xl font-extrabold tracking-tight text-title">کارکنان</h1>
            <p className="mt-1 text-sm text-sub">
              فهرست کارکنان این شرکت. ثبت کارمند نیازی به حساب کاربری ندارد؛ اتصال حساب، اختیاری و جداگانه است.
            </p>
          </div>
          {canCreate ? (
            <Link to="/hr/org/employees/new" className={buttonVariants({ size: 'lg' })}>
              کارمند جدید
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
          <form
            role="search"
            aria-label="جستجو و فیلتر کارکنان"
            onSubmit={(event) => event.preventDefault()}
            className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2 lg:grid-cols-4"
          >
            <FormField label="جستجو" hint="نام، نام خانوادگی یا شماره پرسنلی">
              <Input value={searchText} maxLength={120} autoComplete="off" onChange={(event) => setSearchText(event.target.value)} />
            </FormField>
            <FormField label="وضعیت اشتغال">
              <Select
                value={status}
                onChange={(event) => {
                  setStatus(event.target.value as StatusFilter);
                  setPage(1);
                }}
              >
                <option value="all">همه</option>
                <option value="1">{hrEmploymentStatusLabel[1]}</option>
                <option value="2">{hrEmploymentStatusLabel[2]}</option>
                <option value="3">{hrEmploymentStatusLabel[3]}</option>
              </Select>
            </FormField>
            <FormField label="واحد سازمانی">
              <Select
                value={unitId}
                onChange={(event) => {
                  setUnitId(event.target.value);
                  setPage(1);
                }}
              >
                <option value="">همه واحدها</option>
                {unitOptions.map((option) => (
                  <option key={option.key} value={option.key}>
                    {option.label}
                  </option>
                ))}
              </Select>
            </FormField>
          </form>

          <DataTable
            caption="فهرست کارکنان"
            columns={columns}
            rows={data?.items ?? []}
            getRowKey={(employee) => employee.id}
            loading={loading}
            error={failure?.message ?? null}
            onRetry={() => setAttempt((value) => value + 1)}
            rowHref={(employee) => `/hr/org/employees/${employee.id}`}
            emptyTitle="کارمندی پیدا نشد"
            emptyDescription={filtered ? 'با جستجو یا فیلتر فعلی کارمندی پیدا نشد.' : 'هنوز کارمندی ثبت نشده است.'}
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
