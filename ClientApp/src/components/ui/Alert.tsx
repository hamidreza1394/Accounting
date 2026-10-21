import { forwardRef, type ReactNode } from 'react';
import { AlertIcon, InfoIcon } from './icons';

type Tone = 'error' | 'warning' | 'info';

const tones: Record<Tone, string> = {
  error: 'border-danger/30 bg-danger-soft text-danger',
  warning: 'border-gold/50 bg-warning-soft text-warning',
  info: 'border-line bg-cool text-navy',
};

type AlertProps = {
  tone: Tone;
  children: ReactNode;
  action?: ReactNode;
};

/**
 * A message box. Errors and warnings use `role="alert"` so a screen reader announces them;
 * the element can receive programmatic focus to move the user to the message after a failure.
 */
export const Alert = forwardRef<HTMLDivElement, AlertProps>(function Alert({ tone, children, action }, ref) {
  return (
    <div
      ref={ref}
      role={tone === 'info' ? 'status' : 'alert'}
      tabIndex={-1}
      className={`flex flex-col gap-3 rounded-xl border p-4 text-sm outline-none focus-visible:ring-2 focus-visible:ring-gold sm:flex-row sm:items-center sm:justify-between ${tones[tone]}`}
    >
      <p className="flex items-start gap-2 font-medium">
        {tone === 'info' ? <InfoIcon className="mt-0.5 shrink-0" /> : <AlertIcon className="mt-0.5 shrink-0" />}
        <span>{children}</span>
      </p>
      {action}
    </div>
  );
});
