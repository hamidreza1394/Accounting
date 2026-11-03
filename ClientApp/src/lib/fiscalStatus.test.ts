import { describe, expect, it } from 'vitest';
import type { FiscalPeriodStatus } from '../api/types';
import { blockedPeriodLabel, periodPillClass, periodStatusLabel, statusDotClass, yearBadgeClass, yearStatusLabel } from './fiscalStatus';

const statuses: FiscalPeriodStatus[] = ['Open', 'Closed', 'Archived'];

describe('fiscalStatus', () => {
  it('names every status in each context with the established wording', () => {
    expect(yearStatusLabel).toEqual({ Open: 'فعال', Closed: 'بسته شده', Archived: 'بایگانی' });
    expect(periodStatusLabel).toEqual({ Open: 'باز', Closed: 'بسته', Archived: 'بایگانی' });
    expect(blockedPeriodLabel).toEqual({ Open: 'باز', Closed: 'بسته', Archived: 'بایگانی‌شده' });
  });

  it.each(statuses)('has non-empty labels and styles for %s', (status) => {
    for (const table of [yearStatusLabel, periodStatusLabel, blockedPeriodLabel, yearBadgeClass, periodPillClass, statusDotClass]) {
      expect(table[status].length).toBeGreaterThan(0);
    }
  });

  it('uses design tokens only (no raw hex or default palette classes)', () => {
    for (const table of [yearBadgeClass, periodPillClass, statusDotClass]) {
      for (const status of statuses) {
        expect(table[status]).not.toMatch(/#[0-9a-f]{3,8}|\b(slate|emerald|rose|amber|sky|blue|gray)-\d/i);
      }
    }
  });
});
