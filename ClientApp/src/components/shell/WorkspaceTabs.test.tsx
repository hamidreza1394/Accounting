import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { openSystemMenu } from '@/test/sidebar';
import { AppShell } from '../AppShell';
import { useDocumentTitle } from '@/lib/router';
import { resetFormsForTests, useFormGuard } from '@/lib/formGuard';
import type { AccessSnapshot } from '@/lib/permissions';
import type { SelectedContext, Session } from '../../api/types';

const session: Session = { username: 'hamid', fullName: 'حمید رضایی', accountKind: 'User', isAdministrator: false };
const access: AccessSnapshot = { status: 'ready', isInstallationAdministrator: true, companyId: 'company-1', companyCodes: [] };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر سیستم' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

function Page({ label }: { label: string }) {
  return <h1>{label}</h1>;
}

/** A page whose form can be driven through the four lifecycle states, wired like the real forms. */
function DirtyFormPage({ label }: { label: string }) {
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useFormGuard({ dirty, saving, errorMessage: error, dirtyMessage: 'فرم آزمایشی تغییرات ذخیره‌نشده دارد.' });
  return (
    <div>
      <h1>{label}</h1>
      <button type="button" onClick={() => setDirty(true)}>خراب‌کردن فرم</button>
      <button type="button" onClick={() => { setSaving(true); }}>شروع ذخیره</button>
      <button type="button" onClick={() => { setSaving(false); setError('ذخیره انجام نشد.'); }}>شکست ذخیره</button>
      <button type="button" onClick={() => { setSaving(false); setError(null); setDirty(false); }}>پاک‌سازی فرم</button>
    </div>
  );
}

const routes = [
  { path: '/', element: () => <Page label="داشبورد" />, title: 'داشبورد' },
  { path: '/admin/users', element: () => <div><h1>فهرست کاربران</h1><a href="#q">نشانه</a></div>, title: 'مدیریت کاربران' },
  { path: '/admin/users/:id', element: () => <Page label="جزئیات کاربر" />, title: 'جزئیات کاربر' },
  { path: '/hr/org/units', element: () => <Page label="واحدهای سازمانی" />, title: 'واحدهای سازمانی' },
  { path: '/form', element: () => <DirtyFormPage label="فرم آزمایشی" />, title: 'فرم آزمایشی' },
] as const;

let view: ReturnType<typeof render> | null = null;
function NamedDetail() {
  useDocumentTitle('سارا کریمی');
  return <Page label="سارا کریمی" />;
}

function setup(at = '/app/') {
  window.history.pushState(null, '', at);
  view = render(
    <AppShell session={session} context={context} access={access} routes={routes} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
      <main>جایگزین</main>
    </AppShell>,
  );
}

const tabbar = () => screen.getByRole('navigation', { name: 'تب‌های کاری باز' });
/** The activation button of one tab (close buttons carry the distinct "بستن …" name). */
function tabButton(name: RegExp | string): HTMLElement {
  const matches = within(tabbar())
    .getAllByRole('button')
    .filter((button) => button.className.includes('faranou-tab-button'))
    .filter((button) => (typeof name === 'string' ? button.textContent?.trim() === name : name.test(button.textContent ?? '')));
  if (matches.length !== 1) throw new Error(`expected exactly one tab button for ${String(name)}, found ${matches.length}`);
  return matches[0]!;
}
const tabNames = () => within(tabbar()).getAllByRole('button').filter((button) => !button.className.includes('faranou-tab-close') && !button.className.includes('faranou-tabbar-new')).map((button) => button.textContent);

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
});

