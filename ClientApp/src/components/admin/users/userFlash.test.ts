import { afterEach, describe, expect, it, vi } from 'vitest';
import { clearUserFlash, peekUserFlash, setUserFlash, userFlashText, userFlashTtlMs, userFlashWarnings } from './userFlash';

describe('userFlash', () => {
  afterEach(() => {
    clearUserFlash();
    vi.restoreAllMocks();
  });

  it('is for one user, does not vanish when read, and can be cleared', () => {
    setUserFlash('a', 'created');
    expect(peekUserFlash('a')).toBe('created');
    expect(peekUserFlash('a')).toBe('created');
    expect(peekUserFlash('b')).toBeNull();
    clearUserFlash();
    expect(peekUserFlash('a')).toBeNull();
  });

  it('expires when nobody consumed it', () => {
    const now = vi.spyOn(Date, 'now').mockReturnValue(1_000_000);
    setUserFlash('a', 'updated');
    now.mockReturnValue(1_000_000 + userFlashTtlMs - 1);
    expect(peekUserFlash('a')).toBe('updated');
    now.mockReturnValue(1_000_000 + userFlashTtlMs + 1);
    expect(peekUserFlash('a')).toBeNull();
  });

  it('has a Persian text for every kind and marks only the half-finished creations as warnings', () => {
    for (const text of Object.values(userFlashText)) expect(text).toMatch(/[\u0600-\u06FF]/);
    expect([...userFlashWarnings].sort()).toEqual(['createdEmployeeDuplicate', 'createdEmployeeFailed']);
  });
});
