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
 * disabled state are the platform's own. The selected state is shown by a filled check mark, a
 * heavier border and a tinted background, never by colour alone; a disabled card is muted and
 * its indicator is greyed.
 */
export function ChoiceCard({ name, value, checked, disabled = false, onChange, describedBy, children }: ChoiceCardProps) {
  return (
    <label
      className={`flex items-center gap-4 rounded-xl border p-4 transition-colors ${
        disabled
          ? 'cursor-not-allowed border-slate-300 bg-ws-bg'
          : checked
            ? 'cursor-pointer border-2 border-ws-teal bg-ws-teal-soft'
            : 'cursor-pointer border-ws-line bg-white hover:border-ws-teal/60'
      } has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ws-blue`}
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
      <span className="min-w-0 flex-1">{children}</span>
      <span
        aria-hidden="true"
        className={`inline-flex size-6 shrink-0 items-center justify-center rounded-full border-2 text-white ${
          disabled ? 'border-slate-300 bg-slate-200' : checked ? 'border-ws-teal bg-ws-teal' : 'border-slate-400 bg-white'
        }`}
      >
        {checked && !disabled ? <CheckIcon width={14} height={14} strokeWidth={3.2} /> : null}
      </span>
    </label>
  );
}
