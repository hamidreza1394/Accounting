import type { CompanyChoice, FiscalPeriodChoice, SelectedContext } from '../api/types';
import { formatPersianDate } from './date';

// The selection screen is organised by fiscal year first (the year decides which companies are
// offered). The server has no "fiscal year" entity: a year here is only the set of periods,
// across the user's companies, that cover exactly the same dates. It is derived from the
// choices the server already returned; nothing is guessed from the system clock.

/** One company's period inside a fiscal year. */
export type YearEntry = {
  company: CompanyChoice;
  period: FiscalPeriodChoice;
};

export type FiscalYear = {
  /** `startDate|endDate` of the shared dates. */
  key: string;
  title: string;
  startDate: string;
  endDate: string;
  /** One entry per company, sorted by company name (Persian collation). */
  entries: YearEntry[];
  companyCount: number;
  enterableCount: number;
  /** True when at least one company can be entered in this year. */
  isEnterable: boolean;
};

export function yearKey(period: { startDate: string; endDate: string }): string {
  return `${period.startDate}|${period.endDate}`;
}

const collator = new Intl.Collator('fa');

/** A number with Persian digits, for counts shown to the user. */
export function formatCount(value: number): string {
  return new Intl.NumberFormat('fa-IR', { useGrouping: false }).format(value);
}

function titleOf(startDate: string, endDate: string, periods: FiscalPeriodChoice[]): string {
  const first = periods[0]?.title ?? '';
  if (first !== '' && periods.every((period) => period.title === first)) {
    return first;
  }
  return `دوره ${formatPersianDate(startDate)} تا ${formatPersianDate(endDate)}`;
}

/** Groups the periods of all companies by their dates; the newest year comes first. */
export function groupFiscalYears(companies: readonly CompanyChoice[]): FiscalYear[] {
  const groups = new Map<string, { startDate: string; endDate: string; entries: Map<string, YearEntry> }>();

  for (const company of companies) {
    for (const period of company.fiscalPeriods) {
      const key = yearKey(period);
      let group = groups.get(key);
      if (!group) {
        group = { startDate: period.startDate, endDate: period.endDate, entries: new Map() };
        groups.set(key, group);
      }
      // A company appears once per year; if it has two periods with identical dates, an
      // enterable one wins so the company is not hidden by an unusable duplicate.
      const existing = group.entries.get(company.id);
      if (!existing || (!existing.period.isSelectable && period.isSelectable)) {
        group.entries.set(company.id, { company, period });
      }
    }
  }

  const years = [...groups.entries()].map(([key, group]): FiscalYear => {
    const entries = [...group.entries.values()].sort((a, b) => collator.compare(a.company.name, b.company.name));
    const enterableCount = entries.filter((entry) => entry.period.isSelectable).length;
    return {
      key,
      title: titleOf(group.startDate, group.endDate, entries.map((entry) => entry.period)),
      startDate: group.startDate,
      endDate: group.endDate,
      entries,
      companyCount: entries.length,
      enterableCount,
      isEnterable: enterableCount > 0,
    };
  });

  return years.sort((a, b) => b.startDate.localeCompare(a.startDate) || b.endDate.localeCompare(a.endDate));
}

/** Companies that cannot be entered in any year (no period, or only unusable ones). */
export function companiesWithoutEnterablePeriod(companies: readonly CompanyChoice[]): CompanyChoice[] {
  return companies
    .filter((company) => !company.fiscalPeriods.some((period) => period.isSelectable))
    .sort((a, b) => collator.compare(a.name, b.name));
}

export type Selection = { yearKey: string | null; companyId: string | null };

function singleEnterableCompany(year: FiscalYear | undefined): string | null {
  const enterable = year?.entries.filter((entry) => entry.period.isSelectable) ?? [];
  return enterable.length === 1 ? (enterable[0]?.company.id ?? null) : null;
}

function isEnterableIn(year: FiscalYear | undefined, companyId: string | null): boolean {
  return Boolean(year?.entries.some((entry) => entry.company.id === companyId && entry.period.isSelectable));
}

/**
 * The selection to start with, only when there is no ambiguity: the single enterable year;
 * otherwise the year of the current choice (the user came to change it).
 */
export function initialSelection(years: readonly FiscalYear[], current: SelectedContext | null): Selection {
  const enterableYears = years.filter((year) => year.isEnterable);
  const currentYear = current ? years.find((year) => year.key === yearKey(current.fiscalPeriod)) : undefined;

  const year = enterableYears.length === 1 ? enterableYears[0] : currentYear?.isEnterable ? currentYear : undefined;
  if (!year) {
    return { yearKey: null, companyId: null };
  }
  const companyId = isEnterableIn(year, current?.company.id ?? null)
    ? (current?.company.id ?? null)
    : singleEnterableCompany(year);
  return { yearKey: year.key, companyId };
}

/**
 * Changing the year keeps the company only if it can be entered in the new year; otherwise it
 * is cleared (or pre-selected when it is the only enterable one).
 */
export function selectYear(years: readonly FiscalYear[], previous: Selection, nextKey: string): Selection {
  const year = years.find((item) => item.key === nextKey);
  if (!year?.isEnterable) {
    return previous;
  }
  const companyId = isEnterableIn(year, previous.companyId) ? previous.companyId : singleEnterableCompany(year);
  return { yearKey: year.key, companyId };
}

/** The entry that the selection points at, only if it can be entered. */
export function selectedEntry(years: readonly FiscalYear[], selection: Selection): YearEntry | undefined {
  const year = years.find((item) => item.key === selection.yearKey);
  return year?.entries.find((entry) => entry.company.id === selection.companyId && entry.period.isSelectable);
}
