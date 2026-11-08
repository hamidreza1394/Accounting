import { useState } from 'react';
import {
  getAccessCatalogSubsystems,
  getAdminUserLoginHistory,
  getAdminUserMembershipRequests,
  getAdminUserSessions,
  getEffectivePermissions,
  getSecurityAuditEvents,
  ApiError,
} from '@/api/client';
import type {
  AccessSubsystem,
  AdminUserDetails,
  AdminUserMembership,
  AdminUserMembershipRequest,
  AdminUserSession,
  EffectivePermissions,
  LoginHistoryItem,
  SecurityAuditEvent,
  SecurityAuditPage,
} from '@/api/types';
import { formatDateTime } from '@/lib/format';
import { Link } from '@/lib/router';
import { eventTypeLabel } from '@/lib/securityEvents';
import { listHrEmployees } from '@/features/hr/api/hrClient';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { EmptyState } from '@/components/Feedback';
import { Info, TabDataBoundary, useProfileTabData } from './profileShared';
import { AdminRevokeSessionDialog, type PendingRevocation } from './AdminRevokeSessionDialog';
import { HrLinkActions, type LinkTarget, type UnlinkTarget } from './HrLinkActions';
import { fullName } from '../userDisplay';

type Shared = {
  user: AdminUserDetails;
  cache: Map<string, unknown>;
  onUnauthenticated: (error: unknown) => boolean;
};

function Verified({ value }: { value: boolean }) {
  return value ? <StatusBadge status="active">تأیید شده</StatusBadge> : <StatusBadge status="pending">تأیید نشده</StatusBadge>;
}

function Label({ code }: { code: string }) {
  return <span title={code}>{eventTypeLabel(code) ?? code}</span>;
}

// ------------------------------------------------------------------ tab 1: identity & HR

type HrLinkRow = {
  companyId: string;
  companyName: string;
  employeeId: string | null;
  employeeNumber: string | null;
  employeeName: string | null;
  /** True when the viewer may not read the HR records of that company (the field stays hidden, not faked). */
  hidden: boolean;
};

/**
 * Tab 1 — personal, contact and linked personnel record. The identity fields come from the
 * already-loaded user details (no request); the linked employee is fetched per active
 * membership only when this tab opens (`hr.employees.list` decides per company; 403/404
 * companies are shown as "not visible for you" instead of an error). Connect / disconnect
 * actions go through the real HR endpoints (`hr.employees.user-link.manage` — the server
 * decides per company) and refresh the section from the server after every success.
 */
