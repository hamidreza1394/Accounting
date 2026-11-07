import { useEffect, useState, type ReactNode } from 'react';
import { ArrowRight } from 'lucide-react';
import { ApiError, getAdminUser } from '../../../api/client';
import type { AdminUserDetails, AdminUserMembership } from '../../../api/types';
import { formatDate, formatDateTime } from '../../../lib/format';
import { buttonVariants } from '../../ui/button';
import { Link, useDocumentTitle, useParams } from '../../../lib/router';
import { EmptyState, ErrorState, LoadingState } from '../../Feedback';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { DataTable, type DataTableColumn } from '../../ui/DataTable';
import { StatusBadge } from '../../ui/StatusBadge';
import { Tabs } from '../../ui/Tabs';
import { fullName } from './userDisplay';
import { userListBackPath } from './userListReturn';
import { clearUserFlash, peekUserFlash, userFlashText } from './userFlash';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** UI hint only (the server decides): shows the edit button. */
  canUpdate?: boolean;
};

type State =
  | { kind: 'loading' }
  | { kind: 'ready'; user: AdminUserDetails }
  | { kind: 'error'; status: number | null; message: string };

const pageHeading = <h1 className="text-2xl font-extrabold tracking-tight text-title">اطلاعات کاربر</h1>;

/**
 * The back affordance, present in every state (loading, error, ready) directly under the
 * breadcrumb. It returns to the list as this visit left it — search, filters and paging —
 * remembered in memory by the list page itself (see `userListReturn.ts`); a fresh load falls
 * back to the plain list.
 */
function BackToList() {
  return (
    <Link
      to={userListBackPath()}
      className="inline-flex items-center gap-1.5 rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
    >
      <ArrowRight size={16} aria-hidden="true" />
      بازگشت به فهرست کاربران
    </Link>
  );
}

function Info({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0 rounded-xl bg-cool px-3.5 py-3">
      <dt className="text-xs font-medium text-sub">{label}</dt>
      <dd className="mt-1 break-words text-sm font-semibold text-ink">{children}</dd>
    </div>
  );
}

const membershipColumns: readonly DataTableColumn<AdminUserMembership>[] = [
  { key: 'company', header: 'شرکت', cell: (membership) => membership.companyName },
  {
    key: 'role',
    header: 'نقش در شرکت',
    cell: (membership) => (
      <span>
        {membership.roleName} <span dir="ltr" className="text-xs text-sub">({membership.roleCode})</span>
      </span>
    ),
  },
  { key: 'status', header: 'وضعیت', cell: (membership) => <StatusBadge status={membership.isActive ? 'active' : 'inactive'} /> },
  { key: 'created', header: 'تاریخ عضویت', cell: (membership) => formatDate(membership.createdAtUtc) },
];

function Verified({ value }: { value: boolean }) {
  return value ? <StatusBadge status="active">تأیید شده</StatusBadge> : <StatusBadge status="pending">تأیید نشده</StatusBadge>;
}

function DetailTabs({ user }: { user: AdminUserDetails }) {
  const [tab, setTab] = useState('general');
  return (
    <Tabs
      label="بخش‌های اطلاعات کاربر"
      value={tab}
      onValueChange={setTab}
      items={[
        {
          id: 'general',
          label: 'اطلاعات عمومی',
          content: (
            <dl className="grid gap-3 sm:grid-cols-2">
              <Info label="نام و نام خانوادگی">{fullName(user)}</Info>
              <Info label="نام کاربری">
                <span dir="ltr">{user.username ?? '—'}</span>
              </Info>
              <Info label="نقش قدیمی حساب">{user.legacyRoleName ?? 'تعریف نشده'}</Info>
              <Info label="آخرین ورود موفق">{formatDateTime(user.lastLoginDateTime, 'هرگز')}</Info>
              <Info label="تاریخ ایجاد حساب">{formatDateTime(user.insertDateTime)}</Info>
              <Info label="آخرین ویرایش">{formatDateTime(user.updateDateTime)}</Info>
            </dl>
          ),
        },
        {
          id: 'contact',
          label: 'اطلاعات تماس',
          content: (
            <dl className="grid gap-3 sm:grid-cols-2">
              <Info label="ایمیل">
                <span dir="ltr">{user.emailAddress}</span>
              </Info>
              <Info label="وضعیت تأیید ایمیل">
                <Verified value={user.isEmailAddressVerified} />
              </Info>
              <Info label="شماره موبایل">{user.cellPhoneNumber ? <span dir="ltr">{user.cellPhoneNumber}</span> : 'ثبت نشده'}</Info>
              <Info label="وضعیت تأیید موبایل">
                <Verified value={user.isCellPhoneNumberVerified} />
              </Info>
            </dl>
          ),
        },
        {
          id: 'memberships',
          label: 'شرکت‌ها و عضویت‌ها',
          content:
            user.memberships.length === 0 ? (
              <EmptyState title="این حساب هنوز عضویت شرکتی ندارد." />
            ) : (
              <DataTable
                caption="عضویت‌های شرکتی کاربر"
                columns={membershipColumns}
                rows={user.memberships}
                getRowKey={(membership) => membership.companyId}
              />
            ),
        },
      ]}
    />
  );
}

