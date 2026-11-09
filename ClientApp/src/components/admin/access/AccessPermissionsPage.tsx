import { useCallback, useEffect, useId, useRef, useState, type MouseEvent, type ReactNode } from 'react';
import { ApiError, assignCompanyRoleMember, getCompanyRoles, getEffectivePermissions } from '../../../api/client';
import type { CompanyDirectoryUser, CompanyRole, EffectivePermissions } from '../../../api/types';
import type { CanHint } from '../../../lib/permissions';
import { Link, useNavigate, useSearchParams } from '../../../lib/router';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { ErrorState, LoadingState } from '../../Feedback';
import { Alert } from '../../ui/Alert';
import { Badge } from '../../ui/Badge';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { FormField, Select } from '../../ui/FormField';
import { Button } from '../../ui/button';
import { CompanySelect } from '../roles/CompanySelect';
import { newRolePath, resolveCompanyId, useCompanyChoices } from '../roles/useCompanyChoices';
import { usePermissionCatalog } from '../roles/usePermissionCatalog';
import { AccessRoleEditor } from './AccessRoleEditor';
import { AccessUserPicker, userDisplayName } from './AccessUserPicker';
import { useAccessSubsystems } from './useAccessSubsystems';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** The work-context company; used until the user picks another one. */
  defaultCompanyId: string;
  /** UI hints only (`/me/permissions`); the server decides every request. */
  can?: CanHint;
  isInstallationAdministrator?: boolean;
  /** A 403 from the server: the caller re-reads the real permissions. */
  onForbidden?: (() => void) | undefined;
};

const denyAll: CanHint = () => false;
const number = new Intl.NumberFormat('fa-IR');

type RolesState =
  | { kind: 'loading' }
  | { kind: 'ready'; roles: CompanyRole[]; version: number; companyId: string }
  | { kind: 'error'; status: number | null; message: string };

type EffectiveState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; data: EffectivePermissions }
  | { kind: 'none' }
  | { kind: 'error'; message: string };

function Section({ title, children }: { title: string; children: ReactNode }) {
  const id = useId();
  return (
    <section aria-labelledby={id} className="space-y-3 rounded-xl border border-line bg-white p-5">
      <h2 id={id} className="text-base font-bold text-ink">
        {title}
      </h2>
      {children}
    </section>
  );
}

/**
 * «مجوزهای دسترسی»: picks a company (from the server's list), optionally a user of that company and a real
 * role, assigns the role through the existing endpoint and edits the role's permissions in two tabs. The
 * page only presents and sends what the server accepts; the company id lives only in the URL (never in a
 * body) and the server re-checks membership, permission, ceiling and concurrency on every request.
 */
