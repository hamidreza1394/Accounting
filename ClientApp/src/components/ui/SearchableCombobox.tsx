import { useCallback, useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { Check, ChevronDown, Loader2, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { isAbortError } from '@/lib/staleGuard';

/**
 * The shared searchable single-select of the app (D-082: medium-sized lists that are too long for
 * a plain `<select>`). One option is `code + name`; the search matches the code, the name, the
 * description and any extra keyword. It works with a local `options` list today and with an async
 * `loadOptions` source (server-side search, abortable and debounced) without a change of its
 * callers' markup.
 *
 * ARIA 1.2 combobox pattern: the input owns focus (`role="combobox"`, `aria-expanded`,
 * `aria-controls`, `aria-activedescendant`), the popup is a `listbox` of `option`s, ArrowUp/Down
 * move the active option, Enter chooses it, Escape closes, Tab leaves without choosing. It is
 * safe inside `FormField` (which wires `id`, `aria-describedby`, `aria-invalid`, `aria-required`).
 * Presentation only: it never decides what the user may choose; the server validates the value.
 */
export type ComboboxOption = {
  value: string;
  label: string;
  /** Short identifier shown first and always left-to-right (a country code, an account code). */
  code?: string;
  /** Secondary text (for example the English name). */
  description?: string;
  /** Extra search terms that are not displayed. */
  keywords?: readonly string[];
  disabled?: boolean;
};

export type SearchableComboboxProps = {
  /** The chosen option value, or `null` for no choice. */
  value: string | null;
  onChange: (value: string | null, option: ComboboxOption | null) => void;
  /** A local option list (filtered on the client). Ignored when `loadOptions` is given. */
  options?: readonly ComboboxOption[];
  /** An async source: called with the typed text; the previous call is aborted. */
  loadOptions?: (query: string, signal: AbortSignal) => Promise<readonly ComboboxOption[]>;
  /** The chosen option when it is not part of the current (filtered or remote) list. */
  selectedOption?: ComboboxOption | null;
  /** Wired by `FormField`. */
  id?: string;
  'aria-describedby'?: string;
  'aria-invalid'?: boolean;
  'aria-required'?: boolean;
  /** Use only when no visible label is associated with the input. */
  'aria-label'?: string;
  placeholder?: string;
  emptyText?: string;
  loadingText?: string;
  errorText?: string;
  retryLabel?: string;
  clearLabel?: string;
  listLabel?: string;
  /** Screen-reader count of the results, e.g. «۵ مورد یافت شد». */
  resultsCountText?: (count: number) => string;
  /** The note under a list cut at `maxVisible`. */
  truncatedText?: (shown: number) => string;
  disabled?: boolean;
  className?: string;
  /** Debounce of the async source, in milliseconds. */
  searchDelayMs?: number;
  /** The most options rendered at once; the rest is reached by typing. */
  maxVisible?: number;
};

const arabicIndicDigits = '٠١٢٣٤٥٦٧٨٩';
const persianDigits = '۰۱۲۳۴۵۶۷۸۹';

/** Makes Persian/Arabic spelling variants, digits and case compare equal. Display text is never changed. */
export function normalizeSearchText(value: string): string {
  let text = value.normalize('NFKC').trim().toLowerCase();
  text = text.replace(/ي/g, 'ی').replace(/ك/g, 'ک').replace(/ۀ/g, 'ه').replace(/ة/g, 'ه');
  text = text.replace(/[ً-ٰٟ‌‍‎‏]/g, '');
  text = text.replace(/[٠-٩]/g, (digit) => String(arabicIndicDigits.indexOf(digit)));
  text = text.replace(/[۰-۹]/g, (digit) => String(persianDigits.indexOf(digit)));
  return text.replace(/\s+/g, ' ');
}

function optionText(option: ComboboxOption): string {
  return option.code ? `${option.code} — ${option.label}` : option.label;
}

function rank(option: ComboboxOption, query: string): number {
  const code = option.code ? normalizeSearchText(option.code) : '';
  const label = normalizeSearchText(option.label);
  if (code === query) return 0;
  if (code.startsWith(query) || label.startsWith(query)) return 1;
  const secondary = [option.description ?? '', ...(option.keywords ?? [])].map(normalizeSearchText);
  if (secondary.some((text) => text.startsWith(query))) return 2;
  if (code.includes(query) || label.includes(query)) return 3;
  if (secondary.some((text) => text.includes(query))) return 4;
  return -1;
}

/** Filters and orders a local list: exact code first, then prefix matches, then contains. Stable inside a rank. */
export function filterComboboxOptions(options: readonly ComboboxOption[], query: string): ComboboxOption[] {
  const normalized = normalizeSearchText(query);
  if (normalized === '') return [...options];
  return options
    .map((option, index) => ({ option, index, score: rank(option, normalized) }))
    .filter((entry) => entry.score >= 0)
    .sort((left, right) => left.score - right.score || left.index - right.index)
    .map((entry) => entry.option);
}

/** `query` is the text the options belong to: options of another query are stale and must not be chosen. */
type Remote = { status: 'idle' | 'loading' | 'ready' | 'error'; options: readonly ComboboxOption[]; query: string };

export function SearchableCombobox({
  value,
  onChange,
  options = [],
  loadOptions,
  selectedOption = null,
  id,
  placeholder = 'جست‌وجو و انتخاب…',
  emptyText = 'موردی پیدا نشد',
  loadingText = 'در حال دریافت…',
  errorText = 'دریافت فهرست انجام نشد.',
  retryLabel = 'تلاش دوباره',
  clearLabel = 'پاک‌کردن انتخاب',
  listLabel = 'گزینه‌ها',
  resultsCountText = (count) => `${count.toLocaleString('fa-IR')} مورد یافت شد`,
  truncatedText = (shown) => `فقط ${shown.toLocaleString('fa-IR')} مورد نخست نمایش داده شد؛ برای نتیجهٔ دقیق‌تر جست‌وجو کنید.`,
  disabled = false,
  className,
  searchDelayMs = 300,
  maxVisible = 300,
  ...aria
}: SearchableComboboxProps) {
  const generatedId = useId();
  const inputId = id ?? `${generatedId}-input`;
  const listId = `${generatedId}-list`;
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  // `null` = the user is not editing: the input shows the chosen option; text = the typed search.
  const [query, setQuery] = useState<string | null>(null);
  const [activeIndex, setActiveIndex] = useState(0);
  const [remote, setRemote] = useState<Remote>({ status: 'idle', options: [], query: '' });
  const [attempt, setAttempt] = useState(0);
  const lastChosen = useRef<ComboboxOption | null>(null);

  const isRemote = loadOptions !== undefined;
  const searchText = query ?? '';

  // Async source: debounced, abortable; a late answer of an older query is never shown.
  useEffect(() => {
    if (!isRemote || !open) return undefined;
    const controller = new AbortController();
    setRemote((previous) => ({ status: 'loading', options: previous.options, query: previous.query }));
    const timer = window.setTimeout(
      () => {
        loadOptions(searchText, controller.signal)
          .then((result) => {
            if (!controller.signal.aborted) setRemote({ status: 'ready', options: result, query: searchText });
          })
          .catch((cause: unknown) => {
            if (controller.signal.aborted || isAbortError(cause)) return;
            setRemote({ status: 'error', options: [], query: searchText });
          });
      },
      searchText === '' ? 0 : searchDelayMs,
    );
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
    // `loadOptions` is intentionally not a dependency: callers pass inline closures; a new
    // query, an open/close or a retry reloads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isRemote, open, searchText, attempt, searchDelayMs]);

  const candidates = useMemo(
    () => (isRemote ? remote.options : filterComboboxOptions(options, searchText)),
    [isRemote, remote.options, options, searchText],
  );
  const visible = useMemo(() => candidates.slice(0, maxVisible), [candidates, maxVisible]);
  const truncated = candidates.length > visible.length;

  const chosen = useMemo<ComboboxOption | null>(() => {
    if (value === null) return null;
    return (
      options.find((option) => option.value === value) ??
      (selectedOption?.value === value ? selectedOption : null) ??
      remote.options.find((option) => option.value === value) ??
      (lastChosen.current?.value === value ? lastChosen.current : null)
    );
  }, [value, options, selectedOption, remote.options]);

  const shownText = query ?? (chosen ? optionText(chosen) : value ?? '');
  const loading = isRemote && remote.status === 'loading';
  // While the list is being reloaded for a changed query it still shows the OLD options: they must not be chosen
  // (Enter or click), or a fast typist would select a row that no longer matches what is typed.
  const stale = isRemote && (remote.status === 'loading' || remote.query !== searchText);
  const failed = isRemote && remote.status === 'error';
  const safeActive = visible.length === 0 ? -1 : Math.min(activeIndex, visible.length - 1);
  const activeId = open && safeActive >= 0 ? `${listId}-opt-${safeActive}` : undefined;

  useEffect(() => {
    if (!open || safeActive < 0) return;
    document.getElementById(`${listId}-opt-${safeActive}`)?.scrollIntoView?.({ block: 'nearest' });
  }, [open, safeActive, listId]);

  const close = useCallback(() => {
    setOpen(false);
    setQuery(null);
  }, []);

  const openList = useCallback(() => {
    if (disabled) return;
    const selectedIndex = visible.findIndex((option) => option.value === value);
    setActiveIndex(selectedIndex >= 0 ? selectedIndex : 0);
    setOpen(true);
  }, [disabled, visible, value]);

  const choose = (option: ComboboxOption) => {
    if (option.disabled || stale) return;
    lastChosen.current = option;
    onChange(option.value, option);
    close();
  };

  const clear = () => {
    lastChosen.current = null;
    onChange(null, null);
    close();
    inputRef.current?.focus();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!open) openList();
        else setActiveIndex(Math.min(safeActive + 1, Math.max(visible.length - 1, 0)));
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (!open) openList();
        else setActiveIndex(Math.max(safeActive - 1, 0));
        break;
      case 'Enter':
        // While the popup is open Enter chooses; it must never submit the surrounding form.
        if (open) {
          event.preventDefault();
          const option = visible[safeActive];
          if (option) choose(option);
        }
        break;
      case 'Escape':
        if (open) {
          event.preventDefault();
          close();
        }
        break;
      case 'Tab':
        close();
        break;
      default:
        break;
    }
  };

  return (
    <div
      ref={containerRef}
      className={cn('relative', className)}
      onBlur={(event) => {
        if (!containerRef.current?.contains(event.relatedTarget as Node | null)) close();
      }}
    >
      <div className="relative">
        <input
          ref={inputRef}
          id={inputId}
          type="text"
          role="combobox"
          aria-expanded={open}
          aria-controls={open ? listId : undefined}
          aria-activedescendant={activeId}
          aria-autocomplete="list"
          aria-haspopup="listbox"
          aria-describedby={aria['aria-describedby']}
          aria-invalid={aria['aria-invalid']}
          aria-required={aria['aria-required']}
          aria-label={aria['aria-label']}
          aria-busy={loading || undefined}
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
          placeholder={placeholder}
          value={shownText}
          onChange={(event) => {
            setQuery(event.target.value);
            setActiveIndex(0);
            setOpen(true);
          }}
          onFocus={(event) => event.currentTarget.select()}
          onClick={() => (open ? undefined : openList())}
          onKeyDown={onKeyDown}
          className="h-10 w-full rounded-lg border border-line bg-white ps-3 pe-16 text-sm text-ink outline-none transition-colors placeholder:text-muted focus-visible:border-teal-deep focus-visible:ring-2 focus-visible:ring-gold aria-[invalid=true]:border-danger disabled:cursor-not-allowed disabled:bg-cool disabled:text-sub"
        />
        <div className="absolute inset-y-0 end-2 flex items-center gap-1">
          {value !== null && !disabled ? (
            <button
              type="button"
              aria-label={clearLabel}
              onClick={clear}
              className="flex size-6 items-center justify-center rounded text-sub outline-none hover:bg-cool hover:text-ink focus-visible:ring-2 focus-visible:ring-gold"
            >
              <X size={14} aria-hidden="true" />
            </button>
          ) : null}
          <span aria-hidden="true" className="pointer-events-none text-sub">
            {loading ? <Loader2 size={16} className="motion-safe:animate-spin" /> : <ChevronDown size={16} />}
          </span>
        </div>
      </div>
      {open ? (
        <div className="absolute inset-x-0 top-full z-30 mt-1 overflow-hidden rounded-lg border border-line bg-white shadow-lg">
          {failed ? (
            <div role="alert" className="flex items-center justify-between gap-2 px-3 py-2 text-xs text-danger">
              <span>{errorText}</span>
              <button
                type="button"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => setAttempt((current) => current + 1)}
                className="rounded font-medium text-teal-deep underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-gold"
              >
                {retryLabel}
              </button>
            </div>
          ) : null}
          <div
            role="status"
            className={cn(!failed && (loading || visible.length === 0) ? 'px-3 py-2 text-xs text-sub' : 'sr-only')}
          >
            {failed ? '' : loading ? loadingText : visible.length === 0 ? emptyText : resultsCountText(visible.length)}
          </div>
          <ul id={listId} role="listbox" aria-label={listLabel} className="max-h-64 overflow-y-auto py-1">
            {visible.map((option, index) => {
              const selected = option.value === value;
              const active = index === safeActive;
              return (
                <li
                  key={option.value}
                  id={`${listId}-opt-${index}`}
                  role="option"
                  aria-selected={selected}
                  aria-disabled={option.disabled || stale || undefined}
                  onMouseDown={(event) => event.preventDefault()}
                  onMouseEnter={() => setActiveIndex(index)}
                  onClick={() => choose(option)}
                  className={cn(
                    'flex cursor-pointer items-center gap-2 px-3 py-2 text-sm text-ink',
                    active && 'bg-cool',
                    selected && 'font-semibold',
                    (option.disabled || stale) && 'cursor-not-allowed text-muted',
                  )}
                >
                  {option.code ? (
                    <bdi dir="ltr" className="w-9 shrink-0 text-start font-mono text-xs text-sub">
                      {option.code}
                    </bdi>
                  ) : null}
                  <span className="min-w-0 flex-1">
                    {option.label}
                    {option.description ? (
                      <bdi dir="ltr" className="ms-2 text-xs font-normal text-sub">
                        {option.description}
                      </bdi>
                    ) : null}
                  </span>
                  {selected ? <Check size={14} aria-hidden="true" className="shrink-0 text-teal-deep" /> : null}
                </li>
              );
            })}
          </ul>
          {truncated ? (
            <p className="border-t border-line px-3 py-2 text-xs text-sub">
              {truncatedText(visible.length)}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
