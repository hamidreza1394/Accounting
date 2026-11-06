import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Laptop, LogOut } from 'lucide-react';
import { ApiError, getMySessions, revokeMyOtherSessions, revokeMySession } from '../../api/client';
import type { MySession } from '../../api/types';
import { formatDateTime } from '../../lib/format';
import { LoadingState } from '../Feedback';
import { Alert } from '../ui/Alert';
import { ConfirmDialog } from '../ui/ConfirmDialog';

type Props = {
  /** Handles a 401 (expired, revoked elsewhere): the app returns to the login page. Returns true when it did. */
  onUnauthenticated: (error: unknown) => boolean;
  /** Called once the server confirmed that THIS browser's session ended (the cookie is already invalid). */
  onSignedOut: () => void;
};

type Load = { kind: 'loading' } | { kind: 'ready'; sessions: MySession[] } | { kind: 'error'; message: string };
type Pending = { kind: 'one'; session: MySession } | { kind: 'others'; count: number };

const number = new Intl.NumberFormat('fa-IR');
const loadFailure = 'دریافت نشست‌های فعال انجام نشد. لطفاً دوباره تلاش کنید.';
const rateLimited = 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';

/** The current session first, then the most recently active. */
function ordered(sessions: MySession[]): MySession[] {
  return [...sessions].sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent) || b.lastSeenAtUtc.localeCompare(a.lastSeenAtUtc));
}

function failureMessage(cause: unknown, fallback: string): string {
  if (cause instanceof ApiError) return cause.status === 429 ? rateLimited : cause.message;
  return fallback;
}

/**
 * The signed-in user's own sessions (D-069). Only the server's fields are shown (two times and the current flag);
 * the session id is used for the revoke request but never displayed, and nothing is kept in browser storage.
 * The server decides everything: an unknown or foreign id answers 404, and ending the current session also ends
 * this browser's cookie, after which the app returns to the login page.
 */
