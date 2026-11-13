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
import { listParties } from '../api/tradeClient';
import { partyRoles, type PartyListItem, type PartyRole } from '../api/tradeTypes';
import { PartyRoleBadges } from '../components/PartyRoleBadges';
import { TradeForbidden, TradeModuleUnavailable } from '../components/TradeStates';
import { useCountries } from '../components/useCountries';
import { isModuleBlocked, mapTradeFailure, type TradeFailure } from '../lib/tradeErrors';
import { partyRoleLabel, partyTitle } from '../lib/tradeLabels';
import { usePartyListVersion } from '../lib/partyListRefresh';

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
 * The party list (`GET /trade/parties`): server-side search (code, legal and display name), role and status
 * filters and paging. The server orders by code, so there is no client sorting. A row opens the details.
 * The legal identifier is never part of the list.
 */
export function PartiesPage({ onUnauthenticated, searchDelayMs = defaultSearchDelayMs }: Props) {
  const access = useTradeAccess();
  const canCreate = access.can('party.create');
  const countries = useCountries(onUnauthenticated);
  const listVersion = usePartyListVersion();

  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [role, setRole] = useState<'all' | PartyRole>('all');
  const [status, setStatus] = useState<StatusFilter>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(20);
  const [data, setData] = useState<{ items: readonly PartyListItem[]; totalCount: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<TradeFailure | null>(null);
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
    listParties(
      {
        ...(search === '' ? {} : { search }),
        ...(role === 'all' ? {} : { role }),
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
        setFailure(mapTradeFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [search, role, status, page, pageSize, attempt, listVersion, onUnauthenticated]);

  const countryNames = useMemo(() => new Map(countries.countries.map((country) => [country.code, country.nameFa])), [countries.countries]);

  const columns = useMemo<readonly DataTableColumn<PartyListItem>[]>(
    () => [
      {
        key: 'name',
        header: 'طرف‌حساب',
        cell: (party) => (
          <span className="flex flex-col">
            <span className="font-medium">{partyTitle(party)}</span>
            {party.displayName && party.displayName !== party.legalName ? <span className="text-xs text-sub">{party.legalName}</span> : null}
          </span>
        ),
      },
      {
        key: 'code',
        header: 'کد',
        cell: (party) => (
          <bdi dir="ltr" className="whitespace-nowrap font-mono text-xs">
            {party.code}
          </bdi>
        ),
      },
      {
        key: 'country',
        header: 'کشور',
        cell: (party) =>
          party.countryCode ? (
            <span className="flex items-center gap-2">
              <bdi dir="ltr" className="font-mono text-xs text-sub">
                {party.countryCode}
              </bdi>
              {countryNames.get(party.countryCode) ?? null}
            </span>
          ) : (
            '—'
          ),
      },
      { key: 'roles', header: 'نقش‌ها', cell: (party) => <PartyRoleBadges roles={party.roles} /> },
      { key: 'status', header: 'وضعیت', cell: (party) => <StatusBadge status={party.isActive ? 'active' : 'inactive'} /> },
    ],
    [countryNames],
  );

  usePageBreadcrumb([{ label: 'بازرگانی خارجی' }, { label: 'طرف‌حساب‌ها' }]);

  const filtered = search !== '' || role !== 'all' || status !== 'all';

  if (isModuleBlocked(failure)) return <TradeModuleUnavailable reason={failure?.kind === 'context-required' ? 'context' : 'module'} />;
  if (failure?.kind === 'forbidden') return <TradeForbidden />;

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-title">طرف‌حساب‌ها</h1>
          <p className="mt-1 text-sm text-sub">
            واردکنندگان، فروشندگان، بانک‌ها و سایر طرف‌های معامله در این شرکت. کد هر طرف‌حساب در شرکت یکتاست.
          </p>
        </div>
        {canCreate ? (
          <Link to="/trade/parties/new" className={buttonVariants({ size: 'lg' })}>
            <Plus aria-hidden="true" />
            طرف‌حساب جدید
          </Link>
        ) : null}
      </header>

      <form
        role="search"
        aria-label="جست‌وجو و فیلتر طرف‌حساب‌ها"
        onSubmit={(event) => event.preventDefault()}
        className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2 lg:grid-cols-3"
      >
        <FormField label="جست‌وجو" hint="کد، نام حقوقی یا نام نمایشی">
          <Input value={searchText} maxLength={maxSearchLength} autoComplete="off" onChange={(event) => setSearchText(event.target.value)} />
        </FormField>
        <FormField label="نقش">
          <Select
            value={role}
            onChange={(event) => {
              setRole(event.target.value as 'all' | PartyRole);
              setPage(1);
            }}
          >
            <option value="all">همه نقش‌ها</option>
            {partyRoles.map((value) => (
              <option key={value} value={value}>
                {partyRoleLabel[value]}
              </option>
            ))}
          </Select>
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
        caption="فهرست طرف‌حساب‌ها"
        columns={columns}
        rows={data?.items ?? []}
        getRowKey={(party) => party.id}
        loading={loading}
        error={failure?.message ?? null}
        onRetry={() => setAttempt((value) => value + 1)}
        rowHref={(party) => `/trade/parties/${party.id}`}
        emptyTitle="طرف‌حسابی پیدا نشد"
        emptyDescription={
          filtered ? (
            'با جست‌وجو یا فیلتر فعلی طرف‌حسابی پیدا نشد.'
          ) : canCreate ? (
            <>
              هنوز طرف‌حسابی ثبت نشده است.{' '}
              <Link to="/trade/parties/new" className="font-bold text-teal-deep underline">
                نخستین طرف‌حساب را ثبت کنید
              </Link>
            </>
          ) : (
            'هنوز طرف‌حسابی ثبت نشده است.'
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
