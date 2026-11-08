import { useEffect, useId, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Button } from './button';

type ConfirmDialogProps = {
  open: boolean;
  title: string;
  description?: ReactNode;
  /**
   * Extra body content between the description and the actions (a step-up form).
   * It participates in the focus trap and must label its own fields.
   */
  children?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  /** `danger` uses role `alertdialog`, the destructive style, and focuses Cancel first. */
  tone?: 'default' | 'danger';
  /** While true, both buttons ignore activation, so the action cannot be submitted twice. */
  busy?: boolean;
  /**
   * False renders a refusal: the dialog only informs and offers the cancel/acknowledge
   * button — used when an action is blocked outright (a `Saving`/`Error` tab cannot close).
   */
  confirmVisible?: boolean;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
};

// Busy buttons are `aria-disabled` and stay focusable, so Tab stays trapped while busy.
const focusableSelector = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * A modal confirmation. Focus moves into the dialog and is trapped, Escape cancels, and focus
 * returns to the element that opened it. Buttons use `aria-disabled` while busy (not `disabled`),
 * so focus is never lost to the page behind while an action runs.
 */
export function ConfirmDialog({
  open,
  title,
  description,
  children,
  confirmLabel = 'تأیید',
  cancelLabel = 'انصراف',
  tone = 'default',
  busy = false,
  confirmVisible = true,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const confirmRef = useRef<HTMLButtonElement>(null);
  const openerRef = useRef<HTMLElement | null>(null);
  const runningRef = useRef(false);
  const latest = useRef({ busy, onCancel });
  latest.current = { busy, onCancel };

  useEffect(() => {
    if (!open) return undefined;
    openerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    (tone === 'danger' || !confirmVisible ? cancelRef : confirmRef).current?.focus();
    return () => {
      const opener = openerRef.current;
      if (opener?.isConnected) opener.focus();
      openerRef.current = null;
    };
    // The focus target is chosen when the dialog opens; a tone change must not steal focus.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Escape is handled at the document level, so it works wherever focus is; ignored while busy.
  useEffect(() => {
    if (!open) return undefined;
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape' && !latest.current.busy) {
        event.stopPropagation();
        latest.current.onCancel();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [open]);

  if (!open) return null;

  const cancel = () => {
    if (!busy) onCancel();
  };

  const confirm = async () => {
    if (busy || runningRef.current) return;
    runningRef.current = true;
    try {
      await onConfirm();
    } catch {
      // The caller surfaces its own error; the dialog must stay usable and not leak a rejection.
    } finally {
      runningRef.current = false;
    }
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'Tab') return;
    const focusable = Array.from(dialogRef.current?.querySelectorAll<HTMLElement>(focusableSelector) ?? []);
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!first || !last) return;
    const active = document.activeElement;
    if (event.shiftKey && (active === first || !dialogRef.current?.contains(active))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !dialogRef.current?.contains(active))) {
      event.preventDefault();
      first.focus();
    }
  };

  const blockedClass = 'aria-disabled:pointer-events-none aria-disabled:opacity-50';

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-night/60 p-4" onMouseDown={(event) => {
      if (event.target === event.currentTarget) cancel();
    }}>
      <div
        ref={dialogRef}
        role={tone === 'danger' ? 'alertdialog' : 'dialog'}
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        aria-busy={busy || undefined}
        onKeyDown={handleKeyDown}
        className="w-full max-w-md rounded-xl border border-line bg-white p-6 shadow-xl"
      >
        <h2 id={titleId} className="text-lg font-bold text-ink">
          {title}
        </h2>
        {description ? (
          <div id={descriptionId} className="mt-2 text-sm leading-7 text-sub">
            {description}
          </div>
        ) : null}
        {children ? <div className="mt-4 space-y-4">{children}</div> : null}
        <div className="mt-6 flex flex-wrap justify-end gap-2">
          <Button ref={cancelRef} type="button" variant="outline" size="lg" aria-disabled={busy || undefined} className={blockedClass} onClick={cancel}>
            {cancelLabel}
          </Button>
          {confirmVisible ? (
            <Button
              ref={confirmRef}
              type="button"
              variant={tone === 'danger' ? 'destructive' : 'default'}
              size="lg"
              aria-disabled={busy || undefined}
              className={blockedClass}
              onClick={() => void confirm()}
            >
              {busy ? 'در حال انجام…' : confirmLabel}
            </Button>
          ) : null}
        </div>
      </div>
    </div>,
    document.body,
  );
}
