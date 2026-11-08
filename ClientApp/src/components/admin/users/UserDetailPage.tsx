import { useEffect, useRef, useState, type ReactNode } from 'react';
import { ArrowRight, ShieldCheck } from 'lucide-react';
import { ApiError, getAdminUser } from '../../../api/client';
import type { AdminUserDetails } from '../../../api/types';
import { buttonVariants } from '../../ui/button';
import { Link, useDocumentTitle, useParams } from '../../../lib/router';
import { ErrorState, LoadingState } from '../../Feedback';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { StatusBadge } from '../../ui/StatusBadge';
import { Tabs } from '../../ui/Tabs';
import { fullName } from './userDisplay';
import { userListBackPath } from './userListReturn';
import { clearUserFlash, peekUserFlash, userFlashText } from './userFlash';
import {
  ProfileAuditTab,
  ProfileIdentityTab,
  ProfileLoginHistoryTab,
  ProfileMembershipsTab,
  ProfilePermissionsTab,
  ProfileSecurityTab,
  ProfileSubsystemsTab,
} from './profile/ProfileTabs';

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

/** The two-letter initials placeholder; the real personnel photo belongs to the HR record (D-072). */
function Avatar({ name }: { name: string }) {
  const initials = name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('');
  return (
    <span aria-hidden="true" className="flex size-11 shrink-0 items-center justify-center rounded-full bg-navy text-base font-bold text-white">
      {initials || '؟'}
    </span>
  );
}

/**
 * The seven-tab profile workspace. Each tab owns its data: the panel mounts only while the
 * tab is active (the `Tabs` component renders just the active panel), so an inactive tab
 * never sends a request; the page-level cache keeps each tab's payload so switching back
 * never re-requests and nothing loaded is lost.
 */
function ProfileWorkspace({ user, cache, onUnauthenticated }: {
  user: AdminUserDetails;
  cache: Map<string, unknown>;
  onUnauthenticated: (error: unknown) => boolean;
}) {
  const [tab, setTab] = useState<string>('identity');
  const shared = { user, cache, onUnauthenticated };
  return (
    <Tabs
      label="بخش‌های پرونده کاربر"
      value={tab}
      onValueChange={setTab}
      items={[
        { id: 'identity', label: 'مشخصات فردی و پرسنلی', content: <ProfileIdentityTab {...shared} /> },
        { id: 'security', label: 'حساب کاربری و امنیت', content: <ProfileSecurityTab {...shared} /> },
        { id: 'memberships', label: 'شرکت‌ها و عضویت‌ها', content: <ProfileMembershipsTab user={user} /> },
        { id: 'subsystems', label: 'زیرسیستم‌ها و لایسنس', content: <ProfileSubsystemsTab {...shared} /> },
        { id: 'permissions', label: 'گروه‌های دسترسی و مجوزهای مؤثر', content: <ProfilePermissionsTab {...shared} /> },
        { id: 'logins', label: 'سوابق ورود و خروج', content: <ProfileLoginHistoryTab {...shared} /> },
        { id: 'audit', label: 'سوابق تغییرات مدیریتی', content: <ProfileAuditTab {...shared} /> },
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
  // Per-user in-memory cache of the lazy tab payloads: revisiting a tab never re-requests,
  // an inactive tab never fetches, and switching away never discards loaded data.
  const tabCache = useRef<Map<string, unknown>>(new Map());
  useEffect(() => {
    tabCache.current.clear(); // navigating to another user must not inherit their cache
  }, [id]);

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
    const shared = { user, cache: tabCache.current, onUnauthenticated };
    const hasActiveMembership = user.memberships.some((membership) => membership.isActive);
    body = (
      <>
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <Avatar name={fullName(user)} />
            <div className="min-w-0">
              <h1 className="truncate text-2xl font-extrabold tracking-tight text-title">{fullName(user)}</h1>
              <p dir="ltr" className="mt-1 text-start text-sm text-sub">
                @{user.username ?? '—'}
              </p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            {user.legacyRoleName === 'Admin' ? (
              <span className="inline-flex items-center gap-1 rounded-full bg-navy px-2.5 py-1 text-xs font-bold text-white" title="این حساب نقش مدیر سیستم نصب را دارد.">
                <ShieldCheck size={14} aria-hidden="true" />
                مدیر سیستم
              </span>
            ) : null}
            <StatusBadge status={user.isActive ? 'active' : 'inactive'} />
            <StatusBadge status={hasActiveMembership ? 'active' : 'pending'}>
              {hasActiveMembership ? 'عضو فعال' : 'بدون عضویت فعال'}
            </StatusBadge>
            {canUpdate ? (
              <Link to={`/admin/users/${encodeURIComponent(user.id)}/edit`} className={buttonVariants({ variant: 'outline', size: 'lg' })}>
                ویرایش
              </Link>
            ) : null}
          </div>
        </header>
        <ProfileWorkspace {...shared} />
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
