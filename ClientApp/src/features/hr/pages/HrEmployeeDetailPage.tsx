import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import type { BreadcrumbItem } from '@/components/ui/Breadcrumb';
import { Button, buttonVariants } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { Tabs } from '@/components/ui/Tabs';
import { Link, useDocumentTitle, useParams } from '@/lib/router';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';
import { can } from '@/lib/permissions';
import { deactivateHrEmployee, getHrEmployee, listHrAssignments } from '../api/hrClient';
import type { HrAssignmentItem, HrEmployeeDetail } from '../api/hrTypes';
import { formatHrInstant } from '../lib/hrFormat';
import { HrAssignmentStatusBadge, HrEmploymentStatusBadge } from '../components/HrBadges';
import { HrAssignmentSection } from '../components/HrAssignmentSection';
import { HrHistoryList } from '../components/HrHistoryList';
import { HrUserLinkSection } from '../components/HrUserLinkSection';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

const assignmentPageSizes = [10, 25, 50] as const;

/**
 * The employee file. Four tabs, each backed by exactly one real read model: the detail DTO
 * (`GET /org/employees/{id}` — the rowVersion holder), the assignment list
 * (`GET /org/assignments?employeeId=`), the account link section and the audit history. The
 * termination action is here because the server exposes it on this resource and makes it
 * irreversible (it also ends the active assignment in the same transaction).
 */
