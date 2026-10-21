import { describe, expect, it } from 'vitest';
import { matchesCompany, normalizeSearchText } from './search';

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