export function ProfileIdentityTab({ user, cache, onUnauthenticated }: Shared) {
  const active = user.memberships.filter((membership) => membership.isActive);
  const linksKey = `${user.id}:hr-links`;
  const state = useProfileTabData<HrLinkRow[]>(
    cache,
    linksKey,
    async (signal) => {
      if (active.length === 0) return [];
      const rows = await Promise.all(
        active.slice(0, 8).map(async (membership): Promise<HrLinkRow> => {
          try {
            const page = await listHrEmployees(membership.companyId, { userId: user.id, page: 1, pageSize: 5 }, signal);
            const employee = page.items[0] ?? null;
            return {
              companyId: membership.companyId,
              companyName: membership.companyName,
              employeeId: employee?.id ?? null,
              employeeNumber: employee?.employeeNumber ?? null,
              employeeName: employee ? `${employee.firstName} ${employee.lastName}`.trim() : null,
              hidden: false,
            };
          } catch (error) {
            if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
              return { companyId: membership.companyId, companyName: membership.companyName, employeeId: null, employeeNumber: null, employeeName: null, hidden: true };
            }
            throw error;
          }
        }),
      );
      return rows;
    },
    onUnauthenticated,
  );
  const [linkTarget, setLinkTarget] = useState<LinkTarget | null>(null);
  const [unlinkTarget, setUnlinkTarget] = useState<UnlinkTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  /** After a confirmed link/unlink the section reloads from the server — never a stale row. */
  const handleDone = (message: string) => {
    setNotice(message);
    cache.delete(linksKey);
    state.retry();
  };

  return (
    <div className="space-y-6">
      <section aria-labelledby="profile-identity-heading">
        <h2 id="profile-identity-heading" className="mb-3 text-base font-bold text-ink">مشخصات فردی</h2>
        <dl className="grid gap-3 sm:grid-cols-2">
          <Info label="نام و نام خانوادگی">{fullName(user)}</Info>
          <Info label="نام کاربری">
            <span dir="ltr">{user.username ?? '—'}</span>
          </Info>
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
      </section>
      <section aria-labelledby="profile-hr-heading">
        <h2 id="profile-hr-heading" className="mb-3 text-base font-bold text-ink">پرونده پرسنلی مرتبط</h2>
        {/* The live region exists from the first render; the message arrives later, so it is announced. */}
        <div aria-live="polite">
          {notice ? (
            <p role="status" className="mb-3 rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">
              {notice}
            </p>
          ) : null}
        </div>
        <TabDataBoundary state={state} label="در حال دریافت پرونده پرسنلی…">
          {(rows) =>
            rows.length === 0 ? (
              <EmptyState title="این حساب عضویت فعال شرکتی ندارد؛ پرونده پرسنلی به عضویت همان شرکت متصل می‌شود." />
            ) : (
              <dl className="grid gap-3 sm:grid-cols-2">
                {rows.map((row) => (
                  <div key={row.companyId} className="rounded-xl bg-cool px-3.5 py-3">
                    <dt className="text-xs font-medium text-sub">پرونده پرسنلی — {row.companyName}</dt>
                    <dd className="mt-1 break-words text-sm font-semibold text-ink">
                      {row.hidden ? (
                        <span className="text-sub">نمایش این بخش برای شما مجاز نیست.</span>
                      ) : row.employeeNumber ? (
                        <>
                          <span dir="ltr">{row.employeeNumber}</span>
                          {row.employeeName ? <span className="text-sub"> — {row.employeeName}</span> : null}
                        </>
                      ) : (
                        <span className="text-sub">پرونده پرسنلی متصل نشده است.</span>
                      )}
                    </dd>
                    {!row.hidden && row.employeeId ? (
                      <button
                        type="button"
                        onClick={() => {
                          setNotice(null);
                          setUnlinkTarget({ companyId: row.companyId, companyName: row.companyName, employeeId: row.employeeId as string, employeeName: row.employeeName ?? '' });
                        }}
                        aria-label={`قطع اتصال پرونده پرسنلی در ${row.companyName}`}
                        className="mt-2 inline-flex min-h-9 items-center rounded-lg border border-danger/40 px-3 text-xs font-bold text-danger outline-none hover:bg-white focus-visible:ring-2 focus-visible:ring-gold"
                      >
                        قطع اتصال
                      </button>
                    ) : null}
                    {!row.hidden && !row.employeeId ? (
                      <button
                        type="button"
                        onClick={() => {
                          setNotice(null);
                          setLinkTarget({ companyId: row.companyId, companyName: row.companyName });
                        }}
                        aria-label={`اتصال پرونده پرسنلی در ${row.companyName}`}
                        className="mt-2 inline-flex min-h-9 items-center rounded-lg border border-teal-deep/40 px-3 text-xs font-bold text-teal-deep outline-none hover:bg-white focus-visible:ring-2 focus-visible:ring-gold"
                      >
                        اتصال پرونده پرسنلی
                      </button>
                    ) : null}
                  </div>
                ))}
              </dl>
            )
          }
        </TabDataBoundary>
      </section>
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

// ------------------------------------------------------------------ tab 2: account & security

const number = new Intl.NumberFormat('fa-IR');

/**
 * Tab 2 — account status and the allow-listed active sessions (D-069: no IP, no user agent).
 * Revoking another user's session is the step-up flow of D-073 §1: the administrator
 * re-authenticates and picks a server-owned reason; every mutation refetches the list so
 * it never shows a stale (still-active) row after the server already ended the session.
 */
export function ProfileSecurityTab({ user, cache, onUnauthenticated }: Shared) {
  const sessionsKey = `${user.id}:sessions`;
  const state = useProfileTabData<AdminUserSession[]>(
    cache,
    sessionsKey,
    (signal) => getAdminUserSessions(user.id, signal),
    onUnauthenticated,
  );
  const [pending, setPending] = useState<PendingRevocation | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  /** After a confirmed revocation the cached list is dropped and reloaded from the server. */
  const handleSuccess = (message: string) => {
    setNotice(message);
    cache.delete(sessionsKey);
    state.retry();
  };

  const actionColumn: DataTableColumn<AdminUserSession> = {
    key: 'action',
    header: 'عملیات',
    cell: (session) => (
      <button
        type="button"
        onClick={() => {
          setNotice(null);
          setPending({ kind: 'one', session });
        }}
        aria-label={`ابطال نشست ایجادشده در ${formatDateTime(session.createdAtUtc)}`}
        className="inline-flex min-h-9 items-center rounded-lg border border-danger/40 px-3 text-xs font-bold text-danger outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
      >
        ابطال
      </button>
    ),
  };

  return (
    <div className="space-y-6">
      <section aria-labelledby="profile-account-heading">
        <h2 id="profile-account-heading" className="mb-3 text-base font-bold text-ink">وضعیت حساب</h2>
        <dl className="grid gap-3 sm:grid-cols-2">
          <Info label="وضعیت حساب">
            <StatusBadge status={user.isActive ? 'active' : 'inactive'} />
          </Info>
          <Info label="نقش قدیمی حساب">{user.legacyRoleName ?? 'تعریف نشده'}</Info>
          <Info label="آخرین ورود موفق">{formatDateTime(user.lastLoginDateTime, 'هرگز')}</Info>
          <Info label="تاریخ ایجاد حساب">{formatDateTime(user.insertDateTime)}</Info>
          <Info label="آخرین ویرایش">{formatDateTime(user.updateDateTime)}</Info>
        </dl>
      </section>
      <section aria-labelledby="profile-sessions-heading">
        <h2 id="profile-sessions-heading" className="mb-3 text-base font-bold text-ink">نشست‌های فعال</h2>
        {/* The live region exists from the first render; the message arrives later, so it is announced. */}
        <div aria-live="polite">
          {notice ? (
            <p role="status" className="mb-3 rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">
              {notice}
            </p>
          ) : null}
        </div>
        <TabDataBoundary state={state} label="در حال دریافت نشست‌های فعال…">
          {(sessions) => (
            <div className="space-y-3">
              {sessions.length === 0 ? (
                <EmptyState title="نشست فعالی برای این حساب وجود ندارد." />
              ) : (
                <DataTable
                  caption="نشست‌های فعال کاربر"
                  columns={[...sessionColumns, actionColumn]}
                  rows={sessions}
                  getRowKey={(session) => session.sessionId}
                />
              )}
              {sessions.length > 0 ? (
                <button
                  type="button"
                  onClick={() => {
                    setNotice(null);
                    setPending({ kind: 'all', count: sessions.length });
                  }}
                  className="min-h-10 rounded-lg border border-danger/40 px-4 text-xs font-bold text-danger outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
                >
                  ابطال همهٔ نشست‌های فعال ({number.format(sessions.length)})
                </button>
              ) : null}
              <p className="text-xs leading-6 text-sub">
                ابطال نشست کاربر دیگر نیازمند تأیید هویت شما و انتخاب دلیل است؛ عملیات در سوابق امنیتی ثبت می‌شود (D-073).
              </p>
            </div>
          )}
        </TabDataBoundary>
      </section>
      <AdminRevokeSessionDialog
        userId={user.id}
        pending={pending}
        onClose={() => setPending(null)}
        onSuccess={handleSuccess}
        onUnauthenticated={onUnauthenticated}
      />
    </div>
  );
}

const sessionColumns: readonly DataTableColumn<AdminUserSession>[] = [
  {
    key: 'created',
    header: 'زمان ایجاد نشست',
    cell: (session) => formatDateTime(session.createdAtUtc),
  },
  {
    key: 'seen',
    header: 'آخرین فعالیت',
    cell: (session) => formatDateTime(session.lastSeenAtUtc),
  },
];

// ------------------------------------------------------------------ tab 3: memberships

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
  { key: 'created', header: 'تاریخ عضویت', cell: (membership) => formatDateTime(membership.createdAtUtc) },
];

