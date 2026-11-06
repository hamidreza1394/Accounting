import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ApiError, getCompanyRoleMembers, getCompanyRoles, transferCompanyRoleMembers } from '../../../api/client';
import type { CompanyRole, CompanyRoleMember, CompanyRoleMemberPage } from '../../../api/types';
import { formatDate } from '../../../lib/format';
import { Alert } from '../../ui/Alert';
import { Button } from '../../ui/button';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { DataTable, type DataTableColumn } from '../../ui/DataTable';
import { FormField, Select } from '../../ui/FormField';
import { StatusBadge } from '../../ui/StatusBadge';
import { RoleMemberAssign } from './RoleMemberAssign';

type Props = {
  role: CompanyRole;
  companyId: string;
  /** UI hint only (the server decides): `roles.assign` in this company, or installation administrator. */
  canAssign: boolean;
  onUnauthenticated: (error: unknown) => boolean;
  /** A 403 from the server: the caller re-reads the real permissions. */
  onForbidden?: (() => void) | undefined;
  /** Reports the current number of active members after every successful load. */
  onMemberCount?: (count: number) => void;
};

/** Mirrors `CompanyRoleAdministrationEndpoints.MaxTransferBatchSize`; the server enforces it. */
export const maxTransferBatchSize = 500;

const pageSizes = [10, 20, 50] as const;
const number = new Intl.NumberFormat('fa-IR');

type Targets = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ready'; roles: CompanyRole[] } | { kind: 'error'; message: string };

function displayName(member: CompanyRoleMember): string {
  const full = `${member.firstName ?? ''} ${member.lastName ?? ''}`.trim();
  return full || member.username || 'کاربر بدون نام';
}

function messageOf(cause: unknown, fallback: string): string {
  return cause instanceof ApiError ? cause.message : fallback;
}

/**
 * The active members of one access group and the bulk move of all of them to another group of the
 * same company. A move only changes the group of memberships that are already active; it never
 * creates or reactivates a membership (D-065) and is refused by the server for system groups.
 */