export function ActiveSessions({ onUnauthenticated, onSignedOut }: Props) {
  const titleId = useId();
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  const [pending, setPending] = useState<Pending | null>(null);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const busyRef = useRef(false);
  const failureRef = useRef<HTMLDivElement>(null);
  const reload = useCallback(() => setAttempt((value) => value + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    getMySessions(controller.signal)
      .then((sessions) => {
        if (!controller.signal.aborted) setLoad({ kind: 'ready', sessions: ordered(sessions) });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        // A list that cannot be refreshed is never shown stale after a mutation; the error replaces it.
        setLoad({ kind: 'error', message: failureMessage(cause, loadFailure) });
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);

  useEffect(() => {
    if (failure) failureRef.current?.focus();
  }, [failure]);

  const confirm = async () => {
    if (!pending || busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setFailure(null);
    setNotice(null);
    const action = pending;
    try {
      if (action.kind === 'others') {
        const ended = await revokeMyOtherSessions();
        setNotice(ended === 0 ? 'نشست دیگری برای پایان‌دادن وجود نداشت.' : `${number.format(ended)} نشست دیگر پایان یافت.`);
      } else {
        await revokeMySession(action.session.sessionId);
        if (action.session.isCurrent) {
          // The server ended this browser's session: leave at once, never show data of a dead session.
          setPending(null);
          onSignedOut();
          return;
        }
        setNotice('نشست پایان یافت.');
      }
      setPending(null);
      reload();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      setPending(null);
      if (cause instanceof ApiError && cause.status === 404 && action.kind === 'one' && !action.session.isCurrent) {
        // Already ended (another tab, expiry) or never ours: the goal is reached; show the real list.
        setNotice('این نشست قبلاً پایان یافته است.');
      } else {
        setFailure(failureMessage(cause, 'پایان‌دادن به نشست انجام نشد. فهرست را بررسی کنید و دوباره تلاش کنید.'));
      }
      reload();
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  const sessions = load.kind === 'ready' ? load.sessions : [];
  const others = sessions.filter((session) => !session.isCurrent);

  return (
    <section aria-labelledby={titleId} className="mt-8 space-y-4 border-t border-line pt-6">
      <div className="space-y-1">
        <h3 id={titleId} className="text-base font-extrabold text-title">
          نشست‌های فعال
        </h3>
        <p className="text-xs leading-6 text-sub">دستگاه‌ها و مرورگرهایی که اکنون با حساب شما وارد شده‌اند. اگر نشستی را نمی‌شناسید، آن را پایان دهید و رمز خود را تغییر دهید.</p>
      </div>

      {notice ? (
        <p role="status" className="rounded-lg border border-line bg-cool px-4 py-3 text-sm font-medium text-navy">
          {notice}
        </p>
      ) : null}
      {failure ? (
        <Alert ref={failureRef} tone="error">
          {failure}
        </Alert>
      ) : null}

      {load.kind === 'loading' ? <LoadingState label="در حال دریافت نشست‌ها…" /> : null}
      {load.kind === 'error' ? (
        <Alert
          tone="error"
          action={
            <button type="button" onClick={() => { setLoad({ kind: 'loading' }); reload(); }} className="min-h-9 rounded-lg border border-line bg-white px-3 text-xs font-bold text-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold">
              تلاش دوباره
            </button>
          }
        >
          {load.message}
        </Alert>
      ) : null}

      {load.kind === 'ready' ? (
        <>
          <ul aria-label="نشست‌های فعال" className="divide-y divide-line rounded-xl border border-line">
            {sessions.map((session) => (
              <li key={session.sessionId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <div className="flex min-w-0 items-start gap-3">
                  <Laptop size={20} className="mt-0.5 shrink-0 text-sub" aria-hidden="true" />
                  <div className="min-w-0 space-y-1">
                    {session.isCurrent ? (
                      <span className="inline-block rounded-md border border-teal-deep/30 bg-teal-deep/10 px-2 py-0.5 text-xs font-bold text-teal-deep">این دستگاه</span>
                    ) : null}
                    <p className="text-xs text-ink">ایجاد: {formatDateTime(session.createdAtUtc)}</p>
                    <p className="text-xs text-sub">آخرین فعالیت: {formatDateTime(session.lastSeenAtUtc)}</p>
                  </div>
                </div>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => { setFailure(null); setNotice(null); setPending({ kind: 'one', session }); }}
                  aria-label={session.isCurrent ? 'خروج از این دستگاه' : `پایان نشست دیگر (ایجاد ${formatDateTime(session.createdAtUtc)})`}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-lg border border-line px-3 text-xs font-bold text-danger outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-60"
                >
                  <LogOut size={14} aria-hidden="true" />
                  {session.isCurrent ? 'خروج از این دستگاه' : 'پایان نشست'}
                </button>
              </li>
            ))}
          </ul>
          {others.length > 0 ? (
            <button
              type="button"
              disabled={busy}
              onClick={() => { setFailure(null); setNotice(null); setPending({ kind: 'others', count: others.length }); }}
              className="min-h-11 rounded-lg border border-danger/40 px-4 text-xs font-bold text-danger outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-60"
            >
              پایان همهٔ نشست‌های دیگر
            </button>
          ) : (
            <p className="text-xs text-sub">نشست دیگری فعال نیست.</p>
          )}
        </>
      ) : null}

      <ConfirmDialog
        open={pending !== null}
        tone="danger"
        busy={busy}
        title={pending?.kind === 'others' ? 'پایان نشست‌های دیگر' : pending?.session.isCurrent ? 'خروج از این دستگاه' : 'پایان نشست'}
        confirmLabel={pending?.kind === 'others' ? 'پایان نشست‌های دیگر' : pending?.session.isCurrent ? 'خروج از این دستگاه' : 'پایان نشست'}
        onConfirm={confirm}
        onCancel={() => {
          if (!busy) setPending(null);
        }}
        description={
          pending?.kind === 'others'
            ? `${number.format(pending.count)} نشست دیگر پایان می‌یابد و آن دستگاه‌ها باید دوباره وارد شوند. این دستگاه وارد می‌ماند.`
            : pending?.session.isCurrent
              ? 'نشست این دستگاه پایان می‌یابد و به صفحهٔ ورود بازمی‌گردید.'
              : 'آن دستگاه از حساب شما خارج می‌شود و باید دوباره وارد شود.'
        }
      />
    </section>
  );
}
