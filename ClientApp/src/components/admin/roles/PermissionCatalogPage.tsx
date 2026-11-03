import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { Search, X } from 'lucide-react';
import { ApiError, getPermissionCatalog } from '../../../api/client';
import type { PermissionCatalogItem } from '../../../api/types';
import { filterPermissions, operationLabels, operationOf, subsystemLabel } from '../../../lib/permissionTree';
import { useSearchParams } from '../../../lib/router';
import { Badge } from '../../ui/Badge';
import { Breadcrumb } from '../../ui/Breadcrumb';
import { DataTable, type DataTableColumn } from '../../ui/DataTable';
import { FormField, Input, Select } from '../../ui/FormField';
import { Button } from '../../ui/button';

type Props = { onUnauthenticated: (error: unknown) => boolean };

const searchDebounceMs = 300;
const number = new Intl.NumberFormat('fa-IR');

type Failure = { message: string; forbidden: boolean };

function toFailure(cause: unknown): Failure {
  if (cause instanceof ApiError) {
    return cause.status === 403
      ? { message: 'شما مجوز مشاهدهٔ کاتالوگ مجوزها را ندارید.', forbidden: true }
      : { message: cause.message, forbidden: false };
  }
  return { message: 'دریافت کاتالوگ مجوزها انجام نشد. لطفاً دوباره تلاش کنید.', forbidden: false };
}

const columns: readonly DataTableColumn<PermissionCatalogItem>[] = [
  {
    key: 'code',
    header: 'کد مجوز',
    cell: (item) => (
      <code dir="ltr" className="block break-all text-start text-xs text-ink">
        {item.code}
      </code>
    ),
  },
  {
    key: 'name',
    header: 'عنوان',
    cell: (item) => (
      // Native disclosure: keyboard and screen-reader support without extra code.
      <details>
        <summary className="cursor-pointer rounded font-semibold outline-none focus-visible:ring-2 focus-visible:ring-gold max-sm:min-h-11 max-sm:py-2.5">
          {item.name}
        </summary>
        <dl className="mt-2 space-y-1 text-xs leading-5 text-sub">
          <div>
            <dt className="inline font-semibold">توضیح: </dt>
            <dd className="inline">{item.description ?? 'ثبت نشده'}</dd>
          </div>
          <div>
            <dt className="inline font-semibold">شناسه: </dt>
            <dd dir="ltr" className="inline break-all">
              {item.id}
            </dd>
          </div>
        </dl>
      </details>
    ),
  },
  { key: 'description', header: 'توضیح', cell: (item) => <span className="line-clamp-2 min-w-48 text-sub">{item.description ?? '—'}</span> },
  {
    key: 'subsystem',
    header: 'زیرسیستم',
    cell: (item) => (
      <span className="whitespace-nowrap">
        {subsystemLabel(item.subsystemCode) ? `${subsystemLabel(item.subsystemCode)} ` : null}
        <bdi dir="ltr" className="text-xs text-sub">
          {item.subsystemCode}
        </bdi>
      </span>
    ),
  },
  {
    key: 'operation',
    header: 'نوع عملیات (استنباط‌شده)',
    cell: (item) => {
      const operation = operationOf(item.code);
      return operation ? <Badge tone="neutral" icon={null}>{operationLabels[operation]}</Badge> : '—';
    },
  },
];

/**
 * The read-only permission catalog. The endpoint is unpaged and small (a few hundred entries), so
 * search and the subsystem filter run in the browser; the state lives in the URL (`q`, `subsystem`).
 * The operation column is derived from the last code segment on the client (UI-derived).
 */
