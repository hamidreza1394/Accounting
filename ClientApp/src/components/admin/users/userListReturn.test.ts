import { describe, expect, it } from 'vitest';
import { rememberUserListLocation, resetUserListReturnForTests, userListBackPath } from './userListReturn';

describe('userListReturn (in-memory list location for back navigation)', () => {
  it('remembers the list query and returns it verbatim', () => {
    resetUserListReturnForTests();
    rememberUserListLocation('q=sara&status=active&page=2');
    expect(userListBackPath()).toBe('/admin/users?q=sara&status=active&page=2');
  });

  it('stores the plain list when the query is empty or blank', () => {
    resetUserListReturnForTests();
    rememberUserListLocation('');
    expect(userListBackPath()).toBe('/admin/users');
    rememberUserListLocation('   ');
    expect(userListBackPath()).toBe('/admin/users');
  });

  it('falls back to the plain list before any list visit', () => {
    resetUserListReturnForTests();
    expect(userListBackPath()).toBe('/admin/users');
  });

  it('keeps only the latest visit', () => {
    resetUserListReturnForTests();
    rememberUserListLocation('q=old&page=9');
    rememberUserListLocation('q=new&page=1');
    expect(userListBackPath()).toBe('/admin/users?q=new&page=1');
  });
});
