import { useEffect, useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { buttonVariants } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { FormField, Input, Select } from '@/components/ui/FormField';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { Link } from '@/lib/router';
import { isAbortError } from '@/lib/staleGuard';
import { useTradeAccess } from '../access/TradeAccess';
import { listProducts } from '../api/tradeClient';
import type { ProductListItem } from '../api/tradeTypes';
import { TradeForbidden, TradeModuleUnavailable } from '../components/TradeStates';
import { useUnits } from '../components/useUnits';
import { useProductListVersion } from '../lib/productListRefresh';
import type { ProductFieldKey } from '../lib/productValidation';
import { isModuleBlocked, mapProductFailure, type TradeFailure } from '../lib/tradeErrors';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** Debounce of the search box in milliseconds (tests shorten it). */
  searchDelayMs?: number;
};

const pageSizes = [10, 20, 50, 100] as const;
const defaultSearchDelayMs = 350;
const maxSearchLength = 100;
type StatusFilter = 'all' | 'active' | 'inactive';

/**
 * The product list (`GET /trade/products`): server-side search (code, name), status filter and paging. The
 * server orders by code, so there is no client sorting. A row opens the details.
 */
export function ProductsPage({ onUnauthenticated, searchDelayMs = defaultSearchDelayMs }: Props) {
  const access = useTradeAccess();
  const canCreate = access.can('product.create');
  const units = useUnits(onUnauthenticated);
  const listVersion = useProductListVersion();

  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<StatusFilter>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(20);
  const [data, setData] = useState<{ items: readonly ProductListItem[]; totalCount: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<TradeFailure<ProductFieldKey> | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    // Compare the trimmed text, so typing only a space never starts a timer or resets the page.
    const next = searchText.trim();
    if (next === search) return undefined;
    const timer = window.setTimeout(() => {
      setPage(1);
      setSearch(next);
    }, searchDelayMs);
    return () => window.clearTimeout(timer);
  }, [searchText, search, searchDelayMs]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    listProducts(
      {
        ...(search === '' ? {} : { search }),
        ...(status === 'all' ? {} : { isActive: status === 'active' }),
        page,
        pageSize,
      },
      controller.signal,
    )
      .then((result) => {
        if (controller.signal.aborted) return;
        setData({ items: result.items, totalCount: result.totalCount });
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || isAbortError(cause) || onUnauthenticated(cause)) return;
        setFailure(mapProductFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [search, status, page, pageSize, attempt, listVersion, onUnauthenticated]);

  const unitNames = useMemo(() => new Map(units.units.map((unit) => [unit.code, unit.nameFa])), [units.units]);

  const columns = useMemo<readonly DataTableColumn<ProductListItem>[]>(
    () => [
      {
        key: 'code',
        header: 'کد',
        cell: (product) => (
          <bdi dir="ltr" className="whitespace-nowrap font-mono text-xs">
            {product.code}
          </bdi>
        ),
      },
      { key: 'name', header: 'نام کالا', cell: (product) => <span className="font-medium">{product.name}</span> },
      {
        key: 'group',
        header: 'گروه کالا',
        cell: (product) =>
          product.productGroupCode ? (
            <bdi dir="ltr" className="font-mono text-xs">
              {product.productGroupCode}
            </bdi>
          ) : (
            '—'
          ),
      },
      {
        key: 'hs',
        header: 'کد تعرفه',
        cell: (product) =>
          product.hsCode ? (
            <bdi dir="ltr" className="whitespace-nowrap font-mono text-xs">
              {product.hsCode}
            </bdi>
          ) : (
            '—'
          ),
      },
      {
        key: 'unit',
        header: 'واحد پایه',
        cell: (product) => (
          <span className="flex items-center gap-2">
            <bdi dir="ltr" className="font-mono text-xs text-sub">
              {product.baseUnitCode}
            </bdi>
            {unitNames.get(product.baseUnitCode) ?? null}
          </span>
        ),
      },
      { key: 'status', header: 'وضعیت', cell: (product) => <StatusBadge status={product.isActive ? 'active' : 'inactive'} /> },
    ],
    [unitNames],
  );

  usePageBreadcrumb([{ label: 'بازرگانی خارجی' }, { label: 'کالاها' }]);

  const filtered = search !== '' || status !== 'all';

  if (isModuleBlocked(failure)) return <TradeModuleUnavailable reason={failure?.kind === 'context-required' ? 'context' : 'module'} />;
  if (failure?.kind === 'forbidden') return <TradeForbidden />;

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-title">کالاها</h1>
          <p className="mt-1 text-sm text-sub">
            کالاهای قابل معامله در این شرکت با واحد پایه و کد تعرفه. کد هر کالا در شرکت یکتاست.
          </p>
        </div>
        {canCreate ? (
          <Link to="/trade/products/new" className={buttonVariants({ size: 'lg' })}>
            <Plus aria-hidden="true" />
            کالای جدید
          </Link>
        ) : null}
      </header>

      <form
        role="search"
        aria-label="جست‌وجو و فیلتر کالاها"
        onSubmit={(event) => event.preventDefault()}
        className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2"
      >
        <FormField label="جست‌وجو" hint="کد یا نام کالا">
          <Input value={searchText} maxLength={maxSearchLength} autoComplete="off" onChange={(event) => setSearchText(event.target.value)} />
        </FormField>
        <FormField label="وضعیت">
          <Select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as StatusFilter);
              setPage(1);
            }}
          >
            <option value="all">همه</option>
            <option value="active">فعال</option>
            <option value="inactive">غیرفعال</option>
          </Select>
        </FormField>
      </form>

      <DataTable
        caption="فهرست کالاها"
        columns={columns}
        rows={data?.items ?? []}
        getRowKey={(product) => product.id}
        loading={loading}
        error={failure?.message ?? null}
        onRetry={() => setAttempt((value) => value + 1)}
        rowHref={(product) => `/trade/products/${product.id}`}
        emptyTitle="کالایی پیدا نشد"
        emptyDescription={
          filtered ? (
            'با جست‌وجو یا فیلتر فعلی کالایی پیدا نشد.'
          ) : canCreate ? (
            <>
              هنوز کالایی ثبت نشده است.{' '}
              <Link to="/trade/products/new" className="font-bold text-teal-deep underline">
                نخستین کالا را ثبت کنید
              </Link>
            </>
          ) : (
            'هنوز کالایی ثبت نشده است.'
          )
        }
        pagination={{
          page,
          pageSize,
          total: data?.totalCount ?? 0,
          pageSizeOptions: pageSizes,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size);
            setPage(1);
          },
        }}
      />
    </div>
  );
}