describe('multi-tab workspace (Issue #444 stage 1, PR 4)', () => {
  it('opens every visited page as a tab and re-activates the existing tab instead of duplicating it', async () => {
    const user = userEvent.setup();
    setup();
    expect(await waitFor(() => tabButton('داشبورد'))).toHaveAttribute('aria-current', 'page');

    // Two different pages -> two tabs, the second one active.
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(await waitFor(() => tabButton(/مدیریت کاربران/))).toHaveAttribute('aria-current', 'page');
    expect(tabNames()).toEqual(['داشبورد', 'مدیریت کاربران']);

    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(await waitFor(() => tabButton(/واحدهای سازمانی/))).toHaveAttribute('aria-current', 'page');
    expect(tabNames()).toEqual(['داشبورد', 'مدیریت کاربران', 'واحدهای سازمانی']);

    // The same page again -> no duplicate tab, the existing one is activated.
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(tabButton(/مدیریت کاربران/)).toHaveAttribute('aria-current', 'page'));
    expect(tabNames()).toEqual(['داشبورد', 'مدیریت کاربران', 'واحدهای سازمانی']);
    void user;
  });

  it('gives every record of a parameterised route its own tab', async () => {
    setup();
    for (const id of ['u-1', 'u-2']) {
      // Each navigation gets its own browser task, so the workspace sees both.
      window.history.pushState(null, '', `/app/admin/users/${id}`);
      window.dispatchEvent(new PopStateEvent('popstate'));
      await waitFor(() => expect(window.location.pathname).toBe(`/app/admin/users/${id}`));
    }
    await waitFor(() => expect(tabNames()).toHaveLength(3));
    expect(tabNames()).toEqual(['داشبورد', 'جزئیات کاربر', 'جزئیات کاربر']);
  });

  it('keeps the query string with the tab across switches (filters, search and paging survive)', async () => {
    setup('/app/admin/users?q=sara&status=active&page=2');
    expect(await waitFor(() => tabButton(/مدیریت کاربران/))).toHaveAttribute('aria-current', 'page');
    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(window.location.pathname).toBe('/app/hr/org/units'));
    // Back to the list tab: its own query comes back (the pane stores the path with the tab).
    window.history.pushState(null, '', '/app/admin/users?q=sara&status=active&page=2');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(window.location.search).toBe('?q=sara&status=active&page=2'));
    expect(tabButton(/مدیریت کاربران/)).toHaveAttribute('aria-current', 'page');
  });

  it('closes a background tab without changing the active one, and the active tab with a sensible successor', async () => {
    setup();
    // dashboard -> users (active) -> units (active), one browser task per navigation
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => tabButton(/مدیریت کاربران/));
    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => tabButton(/واحدهای سازمانی/));

    // Closing a background tab (داشبورد) keeps the active tab.
    await userEvent.setup().click(screen.getByRole('button', { name: 'بستن داشبورد' }));
    expect(tabNames()).toEqual(['مدیریت کاربران', 'واحدهای سازمانی']);
    expect(tabButton(/واحدهای سازمانی/)).toHaveAttribute('aria-current', 'page');
    expect(window.location.pathname).toBe('/app/hr/org/units');

    // Closing the ACTIVE tab activates the previously active one (users) and follows its URL.
    await userEvent.setup().click(screen.getByRole('button', { name: 'بستن واحدهای سازمانی' }));
    expect(tabNames()).toEqual(['مدیریت کاربران']);
    expect(tabButton(/مدیریت کاربران/)).toHaveAttribute('aria-current', 'page');
    expect(window.location.pathname).toBe('/app/admin/users');

    // Closing the last tab hands the workspace back to the dashboard.
    await userEvent.setup().click(screen.getByRole('button', { name: 'بستن مدیریت کاربران' }));
    expect(await waitFor(() => tabButton(/داشبورد/))).toHaveAttribute('aria-current', 'page');
    expect(window.location.pathname).toBe('/app/');
  });

  it('keeps browser back/forward working: each history entry lands on its tab', async () => {
    setup();
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => tabButton(/واحدهای سازمانی/));

    window.history.back();
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(tabButton(/مدیریت کاربران/)).toHaveAttribute('aria-current', 'page'));

    window.history.forward();
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(tabButton(/واحدهای سازمانی/)).toHaveAttribute('aria-current', 'page'));
  });

  it('offers the "new tab" action that opens (or focuses) the dashboard tab', async () => {
    setup('/app/admin/users');
    await waitFor(() => tabButton(/مدیریت کاربران/));
    await userEvent.setup().click(screen.getByRole('button', { name: 'بازکردن تب جدید' }));
    expect(await waitFor(() => tabButton(/داشبورد/))).toHaveAttribute('aria-current', 'page');
    expect(window.location.pathname).toBe('/app/');
  });

  it('names the tab after the record once the page declares its title', async () => {
    // The detail page calls `useDocumentTitle('سارا کریمی')`, and the workspace turns that
    // declaration into the tab label (route title stays the fallback).
    const namedRoutes = [
      routes[0],
      routes[1],
      { path: '/admin/users/:id', element: NamedDetail, title: 'جزئیات کاربر' },
    ];
    setup('/app/admin/users/u-1');
    view?.rerender(
      <AppShell session={session} context={context} access={access} routes={namedRoutes} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
        <main>جایگزین</main>
      </AppShell>,
    );
    expect(await waitFor(() => tabButton(/سارا کریمی/))).toHaveAttribute('aria-current', 'page');
  });

  it('marks every tab panel with the tab id and hides the inactive panes', async () => {
    setup();
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => tabButton(/مدیریت کاربران/));
    const activePane = document.querySelector('[data-tab-pane="/admin/users"]');
    expect(activePane).not.toHaveAttribute('hidden');
    const dashboardPane = document.querySelector('[data-tab-pane="/"]')!;
    expect(dashboardPane).toHaveAttribute('hidden');
  });
});

