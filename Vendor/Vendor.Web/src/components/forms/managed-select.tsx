import { useEffect, useId, useState } from 'react';

export type SelectOption = {
  value: string;
  label: string;
  disabled?: boolean;
};

export type ManagedSelectProps = {
  label: string;
  options: SelectOption[];
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
  canManageOptions?: boolean;
  onAddOption?: () => void;
  addOptionLabel?: string;
  hint?: string;
  error?: string;
  name?: string;
  required?: boolean;
  disabled?: boolean;
};

/**
 * Accessible native select for Vendor forms.
 * Defaults to the first enabled option unless a valid enabled defaultValue is supplied.
 * Option management is delegated to a caller with server-derived permission and a real API
 * callback; this component never persists reference data on its own.
 */
export function ManagedSelect({
  label,
  options,
  value,
  defaultValue,
  onValueChange,
  canManageOptions = false,
  onAddOption,
  addOptionLabel = 'افزودن گزینه',
  hint,
  error,
  name,
  required = false,
  disabled = false,
}: ManagedSelectProps) {
  const generatedId = useId();
  const id = name ? `vendor-select-${name}` : generatedId;
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const firstEnabledValue = options.find((option) => !option.disabled)?.value ?? '';
  const initialValue = options.some(
    (option) => option.value === defaultValue && !option.disabled,
  )
    ? defaultValue!
    : firstEnabledValue;
  const [internalValue, setInternalValue] = useState(initialValue);
  const selectedValue = value ?? internalValue;
  const hasSelectedOption = options.some(
    (option) => option.value === selectedValue && !option.disabled,
  );
  const effectiveValue = hasSelectedOption ? selectedValue : firstEnabledValue;

  useEffect(() => {
    if (value === undefined && !options.some(
      (option) => option.value === internalValue && !option.disabled,
    )) {
      setInternalValue(initialValue);
    }
  }, [initialValue, internalValue, options, value]);

  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;
  const canAdd = canManageOptions && typeof onAddOption === 'function';
  const hasEnabledOptions = firstEnabledValue !== '';

  return (
    <div className="grid gap-2 text-start" dir="rtl">
      <label htmlFor={id} className="text-sm font-semibold text-foreground">
        {label}{required && <span aria-hidden="true"> *</span>}
      </label>
      <div className="flex items-stretch gap-2">
        <select
          id={id}
          name={name}
          dir="rtl"
          value={effectiveValue}
          required={required}
          disabled={disabled || !hasEnabledOptions}
          aria-invalid={Boolean(error)}
          aria-describedby={describedBy}
          onChange={(event) => {
            const nextValue = event.currentTarget.value;
            if (value === undefined) setInternalValue(nextValue);
            onValueChange?.(nextValue);
          }}
          className="min-h-11 min-w-0 flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground shadow-sm outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
        >
          {hasEnabledOptions ? (
            options.map((option) => (
              <option key={option.value} value={option.value} disabled={option.disabled}>
                {option.label}
              </option>
            ))
          ) : (
            <option value="">گزینهٔ فعالی موجود نیست</option>
          )}
        </select>
        {canAdd && (
          <button
            type="button"
            onClick={onAddOption}
            disabled={disabled}
            className="min-h-11 shrink-0 rounded-md border border-input bg-background px-3 text-sm font-semibold text-primary outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
          >
            {addOptionLabel}
          </button>
        )}
      </div>
      {hint && <p id={hintId} className="text-xs leading-6 text-muted-foreground">{hint}</p>}
      {error && <p id={errorId} role="alert" className="text-xs leading-6 text-destructive">{error}</p>}
    </div>
  );
}
