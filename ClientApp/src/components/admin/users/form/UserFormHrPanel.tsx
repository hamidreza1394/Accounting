import { useId, useState } from 'react';
import { getHrEmployee } from '@/features/hr/api/hrClient';
import type { HrEmployeeDetail, HrEmployeeListItem } from '@/features/hr/api/hrTypes';
import { hrEmploymentStatusLabel } from '@/features/hr/lib/hrLabels';
import type { AdminUserDetails } from '@/api/types';
import { EmptyState } from '@/components/Feedback';
import { Select } from '@/components/ui/FormField';
import { buttonVariants } from '@/components/ui/button';
import { formatDate } from '@/lib/format';
import { Link } from '@/lib/router';
import { Info, TabDataBoundary, useProfileTabData } from '../profile/profileShared';
import { HrLinkActions, type LinkTarget, type UnlinkTarget } from '../profile/HrLinkActions';
import {
  employeeDetailKey,
  employeeLinkKey,
  isForbidden,
  loadLinkedEmployee,
  type FormTabCache,
  type UserFormContext,
} from './userFormShared';

type PanelProps = {
  user: AdminUserDetails;
  cache: FormTabCache;
  context: UserFormContext;
  onUnauthenticated: (error: unknown) => boolean;
};

type DetailBundle = { detail: HrEmployeeDetail; managerName: string | null };

/** The unit / position / manager of the ACTIVE assignment (D-072); the manager's name is looked up once. */
async function loadDetail(companyId: string, employeeId: string, signal: AbortSignal): Promise<DetailBundle> {
  const detail = await getHrEmployee(companyId, employeeId, signal);
  const managerId = detail.activeAssignment?.managerEmployeeId ?? null;
  if (!managerId) return { detail, managerName: null };
  try {
    const manager = await getHrEmployee(companyId, managerId, signal);
    return { detail, managerName: `${manager.firstName} ${manager.lastName}`.trim() };
  } catch (cause) {
    if (isForbidden(cause)) return { detail, managerName: null };
    throw cause;
  }
}

const dash = '—';

function EmployeeDetails({ employee, companyId, user, cache, canOpen, onUnauthenticated }: {
  employee: HrEmployeeListItem;
  companyId: string;
  user: AdminUserDetails;
  cache: FormTabCache;
  canOpen: boolean;
  onUnauthenticated: (error: unknown) => boolean;
}) {
  const state = useProfileTabData(cache, employeeDetailKey(user.id, employee.id), (signal) => loadDetail(companyId, employee.id, signal), onUnauthenticated);
  return (
    <TabDataBoundary state={state} label="در حال دریافت اطلاعات استخدامی…">
      {({ detail, managerName }) => {
        const assignment = detail.activeAssignment;
        return (
          <div className="space-y-3">
            <dl className="grid gap-3 sm:grid-cols-2">
              <Info label="واحد سازمانی">{assignment?.unitName ?? dash}</Info>
              <Info label="سمت سازمانی">{assignment?.positionName ?? dash}</Info>
              <Info label="مدیر مستقیم">{managerName ?? dash}</Info>
              <Info label="وضعیت اشتغال">{hrEmploymentStatusLabel[detail.employmentStatus]}</Info>
              <Info label="تاریخ استخدام">{formatDate(detail.hiredOnUtc, dash)}</Info>
              {detail.terminatedOnUtc ? <Info label="تاریخ پایان همکاری">{formatDate(detail.terminatedOnUtc, dash)}</Info> : null}
            </dl>
            <p className="text-xs leading-6 text-sub">
              تغییر واحد، سمت و مدیر مستقیم در صفحهٔ پروندهٔ کارمند انجام می‌شود؛ پیش از رفتن، تغییرات ذخیره‌نشدهٔ این فرم را ذخیره کنید.
              {canOpen ? (
                <>
                  {' '}
                  <Link to={`/hr/org/employees/${encodeURIComponent(detail.id)}`} className={buttonVariants({ variant: 'link', size: 'sm' })}>
                    باز کردن پروندهٔ کارمند
                  </Link>
                </>
              ) : null}
            </p>
          </div>
        );
      }}
    </TabDataBoundary>
  );
}

