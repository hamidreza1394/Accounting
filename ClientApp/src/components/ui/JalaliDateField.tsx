import { useId, useState } from 'react';
import { Popover } from '@base-ui/react/popover';
import { DayPicker } from 'react-day-picker/persian';
import 'react-day-picker/style.css';
import { CalendarDays, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatPersianDate } from '@/lib/date';

/**
 * A Persian (Jalali) calendar date field. The VALUE is always a Gregorian ISO calendar date
 * (`yyyy-MM-dd`, or `''` for none): the Jalali calendar is only how it is shown and chosen
 * (D-049, D-068). It carries no time and no time zone; turning a day into UTC instants is
 * `dayStartUtc`/`dayEndUtc` in lib/time.ts (docs/TIME_CONTRACT.md).
 *
 * Built on `react-day-picker/persian` (v9): month/year dropdowns, RTL labels, and Persian digits
 * without a second calendar library.
 */
export type JalaliDateFieldProps = {
  /** Gregorian ISO calendar date, or `''`. */
  value: string;
  onChange: (value: string) => void;
  /** Earliest / latest selectable day (ISO). Days outside are shown disabled. */
  min?: string;
  max?: string;
  placeholder?: string;
  disabled?: boolean;
  /** Accessible name of the "clear" button; defaults to a generic Persian label. */
  clearLabel?: string;
  /** Wired by `FormField`: label association, hint/error description, validity, required. */
  id?: string;
  'aria-describedby'?: string;
  'aria-invalid'?: boolean;
  'aria-required'?: boolean;
  required?: boolean;
  className?: string;
};

const isoPattern = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Local noon of an ISO calendar date: DST transitions cannot move it to another day. */
export function isoToDate(iso: string): Date | undefined {
  const match = isoPattern.exec(iso);
  if (!match) return undefined;
  const [, year, month, day] = match;
  const date = new Date(Number(year), Number(month) - 1, Number(day), 12);
  const valid =
    Number.isFinite(date.getTime()) &&
    date.getFullYear() === Number(year) &&
    date.getMonth() === Number(month) - 1 &&
    date.getDate() === Number(day);
  return valid ? date : undefined;
}

