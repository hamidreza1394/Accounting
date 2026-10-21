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
 * disabled state are the platform's own. The selected state is shown by a check mark and a
 * heavier border, not by colour alone.
 */
export function ChoiceCard({ name, value, checked, disabled = false, onChange, describedBy, children }: ChoiceCardProps) {
  return (
    <label
      className={`relative block rounded-xl border bg-white p-4 transition-colors ${
        disabled
          ? 'cursor-not-allowed border-line/70 bg-cool/60 text-muted'
          : checked
            ? 'cursor-pointer border-navy ring-2 ring-navy'
            : 'cursor-pointer border-line hover:border-navy/50 hover:bg-cool/40'
      } has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-gold`}
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
      {checked ? (
        <span
          aria-hidden="true"
          className="absolute end-3 top-3 inline-flex size-6 items-center justify-center rounded-full bg-navy text-white"
        >
          <CheckIcon width={14} height={14} />
        </span>
      ) : null}
      {children}
    </label>
  );
}
