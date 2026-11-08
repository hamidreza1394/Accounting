import { useEffect, useRef, useState } from 'react';
import { linkHrEmployeeUser, listHrEmployees, unlinkHrEmployeeUser } from '@/features/hr/api/hrClient';
import { ApiError } from '@/api/client';
import type { HrEmployeeListItem } from '@/features/hr/api/hrTypes';
import { Alert } from '@/components/ui/Alert';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { Input } from '@/components/ui/FormField';
import { EmptyState, LoadingState } from '@/components/Feedback';

/**
 * The connect / disconnect actions of the linked personnel record (stage 3, PR 3-C2).
 * The server is the only authority: `hr.employees.user-link.manage` decides per company,
 * the user id must resolve to an active membership of the same company, and the one-to-one
 * anchoring and terminated-employee rules are enforced by the endpoint (D-072). The picker
 * lists only employees without a linked account (the honest "connect a person to their
 * account" flow); re-anchoring an already-connected employee belongs to the HR management
 * pages, never to a silent move from the user profile.
 */

export type LinkTarget = { companyId: string; companyName: string };
export type UnlinkTarget = { companyId: string; companyName: string; employeeId: string; employeeName: string };

const searchFailure = 'جست‌وجوی کارمندان انجام نشد. لطفاً دوباره تلاش کنید.';
const linkFailure = 'اتصال پرونده پرسنلی انجام نشد. لطفاً دوباره تلاش کنید.';
const unlinkFailure = 'قطع اتصال پرونده پرسنلی انجام نشد. لطفاً دوباره تلاش کنید.';
const rateLimited = 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';

function failureMessage(cause: unknown, fallback: string): string {
  if (cause instanceof ApiError) return cause.status === 429 ? rateLimited : cause.message;
  return fallback;
}

const employeeName = (employee: HrEmployeeListItem) => `${employee.firstName} ${employee.lastName}`.trim();

/** The employee picker of the link dialog: a server-side search over the allowed scope. */
function EmployeeSearch({ companyId, onPick }: { companyId: string; onPick: (employee: HrEmployeeListItem) => void }) {
  const [search, setSearch] = useState('');
  const [rows, setRows] = useState<HrEmployeeListItem[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const busyRef = useRef(false);
  const latestSearch = useRef(search);
  latestSearch.current = search;

  const run = async () => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setFailure(null);
    try {
      const page = await listHrEmployees(companyId, { search: latestSearch.current.trim(), page: 1, pageSize: 25 });
      // Only employees without a linked account are connectable here; already-connected
      // records stay visible in the HR management pages, never silently re-anchored.
      setRows(page.items.filter((employee) => !employee.hasLinkedUser));
    } catch (cause) {
      setFailure(failureMessage(cause, searchFailure));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3">
      <form
        className="flex gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          void run();
        }}
      >
        <Input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="نام، نام خانوادگی یا شماره پرسنلی…"
          aria-label="جست‌وجوی کارمند"
          maxLength={120}
          className="flex-1"
        />
        <button
          type="submit"
          disabled={busy}
          className="min-h-10 rounded-lg border border-line bg-white px-4 text-xs font-bold text-teal-deep outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-60"
        >
          جست‌وجو
        </button>
      </form>
      {failure ? <Alert tone="error">{failure}</Alert> : null}
      {busy ? <LoadingState label="در حال جست‌وجو…" /> : null}
      {!busy && rows !== null && rows.length === 0 ? (
        <EmptyState title="کارمندی بدون حساب متصل با این جست‌وجو پیدا نشد." />
      ) : null}
      {!busy && rows !== null && rows.length > 0 ? (
        <ul aria-label="کارمندان بدون حساب متصل" className="max-h-56 divide-y divide-line overflow-y-auto rounded-xl border border-line">
          {rows.map((employee) => (
            <li key={employee.id} className="flex items-center justify-between gap-2 px-3 py-2">
              <span className="min-w-0 text-sm text-ink">
                {employeeName(employee)}
                <span dir="ltr" className="ms-2 text-xs text-sub">
                  {employee.employeeNumber}
                </span>
              </span>
              <button
                type="button"
                onClick={() => onPick(employee)}
                aria-label={`انتخاب ${employeeName(employee)}`}
                className="min-h-9 shrink-0 rounded-lg border border-teal-deep/40 px-3 text-xs font-bold text-teal-deep outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
              >
                انتخاب
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

type Props = {
  linkTarget: LinkTarget | null;
  unlinkTarget: UnlinkTarget | null;
  /** The profile user; the server resolves it to an active membership of the same company. */
  userId: string;
  onClose: () => void;
  onLinked: (message: string) => void;
  onUnlinked: (message: string) => void;
  onUnauthenticated: (error: unknown) => boolean;
};

export function HrLinkActions({ linkTarget, unlinkTarget, userId, onClose, onLinked, onUnlinked, onUnauthenticated }: Props) {
  const [picked, setPicked] = useState<HrEmployeeListItem | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const busyRef = useRef(false);

  useEffect(() => {
    if (linkTarget !== null) {
      setPicked(null);
      setFailure(null);
      setBusy(false);
      busyRef.current = false;
    }
  }, [linkTarget]);

  if (linkTarget === null && unlinkTarget === null) return null;

  const link = async () => {
    if (busyRef.current || linkTarget === null || picked === null) return;
    busyRef.current = true;
    setBusy(true);
    setFailure(null);
    try {
      await linkHrEmployeeUser(linkTarget.companyId, picked.id, userId);
      onLinked(`پرونده پرسنلی «${employeeName(picked)}» به این حساب متصل شد.`);
      onClose();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      setFailure(failureMessage(cause, linkFailure));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  const unlink = async () => {
    if (busyRef.current || unlinkTarget === null) return;
    busyRef.current = true;
    setBusy(true);
    setFailure(null);
    try {
      await unlinkHrEmployeeUser(unlinkTarget.companyId, unlinkTarget.employeeId);
      onUnlinked('اتصال پرونده پرسنلی قطع شد.');
      onClose();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      setFailure(failureMessage(cause, unlinkFailure));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  if (linkTarget !== null) {
    return (
      <ConfirmDialog
        open
        busy={busy}
        title={`اتصال پرونده پرسنلی — ${linkTarget.companyName}`}
        description="کارمندِ بدون حساب متصل را جست‌وجو و انتخاب کنید. اتصال فقط به عضویت فعال همین کاربر در همین شرکت ممکن است و در سوابق امنیتی ثبت می‌شود."
        confirmLabel={picked === null ? 'اتصال' : `اتصال ${employeeName(picked)}`}
        onConfirm={link}
        onCancel={onClose}
      >
        <div className="space-y-3">
          {failure ? <Alert tone="error">{failure}</Alert> : null}
          <EmployeeSearch companyId={linkTarget.companyId} onPick={setPicked} />
        </div>
      </ConfirmDialog>
    );
  }

  return (
    <ConfirmDialog
      open
      tone="danger"
      busy={busy}
      title={`قطع اتصال — ${unlinkTarget?.companyName ?? ''}`}
      description={`اتصال پرونده پرسنلی «${unlinkTarget?.employeeName ?? ''}» از این حساب قطع می‌شود. اتصال دوباره بعداً از همین بخش ممکن است.`}
      confirmLabel="قطع اتصال"
      onConfirm={unlink}
      onCancel={onClose}
    >
      {failure ? <Alert tone="error">{failure}</Alert> : null}
    </ConfirmDialog>
  );
}
