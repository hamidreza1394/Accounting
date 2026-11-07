import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ApiError,
  approveMembershipRequest,
  getAssignableRoles,
  getPendingMembershipRequests,
  rejectMembershipRequest,
} from '../../api/client';
import type { AssignableRole, MembershipRequestPage, PendingMembershipRequest } from '../../api/types';
import { formatDateTime } from '../../lib/format';
import { Alert } from '../ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Button } from '../ui/button';
import { ConfirmDialog } from '../ui/ConfirmDialog';
import { DataTable, type DataTableColumn } from '../ui/DataTable';
import { FormField, Select, Textarea } from '../ui/FormField';
import { StatusBadge } from '../ui/StatusBadge';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** The active work-context company: the queue always belongs to it. */
  companyId: string;
  companyName?: string;
};

const pageSizes = [10, 20, 50] as const;
const reasonLimit = 500;

type Decision = { kind: 'approve' | 'reject'; request: PendingMembershipRequest };
type RolesState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; roles: AssignableRole[] }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

/** Display name plus username, so two requesters with the same name have distinct button names. */
function requesterLabel(request: PendingMembershipRequest): string {
  return request.username ? `${request.requesterDisplayName} (${request.username})` : request.requesterDisplayName;
}

function message(cause: unknown, fallback: string): string {
  return cause instanceof ApiError ? cause.message : fallback;
}

/**
 * The pending membership requests of the active company, for its approver. Approving needs one
 * active access group of THIS company; the server re-checks the permission, the requester, the
 * group and self-approval on every call (D-065). Nothing here grants access by itself.
 */