export function RoleMembersSection({ role, companyId, canAssign, onUnauthenticated, onForbidden, onMemberCount }: Props) {
  const targetFieldId = useId();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(20);
  const [data, setData] = useState<CompanyRoleMemberPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  const [targetId, setTargetId] = useState('');
  const [targets, setTargets] = useState<Targets>({ kind: 'idle' });
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [targetsAttempt, setTargetsAttempt] = useState(0);
  const noticeRef = useRef<HTMLDivElement>(null);
  const failureRef = useRef<HTMLDivElement>(null);
  const onMemberCountRef = useRef(onMemberCount);
  onMemberCountRef.current = onMemberCount;

  // After a transfer the opener button disappears: move focus to the confirmation so keyboard and
  // screen reader users keep their place.
  useEffect(() => {
    if (notice) noticeRef.current?.focus();
  }, [notice]);
  useEffect(() => {
    if (failure) failureRef.current?.focus();
  }, [failure]);

  const canTransfer = canAssign && !role.isSystemRole;
  const canAddMember = canTransfer && role.isActive;
  const total = data?.totalCount ?? 0;
  const tooMany = total > maxTransferBatchSize;

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getCompanyRoleMembers(companyId, role.id, page, pageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        // A stale page (members were moved away) goes to the last page that still exists. Only when
        // that is a different page; otherwise show the data so a racing total cannot hang loading.
        if (result.items.length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (lastPage < page) {
            setPage(lastPage);
            return;
          }
        }
        setData(result);
        setLoading(false);
        onMemberCountRef.current?.(result.totalCount);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setError(messageOf(cause, 'دریافت اعضای گروه انجام نشد. لطفاً دوباره تلاش کنید.'));
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, role.id, page, pageSize, attempt, onUnauthenticated]);

  // The possible targets are only needed when the administrator can move members.
  useEffect(() => {
    if (!canTransfer) return undefined;
    const controller = new AbortController();
    setTargets({ kind: 'loading' });
    getCompanyRoles(companyId, controller.signal)
      .then((roles) => {
        if (!controller.signal.aborted) setTargets({ kind: 'ready', roles });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setTargets({ kind: 'error', message: messageOf(cause, 'دریافت گروه‌های مقصد انجام نشد.') });
      });
    return () => controller.abort();
  }, [canTransfer, companyId, targetsAttempt, onUnauthenticated]);

  const targetChoices = useMemo(
    () => (targets.kind === 'ready' ? targets.roles.filter((candidate) => candidate.id !== role.id && candidate.isActive && !candidate.isSystemRole) : []),
    [targets, role.id],
  );
  const target = targetChoices.find((candidate) => candidate.id === targetId) ?? null;

  const columns = useMemo<readonly DataTableColumn<CompanyRoleMember>[]>(
    () => [
      { key: 'name', header: 'نام', cell: (member) => displayName(member) },
      { key: 'username', header: 'نام کاربری', cell: (member) => (member.username ? <code dir="ltr" className="text-xs">{member.username}</code> : '—') },
      { key: 'status', header: 'وضعیت حساب', cell: (member) => <StatusBadge status={member.isUserActive ? 'active' : 'inactive'} /> },
      { key: 'since', header: 'عضو از', cell: (member) => formatDate(member.memberSinceUtc) },
    ],
    [],
  );

  const confirm = async () => {
    if (!target) return;
    setBusy(true);
    setFailure(null);
    try {
      const moved = await transferCompanyRoleMembers(companyId, role.id, target.id);
      setNotice(`${number.format(moved)} عضو به گروه «${target.name}» منتقل شد.`);
      setConfirming(false);
      setTargetId('');
      setPage(1);
      setAttempt((value) => value + 1);
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      if (cause instanceof ApiError && cause.status === 403) onForbidden?.();
      // A refusal from the server (4xx) is definitive and its message is shown. Anything else (network,
      // 5xx, unreadable answer) may have been applied, so never claim nothing moved; show the real state.
      const refused = cause instanceof ApiError && cause.status >= 400 && cause.status < 500;
      // The dialog closes either way: the target may be stale, so the choice is made again.
      setConfirming(false);
      setFailure(
        refused
          ? cause.message
          : 'نتیجهٔ انتقال نامشخص است. فهرست اعضا دوباره بارگذاری شد؛ آن را بررسی کنید و در صورت نیاز دوباره تلاش کنید.',
      );
      setAttempt((value) => value + 1);
      // The target may be gone or inactive now: refresh the choices and drop a target that no longer qualifies.
      if (cause instanceof ApiError && (cause.status === 404 || cause.status === 409)) {
        setTargetId('');
        setTargetsAttempt((value) => value + 1);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby={`${targetFieldId}-title`} className="space-y-4 rounded-xl border border-line bg-white p-5">
      <div className="space-y-1">
        <h2 id={`${targetFieldId}-title`} className="text-lg font-extrabold text-title">
          اعضای این گروه{data ? ` (${number.format(total)})` : ''}
        </h2>
        <p className="text-sm text-sub">کاربرانی که در این شرکت عضویت فعال دارند و گروه دسترسی‌شان این گروه است.</p>
      </div>

      {notice ? <Alert ref={noticeRef} tone="info">{notice}</Alert> : null}
      {failure ? <Alert ref={failureRef} tone="error">{failure}</Alert> : null}

      <DataTable
        caption={`اعضای گروه ${role.name}`}
        columns={columns}
        rows={data?.items ?? []}
        getRowKey={(member) => member.userId}
        loading={loading}
        error={error}
        onRetry={() => setAttempt((value) => value + 1)}
        emptyTitle="این گروه عضو فعالی ندارد"
        emptyDescription="می‌توانید گروه را غیرفعال کنید یا کاربران را از مسیر مدیریت کاربران به آن اختصاص دهید."
        pagination={{
          page,
          pageSize,
          total,
          pageSizeOptions: pageSizes,
          onPageChange: (next) => {
            setNotice(null);
            setPage(next);
          },
          onPageSizeChange: (size) => {
            setNotice(null);
            setPageSize(size);
            setPage(1);
          },
        }}
      />

      {role.isSystemRole ? (
        <Alert tone="info">انتقال گروهی اعضا از یا به گروه سیستمی مجاز نیست.</Alert>
      ) : canTransfer && total > 0 ? (
        <div className="space-y-3 border-t border-line pt-4">
          <h3 className="text-base font-bold text-ink">انتقال همهٔ اعضا به گروه دیگر</h3>
          <p className="text-sm text-sub">
            فقط عضویت‌هایی که از قبل فعال‌اند به گروه مقصد منتقل می‌شوند؛ عضویت جدیدی ایجاد یا فعال نمی‌شود. پس از انتقال می‌توانید این گروه را غیرفعال کنید.
          </p>
          {tooMany ? (
            <Alert tone="warning">تعداد اعضا از سقف {number.format(maxTransferBatchSize)} نفر در هر انتقال بیشتر است و انتقال گروهی انجام نمی‌شود.</Alert>
          ) : targets.kind === 'error' ? (
            <Alert tone="error">{targets.message}</Alert>
          ) : (
            <div className="flex flex-wrap items-end gap-3">
              <div className="min-w-56 flex-1">
                <FormField label="گروه مقصد" hint="فقط گروه‌های فعال و غیرسیستمی همین شرکت.">
                  <Select
                    id={targetFieldId}
                    value={targetId}
                    onChange={(event) => setTargetId(event.target.value)}
                    disabled={targets.kind !== 'ready'}
                  >
                    <option value="">{targets.kind === 'loading' ? 'در حال دریافت گروه‌ها…' : 'انتخاب کنید'}</option>
                    {targetChoices.map((candidate) => (
                      <option key={candidate.id} value={candidate.id}>
                        {candidate.name}
                      </option>
                    ))}
                  </Select>
                </FormField>
              </div>
              <Button
                type="button"
                size="lg"
                disabled={!target}
                onClick={() => {
                  setFailure(null);
                  setNotice(null);
                  setConfirming(true);
                }}
              >
                انتقال اعضا…
              </Button>
            </div>
          )}
        </div>
      ) : null}

      {canAddMember ? (
        <RoleMemberAssign
          role={role}
          companyId={companyId}
          onUnauthenticated={onUnauthenticated}
          onForbidden={onForbidden}
          onAssigned={() => setAttempt((value) => value + 1)}
        />
      ) : null}

      <ConfirmDialog
        open={confirming && target !== null}
        title="انتقال همهٔ اعضای گروه"
        confirmLabel="انتقال اعضا"
        busy={busy}
        onConfirm={confirm}
        onCancel={() => {
          if (!busy) setConfirming(false);
        }}
        description={
          <div className="space-y-2">
            <p>
              {number.format(total)} عضو فعال گروه «{role.name}» به گروه «{target?.name}» منتقل می‌شوند. این کار دسترسی آن‌ها را به مجوزهای گروه مقصد تغییر می‌دهد و در سوابق امنیتی ثبت می‌شود.
            </p>
          </div>
        }
      />
    </section>
  );
}
