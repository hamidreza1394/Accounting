import { Children, cloneElement, isValidElement, useId, type ComponentProps, type ReactElement, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

const controlClass =
  'w-full rounded-lg border border-line bg-white px-3 text-sm text-ink outline-none transition-colors placeholder:text-muted focus-visible:border-teal-deep focus-visible:ring-2 focus-visible:ring-gold aria-[invalid=true]:border-danger disabled:cursor-not-allowed disabled:bg-cool disabled:text-sub';

export function Input({ className, ...props }: ComponentProps<'input'>) {
  return <input className={cn('h-10', controlClass, className)} {...props} />;
}

export function Select({ className, ...props }: ComponentProps<'select'>) {
  return <select className={cn('h-10', controlClass, className)} {...props} />;
}

export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return <textarea className={cn('min-h-24 py-2', controlClass, className)} {...props} />;
}

type ControlProps = {
  id?: string;
  required?: boolean;
  'aria-describedby'?: string;
  'aria-invalid'?: boolean;
  'aria-required'?: boolean;
};

type FormFieldProps = {
  label: ReactNode;
  required?: boolean;
  hint?: ReactNode;
  error?: ReactNode;
  className?: string;
  /** Exactly one control (`Input`, `Select`, `Textarea` or a native element). */
  children: ReactElement<ControlProps>;
};

/**
 * A label, an optional hint and an error around one control. It wires `id`, `aria-describedby`,
 * `aria-invalid` and `aria-required` into the control, so the error is read with the field and the
 * state is never shown by colour alone.
 */
export function FormField({ label, required, hint, error, className, children }: FormFieldProps) {
  const generatedId = useId();
  const control = Children.only(children);
  if (!isValidElement<ControlProps>(control)) {
    throw new Error('FormField needs exactly one control element.');
  }
  const id = control.props.id ?? generatedId;
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [control.props['aria-describedby'], hintId, errorId].filter(Boolean).join(' ') || undefined;

  const wired: ControlProps = { id };
  if (describedBy) wired['aria-describedby'] = describedBy;
  if (error) wired['aria-invalid'] = true;
  if (required) wired['aria-required'] = true;

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
        {required ? (
          <span aria-hidden="true" className="ms-1 text-danger">
            *
          </span>
        ) : null}
      </label>
      {cloneElement(control, wired)}
      {hint ? (
        <p id={hintId} className="text-xs leading-5 text-sub">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} aria-live="polite" className="text-xs font-medium leading-5 text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}
