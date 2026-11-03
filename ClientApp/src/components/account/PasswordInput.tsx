import { useState, type ComponentProps } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { Input } from '../ui/FormField';

type PasswordInputProps = Omit<ComponentProps<'input'>, 'type'> & {
  /** Name of the field used in the toggle label, for example "رمز فعلی". */
  fieldName: string;
};

/**
 * A password input (always left-to-right) with its own show/hide toggle. The toggle keeps one fixed
 * label and exposes its state through `aria-pressed`. `FormField` wires id and aria props into this
 * component, which forwards them to the input.
 */
export function PasswordInput({ fieldName, className, ...props }: PasswordInputProps) {
  const [visible, setVisible] = useState(false);
  const Icon = visible ? EyeOff : Eye;
  return (
    <div dir="ltr" className="relative">
      {/* The wrapper is LTR like the input, so the toggle (start-0) and the input padding (ps-11) are both on the left. */}
      <Input {...props} type={visible ? 'text' : 'password'} dir="ltr" className={`ps-11 ${className ?? ''}`} />
      <button
        type="button"
        aria-label={`نمایش ${fieldName}`}
        aria-pressed={visible}
        onClick={() => setVisible((value) => !value)}
        className="absolute inset-y-0 start-0 flex w-10 items-center justify-center rounded-s-lg text-sub outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-gold"
      >
        <Icon size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
