import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { ApiError, getAdminCompanies } from '../../../api/client';
import type { AdminCompanyItem } from '../../../api/types';
import { cn } from '@/lib/utils';
import { Input } from '../../ui/FormField';

export type PickedCompany = { id: string; label: string };

type Props = {
  value: PickedCompany | null;
  onChange: (company: PickedCompany | null) => void;
  onUnauthenticated: (error: unknown) => boolean;
  /** Wired by `FormField`. */
  id?: string;
  'aria-describedby'?: string;
  'aria-invalid'?: boolean;
  className?: string;
};

const debounceMs = 300;
const resultLimit = 20;
const number = new Intl.NumberFormat('fa-IR');

type Results =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; companies: AdminCompanyItem[]; total: number }
  | { kind: 'error'; message: string };

/**
 * A search-as-you-type chooser over EVERY company of the installation (`GET /admin/companies`,
 * server-side search and paging; installation administrators only). Unlike the memberships of
 * `GET /companies`, historical events of inactive companies stay filterable here; an inactive
 * company is marked as such. It follows the ARIA combobox pattern like `UserPicker`.
 */
export function CompanyPicker({ value, onChange, onUnauthenticated, className, ...aria }: Props) {
  const generatedId = useId();
  const inputId = aria.id ?? `${generatedId}-input`;
  const listId = `${generatedId}-list`;
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const [results, setResults] = useState<Results>({ kind: 'idle' });
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (value) {
      setResults({ kind: 'idle' });
      return undefined;
    }
    const term = query.trim();
    const controller = new AbortController();
    setResults({ kind: 'loading' });
    const timer = window.setTimeout(() => {
      const query: Parameters<typeof getAdminCompanies>[0] = { page: 1, pageSize: resultLimit };
      if (term !== '') query.search = term;
      getAdminCompanies(query, controller.signal)
        .then((response) => {
          if (controller.signal.aborted) return;
          setResults({ kind: 'ready', companies: response.items, total: response.totalCount });
          setActive(response.items.length > 0 ? 0 : -1);
        })
        .catch((cause: unknown) => {
          if (controller.signal.aborted || onUnauthenticated(cause)) return;
          setResults({
            kind: 'error',
            message:
              cause instanceof ApiError && cause.status === 403
                ? 'فهرست همهٔ شرکت‌های نصب فقط برای مدیر نصب در دسترس است.'
                : 'جستجوی شرکت‌ها انجام نشد.',
          });
        });
    }, debounceMs);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [query, value, onUnauthenticated]);

  // Close when focus leaves the whole control.
  useEffect(() => {
    if (!open) return undefined;
    const onPointerDown = (event: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onPointerDown);
    return () => document.removeEventListener('mousedown', onPointerDown);
  }, [open]);

  const choose = (company: AdminCompanyItem) => {
    onChange({ id: company.id, label: company.name });
    setQuery('');
    setOpen(false);
    setResults({ kind: 'idle' });
  };

  const companies = results.kind === 'ready' ? results.companies : [];
  const optionId = (index: number) => `${listId}-${index}`;

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      if (companies.length > 0) setActive((current) => (current + 1) % companies.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (companies.length > 0) setActive((current) => (current <= 0 ? companies.length - 1 : current - 1));
    } else if (event.key === 'Enter') {
      const company = companies[active];
      if (open && company) {
        event.preventDefault();
        choose(company);
      }
    } else if (event.key === 'Escape' && open) {
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
    }
  };

  if (value) {
    return (
      <div className={cn('flex items-center gap-1', className)}>
        <span id={inputId} className="inline-flex h-10 min-w-0 flex-1 items-center rounded-lg border border-line bg-cool px-3 text-sm text-ink" aria-describedby={aria['aria-describedby']}>
          <span className="truncate">{value.label}</span>
        </span>
        <button
          type="button"
          aria-label={`حذف شرکت انتخاب‌شده ${value.label}`}
          onClick={() => onChange(null)}
          className="inline-flex size-10 shrink-0 items-center justify-center rounded-lg text-sub outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
        >
          ✕
        </button>
      </div>
    );
  }

  const showList = open;
  return (
    <div ref={wrapperRef} className={cn('relative', className)}>
      <Input
        id={inputId}
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={showList && active >= 0 ? optionId(active) : undefined}
        aria-describedby={aria['aria-describedby']}
        aria-invalid={aria['aria-invalid']}
        autoComplete="off"
        placeholder="جستجوی نام شرکت…"
        value={query}
        onChange={(event) => {
          setQuery(event.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={onKeyDown}
      />
      <ul
        id={listId}
        role="listbox"
        aria-label="نتایج جستجوی شرکت"
        hidden={!showList}
        className="absolute inset-x-0 top-full z-20 mt-1 max-h-64 overflow-y-auto rounded-lg border border-line bg-white p-1 shadow-lg"
      >
        {results.kind === 'loading' ? <li role="presentation" className="px-3 py-2 text-sm text-sub">در حال جستجو…</li> : null}
        {results.kind === 'error' ? <li role="alert" className="px-3 py-2 text-sm text-danger">{results.message}</li> : null}
        {results.kind === 'ready' && companies.length === 0 ? <li role="presentation" className="px-3 py-2 text-sm text-sub">شرکتی پیدا نشد.</li> : null}
        {companies.map((company, index) => (
          <li
            key={company.id}
            id={optionId(index)}
            role="option"
            aria-selected={index === active}
            className={cn('cursor-pointer rounded-md px-3 py-2 text-sm', index === active ? 'bg-pick-soft text-ink' : 'text-ink hover:bg-cool')}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => choose(company)}
            onMouseEnter={() => setActive(index)}
          >
            <span>{company.name}</span>
            {!company.isActive ? <span className="ms-2 text-xs text-sub">(غیرفعال)</span> : null}
          </li>
        ))}
        {results.kind === 'ready' && results.total > companies.length ? (
          <li role="presentation" className="px-3 py-2 text-xs text-sub">
            فقط {number.format(companies.length)} نتیجهٔ اول نمایش داده شد؛ جستجو را دقیق‌تر کنید.
          </li>
        ) : null}
      </ul>
    </div>
  );
}