export function HrEmployeeDetailPage({ onUnauthenticated, companyId }: Props) {
  const { employeeId } = useParams();
  const { access } = useCompanyPermissions(companyId, onUnauthenticated);
  // The standard hint (Issue #444 stage 1): a ready installation administrator passes without a
  // company code — the same dual-mode rule the server enforces for every `hr.*` operation.
  const canUpdate = can(access, 'hr.employees.update');

  const [employee, setEmployee] = useState<HrEmployeeDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [tab, setTab] = useState('structure');

  const [confirmingTerminate, setConfirmingTerminate] = useState(false);
  const [terminating, setTerminating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const reload = useCallback(() => setAttempt((value) => value + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getHrEmployee(companyId, employeeId ?? '', controller.signal)
      .then((detail) => {
        if (controller.signal.aborted) return;
        setEmployee(detail);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 403
              ? 'شما مجوز مشاهدهٔ پروندهٔ کارکنان این شرکت (hr.employees.view) را ندارید.'
              : status === 404
                ? 'کارمند پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت پروندهٔ کارمند انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, employeeId, attempt, onUnauthenticated]);

  useDocumentTitle(employee ? `${employee.firstName} ${employee.lastName}` : null);

  const terminate = async () => {
    if (employee === null) return;
    setTerminating(true);
    setActionError(null);
    try {
      await deactivateHrEmployee(companyId, employee.id);
      setConfirmingTerminate(false);
      reload();
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setActionError(cause instanceof ApiError ? cause.message : 'خاتمه همکاری انجام نشد.');
      setConfirmingTerminate(false);
    } finally {
      setTerminating(false);
    }
  };

  const assignmentColumns = useMemo<readonly DataTableColumn<HrAssignmentItem>[]>(
    () => [
      { key: 'unit', header: 'واحد سازمانی', cell: (item) => item.unitName ?? '—' },
      { key: 'position', header: 'سمت', cell: (item) => item.positionName ?? '—' },
      {
        key: 'manager',
        header: 'مدیر',
        cell: (item) =>
          item.managerEmployeeId ? (
            <code dir="ltr" className="text-xs">
              {item.managerEmployeeId}
            </code>
          ) : (
            '—'
          ),
      },
      { key: 'start', header: 'شروع', cell: (item) => formatHrInstant(item.startOnUtc) },
      { key: 'end', header: 'پایان', cell: (item) => formatHrInstant(item.endOnUtc) },
      { key: 'status', header: 'وضعیت', cell: (item) => <HrAssignmentStatusBadge status={item.status} /> },
    ],
    [],
  );

  const crumbs: BreadcrumbItem[] = [{ label: 'منابع انسانی' }, { label: 'کارکنان', to: '/hr/org/employees' }, { label: employee ? `${employee.firstName} ${employee.lastName}` : 'پروندهٔ کارمند' }];
  usePageBreadcrumb(crumbs);
  if (loading) {
    return (
      <div className="space-y-4">
        <p role="status" className="rounded-xl border border-line bg-white p-5 text-sub">
          در حال بارگذاری…
        </p>
      </div>
    );
  }

  if (failure || employee === null) {
    return (
      <div className="space-y-4">
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">یافت نشد یا دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure?.message}</p>
          <Link to="/hr/org/employees" className={buttonVariants({ variant: 'outline', size: 'lg', className: 'mt-4' })}>
            بازگشت به فهرست
          </Link>
        </div>
      </div>
    );
  }

  const terminated = employee.employmentStatus === 3;
  const fullName = `${employee.firstName} ${employee.lastName}`;

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-extrabold tracking-tight text-title">{fullName}</h1>
            <code dir="ltr" className="rounded-md bg-cool px-2 py-1 text-xs text-sub">
              {employee.employeeNumber}
            </code>
            <HrEmploymentStatusBadge status={employee.employmentStatus} />
          </div>
          {canUpdate && !terminated ? (
            <div className="flex flex-wrap gap-2">
              <Link to={`/hr/org/employees/${employee.id}/edit`} className={buttonVariants({ variant: 'outline', size: 'lg' })}>
                ویرایش اطلاعات
              </Link>
              <Button
                type="button"
                variant="outline"
                size="lg"
                disabled={terminating}
                className="border-danger/40 text-danger hover:bg-danger-soft"
                onClick={() => setConfirmingTerminate(true)}
              >
                خاتمه همکاری
              </Button>
            </div>
          ) : null}
        </div>
      </header>

      {terminated ? (
        <Alert tone="warning">همکاری این کارمند خاتمه شده است. رکورد و تاریخچه فقط‌خواندنی‌اند و فعال‌سازی مجدد از مسیر سرور ممکن نیست.</Alert>
      ) : null}
      {actionError ? <Alert tone="error">{actionError}</Alert> : null}

      <Tabs
        label="بخش‌های پروندهٔ کارمند"
        value={tab}
        onValueChange={setTab}
        items={[
          {
            id: 'structure',
            label: 'ساختار سازمانی',
            content: (
              <div className="space-y-5">
                <HrAssignmentSection companyId={companyId} employee={employee} onUnauthenticated={onUnauthenticated} onChanged={reload} />
                <section className="space-y-3">
                  <h2 className="text-lg font-bold text-ink">تاریخچه انتساب‌ها</h2>
                  <AssignmentHistoryTable companyId={companyId} employeeId={employee.id} onUnauthenticated={onUnauthenticated} columns={assignmentColumns} />
                </section>
              </div>
            ),
          },
          {
            id: 'account',
            label: 'حساب کاربری',
            content: <HrUserLinkSection companyId={companyId} employee={employee} onUnauthenticated={onUnauthenticated} onChanged={reload} />,
          },
          {
            id: 'info',
            label: 'اطلاعات پایه',
            content: (
              <div className="grid gap-4 rounded-xl border border-line bg-white p-5 sm:grid-cols-2">
                <InfoRow label="شماره پرسنلی" value={<code dir="ltr" className="text-sm">{employee.employeeNumber}</code>} />
                <InfoRow label="نام و نام خانوادگی" value={fullName} />
                <InfoRow label="وضعیت اشتغال" value={<HrEmploymentStatusBadge status={employee.employmentStatus} />} />
                <InfoRow label="تاریخ شروع همکاری" value={formatHrInstant(employee.hiredOnUtc)} />
                <InfoRow label="تاریخ خاتمه" value={formatHrInstant(employee.terminatedOnUtc)} />
                <InfoRow label="تاریخ ایجاد رکورد" value={formatHrInstant(employee.createdAtUtc)} />
                <InfoRow
                  label="اتصال به حساب کاربری"
                  value={employee.linkedUserId === null ? <span className="text-sub">بدون حساب کاربری</span> : <code dir="ltr" className="text-xs">{employee.linkedUserId}</code>}
                />
                <InfoRow
                  label="توکن هم‌زمانی (rowVersion)"
                  value={
                    <code dir="ltr" className="text-xs text-sub">
                      {employee.rowVersion}
                    </code>
                  }
                />
              </div>
            ),
          },
          {
            id: 'history',
            label: 'تاریخچه رخدادها',
            content: <HrHistoryList companyId={companyId} employeeId={employee.id} onUnauthenticated={onUnauthenticated} />,
          },
        ]}
      />

      <ConfirmDialog
        open={confirmingTerminate}
        title="خاتمه همکاری کارمند"
        description="خاتمه همکاری برگشت‌پذیر نیست؛ انتساب فعال کارمند هم در همان تراکنش پایان می‌یابد و رکورد پس از آن فقط‌خواندنی است."
        confirmLabel="خاتمه همکاری"
        tone="danger"
        busy={terminating}
        onCancel={() => setConfirmingTerminate(false)}
        onConfirm={() => terminate()}
      />
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt className="text-sm text-sub">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium text-ink">{value}</dd>
    </div>
  );
}

/** The assignment history of one employee (`GET /org/assignments?employeeId=`), paged. */
function AssignmentHistoryTable({
  companyId,
  employeeId,
  onUnauthenticated,
  columns,
}: {
  companyId: string;
  employeeId: string;
  onUnauthenticated: (error: unknown) => boolean;
  columns: readonly DataTableColumn<HrAssignmentItem>[];
}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [items, setItems] = useState<readonly HrAssignmentItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    listHrAssignments(companyId, { employeeId }, page, pageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems(result.items);
        setTotal(result.totalCount);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setError(cause instanceof ApiError ? cause.message : 'دریافت تاریخچه انتساب‌ها انجام نشد.');
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, employeeId, page, pageSize, attempt, onUnauthenticated]);

  return (
    <DataTable
      caption="تاریخچه انتساب‌های کارمند"
      columns={columns}
      rows={items}
      getRowKey={(item) => item.id}
      loading={loading}
      error={error}
      onRetry={() => setAttempt((value) => value + 1)}
      emptyTitle="انتسابی ثبت نشده است"
      emptyDescription="با شروع نخستین انتساب، سابقهٔ سازمانی کارمند اینجا ثبت و حفظ می‌شود."
      pagination={{
        page,
        pageSize,
        total,
        pageSizeOptions: assignmentPageSizes,
        onPageChange: setPage,
        onPageSizeChange: (size) => {
          setPageSize(size);
          setPage(1);
        },
      }}
    />
  );
}
