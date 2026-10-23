import { describe, expect, it } from 'vitest';
import { matchesCompany, matchesYear, normalizeSearchText } from './search';

describe('normalizeSearchText', () => {
  it('maps Persian and Arabic digits to Latin digits', () => {
    expect(normalizeSearchText('۱۲۳٤٥٦')).toBe('123456');
  });

  it('unifies Arabic letters, drops invisible marks and collapses spaces', () => {
    expect(normalizeSearchText('  شركت‌ي   كاظمي ')).toBe('شرکت ی کاظمی');
  });
});

describe('matchesCompany', () => {
  it('matches every word against the name or the national id', () => {
    expect(matchesCompany('الف 1010', 'شرکت الف', '10101010101')).toBe(true);
    expect(matchesCompany('الف 9999', 'شرکت الف', '10101010101')).toBe(false);
  });

  it('treats an empty query as everything and a missing id as empty', () => {
    expect(matchesCompany('   ', 'شرکت الف', null)).toBe(true);
    expect(matchesCompany('123', 'شرکت الف', null)).toBe(false);
  });
});

describe('matchesYear', () => {
  it('matches the title and the Jalali dates, ignoring digit style', () => {
    expect(matchesYear('سال ۱۴۰۵', 'سال مالی 1405', '۱۴۰۵/۰۱/۰۱', '۱۴۰۵/۱۲/۲۹')).toBe(true);
    expect(matchesYear('1405/01/01', 'سال مالی', '۱۴۰۵/۰۱/۰۱', '۱۴۰۵/۱۲/۲۹')).toBe(true);
    expect(matchesYear('۱۴۰۴', 'سال مالی ۱۴۰۵', '۱۴۰۵/۰۱/۰۱', '۱۴۰۵/۱۲/۲۹')).toBe(false);
    expect(matchesYear('  ', 'سال', '۱۴۰۵/۰۱/۰۱', '۱۴۰۵/۱۲/۲۹')).toBe(true);
  });
});
