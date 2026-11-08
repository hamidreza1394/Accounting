import { useEffect, useId, useState } from 'react';
import { Loader2, Search } from 'lucide-react';
import { Select } from '@/components/ui/FormField';
import { cn } from '@/lib/utils';
import { isAbortError } from '@/lib/staleGuard';

export type HrPickerOption = { key: string; label: string; detail?: string | null };

type HrSearchSelectProps = {
  /** Accessible name of the search input; the select is named by the wrapping FormField label. */
  searchLabel: string;
  value: string | null;
  onValueChange: (key: string | null) => void;
  /** Loads up to one page of options for the given search term (abortable). */
  load: (search: string, signal: AbortSignal) => Promise<readonly HrPickerOption[]>;
  placeholder?: string;
  /** Shown when the search returns nothing. */
  emptyLabel?: string;
  /** Shown while the first page loads. */
  loadingLabel?: string;
  clearLabel?: string;
  disabled?: boolean;
  className?: string;
};

const searchDelayMs = 350;
const pickerPageSize = 25;

/**
 * A searchable, server-backed single-select for datasets too large for one `<select>` (units,
 * positions, employees, users). A debounced search input reloads the options from the real
 * endpoint; the choice itself is a native `<select>` (keyboard- and screen-reader-friendly).
 * `null` means "no choice" — for the assignment forms that is the documented "keep the current
 * value" state.
 */
export function HrSearchSelect({
  searchLabel,
  value,
  onValueChange,
  load,
  placeholder = 'انتخاب کنید',
  emptyLabel = 'موردی پیدا نشد',
  loadingLabel = 'در حال دریافت…',
  clearLabel = 'پاک‌کردن انتخاب',
  disabled = false,
  className,
}: HrSearchSelectProps) {
  const searchId = useId();
  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [options, setOptions] = useState<readonly HrPickerOption[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (searchText === search) return undefined;
    const timer = window.setTimeout(() => setSearch(searchText), searchDelayMs);
    return () => window.clearTimeout(timer);
  }, [searchText, search]);

  useEffect(() => {
    const controller = new AbortController();
    setFailed(false);
    load(search, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setOptions(result);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || isAbortError(cause)) return;
        setFailed(true);
      });
    return () => controller.abort();
    // `load` is a prop closure over companyId; re-running on its identity keeps the picker honest.
  }, [search, attempt, load]);

  const selected = options?.find((option) => option.key === value) ?? null;

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <div className="relative">
        <Search size={15} aria-hidden="true" className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-sub" />
        <input
          id={searchId}
          type="search"
          value={searchText}
          disabled={disabled}
          aria-label={`جستجو در ${searchLabel}`}
          autoComplete="off"
          maxLength={120}
          placeholder="جستجو…"
          onChange={(event) => setSearchText(event.target.value)}
          className="h-9 w-full rounded-lg border border-line bg-white ps-9 pe-3 text-sm text-ink outline-none transition-colors placeholder:text-muted focus-visible:border-teal-deep focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-not-allowed disabled:bg-cool"
        />
      </div>
      {failed ? (
        <p className="flex items-center gap-2 text-xs text-danger">
          دریافت فهرست انجام نشد.
          <button
            type="button"
            onClick={() => setAttempt((current) => current + 1)}
            className="rounded font-medium text-teal-deep outline-none underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:ring-gold"
          >
            تلاش دوباره
          </button>
        </p>
      ) : options === null ? (
        <p className="flex items-center gap-2 text-xs text-sub">
          <Loader2 size={13} aria-hidden="true" className="motion-safe:animate-spin" />
          {loadingLabel}
        </p>
      ) : options.length === 0 ? (
        <p className="text-xs text-sub">{emptyLabel}</p>
      ) : (
        <Select
          value={value ?? ''}
          disabled={disabled}
          aria-label={searchLabel}
          onChange={(event) => onValueChange(event.target.value === '' ? null : event.target.value)}
          className="h-9"
        >
          <option value="">{placeholder}</option>
          {options.map((option) => (
            <option key={option.key} value={option.key}>
              {option.label}
              {option.detail ? ` (${option.detail})` : ''}
            </option>
          ))}
        </Select>
      )}
      <div className="flex items-center justify-between gap-2 text-xs text-sub">
        <span>
          {selected ? `انتخاب‌شده: ${selected.label}` : `حداکثر ${pickerPageSize} نتیجه؛ برای نتیجهٔ دقیق‌تر جستجو کنید.`}
        </span>
        {value !== null && !disabled ? (
          <button
            type="button"
            onClick={() => onValueChange(null)}
            className="shrink-0 rounded font-medium text-teal-deep outline-none underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:ring-gold"
          >
            {clearLabel}
          </button>
        ) : null}
      </div>
    </div>
  );
}
