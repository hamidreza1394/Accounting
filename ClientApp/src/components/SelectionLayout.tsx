import type { ReactNode } from 'react';
import type { Session } from '../api/types';
import { FaranouLogo } from './ui/FaranouLogo';

type SelectionLayoutProps = {
  /** The signed-in user; omitted while the session is not known yet. */
  session?: Session | null;
  /** Shows the sign-out button in the header. */
  onLogout?: (() => void) | undefined;
  /** The sticky bar at the bottom (summary and actions). */
  footer?: ReactNode;
  /** `wide` is for the two-panel selector; `narrow` for single messages. */
  width?: 'wide' | 'narrow';
  children: ReactNode;
};

/**
 * The shared frame of the screens before the working environment opens (selection, blocked
 * access, loading, errors): a slim light header with the brand and the user, a quiet CSS
 * backdrop and a centred content column. The header and backdrop do not depend on the content,
 * so moving between these screens does not make the page jump.
 */
export function SelectionLayout({ session, onLogout, footer, width = 'wide', children }: SelectionLayoutProps) {
  const initial = session?.fullName.trim().charAt(0) ?? '';

  return (
    <div className="selection-backdrop flex min-h-screen flex-col text-ink">
      <header className="border-b border-line bg-white/90">
        <div className="mx-auto flex min-h-[4.5rem] max-w-[80rem] items-center justify-between gap-3 px-4 py-2 sm:px-8">
          <FaranouLogo size={52} />
          {session ? (
            <div className="flex min-w-0 items-center gap-3">
              <span
                aria-hidden="true"
                className="inline-flex size-9 shrink-0 items-center justify-center rounded-full bg-navy text-sm font-bold text-white"
              >
                {initial}
              </span>
              <span className="min-w-0 truncate text-sm font-semibold text-ink max-sm:sr-only">
                <span className="sr-only">کاربر وارد شده: </span>
                {session.fullName}
              </span>
              {onLogout ? (
                <button
                  type="button"
                  onClick={onLogout}
                  className="min-h-11 cursor-pointer rounded-[10px] border border-slate-400 px-4 text-sm font-medium text-ink transition-colors duration-150 hover:bg-cool motion-reduce:transition-none"
                >
                  خروج
                </button>
              ) : null}
            </div>
          ) : null}
        </div>
      </header>

      <main
        id="main"
        className={`mx-auto w-full flex-1 px-4 py-6 sm:px-8 sm:py-8 ${width === 'wide' ? 'max-w-[80rem]' : 'max-w-xl'}`}
      >
        {children}
      </main>

      {footer}
    </div>
  );
}
