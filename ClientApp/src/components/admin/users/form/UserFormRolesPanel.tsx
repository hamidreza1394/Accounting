import { useId, useState } from 'react';
import { ApiError, assignCompanyRoleMember, getCompanyRoles } from '@/api/client';
import type { AdminUserDetails, AdminUserMembership, CompanyRole } from '@/api/types';
import { EmptyState } from '@/components/Feedback';
import { Alert } from '@/components/ui/Alert';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { Select } from '@/components/ui/FormField';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Button } from '@/components/ui/button';
import { Link } from '@/lib/router';
import { TabDataBoundary, useProfileTabData } from '../profile/profileShared';
import { defaultMembershipCompany } from './UserFormHrPanel';
import { accessManagementPath, employeeLinkKey, loadLinkedEmployee, type FormTabCache, type UserFormContext } from './userFormShared';

type PanelProps = {
  user: AdminUserDetails;
  /** The memberships as the server last reported them (refreshed after a role assignment). */
  memberships: readonly AdminUserMembership[];
  cache: FormTabCache;
  context: UserFormContext;
  onUnauthenticated: (error: unknown) => boolean;
  /** Called after a successful assignment so the page reloads the memberships from the server. */
  onMembershipsChanged: () => void;
};

const membershipColumns: readonly DataTableColumn<AdminUserMembership>[] = [
  { key: 'company', header: 'شرکت', cell: (membership) => membership.companyName },
  {
    key: 'role',
    header: 'نقش نرم‌افزاری',
    cell: (membership) => (
      <span>
        {membership.roleName} <span dir="ltr" className="text-xs text-sub">({membership.roleCode})</span>
      </span>
    ),
  },
  { key: 'status', header: 'وضعیت عضویت', cell: (membership) => <StatusBadge status={membership.isActive ? 'active' : 'inactive'} /> },
];

function assignFailure(cause: unknown): string {
  if (cause instanceof ApiError) return cause.status === 429 ? 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.' : cause.message;
  return 'تخصیص نقش انجام نشد. لطفاً دوباره تلاش کنید.';
}

/** Shows the organizational position of the HR record (read-only; a position alone grants nothing). */
function PositionLine({ user, companyId, cache, onUnauthenticated }: { user: AdminUserDetails; companyId: string; cache: FormTabCache; onUnauthenticated: (error: unknown) => boolean }) {
  const state = useProfileTabData(cache, employeeLinkKey(user.id, companyId), (signal) => loadLinkedEmployee(companyId, user.id, signal), onUnauthenticated);
  return (
    <TabDataBoundary state={state} label="در حال دریافت سمت سازمانی…">
      {(employee) => (
        <p className="text-sm text-ink">
          سمت سازمانی: <strong>{employee?.positionName ?? (employee ? 'سمتی تعیین نشده است' : 'پروندهٔ پرسنلی متصل نشده است')}</strong>
        </p>
      )}
    </TabDataBoundary>
  );
}