describe('dirty-form protection in the workspace (Issue #444 stage 1, PR 5)', () => {
  it('asks before closing a tab that holds a dirty form; cancel keeps the tab, confirm closes it', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));

    // Make the form dirty, then try to close its tab.
    await user.click(screen.getByRole('button', { name: 'خراب‌کردن فرم' }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(dialog).toHaveTextContent('فرم آزمایشی تغییرات ذخیره‌نشده دارد.');

    // Cancel: the tab stays open, nothing is lost.
    await user.click(within(dialog).getByRole('button', { name: 'انصراف' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(tabButton(/فرم آزمایشی/)).toBeInTheDocument();

    // Confirm: the tab finally closes and the workspace lands on the previous tab.
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    await user.click(await screen.findByRole('button', { name: 'بستن تب و از دست دادن تغییرات' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(within(tabbar()).queryByRole('button', { name: /فرم آزمایشی/ })).not.toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/');
  });

  it('refuses to close a Saving tab outright and lets it close once the save resolves', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));

    await user.click(screen.getByRole('button', { name: 'شروع ذخیره' }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('بستن این تب در حال حاضر ممکن نیست');
    // A refusal: there is no destructive action to confirm.
    expect(within(dialog).queryByRole('button', { name: 'بستن تب و از دست دادن تغییرات' })).not.toBeInTheDocument();

    // The tab stays open while saving.
    await user.click(within(dialog).getByRole('button', { name: 'متوجه شدم' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(tabButton(/فرم آزمایشی/)).toBeInTheDocument();

    // The save resolves cleanly: closing now needs no dialog at all.
    await user.click(screen.getByRole('button', { name: 'پاک‌سازی فرم' }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    await waitFor(() => expect(within(tabbar()).queryByRole('button', { name: /فرم آزمایشی/ })).not.toBeInTheDocument());
  });

  it('refuses to close an Error tab until the failure is resolved', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));

    await user.click(screen.getByRole('button', { name: 'شروع ذخیره' }));
    await user.click(screen.getByRole('button', { name: 'شکست ذخیره' }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('بستن این تب تا رفع خطا ممکن نیست');
    await user.click(within(dialog).getByRole('button', { name: 'متوجه شدم' }));
    expect(tabButton(/فرم آزمایشی/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'پاک‌سازی فرم' }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    await waitFor(() => expect(within(tabbar()).queryByRole('button', { name: /فرم آزمایشی/ })).not.toBeInTheDocument());
  });

  it('closes a clean tab immediately without any dialog', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    await waitFor(() => expect(within(tabbar()).queryByRole('button', { name: /فرم آزمایشی/ })).not.toBeInTheDocument());
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('keeps the form state while the tab is hidden (still dirty after coming back)', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await user.click(screen.getByRole('button', { name: 'خراب‌کردن فرم' }));

    // Open a second tab, switch away and back: the pane never unmounted, still dirty.
    await user.click(screen.getByRole('button', { name: 'بازکردن تب جدید' }));
    await waitFor(() => expect(tabButton(/داشبورد/)).toHaveAttribute('aria-current', 'page'));
    await user.click(tabButton(/فرم آزمایشی/));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument();
  });
});

  it('opens a tab from a real sidebar navigation click (menu and tabs share one router)', async () => {
    // The shell sidebar links (admin group, opened by a click for the installation administrator) navigate
    // through the same router the workspace listens to.
    window.history.pushState(null, '', '/app/');
    const shell = render(
      <AppShell session={session} context={context} access={access} routes={routes} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
        <main>جایگزین</main>
      </AppShell>,
    );
    const user = userEvent.setup();
    const nav = await screen.findByRole('navigation', { name: 'منوی اصلی' });
    await openSystemMenu(user, nav);
    await user.click(within(nav).getByRole('link', { name: 'مدیریت کاربران' }));
    expect(await waitFor(() => tabButton(/مدیریت کاربران/))).toHaveAttribute('aria-current', 'page');
    expect(window.location.pathname).toBe('/app/admin/users');
    shell.unmount();
  });