export function AccessPermissionsPage({ onUnauthenticated, defaultCompanyId, can = denyAll, isInstallationAdministrator = false, onForbidden }: Props) {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const companyId = resolveCompanyId(params, defaultCompanyId);
  const { state: choices, retry: retryCompanies } = useCompanyChoices(onUnauthenticated);
  const { state: catalogState, retry: retryCatalog } = usePermissionCatalog(onUnauthenticated);
  const { state: subsystems, retry: retrySubsystems } = useAccessSubsystems(onUnauthenticated);
  const companyName = choices.kind === 'ready' ? choices.companies.find((company) => company.id === companyId)?.name : undefined;
  usePageBreadcrumb([{ label: 'مدیریت سیستم' }, { label: 'مجوزهای دسترسی' }]);

  const roleSelectId = useId();
  const [roles, setRoles] = useState<RolesState>({ kind: 'loading' });
  const [rolesAttempt, setRolesAttempt] = useState(0);
  const [user, setUser] = useState<CompanyDirectoryUser | null>(null);
  const [roleId, setRoleId] = useState('');
  const [effective, setEffective] = useState<EffectiveState>({ kind: 'idle' });
  const [effectiveAttempt, setEffectiveAttempt] = useState(0);
  const [dirty, setDirty] = useState(false);
  const [guard, setGuard] = useState<(() => void) | null>(null);
  const [assignOpen, setAssignOpen] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const assigningRef = useRef(false);
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | null>(null);

  // Changing the company drops everything that belonged to the previous one.
  useEffect(() => {
    setUser(null);
    setRoleId('');
    setMessage(null);
    setEffective({ kind: 'idle' });
  }, [companyId]);

  useEffect(() => {
    const controller = new AbortController();
    setRoles((current) => (current.kind === 'ready' && current.companyId === companyId ? current : { kind: 'loading' }));
    getCompanyRoles(companyId, controller.signal)
      .then((list) => {
        if (controller.signal.aborted) return;
        setRoles((current) => ({ kind: 'ready', roles: list, companyId, version: (current.kind === 'ready' ? current.version : 0) + 1 }));
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        if (cause instanceof ApiError && cause.status === 403) onForbidden?.();
        setRoles({
          kind: 'error',
          status: cause instanceof ApiError ? cause.status : null,
          message: cause instanceof ApiError ? cause.message : 'دریافت نقش‌های شرکت انجام نشد. لطفاً دوباره تلاش کنید.',
        });
      });
    return () => controller.abort();
    // `onForbidden` is a stable callback of the shell; the load must not restart when it is re-created.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyId, rolesAttempt, onUnauthenticated]);

  // The membership and effective permissions of the chosen user (an inactive membership has none: no request).
  const userId = user?.userId;
  const membershipActive = user?.membershipIsActive === true;
  useEffect(() => {
    if (!userId) {
      setEffective({ kind: 'idle' });
      return undefined;
    }
    if (!membershipActive) {
      setEffective({ kind: 'none' });
      return undefined;
    }
    const controller = new AbortController();
    setEffective({ kind: 'loading' });
    getEffectivePermissions(companyId, userId, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setEffective({ kind: 'ready', data });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        if (cause instanceof ApiError && cause.status === 404) setEffective({ kind: 'none' });
        else setEffective({ kind: 'error', message: cause instanceof ApiError ? cause.message : 'دریافت دسترسی‌های مؤثر کاربر انجام نشد.' });
      });
    return () => controller.abort();
  }, [companyId, userId, membershipActive, effectiveAttempt, onUnauthenticated]);

  /** Runs `action` now, or after the user agreed to drop the unsaved permission changes. */
  const guarded = (action: () => void) => {
    if (dirty) setGuard(() => action);
    else action();
  };

  const changeCompany = (next: string) => {
    if (next === companyId) return;
    guarded(() => setParams(new URLSearchParams({ company: next }), { replace: true }));
  };

  const chooseUser = (picked: CompanyDirectoryUser) => {
    const list = roles.kind === 'ready' ? roles.roles : [];
    const targetRoleId = list.some((candidate) => candidate.id === picked.roleId) ? picked.roleId : roleId;
    const apply = () => {
      setUser(picked);
      setMessage(null);
      setRoleId(targetRoleId);
    };
    // Only a different role replaces the editor (and its draft); choosing a user alone keeps the draft.
    if (targetRoleId !== roleId) guarded(apply);
    else apply();
  };

  const chooseRole = (next: string) =>
    guarded(() => {
      setRoleId(next);
      setMessage(null);
    });

  const rolesList = roles.kind === 'ready' ? roles.roles : [];
  const role = rolesList.find((candidate) => candidate.id === roleId) ?? null;
  const canUpdate = can('admin.roles.update', companyId);
  const canAssign = can('admin.roles.assign', companyId);
  const canCreate = can('admin.roles.create', companyId);

  const onSaved = (savedCodes: string[]) => {
    setRoles((current) =>
      current.kind === 'ready'
        ? {
            ...current,
            version: current.version + 1,
            roles: current.roles.map((candidate) =>
              candidate.id === roleId ? { ...candidate, permissionCodes: savedCodes, permissionCount: savedCodes.length } : candidate,
            ),
          }
        : current,
    );
    setMessage({ tone: 'info', text: 'مجوزهای نقش ذخیره شد و تغییرات روی دسترسی مؤثر همهٔ اعضای فعال آن نقش اعمال می‌شود.' });
    setRolesAttempt((value) => value + 1);
    setEffectiveAttempt((value) => value + 1);
  };

  const assignBlock: string | null = !user
    ? null
    : !role
      ? null
      : role.id === user.roleId
        ? 'این نقش هم‌اکنون نقش کاربر است.'
        : dirty
          ? 'ابتدا تغییرات ذخیره‌نشدهٔ مجوزهای نقش را ذخیره یا لغو کنید.'
          : !user.membershipIsActive
            ? 'عضویت کاربر در این شرکت فعال نیست و تخصیص نقش فقط به عضو فعال ممکن است؛ عضویت غیرفعال یا درخواست در انتظار دسترسی عملیاتی ایجاد نمی‌کند.'
            : !user.userIsActive
              ? 'حساب این کاربر غیرفعال است.'
              : role.isSystemRole
                ? 'تخصیص نقش سیستمی مجاز نیست.'
                : !role.isActive
                  ? 'نقش غیرفعال را نمی‌توان به کاربر داد.'
                  : !canAssign
                    ? 'شما مجوز تخصیص نقش را ندارید.'
                    : null;
  const showAssign = user !== null && role !== null && role.id !== user.roleId;

  const assign = async () => {
    if (!user || !role || assigningRef.current) return;
    assigningRef.current = true;
    setAssigning(true);
    setMessage(null);
    try {
      await assignCompanyRoleMember(companyId, role.id, user.userId);
      setUser({ ...user, roleId: role.id, roleName: role.name, roleCode: role.code, roleIsActive: role.isActive });
      setMessage({ tone: 'info', text: `نقش «${role.name}» به ${userDisplayName(user)} داده شد.` });
      setRolesAttempt((value) => value + 1);
      setEffectiveAttempt((value) => value + 1);
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      if (cause instanceof ApiError && cause.status === 403) onForbidden?.();
      setMessage({ tone: 'error', text: cause instanceof ApiError ? cause.message : 'تخصیص نقش انجام نشد. لطفاً دوباره تلاش کنید.' });
    } finally {
      assigningRef.current = false;
      setAssigning(false);
    }
  };

  const onCreateClick = useCallback(
    (event: MouseEvent<HTMLAnchorElement>) => {
      if (!dirty) return;
      event.preventDefault();
      setGuard(() => () => navigate(newRolePath(companyId)));
    },
    [dirty, companyId, navigate],
  );

  let body: ReactNode = null;
  if (roles.kind === 'loading' || catalogState.kind === 'loading') {
    body = <LoadingState label="در حال دریافت نقش‌ها و کاتالوگ مجوزها…" />;
  } else if (roles.kind === 'error') {
    body =
      roles.status === 403 ? (
        <ErrorState message="شما مجوز مشاهدهٔ نقش‌های این شرکت را ندارید." />
      ) : roles.status === 404 ? (
        <ErrorState message="شرکت پیدا نشد یا به آن دسترسی ندارید." />
      ) : (
        <ErrorState message={roles.message} onRetry={() => setRolesAttempt((value) => value + 1)} />
      );
  } else if (catalogState.kind === 'error') {
    body = <ErrorState message={catalogState.message} onRetry={retryCatalog} />;
  } else if (!role) {
    body = <p className="rounded-xl border border-dashed border-line bg-white p-6 text-center text-sm text-sub">برای مدیریت مجوزها، یک نقش را انتخاب کنید.</p>;
  } else if (catalogState.kind === 'ready') {
    body = (
      <section aria-label={`مجوزهای نقش ${role.name}`} className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-base font-bold text-ink">مجوزهای نقش «{role.name}»</h2>
          {role.isSystemRole ? <Badge tone="info" icon={null}>سیستمی</Badge> : null}
          {!role.isActive ? <Badge tone="neutral" icon={null}>غیرفعال</Badge> : null}
          <Badge tone={role.activeMemberCount > 1 ? 'warning' : 'neutral'} icon={null}>
            {number.format(role.activeMemberCount)} عضو فعال
          </Badge>
        </div>
        {role.activeMemberCount > 1 ? (
          <p className="text-xs text-sub">این نقش بین چند کاربر مشترک است؛ هر تغییر مجوز، دسترسی همهٔ آن‌ها را تغییر می‌دهد و پیش از ذخیره اثر آن نمایش داده می‌شود.</p>
        ) : null}
        <AccessRoleEditor
          key={`${role.id}:${roles.version}`}
          role={role}
          companyId={companyId}
          permissions={catalogState.catalog}
          subsystems={subsystems}
          onRetrySubsystems={retrySubsystems}
          canUpdate={canUpdate}
          isInstallationAdministrator={isInstallationAdministrator}
          onUnauthenticated={onUnauthenticated}
          onForbidden={onForbidden}
          onSaved={onSaved}
          onReload={() => setRolesAttempt((value) => value + 1)}
          onDirtyChange={setDirty}
        />
      </section>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">مجوزهای دسترسی</h1>
        <p className="text-sm text-sub">
          نقش کاربر را در یک شرکت تعیین کنید و مجوزهای هر نقش را بر اساس زیرسیستم‌ها و فرم‌های واقعی سامانه مدیریت کنید. دسترسی همیشه از سمت سرور بررسی می‌شود و مجوز مستقیم کاربر وجود ندارد.
        </p>
      </header>

      {message ? <Alert tone={message.tone}>{message.text}</Alert> : null}

      <Section title="شرکت و کاربر">
        <CompanySelect choices={choices} value={companyId} onChange={changeCompany} onRetry={retryCompanies} />
        <AccessUserPicker key={companyId} companyId={companyId} onChoose={chooseUser} onUnauthenticated={onUnauthenticated} />
        {user ? (
          <div aria-label="کاربر انتخاب‌شده" role="group" className="space-y-2 rounded-lg border border-line bg-cool p-4">
            <div className="flex flex-wrap items-center gap-2">
              <p className="font-bold text-ink">{userDisplayName(user)}</p>
              {user.username ? <bdi dir="ltr" className="text-sm text-sub">{user.username}</bdi> : null}
              <Badge tone={user.userIsActive ? 'success' : 'neutral'} icon={null}>{user.userIsActive ? 'حساب فعال' : 'حساب غیرفعال'}</Badge>
              <Badge tone={user.membershipIsActive ? 'success' : 'warning'} icon={null}>
                {user.membershipIsActive ? `عضویت فعال در ${companyName ?? 'شرکت'}` : 'عضویت غیرفعال'}
              </Badge>
              <Button type="button" variant="ghost" size="sm" onClick={() => { setUser(null); setMessage(null); }}>
                حذف انتخاب کاربر
              </Button>
            </div>
            <p className="text-sm text-ink">
              نقش فعلی: <span className="font-semibold">{user.roleName}</span>
              {!user.roleIsActive ? ' (نقش غیرفعال)' : ''}
            </p>
            {!user.membershipIsActive ? (
              <p className="text-xs font-semibold text-danger">عضویت غیرفعال یا در انتظار تأیید دسترسی عملیاتی ایجاد نمی‌کند.</p>
            ) : null}
            {effective.kind === 'loading' ? <p role="status" className="text-xs text-sub">در حال دریافت دسترسی‌های مؤثر…</p> : null}
            {effective.kind === 'ready' ? (
              <p className="text-xs text-sub">
                دسترسی مؤثر: {number.format(effective.data.permissionCodes.length)} مجوز از نقش‌های {effective.data.roleNames.join('، ') || '—'}.
              </p>
            ) : null}
            {effective.kind === 'none' && user.membershipIsActive ? <p className="text-xs text-sub">دسترسی مؤثری برای این کاربر در شرکت ثبت نشده است.</p> : null}
            {effective.kind === 'error' ? <p role="alert" className="text-xs text-danger">{effective.message}</p> : null}
          </div>
        ) : null}
      </Section>

      <Section title="نقش">
        {roles.kind === 'ready' ? (
          <>
            <div className="flex flex-wrap items-end gap-3">
              <FormField label="نقش" className="w-full sm:w-80">
                <Select id={roleSelectId} className="h-11 sm:h-10" value={roleId} onChange={(event) => chooseRole(event.target.value)}>
                  <option value="">انتخاب نقش…</option>
                  {rolesList.map((candidate) => (
                    <option key={candidate.id} value={candidate.id}>
                      {candidate.name}
                      {candidate.isSystemRole ? ' (سیستمی)' : ''}
                      {!candidate.isActive ? ' (غیرفعال)' : ''}
                    </option>
                  ))}
                </Select>
              </FormField>
              {showAssign ? (
                <Button type="button" size="lg" disabled={assignBlock !== null || assigning} aria-busy={assigning || undefined} onClick={() => setAssignOpen(true)}>
                  تخصیص این نقش به کاربر
                </Button>
              ) : null}
              {canCreate ? (
                <Link
                  to={newRolePath(companyId)}
                  onClick={onCreateClick}
                  className="inline-flex min-h-11 items-center rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
                >
                  ایجاد نقش اختصاصی
                </Link>
              ) : null}
            </div>
            {showAssign && assignBlock ? <p className="text-xs text-sub">{assignBlock}</p> : null}
            {rolesList.length === 0 ? <p className="text-sm text-sub">برای این شرکت هنوز نقشی تعریف نشده است.</p> : null}
          </>
        ) : (
          <p className="text-sm text-sub">نقش‌ها پس از دریافت از سرور نمایش داده می‌شوند.</p>
        )}
      </Section>

      {body}

      <ConfirmDialog
        open={assignOpen}
        title="تخصیص نقش به کاربر"
        description={
          user && role
            ? `نقش «${user.roleName}» ${userDisplayName(user)} در ${companyName ? `شرکت «${companyName}»` : 'این شرکت'} با نقش «${role.name}» (${number.format(role.permissionCount)} مجوز) جایگزین می‌شود و دسترسی مؤثر او تغییر می‌کند.`
            : ''
        }
        confirmLabel="تأیید و تخصیص"
        busy={assigning}
        onConfirm={async () => {
          setAssignOpen(false);
          await assign();
        }}
        onCancel={() => setAssignOpen(false)}
      />
      <ConfirmDialog
        open={guard !== null}
        tone="danger"
        title="تغییرات ذخیره‌نشده"
        description="تغییرات مجوزهای نقش ذخیره نشده‌اند و با ادامه از بین می‌روند. ادامه می‌دهید؟"
        confirmLabel="ادامه بدون ذخیره"
        cancelLabel="ماندن و ادامه ویرایش"
        onConfirm={() => {
          const action = guard;
          setGuard(null);
          action?.();
        }}
        onCancel={() => setGuard(null)}
      />
    </div>
  );
}