function CompanyEmployment({ companyId, companyName, user, cache, context, onUnauthenticated }: PanelProps & { companyId: string; companyName: string }) {
  const linkKey = employeeLinkKey(user.id, companyId);
  const state = useProfileTabData(cache, linkKey, (signal) => loadLinkedEmployee(companyId, user.id, signal), onUnauthenticated);
  const [linkTarget, setLinkTarget] = useState<LinkTarget | null>(null);
  const [unlinkTarget, setUnlinkTarget] = useState<UnlinkTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  /** After a confirmed link/unlink everything of this company reloads from the server. */
  const handleDone = (message: string) => {
    setNotice(message);
    for (const key of [...cache.keys()]) {
      if (key === linkKey || key.startsWith(`${user.id}:form-hr-detail:`)) cache.delete(key);
    }
    state.retry();
  };

  return (
    <div className="space-y-4">
      <div aria-live="polite">
        {notice ? (
          <p role="status" className="rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">
            {notice}
          </p>
        ) : null}
      </div>
      <TabDataBoundary state={state} label="در حال دریافت پروندهٔ پرسنلی…">
        {(employee) =>
          employee === null ? (
            <div className="space-y-3">
              <EmptyState title="برای این کاربر در این شرکت پروندهٔ پرسنلی متصل نشده است.">
                پروندهٔ پرسنلی موجود را می‌توان به این حساب متصل کرد؛ حساب باید عضو فعال شرکت باشد.
              </EmptyState>
              <button
                type="button"
                onClick={() => {
                  setNotice(null);
                  setLinkTarget({ companyId, companyName });
                }}
                aria-label={`اتصال پرونده پرسنلی در ${companyName}`}
                className="inline-flex min-h-10 items-center rounded-lg border border-teal-deep/40 px-4 text-sm font-bold text-teal-deep outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
              >
                اتصال پرونده پرسنلی
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              <dl className="grid gap-3 sm:grid-cols-2">
                <Info label="شماره پرسنلی (فقط‌خواندنی)">
                  <span dir="ltr">{employee.employeeNumber}</span>
                </Info>
                <Info label="نام در پروندهٔ پرسنلی">{`${employee.firstName} ${employee.lastName}`.trim()}</Info>
              </dl>
              <EmployeeDetails
                employee={employee}
                companyId={companyId}
                user={user}
                cache={cache}
                canOpen={context.can('hr.employees.view', companyId)}
                onUnauthenticated={onUnauthenticated}
              />
              <button
                type="button"
                onClick={() => {
                  setNotice(null);
                  setUnlinkTarget({ companyId, companyName, employeeId: employee.id, employeeName: `${employee.firstName} ${employee.lastName}`.trim() });
                }}
                aria-label={`قطع اتصال پرونده پرسنلی در ${companyName}`}
                className="inline-flex min-h-10 items-center rounded-lg border border-danger/40 px-4 text-sm font-bold text-danger outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
              >
                قطع اتصال
              </button>
            </div>
          )
        }
      </TabDataBoundary>
      <HrLinkActions
        linkTarget={linkTarget}
        unlinkTarget={unlinkTarget}
        userId={user.id}
        onClose={() => {
          setLinkTarget(null);
          setUnlinkTarget(null);
        }}
        onLinked={handleDone}
        onUnlinked={handleDone}
        onUnauthenticated={onUnauthenticated}
      />
    </div>
  );
}

/** The company the user belongs to by default: the work-context company when it is an active membership. */
export function defaultMembershipCompany(user: AdminUserDetails, activeCompanyId: string | null): string | null {
  const active = user.memberships.filter((membership) => membership.isActive);
  const preferred = active.find((membership) => activeCompanyId !== null && membership.companyId.toLowerCase() === activeCompanyId.toLowerCase());
  return (preferred ?? active[0])?.companyId ?? null;
}

/**
 * Tab 3 (edit mode): the employment data comes from the HR module — the personnel record of the
 * chosen company and its active assignment. Nothing here is a user field (D-072).
 */
export function EmploymentPanel(props: PanelProps) {
  const { user, context } = props;
  const active = user.memberships.filter((membership) => membership.isActive);
  const [companyId, setCompanyId] = useState(() => defaultMembershipCompany(user, context.activeCompanyId));
  const selectId = useId();
  const selected = active.find((membership) => membership.companyId === companyId) ?? null;

  if (active.length === 0 || !selected) {
    return (
      <EmptyState title="این حساب عضویت فعال شرکتی ندارد.">
        اطلاعات استخدامی از پروندهٔ پرسنلی همان شرکت خوانده می‌شود و پروندهٔ پرسنلی به عضویت فعال شرکت متصل می‌شود.
      </EmptyState>
    );
  }
  const allowed = context.can('hr.employees.view', selected.companyId);
  return (
    <div className="space-y-4">
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
      {allowed ? (
        <CompanyEmployment key={selected.companyId} {...props} companyId={selected.companyId} companyName={selected.companyName} />
      ) : (
        <EmptyState title="نمایش اطلاعات استخدامی این شرکت برای شما مجاز نیست.">
          برای دیدن پروندهٔ کارمند، مجوز مشاهدهٔ کارکنان در همان شرکت لازم است؛ سرور هر درخواست را دوباره بررسی می‌کند.
        </EmptyState>
      )}
    </div>
  );
}