/** The local calendar date of a chosen day as ISO `yyyy-MM-dd`. */
export function dateToIso(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${String(date.getFullYear()).padStart(4, '0')}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** `date` limited to [min, max]: an empty field opens on today when allowed, else on the nearest allowed day. */
export function clampToRange(date: Date, min?: Date, max?: Date): Date {
  if (min && date < min) return min;
  if (max && date > max) return max;
  return date;
}

const triggerClass =
  'inline-flex h-10 min-w-0 flex-1 items-center justify-between gap-2 rounded-lg border border-line bg-white px-3 text-sm text-ink outline-none transition-colors focus-visible:border-teal-deep focus-visible:ring-2 focus-visible:ring-gold aria-[invalid=true]:border-danger disabled:cursor-not-allowed disabled:bg-cool disabled:text-sub';

export function JalaliDateField({
  value,
  onChange,
  min,
  max,
  placeholder = 'انتخاب تاریخ',
  disabled = false,
  clearLabel = 'پاک‌کردن تاریخ',
  className,
  ...aria
}: JalaliDateFieldProps) {
  const [open, setOpen] = useState(false);
  const titleId = useId();
  const valueId = useId();
  const selected = isoToDate(value);
  const minDate = min ? isoToDate(min) : undefined;
  const maxDate = max ? isoToDate(max) : undefined;
  const startMonth = minDate ?? new Date(2000, 0, 1);
  const endMonth = maxDate ?? new Date(2100, 11, 1);
  const today = clampToRange(new Date(), minDate, maxDate);
  const todayIso = dateToIso(today);
  const todayDisabled =
    (minDate !== undefined && today < minDate) || (maxDate !== undefined && today > maxDate);

  let text = placeholder;
  if (selected) {
    try {
      text = formatPersianDate(value);
    } catch {
      text = placeholder;
    }
  }

  return (
    <div className={cn('flex items-center gap-1', className)}>
      <Popover.Root open={open} onOpenChange={setOpen}>
        <Popover.Trigger
          type="button"
          disabled={disabled}
          id={aria.id}
          // The name usually comes from the field label, which hides the content; the chosen day is
          // therefore part of the description so a screen reader reads it after the label.
          aria-describedby={[valueId, aria['aria-describedby']].filter(Boolean).join(' ')}
          aria-invalid={aria['aria-invalid']}
          aria-required={aria['aria-required'] ?? aria.required}
          className={triggerClass}
        >
          <span id={valueId} className={cn('truncate', !selected && 'text-muted')} dir={selected ? 'ltr' : undefined}>
            {text}
          </span>
          <CalendarDays size={16} aria-hidden="true" className="shrink-0 text-sub" />
        </Popover.Trigger>
        <Popover.Portal>
          <Popover.Positioner sideOffset={6} className="z-50">
            <Popover.Popup
              aria-labelledby={titleId}
              dir="rtl"
              className="faranou-jalali-picker rounded-xl border border-line bg-white p-3 text-ink shadow-xl outline-none"
              style={
                {
                  '--rdp-accent-color': 'var(--color-teal-deep)',
                  '--rdp-accent-background-color': 'var(--color-pick-soft)',
                  '--rdp-day_button-border-radius': '0.5rem',
                  '--rdp-selected-border': '2px solid var(--color-teal-deep)',
                } as React.CSSProperties
              }
            >
              <Popover.Title id={titleId} className="sr-only">
                انتخاب تاریخ شمسی
              </Popover.Title>
              <DayPicker
                mode="single"
                captionLayout="dropdown"
                navLayout="around"
                {...(selected ? { selected } : {})}
                defaultMonth={selected ?? clampToRange(new Date(), minDate, maxDate)}
                startMonth={startMonth}
                endMonth={endMonth}
                disabled={[...(minDate ? [{ before: minDate }] : []), ...(maxDate ? [{ after: maxDate }] : [])]}
                onSelect={(date) => {
                  // Clicking the selected day again clears it in DayPicker; keep a chosen value instead.
                  if (!date) return;
                  onChange(dateToIso(date));
                  setOpen(false);
                }}
                classNames={{
                  root: 'rdp-root faranou-rdp',
                  months: 'rdp-months',
                  month: 'rdp-month',
                  month_caption: 'rdp-month_caption faranou-rdp-caption',
                  dropdowns: 'rdp-dropdowns faranou-rdp-dropdowns',
                  dropdown_root: 'rdp-dropdown_root faranou-rdp-dropdown',
                  caption_label: 'rdp-caption_label',
                  button_previous: 'rdp-button_previous faranou-rdp-nav',
                  button_next: 'rdp-button_next faranou-rdp-nav',
                  weekdays: 'rdp-weekdays',
                  weekday: 'rdp-weekday faranou-rdp-weekday',
                  week: 'rdp-week',
                  day: 'rdp-day',
                  day_button: 'rdp-day_button faranou-rdp-day',
                  selected: 'rdp-selected',
                  today: 'rdp-today faranou-rdp-today',
                  outside: 'rdp-outside',
                  disabled: 'rdp-disabled',
                  hidden: 'rdp-hidden',
                }}
              />
              <div className="mt-2 flex items-center justify-between gap-2 border-t border-line pt-2">
                <button
                  type="button"
                  disabled={todayDisabled}
                  onClick={() => {
                    onChange(todayIso);
                    setOpen(false);
                  }}
                  className="rounded-md px-3 py-1.5 text-xs font-semibold text-teal-deep outline-none hover:bg-pick-soft focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-not-allowed disabled:text-muted"
                >
                  امروز
                </button>
                {value ? (
                  <button
                    type="button"
                    onClick={() => {
                      onChange('');
                      setOpen(false);
                    }}
                    className="rounded-md px-3 py-1.5 text-xs font-semibold text-sub outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
                  >
                    پاک کردن
                  </button>
                ) : (
                  <span className="text-[11px] text-muted">ماه و سال را از بالا انتخاب کنید</span>
                )}
              </div>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>
      {value && !disabled ? (
        <button
          type="button"
          aria-label={clearLabel}
          onClick={() => onChange('')}
          className="inline-flex size-10 shrink-0 items-center justify-center rounded-lg text-sub outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
        >
          <X size={16} aria-hidden="true" />
        </button>
      ) : null}
    </div>
  );
}
