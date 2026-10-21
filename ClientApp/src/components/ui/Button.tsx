import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const variants: Record<Variant, string> = {
  primary: 'bg-ws-blue text-white hover:bg-ws-blue-deep border border-ws-blue shadow-sm',
  secondary: 'bg-white text-ws-text border border-slate-400 hover:bg-ws-bg',
  danger: 'bg-danger text-white border border-danger hover:opacity-90',
  ghost: 'bg-transparent text-ws-text border border-transparent hover:bg-ws-bg',
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
      className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-[10px] px-5 py-2.5 text-sm font-semibold transition-colors ${
        inert && !loading ? 'cursor-not-allowed border border-slate-300 bg-slate-300 text-slate-600' : variants[variant]
      } ${loading ? 'cursor-progress' : inert ? '' : 'cursor-pointer'}`}
    >
      {loading ? (
        <span aria-hidden="true" className="size-4 animate-spin rounded-full border-2 border-current/30 border-t-current motion-reduce:animate-none" />
      ) : null}
      <span className="inline-flex items-center gap-2">{loading && loadingLabel ? loadingLabel : children}</span>
    </button>
  );
}