/** Tab 3 — the memberships plus the user's own inter-company request states (D-065). */
export function ProfileMembershipsTab({ user, cache, onUnauthenticated }: Shared) {
  const [requestStatus, setRequestStatus] = useState<'' | 'Pending' | 'Approved' | 'Rejected'>('');
  const [requestPage, setRequestPage] = useState(1);
  const requestsState = useProfileTabData(
    cache,
    `${user.id}:membership-requests:${requestStatus}:${requestPage}`,
    (signal) =>
      getAdminUserMembershipRequests(
        user.id,
        { ...(requestStatus === '' ? {} : { status: requestStatus }), page: requestPage, pageSize: 10 },
        signal,
      ),
    onUnauthenticated,
  );

  return (
    <div className="space-y-8">
      <section aria-labelledby="profile-memberships-heading">
        <h2 id="profile-memberships-heading" className="mb-3 text-base font-bold text-ink">عضویت‌های شرکتی</h2>
        {user.memberships.length === 0 ? (
          <EmptyState title="این حساب هنوز عضویت شرکتی ندارد." />
        ) : (
          <DataTable
            caption="عضویت‌های شرکتی کاربر"
            columns={membershipColumns}
            rows={user.memberships}
            getRowKey={(membership) => membership.companyId}
          />
        )}
      </section>
      <section aria-labelledby="profile-requests-heading">
        <h2 id="profile-requests-heading" className="mb-3 text-base font-bold text-ink">درخواست‌های عضویت بین‌شرکتی</h2>
        <p className="mb-3 text-xs leading-6 text-sub">
          تصمیم بر درخواست‌های در انتظار تنها در شرکت مقصد گرفته می‌شود؛ این بخش فقط وضعیت را نشان می‌دهد (D-065).
          {' '}
          <Link to="/admin/membership-requests" className="font-bold text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold">
            رفتن به صف تأیید مقصد
          </Link>
        </p>
        <div className="mb-3">
          <label htmlFor="membership-request-status" className="mb-1 block text-sm font-medium text-ink">فیلتر وضعیت</label>
          <select
            id="membership-request-status"
            value={requestStatus}
            onChange={(event) => {
              setRequestStatus(event.target.value as '' | 'Pending' | 'Approved' | 'Rejected');
              setRequestPage(1);
            }}
            className="w-full max-w-xs rounded-lg border border-line bg-white px-3 py-2 text-sm text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold"
          >
            <option value="">همهٔ وضعیت‌ها</option>
            <option value="Pending">در انتظار تأیید</option>
            <option value="Approved">تأیید شده</option>
            <option value="Rejected">رد شده</option>
          </select>
        </div>
        <TabDataBoundary state={requestsState} label="در حال دریافت درخواست‌های عضویت…">
          {(requestsPage) =>
            requestsPage.items.length === 0 ? (
              <EmptyState title="درخواست عضویتی برای این حساب ثبت نشده است." />
            ) : (
              <div className="space-y-3">
                <DataTable
                  caption="درخواست‌های عضویت کاربر"
                  columns={membershipRequestColumns}
                  rows={requestsPage.items}
                  getRowKey={(request) => request.id}
                />
                {requestsPage.totalCount > requestsPage.pageSize ? (
                  <nav aria-label="صفحه‌بندی درخواست‌های عضویت" className="flex items-center justify-between text-sm">
                    <button
                      type="button"
                      onClick={() => setRequestPage((value) => Math.max(1, value - 1))}
                      disabled={requestPage <= 1}
                      className="rounded-lg border border-line px-3 py-1.5 font-medium text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-40"
                    >
                      قبلی
                    </button>
                    <span className="text-sub">
                      صفحه {requestPage} از {Math.ceil(requestsPage.totalCount / requestsPage.pageSize)}
                    </span>
                    <button
                      type="button"
                      onClick={() => setRequestPage((value) => value + 1)}
                      disabled={requestPage >= Math.ceil(requestsPage.totalCount / requestsPage.pageSize)}
                      className="rounded-lg border border-line px-3 py-1.5 font-medium text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-40"
                    >
                      بعدی
                    </button>
                  </nav>
                ) : null}
              </div>
            )
          }
        </TabDataBoundary>
      </section>
    </div>
  );
}

