import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AppShell } from '../AppShell';
import { useDocumentTitle } from '@/lib/router';
import { resetDirtyFormsForTests, useFormGuard } from '@/lib/formGuard';
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

/** A page whose form can be made dirty, wired to the shared guard like the real forms. */
function DirtyFormPage({ label }: { label: string }) {
  const [dirty, setDirty] = useState(false);
  useFormGuard(dirty, 'فرم آزمایشی تغییرات ذخیره‌نشده دارد.');
  return (
    <div>
      <h1>{label}</h1>
      <button type="button" onClick={() => setDirty(true)}>خراب‌کردن فرم</button>
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

const tablist = () => screen.getByRole('tablist', { name: 'تب‌های کاری باز' });
const tabNames = () => within(tablist()).getAllByRole('tab').map((tab) => tab.textContent);

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetDirtyFormsForTests();
});

describe('multi-tab workspace (Issue #444 stage 1, PR 4)', () => {
  it('opens every visited page as a tab and re-activates the existing tab instead of duplicating it', async () => {
    const user = userEvent.setup();
    setup();
    expect(await within(tablist()).findByRole('tab', { selected: true })).toHaveTextContent('داشبورد');

    // Two different pages -> two tabs, the second one active.
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(await within(tablist()).findByRole('tab', { name: /مدیریت کاربران/ })).toHaveAttribute('aria-selected', 'true');
    expect(tabNames()).toEqual(['داشبورد', 'مدیریت کاربران']);

    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(await within(tablist()).findByRole('tab', { name: /واحدهای سازمانی/ })).toHaveAttribute('aria-selected', 'true');
    expect(tabNames()).toEqual(['داشبورد', 'مدیریت کاربران', 'واحدهای سازمانی']);

    // The same page again -> no duplicate tab, the existing one is activated.
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(within(tablist()).getByRole('tab', { name: /مدیریت کاربران/ })).toHaveAttribute('aria-selected', 'true'));
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
    await waitFor(() => expect(within(tablist()).getAllByRole('tab')).toHaveLength(3));
    const tabs = within(tablist()).getAllByRole('tab');
    expect(tabs.map((tab) => tab.textContent)).toEqual(['داشبورد', 'جزئیات کاربر', 'جزئیات کاربر']);
  });

  it('keeps the query string with the tab across switches (filters, search and paging survive)', async () => {
    setup('/app/admin/users?q=sara&status=active&page=2');
    expect(await within(tablist()).findByRole('tab', { selected: true })).toHaveTextContent('مدیریت کاربران');
    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(window.location.pathname).toBe('/app/hr/org/units'));
    // Back to the list tab: its own query comes back (the pane stores the path with the tab).
    window.history.pushState(null, '', '/app/admin/users?q=sara&status=active&page=2');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(window.location.search).toBe('?q=sara&status=active&page=2'));
    expect(within(tablist()).getByRole('tab', { name: /مدیریت کاربران/ })).toHaveAttribute('aria-selected', 'true');
  });

  it('closes a background tab without changing the active one, and the active tab with a sensible successor', async () => {
    setup();
    // dashboard -> users (active) -> units (active), one browser task per navigation
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await within(tablist()).findByRole('tab', { name: /مدیریت کاربران/ });
    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await within(tablist()).findByRole('tab', { name: /واحدهای سازمانی/ });

    // Closing a background tab (داشبورد) keeps the active tab.
    await userEvent.setup().click(screen.getByRole('button', { name: 'بستن داشبورد' }));
    expect(tabNames()).toEqual(['مدیریت کاربران', 'واحدهای سازمانی']);
    expect(within(tablist()).getByRole('tab', { name: /واحدهای سازمانی/ })).toHaveAttribute('aria-selected', 'true');
    expect(window.location.pathname).toBe('/app/hr/org/units');

    // Closing the ACTIVE tab activates the previously active one (users) and follows its URL.
    await userEvent.setup().click(screen.getByRole('button', { name: 'بستن واحدهای سازمانی' }));
    expect(tabNames()).toEqual(['مدیریت کاربران']);
    expect(within(tablist()).getByRole('tab', { name: /مدیریت کاربران/ })).toHaveAttribute('aria-selected', 'true');
    expect(window.location.pathname).toBe('/app/admin/users');

    // Closing the last tab hands the workspace back to the dashboard.
    await userEvent.setup().click(screen.getByRole('button', { name: 'بستن مدیریت کاربران' }));
    expect(await within(tablist()).findByRole('tab', { name: /داشبورد/ })).toHaveAttribute('aria-selected', 'true');
    expect(window.location.pathname).toBe('/app/');
  });

  it('keeps browser back/forward working: each history entry lands on its tab', async () => {
    setup();
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    window.history.pushState(null, '', '/app/hr/org/units');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await within(tablist()).findByRole('tab', { name: /واحدهای سازمانی/ });

    window.history.back();
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(within(tablist()).getByRole('tab', { name: /مدیریت کاربران/ })).toHaveAttribute('aria-selected', 'true'));

    window.history.forward();
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(within(tablist()).getByRole('tab', { name: /واحدهای سازمانی/ })).toHaveAttribute('aria-selected', 'true'));
  });

  it('offers the "new tab" action that opens (or focuses) the dashboard tab', async () => {
    setup('/app/admin/users');
    await within(tablist()).findByRole('tab', { selected: true });
    await userEvent.setup().click(screen.getByRole('button', { name: 'بازکردن تب جدید' }));
    expect(await within(tablist()).findByRole('tab', { name: /داشبورد/ })).toHaveAttribute('aria-selected', 'true');
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
    expect(await within(tablist()).findByRole('tab', { selected: true, name: /سارا کریمی/ })).toBeInTheDocument();
  });

  it('marks every tab panel with the tab id and hides the inactive panes', async () => {
    setup();
    window.history.pushState(null, '', '/app/admin/users');
    window.dispatchEvent(new PopStateEvent('popstate'));
    const usersTab = await within(tablist()).findByRole('tab', { name: /مدیریت کاربران/ });
    const panel = document.getElementById(usersTab.getAttribute('aria-controls')!);
    expect(panel).not.toHaveAttribute('hidden');
    const dashboardPanel = document.getElementById('tabpanel-/')!;
    expect(dashboardPanel).toHaveAttribute('hidden');
  });
});

