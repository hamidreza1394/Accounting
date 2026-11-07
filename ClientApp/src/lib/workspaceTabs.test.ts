import { describe, expect, it } from 'vitest';
import { homePath, initialTabsState, tabLabel, tabsReducer } from './workspaceTabs';

const start = (pathname = '/', search = '', title = 'داشبورد') => initialTabsState(pathname, search, title);

describe('workspaceTabs model (in-memory, no storage)', () => {
  it('starts with exactly the tab of the current page (refresh behaviour)', () => {
    const state = start('/admin/users', '?q=sara', 'مدیریت کاربران');
    expect(state.tabs).toHaveLength(1);
    expect(state.tabs[0]).toMatchObject({ key: '/admin/users', path: '/admin/users?q=sara', title: 'مدیریت کاربران' });
    expect(state.activeKey).toBe('/admin/users');
  });

  it('opens a different page as a new tab and activates it', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/admin/users', search: '', title: 'مدیریت کاربران' });
    state = tabsReducer(state, { type: 'open', pathname: '/hr/org/units', search: '', title: 'واحدهای سازمانی' });
    expect(state.tabs.map((tab) => tab.key)).toEqual(['/', '/admin/users', '/hr/org/units']);
    expect(state.activeKey).toBe('/hr/org/units');
    expect(state.previousActiveKey).toBe('/admin/users');
  });

  it('never duplicates a tab for the same page and just updates its query', () => {
    let state = start('/admin/users', '?page=1');
    state = tabsReducer(state, { type: 'open', pathname: '/admin/users', search: '?page=2', title: 'مدیریت کاربران' });
    expect(state.tabs).toHaveLength(1);
    expect(state.tabs[0]?.path).toBe('/admin/users?page=2');
  });

  it('treats two records of a parameterised route as two tabs', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/admin/users/u-1', search: '', title: 'جزئیات کاربر' });
    state = tabsReducer(state, { type: 'open', pathname: '/admin/users/u-2', search: '', title: 'جزئیات کاربر' });
    expect(state.tabs.map((tab) => tab.key)).toEqual(['/', '/admin/users/u-1', '/admin/users/u-2']);
    expect(state.tabs.every((tab, index) => state.tabs.findIndex((other) => other.key === tab.key) === index)).toBe(true);
  });

  it('re-activates an open tab and keeps its query', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/admin/users', search: '?q=sara&status=active&page=2', title: 'مدیریت کاربران' });
    state = tabsReducer(state, { type: 'open', pathname: '/hr/org/units', search: '', title: 'واحدهای سازمانی' });
    state = tabsReducer(state, { type: 'open', pathname: '/admin/users', search: '?q=sara&status=active&page=2', title: 'مدیریت کاربران' });
    expect(state.tabs).toHaveLength(3);
    expect(state.activeKey).toBe('/admin/users');
    expect(state.tabs.find((tab) => tab.key === '/admin/users')?.path).toBe('/admin/users?q=sara&status=active&page=2');
  });

  it('activates the previously active tab when the active one closes', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/a', search: '', title: 'الف' });
    state = tabsReducer(state, { type: 'open', pathname: '/b', search: '', title: 'ب' });
    state = tabsReducer(state, { type: 'close', key: '/b' });
    expect(state.activeKey).toBe('/a');
    expect(state.tabs.map((tab) => tab.key)).toEqual(['/', '/a']);
  });

  it('activates the right neighbour when the previously active tab is gone', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/a', search: '', title: 'الف' });
    state = tabsReducer(state, { type: 'open', pathname: '/b', search: '', title: 'ب' });
    // Close the middle tab (`/a`): the next open tab (`/b`) takes over, not the dashboard.
    state = tabsReducer(state, { type: 'close', key: '/a' });
    expect(state.activeKey).toBe('/b');
  });

  it('falls back to the last tab when the closed one was the right-most active', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/a', search: '', title: 'الف' });
    state = tabsReducer(state, { type: 'open', pathname: '/b', search: '', title: 'ب' });
    state = tabsReducer(state, { type: 'open', pathname: '/c', search: '', title: 'ج' });
    // `/` was active before `/c`? No: previous is `/b`; close `/b` first to clear it.
    state = tabsReducer(state, { type: 'close', key: '/b' });
    state = tabsReducer(state, { type: 'open', pathname: '/d', search: '', title: 'د' });
    state = tabsReducer(state, { type: 'close', key: '/d' });
    expect(state.tabs.map((tab) => tab.key)).toEqual(['/', '/a', '/c']);
    expect(state.activeKey).toBe('/c');
  });

  it('ends with no tab and hands control back to the caller (which navigates home)', () => {
    let state = start();
    state = tabsReducer(state, { type: 'close', key: '/' });
    expect(state.tabs).toHaveLength(0);
    expect(state.activeKey).toBeNull();
    expect(homePath).toBe('/');
  });

  it('keeps the closed-tab model untouched when a background tab closes', () => {
    let state = start();
    state = tabsReducer(state, { type: 'open', pathname: '/a', search: '', title: 'الف' });
    state = tabsReducer(state, { type: 'close', key: '/' });
    expect(state.activeKey).toBe('/a');
    expect(state.tabs.map((tab) => tab.key)).toEqual(['/a']);
  });

  it('records the page-declared title and lets the label prefer it over the route title', () => {
    let state = start('/admin/users/u-1', '', 'جزئیات کاربر');
    state = tabsReducer(state, { type: 'set-declared-title', key: '/admin/users/u-1', title: 'نام2 خانوادگی2' });
    const tab = state.tabs[0]!;
    expect(tabLabel(tab)).toBe('نام2 خانوادگی2');
    state = tabsReducer(state, { type: 'set-declared-title', key: '/admin/users/u-1', title: null });
    expect(tabLabel(state.tabs[0]!)).toBe('جزئیات کاربر');
  });

  it('ignores an activate call for an unknown tab', () => {
    const state = start();
    expect(tabsReducer(state, { type: 'activate', key: '/nope' })).toBe(state);
  });
});
