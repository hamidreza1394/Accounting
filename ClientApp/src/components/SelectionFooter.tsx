import type { ReactNode } from 'react';
import type { FiscalPeriodStatus } from '../api/types';
import type { YearEntry, FiscalYear } from '../lib/fiscalYears';
import { Button } from './ui/FaranouButton';
import { BuildingIcon, CalendarIcon, ChevronLeftIcon } from './ui/icons';

type SelectionFooterProps = {
  year: FiscalYear | undefined;
  /** The chosen company and period; present only when it can be entered. */
  entry: YearEntry | undefined;
  submitting: boolean;
  onEnter: () => void;
  /** Present only when there is something to go back to (the user came to change the choice). */
  onCancel?: (() => void) | undefined;
};

const dotClass: Record<FiscalPeriodStatus, string> = {
  Open: 'bg-emerald-400',
  Closed: 'bg-slate-300',
  Archived: 'bg-slate-400',
};

const statusLabel: Record<FiscalPeriodStatus, string> = {
  Open: 'فعال',
  Closed: 'بسته شده',
  Archived: 'بایگانی',
};

/** Original skyline outlines (thin lines, no photo): decoration on the end side of the bar. */
function Skyline() {
  return (
    <svg
      viewBox="0 0 420 88"
      preserveAspectRatio="xMinYMax slice"
      aria-hidden="true"
      focusable="false"
      className="pointer-events-none absolute inset-y-0 left-0 hidden h-full w-[26rem] text-sky-200 lg:block"
    >
      <defs>
        <linearGradient id="sf-fade" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#fff" stopOpacity="1" />
          <stop offset="1" stopColor="#fff" stopOpacity="0" />
        </linearGradient>
        <mask id="sf-mask">
          <rect width="420" height="88" fill="url(#sf-fade)" />
        </mask>
      </defs>
      <g mask="url(#sf-mask)" fill="none" stroke="currentColor" strokeWidth="1" opacity="0.4">
        <path d="M10 88V34l46-14 20 8v60M10 34l46 8v46M56 42l20-14M22 46v42M34 50v38M46 54v34M66 36v52" />
        <path d="M96 88V22h34v66M96 22l34 6M104 32h18M104 44h18M104 56h18M104 68h18M104 80h18" />
        <path d="M150 88V46h26V12h30v76M184 20h14M184 32h14M184 44h14M184 56h14M184 68h14M184 80h14" />
        <path d="M226 88V50h22v38M262 88V36h24v52M300 88V58h20v30M0 88h420" />
      </g>
      <path d="M0 87 Q140 70 420 80" fill="none" stroke="#fab14c" strokeWidth="1" opacity="0.35" />
    </svg>
  );
}

function Summary({ label, icon, children }: { label: string; icon: 'company' | 'period'; children: ReactNode }) {
  const Icon = icon === 'company' ? BuildingIcon : CalendarIcon;
  return (
    <div aria-hidden="true" className="flex min-w-0 items-center gap-3 max-lg:w-full max-lg:basis-[calc(50%-0.75rem)]">
      <span className="flex min-w-0 flex-col leading-snug">
        <span className="text-xs text-white/75">{label}</span>
        <span className="flex min-w-0 items-center gap-2 truncate text-[0.9375rem] font-bold text-white">{children}</span>
      </span>
      <span className="inline-flex size-[3.125rem] shrink-0 items-center justify-center rounded-full border border-white/20 bg-white/10 text-sky-200 max-lg:hidden">
        <Icon width={26} height={26} />
      </span>
    </div>
  );
}

/**
 * The bottom bar of the selector: the chosen company and period, and the call to action. The bar
 * is sticky at the bottom: on desktop the page height is fixed so it sits below the panels, on
 * small screens it stays reachable while the lists scroll under it (the page keeps bottom space). The visual summary is hidden
 * from assistive technology; one polite live region says the same in a sentence.
 */
