import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { ApiError, getAdminUsers } from '../../api/client';
import type { AdminUserListItem } from '../../api/types';
import { cn } from '@/lib/utils';
import { fullName } from './users/userDisplay';
import { Input } from '../ui/FormField';

export type PickedUser = { id: string; label: string };

type Props = {
  value: PickedUser | null;
  onChange: (user: PickedUser | null) => void;
  onUnauthenticated: (error: unknown) => boolean;
  /** Wired by `FormField`. */
  id?: string;
  'aria-describedby'?: string;
  'aria-invalid'?: boolean;
  className?: string;
};

const minQueryLength = 2;
const debounceMs = 300;
const resultLimit = 8;
const number = new Intl.NumberFormat('fa-IR');

type Results =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; users: AdminUserListItem[]; total: number }
  | { kind: 'error'; message: string };

/**
 * A search-as-you-type user chooser for installation administrators (`GET /admin/users?search=`,
 * the real user list; nothing is invented). It follows the ARIA combobox pattern: the input owns
 * focus, ArrowUp/ArrowDown move the active option, Enter chooses it and Escape closes the list.
 */
export function UserPicker({ value, onChange, onUnauthenticated, className, ...aria }: Props) {
  const generatedId = useId();
  const inputId = aria.id ?? `${generatedId}-input`;
  const listId = `${generatedId}-list`;
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const [results, setResults] = useState<Results>({ kind: 'idle' });
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const term = query.trim();
    if (value || term.length < minQueryLength) {
      setResults({ kind: 'idle' });
      return undefined;
    }
    const controller = new AbortController();
    setResults({ kind: 'loading' });
    const timer = window.setTimeout(() => {
      getAdminUsers({ search: term, status: 'all', sort: 'name', descending: false, page: 1, pageSize: resultLimit }, controller.signal)
        .then((response) => {
          if (controller.signal.aborted) return;
          setResults({ kind: 'ready', users: response.items, total: response.totalCount });
          setActive(response.items.length > 0 ? 0 : -1);
        })
        .catch((cause: unknown) => {
          if (controller.signal.aborted || onUnauthenticated(cause)) return;
          setResults({ kind: 'error', message: cause instanceof ApiError && cause.status === 403 ? 'فهرست کاربران فقط برای مدیر نصب در دسترس است.' : 'جستجوی کاربران انجام نشد.' });
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

  const choose = (user: AdminUserListItem) => {
    onChange({ id: user.id, label: fullName(user) === 'بدون نام ثبت‌شده' ? (user.username ?? user.emailAddress ?? 'کاربر بدون نام') : fullName(user) });
    setQuery('');
    setOpen(false);
    setResults({ kind: 'idle' });
  };

  const users = results.kind === 'ready' ? results.users : [];
  const optionId = (index: number) => `${listId}-${index}`;

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      if (users.length > 0) setActive((current) => (current + 1) % users.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (users.length > 0) setActive((current) => (current <= 0 ? users.length - 1 : current - 1));
    } else if (event.key === 'Enter') {
      const user = users[active];
      if (open && user) {
        event.preventDefault();
        choose(user);
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
          aria-label={`حذف کاربر انتخاب‌شده ${value.label}`}
          onClick={() => onChange(null)}
          className="inline-flex size-10 shrink-0 items-center justify-center rounded-lg text-sub outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
        >
          ✕
        </button>
      </div>
    );
  }

  const showList = open && query.trim().length >= minQueryLength;
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
        placeholder="جستجوی نام، نام کاربری یا ایمیل…"
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
        aria-label="نتایج جستجوی کاربر"
        hidden={!showList}
        className="absolute inset-x-0 top-full z-20 mt-1 max-h-64 overflow-y-auto rounded-lg border border-line bg-white p-1 shadow-lg"
      >
        {results.kind === 'loading' ? <li role="presentation" className="px-3 py-2 text-sm text-sub">در حال جستجو…</li> : null}
        {results.kind === 'error' ? <li role="alert" className="px-3 py-2 text-sm text-danger">{results.message}</li> : null}
        {results.kind === 'ready' && users.length === 0 ? <li role="presentation" className="px-3 py-2 text-sm text-sub">کاربری پیدا نشد.</li> : null}
        {users.map((user, index) => (
          <li
            key={user.id}
            id={optionId(index)}
            role="option"
            aria-selected={index === active}
            className={cn('cursor-pointer rounded-md px-3 py-2 text-sm', index === active ? 'bg-pick-soft text-ink' : 'text-ink hover:bg-cool')}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => choose(user)}
            onMouseEnter={() => setActive(index)}
          >
            <span>{fullName(user)}</span>
            {user.username ? <code dir="ltr" className="ms-2 text-xs text-sub">{user.username}</code> : null}
            {!user.isActive ? <span className="ms-2 text-xs text-sub">(غیرفعال)</span> : null}
          </li>
        ))}
        {results.kind === 'ready' && results.total > users.length ? (
          <li role="presentation" className="px-3 py-2 text-xs text-sub">فقط {number.format(users.length)} نتیجهٔ اول نمایش داده شد؛ جستجو را دقیق‌تر کنید.</li>
        ) : null}
      </ul>
    </div>
  );
}
