import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime } from './format';

describe('format', () => {
  it('formats an ISO calendar date as Jalali without a time-zone shift', () => {
    expect(formatDate('2026-03-21')).toBe('۱۴۰۵/۰۱/۰۱');
  });

  it('formats a date-time in local time using the same Jalali helper', () => {
    const local = new Date(2026, 10, 2, 8, 5).toISOString();
    expect(formatDate(local)).toBe('۱۴۰۵/۰۸/۱۱');
    expect(formatDateTime(local)).toBe('۱۴۰۵/۰۸/۱۱ ۰۸:۰۵');
  });

  it('falls back for empty and invalid values', () => {
    expect(formatDate(null)).toBe('ثبت نشده');
    expect(formatDateTime(undefined)).toBe('ثبت نشده');
    expect(formatDateTime('not-a-date')).toBe('ثبت نشده');
    expect(formatDate('2026-02-31', '—')).toBe('—');
    expect(formatDate('', '—')).toBe('—');
  });
});
