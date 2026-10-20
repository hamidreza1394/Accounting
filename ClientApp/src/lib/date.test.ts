import { describe, expect, it } from 'vitest';
import { formatPersianDate } from './date';

describe('formatPersianDate', () => {
  it('converts a stored Gregorian calendar date to the Persian calendar', () => {
    expect(formatPersianDate('2026-03-21')).toBe('۱۴۰۵/۰۱/۰۱');
    expect(formatPersianDate('2026-03-20')).toBe('۱۴۰۴/۱۲/۲۹');
  });

  it('does not shift the day with the time zone of the browser', () => {
    // A date is a calendar date, not an instant; midnight UTC is formatted in UTC.
    expect(formatPersianDate('2025-12-31')).toBe('۱۴۰۴/۱۰/۱۰');
  });

  it('rejects values that are not ISO calendar dates', () => {
    expect(() => formatPersianDate('1405/01/01')).toThrow(RangeError);
    expect(() => formatPersianDate('2026-02-30')).toThrow(RangeError);
  });
});