export function PermissionCatalogPage({ onUnauthenticated }: Props) {
  const [params, setParams] = useSearchParams();
  const q = (params.get('q') ?? '').trim();
  const subsystemParam = params.get('subsystem') ?? '';
  const searchId = useId();
  const [searchInput, setSearchInput] = useState(q);
  const [items, setItems] = useState<PermissionCatalogItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [attempt, setAttempt] = useState(0);

  const paramsRef = useRef(params);
  paramsRef.current = params;
  const ownNavigation = useRef(false);
  const write = (patch: { q?: string; subsystem?: string }, replace: boolean) => {
    const next = new URLSearchParams(paramsRef.current);
    for (const [key, value] of Object.entries(patch)) {
      if (value) next.set(key, value);
      else next.delete(key);
    }
    ownNavigation.current = next.toString() !== paramsRef.current.toString();
    setParams(next, { replace });
  };
  const writeRef = useRef(write);
  writeRef.current = write;

  useEffect(() => {
    const trimmed = searchInput.trim();
    if (trimmed === q) return;
    const timer = window.setTimeout(() => writeRef.current({ q: trimmed }, true), searchDebounceMs);
    return () => window.clearTimeout(timer);
  }, [searchInput, q, params]);

  // Back/forward restores the search box; typing made by this page is kept.
  useEffect(() => {
    if (ownNavigation.current) {
      ownNavigation.current = false;
      return;
    }
    setSearchInput(q);
  }, [params, q]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getPermissionCatalog(controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setFailure(toFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);

  const subsystems = useMemo(() => [...new Set((items ?? []).map((item) => item.subsystemCode))].sort(), [items]);
  const subsystem = subsystems.includes(subsystemParam) ? subsystemParam : '';
  const rows = useMemo(
    () => filterPermissions(items ?? [], q).filter((item) => subsystem === '' || item.subsystemCode === subsystem),
    [items, q, subsystem],
  );
  const hasFilters = q !== '' || subsystem !== '' || searchInput.trim() !== '';

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'مدیریت سیستم' }, { label: 'کاتالوگ مجوزها' }]} />
        <h1 className="text-2xl font-extrabold tracking-tight text-title">کاتالوگ مجوزها</h1>
        <p className="text-sm text-sub">
          فهرست مجوزهای ثبت‌شده در سامانه. این فهرست فقط‌خواندنی است و مجوزها را سامانه تعریف می‌کند؛ تخصیص مجوز از صفحهٔ «گروه‌های دسترسی» انجام می‌شود. ستون «نوع عملیات» از آخرین بخش کد استنباط می‌شود و فقط برای خوانایی است.
        </p>
      </header>

      <form role="search" aria-label="جستجو و فیلتر مجوزها" onSubmit={(event) => event.preventDefault()} className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-60 flex-1 flex-col gap-1.5">
          <label htmlFor={searchId} className="text-sm font-semibold text-ink">جستجوی مجوزها</label>
          <div className="relative">
            <Search aria-hidden="true" size={16} className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-muted" />
            <Input
              id={searchId}
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="کد، عنوان یا توضیح…"
              autoComplete="off"
              className="h-11 ps-9 pe-11 sm:h-10 [&::-webkit-search-cancel-button]:appearance-none"
            />
            {searchInput ? (
              <button
                type="button"
                aria-label="پاک‌کردن جستجو"
                onClick={() => {
                  setSearchInput('');
                  write({ q: '' }, true);
                }}
                className="absolute inset-y-0 end-0 flex w-11 items-center justify-center rounded-e-lg text-sub outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-gold"
              >
                <X aria-hidden="true" size={16} />
              </button>
            ) : null}
          </div>
        </div>
        <FormField label="زیرسیستم" className="w-48">
          <Select className="h-11 sm:h-10" value={subsystem} onChange={(event) => write({ subsystem: event.target.value }, false)}>
            <option value="">همه</option>
            {subsystems.map((code) => (
              <option key={code} value={code}>
                {subsystemLabel(code) ? `${subsystemLabel(code)} (${code})` : code}
              </option>
            ))}
          </Select>
        </FormField>
      </form>

      {items && !failure ? (
        <p role="status" aria-live="polite" className="text-sm text-sub">
          {number.format(rows.length)} مجوز از {number.format(items.length)}
        </p>
      ) : null}

      <DataTable
        caption="کاتالوگ مجوزها"
        columns={columns}
        rows={rows}
        getRowKey={(item) => item.id}
        loading={loading}
        error={failure?.message ?? null}
        {...(failure && !failure.forbidden ? { onRetry: () => setAttempt((value) => value + 1) } : {})}
        emptyTitle="مجوزی پیدا نشد"
        emptyDescription={
          hasFilters ? (
            <Button
              type="button"
              variant="outline"
              size="lg"
              onClick={() => {
                setSearchInput('');
                write({ q: '', subsystem: '' }, false);
              }}
            >
              پاک‌کردن فیلترها
            </Button>
          ) : (
            'هنوز مجوزی در کاتالوگ ثبت نشده است.'
          )
        }
      />
    </div>
  );
}