const membershipRequestStatusLabels: Record<AdminUserMembershipRequest['status'], { label: string; badge: 'pending' | 'active' | 'rejected' }> = {
  Pending: { label: 'در انتظار تأیید', badge: 'pending' },
  Approved: { label: 'تأیید شده', badge: 'active' },
  Rejected: { label: 'رد شده', badge: 'rejected' },
};

const membershipRequestColumns: readonly DataTableColumn<AdminUserMembershipRequest>[] = [
  { key: 'company', header: 'شرکت', cell: (request) => request.destinationCompanyName },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (request) => {
      const status = membershipRequestStatusLabels[request.status];
      return <StatusBadge status={status.badge}>{status.label}</StatusBadge>;
    },
  },
  { key: 'requested', header: 'زمان درخواست', cell: (request) => formatDateTime(request.requestedAtUtc) },
  { key: 'decided', header: 'زمان تصمیم', cell: (request) => (request.decidedAtUtc ? formatDateTime(request.decidedAtUtc) : '—') },
  { key: 'decider', header: 'تصمیم‌گیرنده', cell: (request) => request.decidedByDisplayName ?? '—' },
  { key: 'role', header: 'نقش تخصیص‌یافته', cell: (request) => request.approvedRoleName ?? '—' },
  { key: 'reason', header: 'دلیل رد', cell: (request) => request.decisionReason ?? '—' },
];

