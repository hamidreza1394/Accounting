import type { ReactNode } from 'react';
import { CheckIcon, ChevronLeftIcon } from './icons';

type ChoiceCardProps = {
  /** The radio group the card belongs to. */
  name: string;
  value: string;
  checked: boolean;
  disabled?: boolean;
  /** `period` shows its own radio circle on the start side; `company` shows a tick badge on the corner. */
  variant: 'period' | 'company';
  onChange: (value: string) => void;
  /** The accessible description of a disabled card (why it cannot be chosen). */
  describedBy?: string | undefined;
  children: ReactNode;
};

/**
 * One option of a single-choice group, drawn as a full-width row card. It is a real radio input,
 * so keyboard navigation (arrow keys inside the group, Tab between groups), the selected state
 * and the disabled state are the platform's own. The selected state is never colour alone: a
 * heavier blue border, a filled check (radio circle or corner badge) and a tinted background;
 * a disabled card is muted and its indicator is greyed.
 */
export function ChoiceCard({ name, value, checked, disabled = false, variant, onChange, describedBy, children }: ChoiceCardProps) {
  const active = checked && !disabled;
  const period = variant === 'period';

  return (
    <label
      className={`relative flex items-center rounded-2xl border-2 transition-colors duration-150 motion-reduce:transition-none ${
        period ? 'min-h-20 gap-3 px-3.5 py-2.5' : 'min-h-[4.125rem] gap-3 px-4 py-2'
      } ${
        disabled
          ? 'cursor-not-allowed border-slate-200 bg-cool/80'
          : active
            ? 'cursor-pointer border-pick bg-pick-soft shadow-[0_6px_16px_rgb(60_150_215/0.16)]'
            : `cursor-pointer bg-white shadow-[0_2px_6px_rgb(35_75_125/0.04)] hover:border-pick/60 hover:bg-[#f7fbff] ${period ? 'border-[#d6e3f2]' : 'border-[#e1e9f3]'}`
      } has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-teal-deep`}
    >
      <input
        type="radio"
        name={name}
        value={value}
        checked={checked}
        disabled={disabled}
        aria-describedby={describedBy}
        onChange={() => onChange(value)}
        className="sr-only"
      />
      {period ? (
        <span
          aria-hidden="true"
          className={`inline-flex size-[1.625rem] shrink-0 items-center justify-center rounded-full border-2 text-white ${
            disabled ? 'border-slate-300 bg-slate-200' : active ? 'border-tick bg-tick' : 'border-[#b3bfce] bg-white'
          }`}
        >
          {active ? <CheckIcon width={14} height={14} strokeWidth={3.4} /> : null}
        </span>
      ) : active ? (
        <span
          aria-hidden="true"
          className="absolute -end-2 -top-2 z-10 inline-flex size-[1.875rem] items-center justify-center rounded-full border-[2.5px] border-white bg-tick text-white shadow-[0_2px_6px_rgb(20_120_140/0.35)]"
        >
          <CheckIcon width={15} height={15} strokeWidth={3.4} />
        </span>
      ) : null}
      <span className="min-w-0 flex-1">{children}</span>
      <ChevronLeftIcon width={period ? 16 : 18} height={period ? 16 : 18} className="shrink-0 text-[#7b8ba0]" />
    </label>
  );
}
