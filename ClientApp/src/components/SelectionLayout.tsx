import type { ReactNode } from 'react';
import type { Session } from '../api/types';
import { FaranouLogo } from './ui/FaranouLogo';
import { SelectionBackdrop } from './ui/SelectionBackdrop';

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

/** The door-and-arrow sign-out mark: the door follows the text colour, the arrow is the CTA orange. */
function LogoutMark() {
  return (
    <svg viewBox="0 0 24 24" width="24" height="24" fill="none" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
      <path d="M10 4H7.5A2.5 2.5 0 0 0 5 6.5v11A2.5 2.5 0 0 0 7.5 20H10" stroke="currentColor" strokeWidth="1.8" />
      <path d="M10 12h10M16.5 8.5 20 12l-3.5 3.5" stroke="var(--color-cta)" strokeWidth="2" />
    </svg>
  );
}

/**
 * The shared frame of the screens before the working environment opens (selection, blocked
 * access, loading, errors): a full-width deep-navy header (brand on the start side, the signed-in
 * user and the sign-out on the end side), a light backdrop with a little local depth, and a
 * centred content column. Header and backdrop do not depend on the content, so moving between
 * these screens does not make the page jump.
 */
export function SelectionLayout({ session, onLogout, footer, width = 'wide', children }: SelectionLayoutProps) {
  const initial = session?.fullName.trim().charAt(0) ?? '';

  return (
    <div className={`selection-backdrop relative isolate flex min-h-screen flex-col text-ink ${width === 'wide' ? 'lg:h-dvh lg:min-h-[44rem]' : ''}`}>
      <SelectionBackdrop />

      <header className="on-dark bg-gradient-to-b from-[#15304a] to-[#0f2a45] text-white shadow-[0_2px_12px_rgb(15_42_69/0.25)]">
        <div className="flex min-h-[4.75rem] items-center justify-between gap-3 px-4 py-2 sm:px-8 xl:px-14">
          <span className="inline-flex items-center gap-3">
            <FaranouLogo size={46} label={false} tone="dark" />
            <span className="flex flex-col leading-tight">
              <strong className="text-[1.375rem] font-extrabold text-white">فرانو</strong>
              <span dir="ltr" className="text-[0.6875rem] font-medium tracking-[0.18em] text-white/75">
                FARANOU
              </span>
            </span>
          </span>

          {session ? (
            <div className="flex min-w-0 items-center gap-3 sm:gap-4">
              {onLogout ? (
                <button
                  type="button"
                  onClick={onLogout}
                  aria-label="خروج"
                  title="خروج"
                  className="inline-flex size-[2.875rem] shrink-0 cursor-pointer items-center justify-center rounded-full border-[1.5px] border-white/35 bg-white/10 text-white shadow-[0_0_0_4px_rgb(250_177_76/0.1),inset_0_1px_0_rgb(255_255_255/0.18)] transition-colors duration-150 hover:bg-white/20 motion-reduce:transition-none"
                >
                  <LogoutMark />
                </button>
              ) : null}
              <span className="inline-flex min-w-0 items-center gap-3">
                <span
                  aria-hidden="true"
                  className="inline-flex size-11 shrink-0 items-center justify-center rounded-full border border-white/30 bg-white/15 text-lg font-bold text-white"
                >
                  {initial}
                </span>
                <span className="flex min-w-0 flex-col leading-snug max-sm:sr-only">
                  <span className="sr-only">کاربر وارد شده: </span>
                  <span className="truncate text-[0.9375rem] font-bold text-white">{session.fullName}</span>
                  <bdi dir="ltr" className="truncate text-start text-xs text-white/75">
                    {session.username}
                  </bdi>
                </span>
              </span>
            </div>
          ) : null}
        </div>
      </header>

      <main
        id="main"
        className={`mx-auto flex w-full min-h-0 flex-1 flex-col px-4 pb-6 pt-6 sm:px-8 xl:px-14 ${width === 'wide' ? 'max-w-[120rem]' : 'max-w-xl sm:pt-10'}`}
      >
        {children}
      </main>

      {footer}
    </div>
  );
}