export function UserDetailPage({ onUnauthenticated, canUpdate = false }: Props) {
  const { id = '' } = useParams();
  // A one-shot message from the create/edit page (kept in memory only; gone after a refresh).
  const [flash] = useState(() => peekUserFlash(id));
  useEffect(() => clearUserFlash, []);
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    getAdminUser(id, controller.signal)
      .then((user) => {
        if (!controller.signal.aborted) setState({ kind: 'ready', user });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setState({
          kind: 'error',
          status: cause instanceof ApiError ? cause.status : null,
          message: cause instanceof ApiError ? cause.message : 'دریافت اطلاعات کاربر انجام نشد. لطفاً دوباره تلاش کنید.',
        });
      });
    return () => controller.abort();
  }, [id, attempt, onUnauthenticated]);

  const name = state.kind === 'ready' ? fullName(state.user) : 'کاربر';
  useDocumentTitle(state.kind === 'ready' ? name : null);
  const crumbs = [{ label: 'مدیریت سیستم' }, { label: 'کاربران', to: '/admin/users' }, { label: name }];

  usePageBreadcrumb(crumbs);
  let body: ReactNode;
  if (state.kind === 'loading') {
    body = (
      <>
        {pageHeading}
        <LoadingState label="در حال دریافت اطلاعات کاربر…" />
      </>
    );
  } else if (state.kind === 'error') {
    body = (
      <div className="space-y-4">
        {state.status === 404 || state.status === 400 ? null : pageHeading}
        {state.status === 404 || state.status === 400 ? (
          <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
            <h1 className="text-lg font-bold text-ink">کاربر پیدا نشد</h1>
            <p className="mt-2 text-sm text-sub">کاربری با این شناسه وجود ندارد یا به آن دسترسی ندارید.</p>
          </div>
        ) : state.status === 403 ? (
          <ErrorState message="شما مجوز مشاهدهٔ اطلاعات این کاربر را ندارید." />
        ) : (
          <ErrorState message={state.message} onRetry={() => setAttempt((value) => value + 1)} />
        )}
      </div>
    );
  } else {
    const { user } = state;
    body = (
      <>
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div className="min-w-0">
            <h1 className="truncate text-2xl font-extrabold tracking-tight text-title">{fullName(user)}</h1>
            <p dir="ltr" className="mt-1 text-start text-sm text-sub">
              @{user.username ?? '—'}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <StatusBadge status={user.isActive ? 'active' : 'inactive'} />
            {canUpdate ? (
              <Link to={`/admin/users/${encodeURIComponent(user.id)}/edit`} className={buttonVariants({ variant: 'outline', size: 'lg' })}>
                ویرایش
              </Link>
            ) : null}
          </div>
        </header>
        <DetailTabs user={user} />
      </>
    );
  }

  return (
    <div className="space-y-5">
      <BackToList />
      {/* The live region exists from the first render; its text is added later, so it is announced. */}
      <div aria-live="polite">
        {flash && state.kind === 'ready' ? (
          <p className="rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">{userFlashText[flash]}</p>
        ) : null}
      </div>
      {body}
    </div>
  );
}
