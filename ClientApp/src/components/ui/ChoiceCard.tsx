import type { ReactNode } from 'react';
import { CheckIcon } from './icons';

type ChoiceCardProps = {
  /** The radio group the card belongs to. */
  name: string;
  value: string;
  checked: boolean;
  disabled?: boolean;
  onChange: (value: string) => void;
  /** The accessible description of a disabled card (why it cannot be chosen). */
  describedBy?: string | undefined;
  children: ReactNode;
};

/**
 * One option of a single-choice group, drawn as a card. It is a real radio input, so keyboard
 * navigation (arrow keys inside the group, Tab between groups), the selected state and the
 * disabled state are the platform's own. The selected state is shown by a check mark, a
 * heavier navy border and a gold bar on the start edge, never by colour alone; a disabled card
 * is muted and its indicator is greyed.
 */
export function ChoiceCard({ name, value, checked, disabled = false, onChange, describedBy, children }: ChoiceCardProps) {
  const active = checked && !disabled;

  return (
    <label
      className={`relative flex min-h-11 items-center gap-3.5 overflow-hidden rounded-xl border p-4 shadow-[0_1px_2px_rgb(28_61_90/0.05)] transition-colors duration-150 motion-reduce:transition-none ${
        disabled
          ? 'cursor-not-allowed border-slate-300 bg-cool'
          : active
            ? 'cursor-pointer border-2 border-navy bg-white p-[15px]'
            : 'cursor-pointer border-line bg-white hover:border-navy/40 hover:bg-cool/60'
      } has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-teal-deep`}
    >
      {active ? <span aria-hidden="true" className="absolute inset-y-0 end-0 w-1 bg-gold" /> : null}
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
      <span
        aria-hidden="true"
        className={`inline-flex size-6 shrink-0 items-center justify-center rounded-full border-2 text-white ${
          disabled ? 'border-slate-300 bg-slate-200' : active ? 'border-navy bg-navy' : 'border-slate-400 bg-white'
        }`}
      >
        {active ? <CheckIcon width={14} height={14} strokeWidth={3.2} /> : null}
      </span>
      <span className="min-w-0 flex-1">{children}</span>
    </label>
  );
}