// ------------------------------------------------------------------ tab 4: subsystems & licensing

/**
 * Tab 4 — the D-075 subsystem catalog. The user-level entitlement binding is not implemented
 * yet (license is legally separate from the operational permission), so this tab shows only
 * the real catalog and says so — it never invents a status.
 */
export function ProfileSubsystemsTab({ user, cache, onUnauthenticated }: Shared) {
  const state = useProfileTabData<AccessSubsystem[]>(
    cache,
    `${user.id}:subsystems`,
    (signal) => getAccessCatalogSubsystems(signal),
    onUnauthenticated,
  );
  return (
    <div className="space-y-4">
      <p className="rounded-xl border border-line bg-cool p-4 text-sm leading-6 text-navy">
        اتصال لایسنس (Entitlement) به کاربر هنوز پیاده‌سازی نشده است (تصمیم D-075؛ لایسنس از مجوز عملیاتی جداست).
        در این تب فقط کاتالوگ واقعی زیرسیستم‌ها نمایش داده می‌شود و هیچ وضعیت فرضی‌ای نشان داده نمی‌شود.
      </p>
      <TabDataBoundary state={state} label="در حال دریافت کاتالوگ زیرسیستم‌ها…">
        {(subsystems) =>
          subsystems.length === 0 ? (
            <EmptyState title="کاتالوگ زیرسیستم‌ها خالی است." />
          ) : (
            <ul className="grid gap-2 sm:grid-cols-2" aria-label="کاتالوگ زیرسیستم‌ها">
              {subsystems.map((subsystem) => (
                <li key={subsystem.code} className="rounded-xl border border-line bg-white px-3.5 py-3">
                  <p className="text-sm font-bold text-ink">{subsystem.title}</p>
                  <p dir="ltr" className="mt-0.5 text-start text-xs text-sub">
                    {subsystem.code} · {subsystem.entitlementScope}
                    {subsystem.entitlementLinked && subsystem.entitlementCode ? ` · ${subsystem.entitlementCode}` : ''}
                  </p>
                </li>
              ))}
            </ul>
          )
        }
      </TabDataBoundary>
    </div>
  );
}

