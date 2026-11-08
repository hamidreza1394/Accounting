import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ApiError, getCompanyRoles } from '../../../api/client';
import type { CompanyRole } from '../../../api/types';
import type { CanHint } from '../../../lib/permissions';
import { Link, useSearchParams } from '../../../lib/router';
import { Badge } from '../../ui/Badge';
import { diffCodes } from '../../../lib/permissionTree';
import { FormField, Select } from '../../ui/FormField';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
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
  const canCreate = companyId !== '' && can('admin.roles.create', companyId);
  // Client-side comparison of two roles of the same company; the data comes from the same
  // server list already loaded for the table — no extra request, no new API.
  const [compareA, setCompareA] = useState('');
  const [compareB, setCompareB] = useState('');
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
      {
        key: 'copy',
        header: 'کپی',
        align: 'center',
        cell: (role) =>
          role.isSystemRole ? (
            <span className="text-xs text-sub">—</span>
          ) : canCreate ? (
            <Link
              to={newRolePath(companyId, role.id)}
              className="inline-flex min-h-11 items-center rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold md:min-h-0"
              aria-label={`کپی گروه ${role.name}`}
            >
              کپی…
            </Link>
          ) : null,
      },
    ],
    [canCreate, companyId],
  );

  const companyName = choices.kind === 'ready' ? choices.companies.find((company) => company.id === companyId)?.name : undefined;

  usePageBreadcrumb([{ label: 'مدیریت سیستم' }, { label: 'گروه‌های دسترسی' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
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

      {roles !== null && roles.length >= 2 ? <RoleCompare roles={roles} a={compareA} b={compareB} onA={setCompareA} onB={setCompareB} /> : null}
    </div>
  );
}

const compareSelectHint = 'فقط گروه‌های همین شرکت مقایسه می‌شوند؛ مقایسه فقط نمایشی است و هیچ تغییری اعمال نمی‌کند.';

function RoleCompare({
  roles,
  a,
  b,
  onA,
  onB,
}: {
  roles: CompanyRole[];
  a: string;
  b: string;
  onA: (value: string) => void;
  onB: (value: string) => void;
}) {
  const roleA = roles.find((role) => role.id === a) ?? null;
  const roleB = roles.find((role) => role.id === b) ?? null;
  const both = roleA !== null && roleB !== null;
  const diff = useMemo(() => (both ? diffCodes(roleA!.permissionCodes, roleB!.permissionCodes) : null), [roleA, roleB, both]);
  const comparisonId = useId();

  return (
    <section aria-labelledby={comparisonId} className="space-y-3 rounded-xl border border-line bg-white p-5">
      <h2 id={comparisonId} className="text-lg font-extrabold text-title">
        مقایسهٔ دو گروه
      </h2>
      <p className="text-sm text-sub">{compareSelectHint}</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-56 flex-1">
          <FormField label="گروه اول">
            <Select value={a} onChange={(event) => onA(event.target.value)}>
              <option value="">انتخاب کنید</option>
              {roles.map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name}
                </option>
              ))}
            </Select>
          </FormField>
        </div>
        <div className="min-w-56 flex-1">
          <FormField label="گروه دوم">
            <Select value={b} onChange={(event) => onB(event.target.value)}>
              <option value="">انتخاب کنید</option>
              {roles.map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name}
                </option>
              ))}
            </Select>
          </FormField>
        </div>
      </div>
      {diff && roleA && roleB ? (
        <div className="space-y-2 text-sm">
          <p className="text-sub">
            مشترک: <span className="font-semibold text-ink">{number.format(roleA.permissionCodes.length - diff.removed.length)}</span> مجوز
          </p>
          <div className="grid gap-3 md:grid-cols-2">
            <div className="rounded-lg border border-line p-3">
              <h3 className="text-sm font-bold text-success">فقط در «{roleA.name}» ({number.format(diff.removed.length)})</h3>
              {diff.removed.length === 0 ? (
                <p className="mt-1 text-xs text-sub">هیچ</p>
              ) : (
                <ul className="mt-1 space-y-1">
                  {diff.removed.map((code) => (
                    <li key={code} className="text-xs text-ink">
                      <bdi dir="ltr">{code}</bdi>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className="rounded-lg border border-line p-3">
              <h3 className="text-sm font-bold text-danger">فقط در «{roleB.name}» ({number.format(diff.added.length)})</h3>
              {diff.added.length === 0 ? (
                <p className="mt-1 text-xs text-sub">هیچ</p>
              ) : (
                <ul className="mt-1 space-y-1">
                  {diff.added.map((code) => (
                    <li key={code} className="text-xs text-ink">
                      <bdi dir="ltr">{code}</bdi>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>
      ) : both || roleA || roleB ? (
        <p className="text-sm text-sub">برای مقایسه، هر دو گروه را انتخاب کنید.</p>
      ) : null}
    </section>
  );
}