function RoleAssignment({ user, companyId, companyName, memberships, cache, onUnauthenticated, onMembershipsChanged }: PanelProps & { companyId: string; companyName: string }) {
  const rolesKey = `${user.id}:form-roles:${companyId}`;
  const state = useProfileTabData<CompanyRole[]>(cache, rolesKey, (signal) => getCompanyRoles(companyId, signal), onUnauthenticated);
  const selectId = useId();
  const [roleId, setRoleId] = useState('');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const current = memberships.find((membership) => membership.companyId === companyId);

  const assign = async (roles: readonly CompanyRole[]) => {
    const role = roles.find((candidate) => candidate.id === roleId);
    if (!role || busy) return;
    setBusy(true);
    setFailure(null);
    try {
      await assignCompanyRoleMember(companyId, role.id, user.id);
      setConfirmOpen(false);
      setRoleId('');
      setNotice(`نقش «${role.name}» برای این کاربر در «${companyName}» تخصیص داده شد.`);
      cache.delete(rolesKey);
      onMembershipsChanged();
    } catch (cause) {
      setConfirmOpen(false);
      if (!onUnauthenticated(cause)) setFailure(assignFailure(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <TabDataBoundary state={state} label="در حال دریافت نقش‌های شرکت…">
      {(roles) => {
        const assignable = roles.filter((role) => role.isActive && !role.isSystemRole);
        const selected = assignable.find((role) => role.id === roleId);
        return (
          <div className="space-y-3">
            {failure ? <Alert tone="error">{failure}</Alert> : null}
            <div aria-live="polite">{notice ? <p role="status" className="rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">{notice}</p> : null}</div>
            {assignable.length === 0 ? (
              <EmptyState title="نقش قابل‌تخصیصی در این شرکت وجود ندارد." />
            ) : (
              <div className="flex flex-wrap items-end gap-3">
                <div className="flex min-w-56 flex-col gap-1.5">
                  <label htmlFor={selectId} className="text-sm font-semibold text-ink">
                    نقش نرم‌افزاری جدید
                  </label>
                  <Select id={selectId} value={roleId} onChange={(event) => setRoleId(event.target.value)}>
                    <option value="">انتخاب نقش…</option>
                    {assignable.map((role) => (
                      <option key={role.id} value={role.id} disabled={current?.roleCode === role.code}>
                        {role.name}
                        {current?.roleCode === role.code ? ' (نقش فعلی)' : ''}
                      </option>
                    ))}
                  </Select>
                </div>
                <Button type="button" size="lg" disabled={!selected || busy} onClick={() => setConfirmOpen(true)}>
                  تخصیص نقش
                </Button>
              </div>
            )}
            <p className="text-xs leading-6 text-sub">
              فقط برای عضو فعال شرکت و نقش‌های غیرسیستمی همان شرکت؛ سرور سقف تفویض شما و مجاز بودن عملیات را دوباره بررسی می‌کند. این عملیات بلافاصله اعمال می‌شود و به دکمهٔ «ذخیرهٔ تغییرات» فرم وابسته نیست.
            </p>
            <ConfirmDialog
              open={confirmOpen}
              title="تخصیص نقش"
              description={`نقش «${selected?.name ?? ''}» به این کاربر در «${companyName}» داده می‌شود و مجوزهای مؤثر او تغییر می‌کند. ادامه می‌دهید؟`}
              confirmLabel="تخصیص نقش"
              cancelLabel="انصراف"
              busy={busy}
              onConfirm={() => assign(roles)}
              onCancel={() => setConfirmOpen(false)}
            />
          </div>
        );
      }}
    </TabDataBoundary>
  );
}

/**
 * Tab 4 (edit mode): the real memberships (company role per membership), the organizational
 * position from HR, and the existing role-assignment action. No direct permission exists here;
 * detailed permissions are managed on the access management page.
 */
export function RolesPanel(props: PanelProps) {
  const { user, memberships, context } = props;
  const active = memberships.filter((membership) => membership.isActive);
  const [companyId, setCompanyId] = useState(() => defaultMembershipCompany(user, context.activeCompanyId));
  const selectId = useId();
  const selected = active.find((membership) => membership.companyId === companyId) ?? null;

  return (
    <div className="space-y-6">
      <section aria-labelledby={`${selectId}-memberships`} className="space-y-3">
        <h2 id={`${selectId}-memberships`} className="text-base font-bold text-ink">
          نقش‌ها در شرکت‌ها
        </h2>
        {memberships.length === 0 ? (
          <EmptyState title="این حساب هنوز عضویت شرکتی ندارد." />
        ) : (
          <DataTable caption="نقش نرم‌افزاری کاربر در هر عضویت" columns={membershipColumns} rows={[...memberships]} getRowKey={(membership) => membership.companyId} />
        )}
      </section>

      <section aria-labelledby={`${selectId}-assign`} className="space-y-3">
        <h2 id={`${selectId}-assign`} className="text-base font-bold text-ink">
          تخصیص نقش و سمت سازمانی
        </h2>
        {!selected ? (
          <EmptyState title="تخصیص نقش فقط برای عضو فعال یک شرکت ممکن است." />
        ) : (
          <>
            <div className="flex max-w-sm flex-col gap-1.5">
              <label htmlFor={selectId} className="text-sm font-semibold text-ink">
                شرکت
              </label>
              <Select id={selectId} value={selected.companyId} onChange={(event) => setCompanyId(event.target.value)}>
                {active.map((membership) => (
                  <option key={membership.companyId} value={membership.companyId}>
                    {membership.companyName}
                  </option>
                ))}
              </Select>
            </div>
            {context.can('hr.employees.view', selected.companyId) ? (
              <PositionLine key={`pos-${selected.companyId}`} user={user} companyId={selected.companyId} cache={props.cache} onUnauthenticated={props.onUnauthenticated} />
            ) : (
              <p className="text-sm text-sub">نمایش سمت سازمانی برای شما مجاز نیست.</p>
            )}
            {context.can('admin.roles.assign', selected.companyId) ? (
              <RoleAssignment key={selected.companyId} {...props} companyId={selected.companyId} companyName={selected.companyName} />
            ) : (
              <p className="text-sm text-sub">تخصیص نقش برای شما مجاز نیست.</p>
            )}
          </>
        )}
        <p className="rounded-xl bg-cool p-3 text-xs leading-6 text-sub">
          سمت سازمانی به‌تنهایی هیچ مجوز نرم‌افزاری ایجاد نمی‌کند؛ دسترسی کاربر فقط از نقش‌های نرم‌افزاری او می‌آید.
          {' '}
          <Link to={accessManagementPath} className="font-bold text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold">
            مدیریت مجوزهای دسترسی
          </Link>
        </p>
      </section>
    </div>
  );
}
