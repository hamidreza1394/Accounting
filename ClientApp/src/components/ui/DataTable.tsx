import type { ReactNode } from 'react';
import { ChevronLeft, ChevronRight, ChevronsUpDown, ChevronDown, ChevronUp } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Link } from '@/lib/router';
import { EmptyState, ErrorState } from '../Feedback';
import { Button } from './button';
import { Select } from './FormField';

export type SortDirection = 'asc' | 'desc';
export type SortState = { key: string; direction: SortDirection };

export type DataTableColumn<T> = {
  key: string;
  header: ReactNode;
  cell: (row: T) => ReactNode;
  sortable?: boolean;
  align?: 'start' | 'end' | 'center';
  className?: string;
};

export type DataTablePagination = {
  page: number;
  pageSize: number;
  total: number;
  pageSizeOptions?: readonly number[];
  onPageChange: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
};

type DataTableProps<T> = {
  /** Accessible caption (visually hidden); names the table and its scroll region. */
  caption: string;
  columns: readonly DataTableColumn<T>[];
  rows: readonly T[];
  getRowKey: (row: T) => string;
  loading?: boolean;
  /** A user-safe message; shows the error state instead of rows. */
  error?: string | null;
  onRetry?: () => void;
  emptyTitle?: string;
  emptyDescription?: ReactNode;
  sort?: SortState | null;
  onSortChange?: (sort: SortState) => void;
  pagination?: DataTablePagination;
  /** When it returns an app-relative path, the row is clickable through a real link in the first cell. */
  rowHref?: (row: T) => string | undefined;
};

const alignClass = { start: 'text-start', end: 'text-end', center: 'text-center' } as const;
const skeletonRows = 5;
const number = new Intl.NumberFormat('fa-IR');

function ariaSort(sort: SortState | null | undefined, key: string): 'ascending' | 'descending' | 'none' {
  if (sort?.key !== key) return 'none';
  return sort.direction === 'asc' ? 'ascending' : 'descending';
}

function Pagination({ pagination }: { pagination: DataTablePagination }) {
  const { page, pageSize, total, pageSizeOptions = [10, 20, 50], onPageChange, onPageSizeChange } = pagination;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line bg-white px-4 py-3 text-sm text-sub">
      <p>
        نمایش {number.format(from)} تا {number.format(to)} از {number.format(total)}
      </p>
      <div className="flex flex-wrap items-center gap-3">
        {onPageSizeChange ? (
          <label className="flex items-center gap-2">
            <span>تعداد در صفحه</span>
            <Select className="h-9 w-auto" value={pageSize} onChange={(event) => onPageSizeChange(Number(event.target.value))}>
              {pageSizeOptions.map((option) => (
                <option key={option} value={option}>
                  {number.format(option)}
                </option>
              ))}
            </Select>
          </label>
        ) : null}
        <nav aria-label="صفحه‌بندی" className="flex items-center gap-2">
          <Button type="button" variant="outline" size="lg" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
            <ChevronRight aria-hidden="true" />
            قبلی
          </Button>
          <span aria-current="page">
            صفحه {number.format(page)} از {number.format(pageCount)}
          </span>
          <Button type="button" variant="outline" size="lg" disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
            بعدی
            <ChevronLeft aria-hidden="true" />
          </Button>
        </nav>
      </div>
    </div>
  );
}

/**
 * A presentational, server-driven table: sorting and paging are requested through callbacks and the
 * parent loads the rows. Shows loading skeleton, empty and error states and scrolls horizontally
 * below `md`.
 */
export function DataTable<T>({
  caption,
  columns,
  rows,
  getRowKey,
  loading = false,
  error = null,
  onRetry,
  emptyTitle = 'موردی پیدا نشد',
  emptyDescription,
  sort,
  onSortChange,
  pagination,
  rowHref,
}: DataTableProps<T>) {
  const toggleSort = (key: string) => {
    onSortChange?.({ key, direction: sort?.key === key && sort.direction === 'asc' ? 'desc' : 'asc' });
  };
  const showRows = !loading && !error && rows.length > 0;

  return (
    <div className="overflow-hidden rounded-xl border border-line bg-white">
      <div role="region" aria-label={caption} tabIndex={0} className="max-md:overflow-x-auto outline-none focus-visible:ring-2 focus-visible:ring-gold">
        <table className="w-full min-w-max border-collapse text-sm md:min-w-full" aria-busy={loading || undefined}>
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  aria-sort={column.sortable ? ariaSort(sort, column.key) : undefined}
                  className={cn('sticky top-0 z-10 border-b border-line bg-cool px-4 py-3 font-semibold text-ink', alignClass[column.align ?? 'start'])}
                >
                  {column.sortable ? (
                    <button
                      type="button"
                      onClick={() => toggleSort(column.key)}
                      className="inline-flex items-center gap-1.5 rounded font-semibold outline-none focus-visible:ring-2 focus-visible:ring-gold"
                    >
                      {column.header}
                      {sort?.key !== column.key ? (
                        <ChevronsUpDown aria-hidden="true" size={14} className="text-muted" />
                      ) : sort.direction === 'asc' ? (
                        <ChevronUp aria-hidden="true" size={14} />
                      ) : (
                        <ChevronDown aria-hidden="true" size={14} />
                      )}
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading
              ? Array.from({ length: skeletonRows }, (_, row) => (
                  <tr key={row} aria-hidden="true" className="border-b border-line last:border-b-0">
                    {columns.map((column) => (
                      <td key={column.key} className="px-4 py-3">
                        <div className="h-4 w-3/4 rounded bg-cool motion-safe:animate-pulse" />
                      </td>
                    ))}
                  </tr>
                ))
              : null}
            {!loading && error ? (
              <tr>
                <td colSpan={columns.length} className="p-4">
                  <ErrorState message={error} {...(onRetry ? { onRetry } : {})} />
                </td>
              </tr>
            ) : null}
            {!loading && !error && rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="p-4">
                  <EmptyState title={emptyTitle}>{emptyDescription}</EmptyState>
                </td>
              </tr>
            ) : null}
            {showRows
              ? rows.map((row) => {
                  const href = rowHref?.(row);
                  return (
                    <tr key={getRowKey(row)} className={cn('relative border-b border-line last:border-b-0', href && 'hover:bg-cool/60')}>
                      {columns.map((column, index) => {
                        const content = column.cell(row);
                        return (
                          <td key={column.key} className={cn('px-4 py-3 text-ink', alignClass[column.align ?? 'start'], column.className)}>
                            {href && index === 0 ? (
                              <Link to={href} className="font-medium text-teal-deep outline-none after:absolute after:inset-0 focus-visible:after:ring-2 focus-visible:after:ring-gold">
                                {content}
                              </Link>
                            ) : (
                              content
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  );
                })
              : null}
          </tbody>
        </table>
      </div>
      {loading ? (
        <p role="status" className="sr-only">
          در حال بارگذاری…
        </p>
      ) : null}
      {pagination ? <Pagination pagination={pagination} /> : null}
    </div>
  );
}
