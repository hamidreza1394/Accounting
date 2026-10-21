import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const variants: Record<Variant, string> = {
  primary: 'bg-navy text-white hover:bg-navy-deep border border-navy',
  secondary: 'bg-white text-navy border border-line hover:border-navy/40 hover:bg-cool',
  danger: 'bg-danger text-white border border-danger hover:opacity-90',
  ghost: 'bg-transparent text-navy border border-transparent hover:bg-cool',
};

type ButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'className'> & {
  variant?: Variant;
  /** Shows a spinner and blocks further activation while an action is running. */
  loading?: boolean;
  loadingLabel?: string;
  children: ReactNode;
};

/**
 * The one button of the client. A running action keeps the button focusable-but-inert
 * (`aria-disabled`) instead of removing it, so focus is not lost and a second click is ignored.
 */
export function Button({
  variant = 'primary',
  loading = false,
  loadingLabel,
  disabled,
  onClick,
  type = 'button',
  children,
  ...rest
}: ButtonProps) {
  const inert = Boolean(disabled) || loading;

  return (
    <button
      {...rest}
      type={type}
      aria-disabled={inert || undefined}
      aria-busy={loading || undefined}
      onClick={(event) => {
        if (inert) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
      className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-5 py-2.5 text-sm font-semibold transition-colors ${
        variants[variant]
      } ${inert ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}
    >
      {loading ? (
        <span aria-hidden="true" className="size-4 animate-spin rounded-full border-2 border-current/30 border-t-current motion-reduce:animate-none" />
      ) : null}
      <span className="inline-flex items-center gap-2">{loading && loadingLabel ? loadingLabel : children}</span>
    </button>
  );
}