export function PendingMembershipRequestsPage({ onUnauthenticated, companyId, companyName }: Props) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(20);
  const [data, setData] = useState<MembershipRequestPage<PendingMembershipRequest> | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [flash, setFlash] = useState<string | null>(null);
  const flashRef = useRef<HTMLDivElement>(null);

  const [decision, setDecision] = useState<Decision | null>(null);
  const [busy, setBusy] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [roleId, setRoleId] = useState('');
  const [reason, setReason] = useState('');
  const [rolesState, setRolesState] = useState<RolesState>({ kind: 'idle' });
  const [rolesPage, setRolesPage] = useState(1);
  const [rolesTotal, setRolesTotal] = useState(0);

  const reload = useCallback(() => setAttempt((value) => value + 1), []);

  // After a decision the row (and the button that opened the dialog) is gone: move focus to the
  // confirmation so keyboard and screen reader users are not left on <body>.
  useEffect(() => {
    if (flash) flashRef.current?.focus();
  }, [flash]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getPendingMembershipRequests(companyId, page, pageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        // A stale page (the last items of the last page were just decided) moves to the last page
        // that still exists. Only when that really is a different page; otherwise show the data,
        // so a racing total can never leave the table loading forever.
        if (result.items.length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (lastPage < page) {
            setPage(lastPage);
            return;
          }
        }
        setData(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 404
              ? 'شما مجوز بررسی درخواست‌های عضویت این شرکت را ندارید یا شرکت پیدا نشد.'
              : message(cause, 'دریافت درخواست‌ها انجام نشد. لطفاً دوباره تلاش کنید.'),
        });
        setLoading(false);
      });
  return () => controller.abort();
  }, [companyId, page, pageSize, attempt, onUnauthenticated]);

  // Roles are needed only when the approver opens the approve dialog.
  useEffect(() => {
    if (decision?.kind !== 'approve') return undefined;
    const controller = new AbortController();
    setRolesState({ kind: 'loading' });
    getAssignableRoles(companyId, rolesPage, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) { setRolesState({ kind: 'ready', roles: result.items.filter((role) => !role.isSystemRole) }); setRolesTotal(result.totalCount); }
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        if (cause instanceof ApiError && (cause.status === 403 || cause.status === 404)) setRolesState({ kind: 'forbidden' });
        else setRolesState({ kind: 'error', message: message(cause, 'دریافت گروه‌های دسترسی انجام نشد.') });
      });
    return () => controller.abort();
  }, [decision?.kind, decision?.request.id, companyId, rolesPage, onUnauthenticated]);

  const open = (kind: Decision['kind'], request: PendingMembershipRequest) => {
    setFlash(null);
    setDialogError(null);
    setRoleId('');
    setReason('');
    setRolesState({ kind: 'idle' });
    setRolesPage(1);
    setRolesTotal(0);
    setDecision({ kind, request });
  };
  const close = () => {
    if (!busy) setDecision(null);
  };

  const confirm = async () => {
    if (!decision) return;
    setDialogError(null);
    if (decision.kind === 'approve' && rolesState.kind !== 'ready') {
      setDialogError(
        rolesState.kind === 'loading'
          ? 'گروه‌های دسترسی هنوز دریافت نشده‌اند؛ چند لحظه صبر کنید.'
          : 'بدون فهرست گروه‌های دسترسی، تأیید ممکن نیست.',
      );
      return;
    }
    if (decision.kind === 'approve' && !roleId) {
      setDialogError('یک گروه دسترسی برای کاربر انتخاب کنید.');
      return;
    }
    if (decision.kind === 'reject' && reason.length > reasonLimit) {
      setDialogError(`توضیح رد درخواست باید حداکثر ${reasonLimit} نویسه باشد.`);
      return;
    }
    setBusy(true);
    const name = decision.request.requesterDisplayName;
    try {
      if (decision.kind === 'approve') await approveMembershipRequest(companyId, decision.request.id, roleId);
      else await rejectMembershipRequest(companyId, decision.request.id, reason.trim() === '' ? null : reason.trim());
      setFlash(decision.kind === 'approve' ? `درخواست «${name}» تأیید شد.` : `درخواست «${name}» رد شد.`);
      setDecision(null);
      reload();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      setDialogError(message(cause, 'انجام عملیات ممکن نشد. لطفاً دوباره تلاش کنید.'));
      // Someone else decided it first, or it changed: the list must not keep offering it.
      if (cause instanceof ApiError && (cause.status === 404 || cause.status === 409)) reload();
    } finally {
      setBusy(false);
    }
  };

  const columns = useMemo<readonly DataTableColumn<PendingMembershipRequest>[]>(
    () => [
      {
        key: 'requester',
        header: 'درخواست‌کننده',
        cell: (request) => (
          <span className="flex flex-col">
            <span>{request.requesterDisplayName}</span>
            {request.username ? <code dir="ltr" className="text-xs text-sub">{request.username}</code> : null}
          </span>
        ),
      },
      { key: 'requested', header: 'تاریخ درخواست', cell: (request) => formatDateTime(request.requestedAtUtc) },
      {
        key: 'account',
        header: 'وضعیت حساب',
        cell: (request) => <StatusBadge status={request.isRequesterActive ? 'active' : 'inactive'} />,
      },
      {
        key: 'actions',
        header: 'عملیات',
        cell: (request) => (
          <span className="flex flex-wrap gap-2">
            <Button
              type="button"
              size="sm"
              disabled={!request.isRequesterActive}
              onClick={() => open('approve', request)}
              aria-label={`تأیید درخواست ${requesterLabel(request)}`}
            >
              تأیید
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => open('reject', request)}
              aria-label={`رد درخواست ${requesterLabel(request)}`}
            >
              رد
            </Button>
          </span>
        ),
      },
    ],
    [],
  );

  const forbidden = failure?.status === 404;
  const approving = decision?.kind === 'approve';

  usePageBreadcrumb([{ label: 'مدیریت سیستم' }, { label: 'درخواست‌های عضویت' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">درخواست‌های عضویت در انتظار تأیید</h1>
        <p className="text-sm text-sub">
          درخواست‌هایی که برای عضویت در {companyName ? `شرکت «${companyName}»` : 'شرکت انتخاب‌شده'} ثبت شده‌اند. عضویت فقط پس از تأیید شما و تعیین گروه دسترسی ایجاد می‌شود.
        </p>
      </header>

      {flash ? <Alert ref={flashRef} tone="info">{flash}</Alert> : null}

      {forbidden ? (
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure.message}</p>
        </div>
      ) : (
        <DataTable
          caption="فهرست درخواست‌های عضویت در انتظار تأیید"
          columns={columns}
          rows={data?.items ?? []}
          getRowKey={(request) => request.id}
          loading={loading}
          error={failure?.message ?? null}
          onRetry={reload}
          emptyTitle="درخواستی در انتظار تأیید نیست"
          emptyDescription="درخواست جدیدی برای عضویت در این شرکت ثبت نشده است."
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
      )}

      <ConfirmDialog
        open={decision !== null}
        title={approving ? 'تأیید درخواست عضویت' : 'رد درخواست عضویت'}
        tone={approving ? 'default' : 'danger'}
        confirmLabel={approving ? 'تأیید و ایجاد عضویت' : 'رد درخواست'}
        busy={busy}
        onConfirm={confirm}
        onCancel={close}
        description={
          decision ? (
            <div className="space-y-3">
              <p>
                {approving
                  ? `با تأیید، «${decision.request.requesterDisplayName}» با گروه دسترسی انتخاب‌شده عضو این شرکت می‌شود.`
                  : `درخواست «${decision.request.requesterDisplayName}» رد می‌شود و دسترسی‌ای ایجاد نخواهد شد.`}
              </p>
              {approving ? (
                <>
                  {rolesState.kind === 'forbidden' ? (
                    <p role="alert" className="text-danger">
                      حساب شما به فهرست گروه‌های دسترسی این شرکت دسترسی ندارد، پس تأیید از این صفحه ممکن نیست. با مدیر نصب هماهنگ کنید.
                    </p>
                  ) : rolesState.kind === 'error' ? (
                    <p role="alert" className="text-danger">{rolesState.message}</p>
                  ) : (
                    <FormField label="گروه دسترسی" required>
                      <Select
                        value={roleId}
                        onChange={(event) => setRoleId(event.target.value)}
                        disabled={rolesState.kind !== 'ready'}
                      >
                        <option value="">
                          {rolesState.kind === 'loading' ? 'در حال دریافت گروه‌ها…' : 'انتخاب کنید'}
                        </option>
                        {rolesState.kind === 'ready'
                          ? rolesState.roles.map((role) => (
                              <option key={role.id} value={role.id}>
                                {role.name}
                                {role.isSystemRole ? ' (سیستمی)' : ''}
                              </option>
                            ))
                          : null}
                      </Select>
                    </FormField>
                  )}
                  {rolesState.kind === 'ready' && rolesTotal > 100 ? (
                    <div className="flex gap-2">
                      <Button type="button" variant="outline" disabled={rolesPage === 1} onClick={() => { setRoleId(''); setRolesPage(rolesPage - 1); }}>گروه‌های قبلی</Button>
                      <Button type="button" variant="outline" disabled={rolesPage * 100 >= rolesTotal} onClick={() => { setRoleId(''); setRolesPage(rolesPage + 1); }}>گروه‌های بعدی</Button>
                    </div>
                  ) : null}
                </>
              ) : (
                <FormField label="توضیح رد درخواست (اختیاری)" hint={`حداکثر ${reasonLimit} نویسه؛ برای درخواست‌کننده نمایش داده می‌شود.`}>
                  <Textarea value={reason} maxLength={reasonLimit} onChange={(event) => setReason(event.target.value)} />
                </FormField>
              )}
              {dialogError ? <p role="alert" className="font-medium text-danger">{dialogError}</p> : null}
            </div>
          ) : null
        }
      />
    </div>
  );
}
