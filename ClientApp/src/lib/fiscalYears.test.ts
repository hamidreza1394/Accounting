import { describe, expect, it } from 'vitest';
import type { CompanyChoice, SelectedContext } from '../api/types';
import { company, period, selected } from '../test/api';
import {
  companiesWithoutEnterablePeriod,
  formatCount,
  groupFiscalYears,
  initialSelection,
  selectedEntry,
  selectYear,
} from './fiscalYears';

const y1405 = { startDate: '2026-03-21', endDate: '2027-03-20' };
const y1404 = { startDate: '2025-03-21', endDate: '2026-03-20' };
const archived = { status: 'Archived', isSelectable: false, accessMode: null };

const choices = (...items: unknown[]) => items as CompanyChoice[];

describe('groupFiscalYears', () => {
  it('groups periods with the same dates across companies and puts the newest year first', () => {
    const years = groupFiscalYears(
      choices(
        company('a', 'الف', [period('a4', 'سال ۱۴۰۴', y1404), period('a5', 'سال ۱۴۰۵', y1405)]),
        company('b', 'ب', [period('b5', 'سال ۱۴۰۵', y1405)]),
      ),
    );

    expect(years.map((year) => year.key)).toEqual(['2026-03-21|2027-03-20', '2025-03-21|2026-03-20']);
    expect(years[0]?.companyCount).toBe(2);
    expect(years[1]?.companyCount).toBe(1);
  });

  it('uses the shared title, or a date-range title when the titles differ', () => {
    const years = groupFiscalYears(
      choices(
        company('a', 'الف', [period('a5', 'سال مالی ۱۴۰۵', y1405), period('a4', 'سال مالی ۱۴۰۴', y1404)]),
        company('b', 'ب', [period('b5', 'دوره دیگر', y1405), period('b4', 'سال مالی ۱۴۰۴', y1404)]),
      ),
    );

    expect(years[0]?.title).toBe('دوره ۱۴۰۵/۰۱/۰۱ تا ۱۴۰۵/۱۲/۲۹');
    expect(years[1]?.title).toBe('سال مالی ۱۴۰۴');
  });

  it('marks a year without any enterable entry as not enterable and counts real numbers', () => {
    const years = groupFiscalYears(
      choices(
        company('a', 'الف', [period('a4', 'سال ۱۴۰۴', { ...y1404, ...archived })]),
        company('b', 'ب', [period('b4', 'سال ۱۴۰۴', { ...y1404, ...archived }), period('b5', 'سال ۱۴۰۵', y1405)]),
      ),
    );

    const old = years.find((year) => year.key === '2025-03-21|2026-03-20');
    expect(old?.isEnterable).toBe(false);
    expect(old?.companyCount).toBe(2);
    expect(old?.enterableCount).toBe(0);
    expect(years[0]?.isEnterable).toBe(true);
    expect(years[0]?.enterableCount).toBe(1);
  });

  it('keeps same-named companies apart by id and sorts names with Persian collation', () => {
    const years = groupFiscalYears(
      choices(
        company('z', 'یاس', [period('z1', 'دوره', y1405)]),
        company('b', 'آریا', [period('b1', 'دوره', y1405)], '111'),
        company('c', 'آریا', [period('c1', 'دوره', y1405)], '222'),
        company('d', 'بهار', [period('d1', 'دوره', y1405)]),
      ),
    );

    const entries = years[0]?.entries ?? [];
    expect(entries.map((entry) => entry.company.name)).toEqual(['آریا', 'آریا', 'بهار', 'یاس']);
    expect(entries.filter((entry) => entry.company.name === 'آریا').map((entry) => entry.company.nationalId).sort()).toEqual(['111', '222']);
  });

  it('prefers an enterable period when a company has two periods with identical dates', () => {
    const years = groupFiscalYears(
      choices(company('a', 'الف', [period('x', 'قدیمی', { ...y1405, ...archived }), period('y', 'جدید', y1405)])),
    );

    expect(years[0]?.entries).toHaveLength(1);
    expect(years[0]?.entries[0]?.period.id).toBe('y');
  });

  it('returns nothing for no companies', () => {
    expect(groupFiscalYears([])).toEqual([]);
  });
});

describe('companiesWithoutEnterablePeriod', () => {
  it('lists companies with no period or only unusable ones', () => {
    const list = companiesWithoutEnterablePeriod(
      choices(
        company('a', 'الف', [period('a1', 'دوره', archived)]),
        company('b', 'ب', []),
        company('c', 'ج', [period('c1', 'دوره')]),
      ),
    );

    expect(list.map((item) => item.id)).toEqual(['a', 'b']);
  });
});

