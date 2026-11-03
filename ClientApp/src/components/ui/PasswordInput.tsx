import { useState, type ComponentProps } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { Input } from './FormField';

type PasswordInputProps = Omit<ComponentProps<'input'>, 'type'> & {
  /** Name of the field used in the toggle label, for example "رمز فعلی". */
  fieldName: string;
  /**
   * Which side of the (left-to-right) box carries the show/hide toggle. The wrapper is always
   * `dir="ltr"`, so `start` is the left edge and `end` the right edge; the input padding is
   * paired with the same side. Defaults to `start` (the account drawer); the user form uses `end`.
   */
  togglePlacement?: 'start' | 'end';
};

/**
 * The one password box of the client: always left-to-right (passwords are Latin text), spelling
 * aids off, with its own show/hide toggle. The toggle keeps one fixed label and exposes its state
 * through `aria-pressed` (the label never flips, so a screen reader does not hear it twice).
 * `FormField` wires id and aria props into this component, which forwards them to the input.
 * The value stays in the caller's state; nothing is stored here.
 */
export function PasswordInput({ fieldName, togglePlacement = 'start', className, ...props }: PasswordInputProps) {
  const [visible, setVisible] = useState(false);
  const Icon = visible ? EyeOff : Eye;
  const atEnd = togglePlacement === 'end';
  return (
    // The wrapper is LTR like the input, so the toggle, the input padding and the text alignment
    // all refer to the same side, whatever the page direction is.
    <div dir="ltr" className="relative">
      <Input
        {...props}
        type={visible ? 'text' : 'password'}
        dir="ltr"
        spellCheck={false}
        autoCapitalize="none"
        autoCorrect="off"
        className={`text-start ${atEnd ? 'pe-11' : 'ps-11'} ${className ?? ''}`}
      />
      <button
        type="button"
        aria-label={`نمایش ${fieldName}`}
        aria-pressed={visible}
        onClick={() => setVisible((value) => !value)}
        className={`absolute inset-y-0 flex w-10 items-center justify-center text-sub outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-gold ${
          atEnd ? 'end-0 rounded-e-lg' : 'start-0 rounded-s-lg'
        }`}
      >
        <Icon size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