export function SelectionFooter({ year, entry, submitting, onEnter, onCancel }: SelectionFooterProps) {
  const ready = Boolean(entry);
  const inert = !ready || submitting;

  return (
    <div className="sticky bottom-0 z-20 bg-gradient-to-t from-[#eef3fa] via-[#eef3fa]/90 to-transparent px-4 pb-3 pt-5 sm:px-8 xl:px-14">
      <footer className="on-dark relative mx-auto flex max-w-[120rem] flex-wrap items-center gap-x-6 gap-y-3 overflow-hidden rounded-[1.75rem] bg-gradient-to-l from-[#0f2842] via-[#12314f] to-[#1c4f7a] px-4 py-3 shadow-[0_10px_30px_rgb(15_40_66/0.35)] lg:min-h-[5.5rem] lg:px-5">
        <Skyline />

        <p aria-live="polite" className="sr-only">
          {entry && year ? (
            <>
              <span>انتخاب شما: </span>
              <strong>{year.title}</strong>
              <span> ({statusLabel[entry.period.status]})</span>
              <span> · </span>
              <strong>{entry.company.name}</strong>
              {entry.company.nationalId ? <bdi dir="ltr"> ({entry.company.nationalId})</bdi> : null}
              {entry.period.accessMode === 'readOnly' ? <span> (فقط مشاهده)</span> : null}
            </>
          ) : (
            <span>{year ? 'یک شرکت را انتخاب کنید.' : 'دوره مالی و شرکت هنوز انتخاب نشده است.'}</span>
          )}
        </p>

        <div className="relative flex w-full flex-col-reverse gap-2.5 max-lg:order-last sm:flex-row sm:items-center lg:w-auto">
          <button
            type="button"
            aria-disabled={inert || undefined}
            aria-busy={submitting || undefined}
            onClick={() => {
              if (!inert) {
                onEnter();
              }
            }}
            className={`inline-flex h-14 w-full items-center justify-center gap-4 rounded-full text-lg font-extrabold transition-shadow duration-150 motion-reduce:transition-none sm:flex-1 lg:h-[4.25rem] lg:w-[17rem] lg:flex-none lg:text-[1.375rem] ${
              ready && !submitting
                ? 'cursor-pointer bg-gradient-to-l from-cta via-[#fdc978] to-cta-soft text-night shadow-[0_10px_24px_rgb(250_177_76/0.38),inset_0_1px_0_rgb(255_255_255/0.6)] hover:shadow-[0_12px_28px_rgb(250_177_76/0.5),inset_0_1px_0_rgb(255_255_255/0.6)]'
                : submitting
                  ? 'cursor-progress bg-gradient-to-l from-cta via-[#fdc978] to-cta-soft text-night opacity-90'
                  : 'cursor-not-allowed border border-white/20 bg-white/15 text-white/75'
            }`}
          >
            {submitting ? (
              <>
                <span aria-hidden="true" className="size-5 animate-spin rounded-full border-2 border-current/30 border-t-current motion-reduce:animate-none" />
                در حال ورود…
              </>
            ) : (
              <>
                ورود به محیط کاری
                <ChevronLeftIcon width={22} height={22} strokeWidth={2.6} />
              </>
            )}
          </button>
          {onCancel ? (
            <Button variant="onDark" onClick={onCancel} disabled={submitting}>
              انصراف
            </Button>
          ) : null}
        </div>

        <div className="relative flex min-w-0 flex-1 flex-wrap items-center gap-x-6 gap-y-3 lg:flex-nowrap lg:justify-center">
          <Summary label="دوره مالی انتخاب‌شده" icon="period">
            {entry && year ? (
              <>
                <span className="truncate">{year.title}</span>
                <span aria-hidden="true" className={`size-2.5 shrink-0 rounded-full ${dotClass[entry.period.status]}`} />
                <span className="shrink-0 text-xs font-medium text-white/80">{statusLabel[entry.period.status]}</span>
              </>
            ) : (
              <span className="font-medium text-white/75">انتخاب نشده</span>
            )}
          </Summary>
          <span aria-hidden="true" className="h-10 w-px bg-white/20 max-lg:hidden" />
          <Summary label="شرکت انتخاب‌شده" icon="company">
            {entry ? <span className="truncate">{entry.company.name}</span> : <span className="font-medium text-white/75">انتخاب نشده</span>}
          </Summary>
        </div>
      </footer>
    </div>
  );
}