describe('selection', () => {
  const a = company('a', 'الف', [period('a5', 'سال ۱۴۰۵', y1405), period('a4', 'سال ۱۴۰۴', y1404)]);
  const b = company('b', 'ب', [period('b5', 'سال ۱۴۰۵', y1405), period('b4', 'سال ۱۴۰۴', { ...y1404, ...archived })]);
  const c = company('c', 'ج', [period('c4', 'سال ۱۴۰۴', y1404)]);

  it('preselects the only enterable year, and its only enterable company', () => {
    const years = groupFiscalYears(choices(company('a', 'الف', [period('a5', 'سال ۱۴۰۵', y1405)])));

    expect(initialSelection(years, null)).toEqual({ yearKey: '2026-03-21|2027-03-20', companyId: 'a' });
  });

  it('preselects the newest open year when several years are available, without guessing a company', () => {
    const years = groupFiscalYears(choices(a, b));

    expect(initialSelection(years, null)).toEqual({ yearKey: '2026-03-21|2027-03-20', companyId: null });
  });

  it('prefers the newest open year over a newer closed-only year', () => {
    const years = groupFiscalYears(
      choices(
        company('closed', 'شرکت بسته', [period('closed-1', 'سال بسته', { ...y1405, status: 'Closed', isSelectable: true, accessMode: 'readOnly' })]),
        company('open', 'شرکت باز', [period('open-1', 'سال باز', y1404)]),
      ),
    );

    expect(initialSelection(years, null)).toEqual({ yearKey: '2025-03-21|2026-03-20', companyId: 'open' });
  });

  it('preselects the saved default company when it is enterable in the newest open year', () => {
    const years = groupFiscalYears(choices(a, b));

    expect(initialSelection(years, null, 'b')).toEqual({ yearKey: '2026-03-21|2027-03-20', companyId: 'b' });
  });

  it('ignores a saved default company when it cannot be entered in the selected year', () => {
    const years = groupFiscalYears(choices(a, b));

    expect(initialSelection(years, null, 'missing')).toEqual({ yearKey: '2026-03-21|2027-03-20', companyId: null });
  });

  it('preserves an explicit current company over the saved default during context switching', () => {
    const years = groupFiscalYears(choices(a, b));
    const current = selected(a, a.fiscalPeriods[0]) as SelectedContext;

    expect(initialSelection(years, current, 'b')).toEqual({ yearKey: '2026-03-21|2027-03-20', companyId: 'a' });
  });

  it('preselects the year but not the company when the only year has several enterable companies', () => {
    const years = groupFiscalYears(choices(company('a', 'الف', [period('a5', 'x', y1405)]), company('b', 'ب', [period('b5', 'x', y1405)])));

    expect(initialSelection(years, null)).toEqual({ yearKey: '2026-03-21|2027-03-20', companyId: null });
  });

  it('starts at the current choice when the user came to change it', () => {
    const years = groupFiscalYears(choices(a, b));
    const current = selected(a, a.fiscalPeriods[1]) as SelectedContext;

    expect(initialSelection(years, current)).toEqual({ yearKey: '2025-03-21|2026-03-20', companyId: 'a' });
  });

  it('keeps the company when the new year allows it, clears it otherwise', () => {
    const years = groupFiscalYears(choices(a, b, c));
    const key5 = '2026-03-21|2027-03-20';
    const key4 = '2025-03-21|2026-03-20';

    expect(selectYear(years, { yearKey: key5, companyId: 'a' }, key4)).toEqual({ yearKey: key4, companyId: 'a' });
    // b is archived in 1404, so it is cleared; a and c are enterable there, so nothing is picked.
    expect(selectYear(years, { yearKey: key5, companyId: 'b' }, key4)).toEqual({ yearKey: key4, companyId: null });
  });

  it('pre-selects the only enterable company of the new year', () => {
    const years = groupFiscalYears(choices(a, b, c));
    const key4 = '2025-03-21|2026-03-20';
    const only = groupFiscalYears(choices(company('a', 'الف', [period('a5', 'x', y1405), period('a4', 'x', y1404)]), company('b', 'ب', [period('b5', 'x', y1405)])));

    expect(selectYear(only, { yearKey: '2026-03-21|2027-03-20', companyId: 'b' }, key4)).toEqual({ yearKey: key4, companyId: 'a' });
    expect(selectYear(years, { yearKey: null, companyId: null }, key4).yearKey).toBe(key4);
  });

  it('ignores a year that cannot be entered', () => {
    const years = groupFiscalYears(choices(company('a', 'الف', [period('a4', 'x', { ...y1404, ...archived }), period('a5', 'x', y1405)])));
    const previous = { yearKey: '2026-03-21|2027-03-20', companyId: 'a' };

    expect(selectYear(years, previous, '2025-03-21|2026-03-20')).toBe(previous);
  });

  it('resolves the entry of a selection only when it can be entered', () => {
    const years = groupFiscalYears(choices(a, b));

    expect(selectedEntry(years, { yearKey: '2026-03-21|2027-03-20', companyId: 'b' })?.period.id).toBe('b5');
    expect(selectedEntry(years, { yearKey: '2025-03-21|2026-03-20', companyId: 'b' })).toBeUndefined();
    expect(selectedEntry(years, { yearKey: null, companyId: 'a' })).toBeUndefined();
  });
});

describe('formatCount', () => {
  it('uses Persian digits', () => {
    expect(formatCount(12)).toBe('۱۲');
  });
});

describe('year status', () => {
  const closed = { status: 'Closed', accessMode: 'readOnly' };
  const statusOf = (...periods: unknown[]) =>
    groupFiscalYears(choices(...periods.map((item, index) => company(`c${index}`, `ش${index}`, [item])))) [0]?.status;

  it('is open when any enterable period is open, closed when the enterable ones are closed', () => {
    expect(statusOf(period('a', 'س', y1405), period('b', 'س', { ...y1405, ...closed }))).toBe('Open');
    expect(statusOf(period('a', 'س', { ...y1405, ...closed }), period('b', 'س', { ...y1405, ...archived }))).toBe('Closed');
  });

  it('keeps the archived status of a year nobody can enter', () => {
    expect(statusOf(period('a', 'س', { ...y1405, ...archived }))).toBe('Archived');
  });
});