// ------------------------------------------------------------------ tab 5: effective permissions

type PermissionBundle = { companyId: string; companyName: string; permissions: EffectivePermissions | null; hidden: boolean };

/**
 * Tab 5 — the effective permissions per active membership, with the source roles of every
 * code (stage 2 capability, reused here — no parallel permission engine). One request per
 * active membership when the tab opens; 403/404 companies are marked hidden, never faked.
 */
export function ProfilePermissionsTab({ user, cache, onUnauthenticated }: Shared) {
  const active = user.memberships.filter((membership) => membership.isActive);
  const state = useProfileTabData<PermissionBundle[]>(
    cache,
    `${user.id}:effective`,
    async (signal) => {
      if (active.length === 0) return [];
      return Promise.all(
        active.slice(0, 8).map(async (membership): Promise<PermissionBundle> => {
          try {
            const permissions = await getEffectivePermissions(membership.companyId, user.id, signal);
            return { companyId: membership.companyId, companyName: membership.companyName, permissions, hidden: false };
          } catch (error) {
            if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
              return { companyId: membership.companyId, companyName: membership.companyName, permissions: null, hidden: true };
            }
            throw error;
          }
        }),
      );
    },
    onUnauthenticated,
  );
  const [search, setSearch] = useState('');
  const needle = search.trim();
  return (
    <div className="space-y-4">
      <div>
        <label htmlFor="permission-search" className="mb-1 block text-sm font-medium text-ink">جستجوی مجوز</label>
        <input
          id="permission-search"
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="کد مجوز یا نام نقش…"
          className="w-full max-w-sm rounded-lg border border-line bg-white px-3 py-2 text-sm text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold"
        />
      </div>
      <TabDataBoundary state={state} label="در حال دریافت مجوزهای مؤثر…">
        {(bundles) =>
          bundles.length === 0 ? (
            <EmptyState title="این حساب عضویت فعال شرکتی ندارد؛ مجوز مؤثر در هر شرکت به عضویت فعال وابسته است." />
          ) : (
            <div className="space-y-6">
              {bundles.map((bundle) => {
                const source = bundle.permissions;
                const rows = (source?.permissions ?? []).filter(
                  (permission) =>
                    !needle ||
                    permission.code.includes(needle) ||
                    permission.sourceRoleNames.some((name) => name.includes(needle)),
                );
                return (
                  <section key={bundle.companyId} aria-labelledby={`perm-${bundle.companyId}`}>
                    <h3 id={`perm-${bundle.companyId}`} className="mb-2 text-sm font-bold text-ink">
                      مجوزهای مؤثر — {bundle.companyName}
                    </h3>
                    {bundle.hidden ? (
                      <p className="rounded-xl border border-line bg-cool p-3 text-sm text-sub">نمایش این بخش برای شما مجاز نیست.</p>
                    ) : !source || source.permissions.length === 0 ? (
                      <EmptyState title="هیچ مجوز فعالی از نقش‌های این شرکت به این کاربر رسیده است." />
                    ) : rows.length === 0 ? (
                      <p className="rounded-xl border border-line bg-cool p-3 text-sm text-sub">نتیجه‌ای برای این جستجو نیست.</p>
                    ) : (
                      <ul className="grid gap-2 sm:grid-cols-2">
                        {rows.map((permission) => (
                          <li key={permission.code} className="rounded-xl border border-line bg-white px-3.5 py-2.5">
                            <p dir="ltr" className="text-start text-sm font-semibold text-ink">{permission.code}</p>
                            <p className="mt-0.5 text-xs text-sub">
                              منبع: {permission.sourceRoleNames.length > 0 ? permission.sourceRoleNames.join('، ') : '—'}
                            </p>
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>
                );
              })}
            </div>
          )
        }
      </TabDataBoundary>
    </div>
  );
}

// ------------------------------------------------------------------ tab 6: login history

const loginHistoryColumns: readonly DataTableColumn<LoginHistoryItem>[] = [
  { key: 'type', header: 'رویداد', cell: (item) => <Label code={item.eventType} /> },
  { key: 'at', header: 'زمان', cell: (item) => formatDateTime(item.occurredAtUtc) },
  {
    key: 'ip',
    header: 'نشانی IP',
    cell: (item) => (item.ipAddress ? <span dir="ltr">{item.ipAddress}</span> : '—'),
  },
];

/** Tab 6 — the authentication events of the user (admin endpoint of PR 3-A). */
export function ProfileLoginHistoryTab({ user, cache, onUnauthenticated }: Shared) {
  const state = useProfileTabData<LoginHistoryItem[]>(
    cache,
    `${user.id}:logins`,
    (signal) => getAdminUserLoginHistory(user.id, signal),
    onUnauthenticated,
  );
  return (
    <TabDataBoundary state={state} label="در حال دریافت سوابق ورود و خروج…">
      {(events) =>
        events.length === 0 ? (
          <EmptyState title="سابقه‌ای از ورود و خروج برای این حساب ثبت نشده است." />
        ) : (
          <DataTable
            caption="سوابق ورود و خروج کاربر"
            columns={loginHistoryColumns}
            rows={events}
            getRowKey={(item) => `${item.eventType}-${item.occurredAtUtc}-${item.ipAddress ?? ''}`}
          />
        )
      }
    </TabDataBoundary>
  );
}

// ------------------------------------------------------------------ tab 7: management change history

const auditColumns: readonly DataTableColumn<SecurityAuditEvent>[] = [
  { key: 'type', header: 'رویداد', cell: (event) => <Label code={event.eventType} /> },
  { key: 'at', header: 'زمان', cell: (event) => formatDateTime(event.occurredAtUtc) },
  { key: 'entity', header: 'موضوع', cell: (event) => (event.entityType ? `${event.entityType}` : '—') },
  {
    key: 'actor',
    header: 'عامل',
    cell: (event) => event.actorDisplayName ?? event.actorUsername ?? '—',
  },
];

/** Tab 7 — the management change history of the user, from the existing audit endpoint. */
export function ProfileAuditTab({ user, cache, onUnauthenticated }: Shared) {
  const [page, setPage] = useState(1);
  const state = useProfileTabData<SecurityAuditPage>(
    cache,
    `${user.id}:audit:${page}`,
    (signal) => getSecurityAuditEvents({ userId: user.id, page, pageSize: 20 }, signal),
    onUnauthenticated,
  );
  return (
    <TabDataBoundary state={state} label="در حال دریافت سوابق تغییرات…">
      {(auditPage) =>
        auditPage.items.length === 0 ? (
          <EmptyState title="تغییر مدیریتی ثبت‌شده‌ای برای این حساب وجود ندارد." />
        ) : (
          <div className="space-y-3">
            <DataTable
              caption="سوابق تغییرات مدیریتی کاربر"
              columns={auditColumns}
              rows={auditPage.items}
              getRowKey={(event) => event.id}
            />
            {auditPage.totalCount > auditPage.pageSize ? (
              <nav aria-label="صفحه‌بندی سوابق تغییرات" className="flex items-center justify-between text-sm">
                <button
                  type="button"
                  onClick={() => setPage((value) => Math.max(1, value - 1))}
                  disabled={page <= 1}
                  className="rounded-lg border border-line px-3 py-1.5 font-medium text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-40"
                >
                  قبلی
                </button>
                <span className="text-sub">
                  صفحه {page} از {Math.ceil(auditPage.totalCount / auditPage.pageSize)}
                </span>
                <button
                  type="button"
                  onClick={() => setPage((value) => value + 1)}
                  disabled={page >= Math.ceil(auditPage.totalCount / auditPage.pageSize)}
                  className="rounded-lg border border-line px-3 py-1.5 font-medium text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-40"
                >
                  بعدی
                </button>
              </nav>
            ) : null}
          </div>
        )
      }
    </TabDataBoundary>
  );
}
