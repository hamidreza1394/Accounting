import { useState, type ReactNode } from 'react';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import type { FormGuard } from '@/lib/formGuard';

/**
 * The state-aware exit of the HR forms (Issue #444 stage 1 completion, PR 5):
 *
 * - `clean`   → leave without any dialog;
 * - `dirty`   → the shared Persian confirmation («ادامهٔ ویرایش» / «لغو تغییرات و بازگشت»);
 * - `saving`  → the exit is blocked while the save is running;
 * - `error`   → the entered data stays; leaving still needs the explicit confirmation,
 *               because leaving drops the changes the user could retry.
 */

export type HrFormExit = {
  /** Asks for the exit according to the lifecycle status (bind to the انصراف button). */
  requestExit: () => void;
  /** The exit button is blocked while saving; show the saving hint next to it. */
  exitBlocked: boolean;
  /** Mount this next to the form; it renders nothing until the user asks to leave. */
  exitDialog: ReactNode;
};

export function useFormExit(guard: FormGuard, exit: () => void, subject = 'اطلاعات واردشده'): HrFormExit {
  const [confirmOpen, setConfirmOpen] = useState(false);

  const requestExit = () => {
    if (guard.status === 'saving') return;
    if (guard.status === 'dirty' || guard.status === 'error') {
      setConfirmOpen(true);
      return;
    }
    exit();
  };

  const dismiss = () => setConfirmOpen(false);

  const exitDialog = (
    <ConfirmDialog
      open={confirmOpen}
      title="لغو تغییرات و بازگشت"
      description={`${subject} هنوز ذخیره نشده‌اند. آیا مایلید بدون ذخیره به صفحهٔ قبلی بازگردید؟`}
      confirmLabel="لغو تغییرات و بازگشت"
      cancelLabel="ادامهٔ ویرایش"
      tone="danger"
      onConfirm={() => {
        setConfirmOpen(false);
        exit();
      }}
      onCancel={dismiss}
    />
  );

  return { requestExit, exitBlocked: guard.status === 'saving', exitDialog };
}
