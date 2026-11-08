import { useEffect, useRef, useState } from 'react';
import {
  ApiError,
  revokeAdminUserSession,
  revokeAllAdminUserSessions,
  type AdminRevocationRequest,
} from '@/api/client';
import type { AdminUserSession } from '@/api/types';
import { Alert } from '@/components/ui/Alert';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { FormField, Select } from '@/components/ui/FormField';
import { PasswordInput } from '@/components/ui/PasswordInput';

/**
 * The D-073 §1 step-up confirmation for revoking another user's sessions: the
 * administrator re-authenticates with their own password and picks a reason from
 * the server-owned code catalog (the labels here are presentation only — the code
 * is what the server stores). The password lives only in the request body state;
 * it is never logged, echoed or kept anywhere else, and the dialog is cleared when
 * it closes. A failed attempt keeps both fields so the retry is a correction, not
 * a retyping — and the dialog stays open with the real server message.
 */

export type PendingRevocation =
  | { kind: 'one'; session: AdminUserSession }
  | { kind: 'all'; count: number };

type Props = {
  /** The targeted user (never the acting administrator; the server rejects self-targeting with 400). */
  userId: string;
  pending: PendingRevocation | null;
  onClose: () => void;
  /** Called after the server confirmed the revocation; the parent refetches the list. */
  onSuccess: (message: string) => void;
  onUnauthenticated: (error: unknown) => boolean;
};

const rateLimited = 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
const fallbackFailure = 'ابطال نشست انجام نشد. لطفاً دوباره تلاش کنید.';
const passwordRequired = 'برای تأیید هویت، رمز عبور خودتان لازم است.';
const reasonRequired = 'انتخاب دلیل ابطال الزامی است.';

/** The server-owned D-073 §1 catalog, shown in Persian; an unknown code is never invented here. */
const reasonOptions: readonly { value: string; label: string }[] = [
  { value: 'admin_revoked_suspected_compromise', label: 'احتمال نفوذ یا سرقت نشست' },
  { value: 'admin_revoked_offboarding', label: 'خروج کاربر از سازمان' },
  { value: 'admin_revoked_user_request', label: 'درخواست خود کاربر' },
  { value: 'admin_revoked_administrative', label: 'دلیل اداری و مدیریتی' },
];

const number = new Intl.NumberFormat('fa-IR');

function failureMessage(cause: unknown): string {
  if (cause instanceof ApiError) return cause.status === 429 ? rateLimited : cause.message;
  return fallbackFailure;
}

export function AdminRevokeSessionDialog({ userId, pending, onClose, onSuccess, onUnauthenticated }: Props) {
  const [password, setPassword] = useState('');
  const [reasonCode, setReasonCode] = useState('');
  const [validation, setValidation] = useState<{ password?: string; reason?: string }>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);

  // Every open starts clean: the previous password state must not survive into the next dialog.
  useEffect(() => {
    if (pending !== null) {
      setPassword('');
      setReasonCode('');
      setValidation({});
      setFailure(null);
      setBusy(false);
      busyRef.current = false;
    }
  }, [pending]);

  if (pending === null) return null;

  const title = pending.kind === 'one' ? 'ابطال این نشست' : 'ابطال همهٔ نشست‌های فعال';
  const description =
    pending.kind === 'one'
      ? 'نشست کاربر پایان می‌یابد و آن دستگاه باید دوباره وارد شود. برای ادامه، هویت خودتان را دوباره تأیید کنید.'
      : `${number.format(pending.count)} نشست فعال این کاربر پایان می‌یابد و همهٔ دستگاه‌ها باید دوباره وارد شوند. برای ادامه، هویت خودتان را دوباره تأیید کنید.`;

  const confirm = async () => {
    if (busyRef.current || pending === null) return;
    const nextValidation: { password?: string; reason?: string } = {};
    if (password.length === 0) nextValidation.password = passwordRequired;
    if (reasonCode === '') nextValidation.reason = reasonRequired;
    setValidation(nextValidation);
    if (nextValidation.password || nextValidation.reason) return;

    busyRef.current = true;
    setBusy(true);
    setFailure(null);
    const body: AdminRevocationRequest = { currentPassword: password, reasonCode };
    try {
      if (pending.kind === 'one') {
        await revokeAdminUserSession(userId, pending.session.sessionId, body);
        onSuccess('نشست کاربر پایان یافت.');
      } else {
        const revokedCount = await revokeAllAdminUserSessions(userId, body);
        onSuccess(
          revokedCount === 0
            ? 'نشست فعالی برای ابطال وجود نداشت.'
            : `${number.format(revokedCount)} نشست فعال این کاربر پایان یافت.`,
        );
      }
      onClose();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      // The dialog stays open with its values; only the real server message is shown.
      setFailure(failureMessage(cause));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  return (
    <ConfirmDialog
      open
      tone="danger"
      busy={busy}
      title={title}
      description={description}
      confirmLabel="ابطال نشست"
      onConfirm={confirm}
      onCancel={onClose}
    >
      <div className="space-y-4">
        {failure ? (
          <Alert tone="error">{failure}</Alert>
        ) : null}
        <FormField label="رمز عبور خودتان" required error={validation.password}>
          <PasswordInput
            fieldName="رمز عبور"
            togglePlacement="end"
            autoComplete="current-password"
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
              if (validation.password) {
                setValidation((value) => {
                  const next = { ...value };
                  delete next.password;
                  return next;
                });
              }
            }}
            disabled={busy}
          />
        </FormField>
        <FormField label="دلیل ابطال" required error={validation.reason}>
          <Select
            value={reasonCode}
            onChange={(event) => {
              setReasonCode(event.target.value);
              if (validation.reason) {
                setValidation((value) => {
                  const next = { ...value };
                  delete next.reason;
                  return next;
                });
              }
            }}
            disabled={busy}
          >
            <option value="">انتخاب کنید…</option>
            {reasonOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </Select>
        </FormField>
        <p className="text-xs leading-6 text-sub">
          رمز عبور فقط در همین درخواست ارسال می‌شود؛ هیچ‌جا ذخیره یا نمایش داده نمی‌شود. دلیل انتخابی در سوابق امنیتی ثبت می‌شود.
        </p>
      </div>
    </ConfirmDialog>
  );
}