describe('dirty-form protection in the workspace (Issue #444 stage 1, PR 5)', () => {
  it('asks before closing a tab that holds a dirty form; cancel keeps the tab, confirm closes it', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await within(tablist()).findByRole('tab', { selected: true, name: /فرم آزمایشی/ });

    // Make the form dirty, then try to close its tab.
    await user.click(screen.getByRole('button', { name: 'خراب‌کردن فرم' }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(dialog).toHaveTextContent('فرم آزمایشی تغییرات ذخیره‌نشده دارد.');

    // Cancel: the tab stays open, nothing is lost.
    await user.click(within(dialog).getByRole('button', { name: 'انصراف' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(within(tablist()).getByRole('tab', { name: /فرم آزمایشی/ })).toBeInTheDocument();

    // Confirm: the tab finally closes and the workspace lands on the previous tab.
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    await user.click(await screen.findByRole('button', { name: 'بستن تب و از دست دادن تغییرات' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(within(tablist()).queryByRole('tab', { name: /فرم آزمایشی/ })).not.toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/');
  });

  it('closes a clean tab immediately without any dialog', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await within(tablist()).findByRole('tab', { selected: true, name: /فرم آزمایشی/ });
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    await waitFor(() => expect(within(tablist()).queryByRole('tab', { name: /فرم آزمایشی/ })).not.toBeInTheDocument());
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('keeps the form state while the tab is hidden (still dirty after coming back)', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await within(tablist()).findByRole('tab', { selected: true, name: /فرم آزمایشی/ });
    await user.click(screen.getByRole('button', { name: 'خراب‌کردن فرم' }));

    // Open a second tab, switch away and back: the pane never unmounted, still dirty.
    await user.click(screen.getByRole('button', { name: 'بازکردن تب جدید' }));
    await waitFor(() => expect(within(tablist()).getByRole('tab', { name: /داشبورد/ })).toHaveAttribute('aria-selected', 'true'));
    await user.click(within(tablist()).getByRole('tab', { name: /فرم آزمایشی/ }));
    await user.click(screen.getByRole('button', { name: 'بستن فرم آزمایشی' }));
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument();
  });
});

  it('opens a tab from a real sidebar navigation click (menu and tabs share one router)', async () => {
    // The shell sidebar links (admin group, open for the installation administrator) navigate
    // through the same router the workspace listens to.
    window.history.pushState(null, '', '/app/');
    const shell = render(
      <AppShell session={session} context={context} access={access} routes={routes} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
        <main>جایگزین</main>
      </AppShell>,
    );
    const user = userEvent.setup();
    const nav = await screen.findByRole('navigation', { name: 'منوی اصلی' });
    await user.click(within(nav).getByRole('link', { name: 'مدیریت کاربران' }));
    expect(await within(tablist()).findByRole('tab', { selected: true, name: /مدیریت کاربران/ })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users');
    shell.unmount();
  });
