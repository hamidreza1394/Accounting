import { useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, getCompanyRoles } from '../../../api/client';
import type { CompanyRole } from '../../../api/types';
import type { CanHint } from '../../../lib/permissions';
import { Link, useSearchParams } from '../../../lib/router';
import { Badge } from '../../ui/Badge';
import { Breadcrumb } from '../../ui/Breadcrumb';
import { DataTable, type DataTableColumn } from '../../ui/DataTable';
import { StatusBadge } from '../../ui/StatusBadge';
import { buttonVariants } from '../../ui/button';
import { CompanySelect } from './CompanySelect';
import { newRolePath, resolveCompanyId, rolePath, useCompanyChoices } from './useCompanyChoices';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** The active work-context company; used when the URL names none. */
  defaultCompanyId: string;
  /** UI hint only (the server decides): shows the create button. Without it nothing is offered. */
  can?: CanHint;
  /** Called when the server answers 403, so the permission hints are reloaded (never trusted over the server). */
  onForbidden?: (() => void) | undefined;
};

const number = new Intl.NumberFormat('fa-IR');
const denyAll: CanHint = () => false;

type Failure = { message: string; status: number | null };

function toFailure(cause: unknown): Failure {
  if (cause instanceof ApiError) {
    if (cause.status === 403) return { message: 'شما مجوز مشاهدهٔ گروه‌های دسترسی را ندارید.', status: 403 };
    if (cause.status === 404) return { message: 'شرکت پیدا نشد یا فعال نیست.', status: 404 };
    return { message: cause.message, status: cause.status };
  }
  return { message: 'دریافت گروه‌های دسترسی انجام نشد. لطفاً دوباره تلاش کنید.', status: null };
}

export function RolesListPage({ onUnauthenticated, defaultCompanyId, can = denyAll, onForbidden }: Props) {
  const [params, setParams] = useSearchParams();
  const companyId = resolveCompanyId(params, defaultCompanyId);
  const { state: choices, retry: retryCompanies } = useCompanyChoices(onUnauthenticated);
  const [roles, setRoles] = useState<CompanyRole[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [attempt, setAttempt] = useState(0);
  const onForbiddenRef = useRef(onForbidden);
  onForbiddenRef.current = onForbidden;

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    setRoles(null);
    getCompanyRoles(companyId, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setRoles(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        if (cause instanceof ApiError && cause.status === 403) onForbiddenRef.current?.();
        setFailure(toFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, attempt, onUnauthenticated]);

  const columns = useMemo<readonly DataTableColumn<CompanyRole>[]>(
    () => [
      { key: 'name', header: 'عنوان گروه', cell: (role) => role.name },
      { key: 'code', header: 'کد', cell: (role) => <code dir="ltr" className="text-xs">{role.code}</code> },
      {
        key: 'type',
        header: 'نوع',
        cell: (role) => (role.isSystemRole ? <StatusBadge status="readonly">سیستمی</StatusBadge> : <Badge tone="info" icon={null}>سفارشی</Badge>),
      },
      { key: 'status', header: 'وضعیت', cell: (role) => <StatusBadge status={role.isActive ? 'active' : 'inactive'} /> },
      { key: 'permissions', header: 'تعداد مجوز', align: 'center', cell: (role) => number.format(role.permissionCount) },
      { key: 'members', header: 'اعضای فعال', align: 'center', cell: (role) => number.format(role.activeMemberCount) },
    ],
    [],
  );

  const companyName = choices.kind === 'ready' ? choices.companies.find((company) => company.id === companyId)?.name : undefined;

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'مدیریت سیستم' }, { label: 'گروه‌های دسترسی' }]} />
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 space-y-2">
            <h1 className="text-2xl font-extrabold tracking-tight text-title">گروه‌های دسترسی</h1>
            <p className="text-sm text-sub">
              گروه‌های دسترسی{companyName ? ` شرکت «${companyName}»` : ' شرکت انتخاب‌شده'}؛ برای مشاهده یا ویرایش مجوزهای یک گروه، آن را انتخاب کنید.
            </p>
          </div>
          {companyId && can('admin.roles.create', companyId) ? (
            <Link to={newRolePath(companyId)} className={buttonVariants({ size: 'lg' })}>
              ایجاد گروه دسترسی
            </Link>
          ) : null}
        </div>
      </header>

      <CompanySelect
        choices={choices}
        value={companyId}
        onChange={(next) => setParams(new URLSearchParams({ company: next }))}
        onRetry={retryCompanies}
      />

      {failure?.status === 404 ? (
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">شرکت پیدا نشد</p>
          <p className="mt-2 text-sm text-sub">{failure.message}</p>
        </div>
      ) : (
        <DataTable
          caption="فهرست گروه‌های دسترسی"
          columns={columns}
          rows={roles ?? []}
          getRowKey={(role) => role.id}
          loading={loading}
          error={failure?.message ?? null}
          {...(failure && failure.status !== 403 ? { onRetry: () => setAttempt((value) => value + 1) } : {})}
          emptyTitle="گروه دسترسی‌ای ثبت نشده است"
          emptyDescription="برای این شرکت هنوز گروهی تعریف نشده است."
          rowHref={(role) => rolePath(companyId, role.id)}
        />
      )}
    </div>
  );
}
