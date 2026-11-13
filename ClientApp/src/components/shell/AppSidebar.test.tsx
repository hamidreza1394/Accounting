import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AccessSnapshot, Permission } from '@/lib/permissions';
import { RouterProvider } from '@/lib/router';
import { TradeAccessContext, createTradeAccess, type TradeAccess } from '@/features/trade/access/TradeAccess';
import { AppSidebar } from './AppSidebar';

const installationAdmin: AccessSnapshot = {
  status: 'ready',
  isInstallationAdministrator: true,
  companyId: 'company-1',
  companyCodes: [],
};
const companyMember = (codes: string[]): AccessSnapshot => ({
  status: 'ready',
  isInstallationAdministrator: false,
  companyId: 'company-1',
  companyCodes: codes,
});

function setup(access: AccessSnapshot = installationAdmin, extra: { onLogout?: () => void; loggingOut?: boolean } = {}) {
  return render(
    <RouterProvider routes={[]}>
      <AppSidebar
        access={access}
        canAccess={(permission: Permission) =>
          access.status === 'ready' && (access.isInstallationAdministrator ||
            (permission === 'hr.units.view' && access.companyCodes.includes('hr.organizational-units.list')))
        }
        onRetryPermissions={vi.fn()}
        onNavigate={vi.fn()}
        mobileOpen={false}
        {...extra}
      />
    </RouterProvider>,
  );
}

beforeEach(() => {
  window.history.pushState(null, '', '/app/');
});

/** Every group starts folded; opening one is always an explicit click. */
async function openSystem() {
  const user = userEvent.setup();
  const system = screen.getByRole('button', { name: 'مدیریت سیستم' });
  await user.click(system);
  return { user, system };
}

describe('AppSidebar (Issue #444 stage 1 shell)', () => {
  it('groups the catalog subsystems as folded first-level groups with collapsible leaves', async () => {
    const user = userEvent.setup();
    setup();
    for (const group of ['حسابداری', 'انبار', 'فروش', 'خزانه‌داری', 'حقوق و دستمزد', 'دارایی ثابت', 'بازرگانی خارجی', 'عمومی']) {
      expect(screen.getByRole('button', { name: group })).toHaveAttribute('aria-expanded', 'false');
    }
    const accounting = screen.getByRole('button', { name: 'حسابداری' });
    expect(screen.queryByText('اسناد و دفاتر')).not.toBeInTheDocument();
    await user.click(accounting);
    expect(accounting).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('اسناد و دفاتر')).toBeInTheDocument();
    await user.click(accounting);
    expect(screen.queryByText('اسناد و دفاتر')).not.toBeInTheDocument();
  });

  it('keeps the section icon when only a later leaf of the section is permitted', async () => {
    const access = companyMember(['hr.organizational-units.list']);
    const { container } = render(
      <RouterProvider routes={[]}>
        <AppSidebar
          access={access}
          canAccess={(permission: Permission) => permission === 'hr.units.view' && access.companyCodes.includes('hr.organizational-units.list')}
          onRetryPermissions={vi.fn()}
          onNavigate={vi.fn()}
          mobileOpen={false}
        />
      </RouterProvider>,
    );
    await openSystem();
    const toggle = screen.getByRole('button', { name: 'ساختار سازمانی' });
    expect(screen.getByRole('link', { name: 'واحدهای سازمانی' })).toBeInTheDocument();
    expect(screen.getByText('کارکنان').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(toggle.querySelector('svg')).not.toBeNull();
    expect(container).toBeTruthy();
  });

  it('moves access permissions directly below user management and defers the access section', async () => {
    setup();
    await openSystem();
    expect(screen.queryByText('منابع انسانی')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ساختار سازمانی' })).toBeInTheDocument();
    expect(screen.queryByText('ساختار سازمانی و کارکنان')).not.toBeInTheDocument();
    expect(screen.getByText('سوابق امنیتی و مدیریتی')).toBeInTheDocument();

    for (const [name, href] of [
      ['مدیریت کاربران', '/app/admin/users'],
      ['مجوزهای دسترسی', '/app/admin/access'],
      ['واحدهای سازمانی', '/app/hr/org/units'],
      ['سمت‌های سازمانی', '/app/hr/org/positions'],
    ] as const) {
      expect(screen.getByRole('link', { name })).toHaveAttribute('href', href);
    }

    const users = screen.getByRole('link', { name: 'مدیریت کاربران' });
    const accessPage = screen.getByRole('link', { name: 'مجوزهای دسترسی' });
    expect(users.compareDocumentPosition(accessPage) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(accessPage.closest('li')?.querySelector('.faranou-nav-section-toggle')).toBeNull();

    const orgSection = screen.getByRole('button', { name: 'ساختار سازمانی' }).closest('li') as HTMLElement;
    expect(within(orgSection).getAllByRole('link').map((link) => link.textContent)).toEqual(['واحدهای سازمانی', 'سمت‌های سازمانی']);

    const employeeSection = screen.getByText('کارکنان').closest('[aria-disabled="true"]') as HTMLElement;
    const deferredAccessSection = screen.getByText('دسترسی و مجوزها').closest('[aria-disabled="true"]') as HTMLElement;
    expect(employeeSection).toBeTruthy();
    expect(deferredAccessSection).toBeTruthy();
    expect(deferredAccessSection.querySelector('a,button')).toBeNull();
    expect(screen.queryByText('گروه‌های دسترسی')).not.toBeInTheDocument();
    expect(screen.queryByText('کاتالوگ و جزئیات مجوزها')).not.toBeInTheDocument();
    expect(employeeSection.compareDocumentPosition(deferredAccessSection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('keeps the access and permissions section disabled at the bottom', async () => {
    setup();
    await openSystem();
    const access = screen.getByText('دسترسی و مجوزها').closest('[aria-disabled="true"]') as HTMLElement;
    expect(access).toBeTruthy();
    expect(access.querySelector('a,button')).toBeNull();
    expect(screen.queryByRole('button', { name: 'دسترسی و مجوزها' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'گروه‌های دسترسی' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'کاتالوگ و جزئیات مجوزها' })).not.toBeInTheDocument();
    const users = screen.getByRole('link', { name: 'مدیریت کاربران' });
    const accessPage = screen.getByRole('link', { name: 'مجوزهای دسترسی' });
    const employees = screen.getByText('کارکنان').closest('[aria-disabled="true"]') as HTMLElement;
    expect(users.compareDocumentPosition(accessPage) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(employees.compareDocumentPosition(access) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('hides «مجوزهای دسترسی» from a member without role permissions (hint only; the server decides)', () => {
    setup(companyMember([]));
    expect(screen.queryByRole('link', { name: 'مجوزهای دسترسی' })).not.toBeInTheDocument();
  });

  it('never creates a link for a page that has no route (placeholders stay disabled)', async () => {
    setup();
    await openSystem();
    expect(screen.getByText('سیاست‌های امنیتی').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('lets the user fold and expand the organizational section without affecting sibling groups', async () => {
    setup();
    const { user } = await openSystem();
    const section = screen.getByRole('button', { name: 'ساختار سازمانی' });
    expect(section).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('کارکنان').closest('[aria-disabled="true"]')).toBeTruthy();
    await user.click(section);
    expect(section).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('کارکنان').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(screen.getByText('دسترسی و مجوزها').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'مدیریت سیستم' })).toHaveAttribute('aria-expanded', 'true');
    await user.click(section);
    expect(screen.getByText('کارکنان').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('has no collapse control or icon-rail state any more', () => {
    const { container } = setup();
    expect(screen.queryByRole('button', { name: /جمع‌کردن منو|باز کردن کامل منو/ })).not.toBeInTheDocument();
    expect(screen.queryByText('جمع‌کردن منو')).not.toBeInTheDocument();
    expect(container.querySelector('.faranou-sidebar-fold')).toBeNull();
    expect(container.querySelector('.faranou-sidebar--collapsed')).toBeNull();
    expect(container.querySelector('[aria-pressed]')).toBeNull();
    // Group labels are always visible text (no icon-only rail, hence no tooltip wrappers).
    expect(screen.getByRole('button', { name: 'مدیریت سیستم' })).toHaveTextContent('مدیریت سیستم');
    expect(screen.getByRole('link', { name: 'داشبورد' })).toHaveTextContent('داشبورد');
  });

  it('renders the menu as one landmark inside a single frame with the footer after it', () => {
    const { container } = setup();
    const aside = container.querySelector('aside.faranou-sidebar') as HTMLElement;
    expect(aside).toHaveClass('faranou-sidebar--premium');
    expect(aside.children).toHaveLength(2);
    expect(aside.children[0]?.tagName).toBe('NAV');
    expect(aside.children[0]).toHaveAttribute('aria-label', 'منوی اصلی');
    expect(aside.children[1]).toHaveClass('faranou-sidebar-footer');
    // Layout relies on logical (RTL-safe) properties only: the markup carries no physical direction utilities.
    expect(aside.innerHTML).not.toMatch(/\b(?:ml|mr|pl|pr|left|right)-/);
  });

  it('uses one icon style: a coloured chip for groups, a uniform plain icon for sub-menu items', async () => {
    const { container } = setup();
    await openSystem();
    const groupChips = container.querySelectorAll('.faranou-nav-group .faranou-nav-icon');
    expect(groupChips.length).toBeGreaterThan(5);
    for (const chip of groupChips) expect(chip.className).not.toContain('--sub');
    const subIcons = container.querySelectorAll('.faranou-nav-sub .faranou-nav-icon');
    expect(subIcons.length).toBeGreaterThan(5);
    for (const icon of subIcons) {
      expect(icon).toHaveClass('faranou-nav-icon--sub');
      expect(icon.className).not.toMatch(/--(sky|emerald|amber|rose|violet|cyan|indigo|teal|orange|blue|green|slate)\b/);
      expect(icon).toHaveAttribute('aria-hidden', 'true');
    }
  });

  it('keeps every group folded by default and every entry Persian once opened (no internal English names)', async () => {
    setup();
    for (const group of screen.getAllByRole('button').filter((button) => button.classList.contains('faranou-nav-group'))) {
      expect(group).toHaveAttribute('aria-expanded', 'false');
    }
    expect(screen.queryByRole('link', { name: 'مدیریت کاربران' })).not.toBeInTheDocument();
    await openSystem();
    expect(screen.getByRole('link', { name: 'مدیریت کاربران' })).toBeInTheDocument();
    for (const internal of ['hr', 'reporting', 'trade', 'roles', 'users', 'consolidation', '/admin', '/hr']) {
      expect(screen.queryByText(internal, { exact: true })).not.toBeInTheDocument();
    }
  });

  it('hides every gated page when neither the installation flag nor any code exists', () => {
    setup(companyMember([]));
    expect(screen.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'مدیریت کاربران' })).not.toBeInTheDocument();
  });

  it('hides the organizational section entirely when none of its pages is permitted', () => {
    setup(companyMember([]));
    // Only company-code gated entries are visible to a plain member; the organizational section is gone entirely.
    expect(screen.queryByRole('button', { name: 'ساختار سازمانی' })).not.toBeInTheDocument();
  });

  it('marks the active page with aria-current, including deep routes, and only that page', async () => {
    window.history.pushState(null, '', '/app/admin/users/123');
    setup();
    await openSystem();
    const active = screen.getByRole('link', { name: 'مدیریت کاربران' });
    expect(active).toHaveAttribute('aria-current', 'page');
    expect(active).toHaveClass('faranou-nav-active');
    expect(document.querySelectorAll('[aria-current="page"]')).toHaveLength(1);
  });

  it('does not open any group for a deep-linked route: the active route only drives the highlight', async () => {
    window.history.pushState(null, '', '/app/hr/org/units');
    setup(installationAdmin);
    const system = screen.getByRole('button', { name: 'مدیریت سیستم' });
    expect(system).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
    // The folded owner of the active page shows a dot, never opens by itself.
    expect(system.querySelector('.faranou-nav-group-dot')).not.toBeNull();
    const user = userEvent.setup();
    await user.click(system);
    expect(system).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'واحدهای سازمانی' })).toHaveAttribute('aria-current', 'page');
  });

  it('keeps the system group folded on the dashboard and on the create-user form', () => {
    for (const path of ['/app/', '/app/admin/users/new']) {
      window.history.pushState(null, '', path);
      const { unmount } = setup();
      expect(screen.getByRole('button', { name: 'مدیریت سیستم' })).toHaveAttribute('aria-expanded', 'false');
      expect(screen.queryByRole('link', { name: 'مدیریت کاربران' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('opens the system group on a click and folds it on the next click, independent of the active page', async () => {
    window.history.pushState(null, '', '/');
    setup();
    const { user, system } = await openSystem();
    expect(system).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'مدیریت کاربران' })).toBeInTheDocument();
    await user.click(system);
    expect(system).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'مدیریت کاربران' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'داشبورد' })).toHaveAttribute('aria-current', 'page');
  });

  it('keeps what the user opened and opens nothing else when navigating to another page', async () => {
    window.history.pushState(null, '', '/');
    setup();
    const { user, system } = await openSystem();
    await user.click(screen.getByRole('link', { name: 'مجوزهای دسترسی' }));
    expect(window.location.pathname).toBe('/app/admin/access');
    expect(system).toHaveAttribute('aria-expanded', 'true');
    for (const group of screen.getAllByRole('button').filter((button) => button.classList.contains('faranou-nav-group') && button !== system)) {
      expect(group).toHaveAttribute('aria-expanded', 'false');
    }
    // A folded group stays folded after navigation too (the dot only marks it as the owner).
    await user.click(system);
    expect(system).toHaveAttribute('aria-expanded', 'false');
    await user.click(screen.getByRole('link', { name: 'داشبورد' }));
    expect(system).toHaveAttribute('aria-expanded', 'false');
  });

  it('supports keyboard operation of groups and sections with Enter and Space', async () => {
    const user = userEvent.setup();
    setup();
    const accounting = screen.getByRole('button', { name: 'حسابداری' });
    accounting.focus();
    expect(accounting).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(accounting).toHaveAttribute('aria-expanded', 'true');
    await user.keyboard(' ');
    expect(accounting).toHaveAttribute('aria-expanded', 'false');
    const system = screen.getByRole('button', { name: 'مدیریت سیستم' });
    system.focus();
    await user.keyboard('{Enter}');
    expect(system).toHaveAttribute('aria-expanded', 'true');
    const section = screen.getByRole('button', { name: 'ساختار سازمانی' });
    section.focus();
    await user.keyboard('{Enter}');
    expect(section).toHaveAttribute('aria-expanded', 'false');
  });

  it('puts the logout button last in the sidebar and calls the shell handler', async () => {
    const user = userEvent.setup();
    const onLogout = vi.fn();
    const { container } = setup(installationAdmin, { onLogout });
    const logout = screen.getByRole('button', { name: 'خروج از حساب' });
    const focusable = container.querySelectorAll('aside a, aside button');
    expect(focusable[focusable.length - 1]).toBe(logout);
    expect(container.querySelector('aside')?.lastElementChild).toContainElement(logout);
    await user.click(logout);
    expect(onLogout).toHaveBeenCalledTimes(1);
  });

  it('disables the sidebar logout while a logout is running', () => {
    setup(installationAdmin, { onLogout: vi.fn(), loggingOut: true });
    expect(screen.getByRole('button', { name: 'در حال خروج…' })).toBeDisabled();
  });

  it('shows no logout when the shell gives no handler', () => {
    setup();
    expect(screen.queryByRole('button', { name: 'خروج از حساب' })).not.toBeInTheDocument();
  });

  it('keeps icon sizes fixed: groups 16px, sub-menu items 15px', async () => {
    const { container } = setup();
    await openSystem();
    expect(container.querySelector('.faranou-nav-group .faranou-nav-icon svg')).toHaveAttribute('width', '16');
    expect(container.querySelector('.faranou-nav-sub .faranou-nav-icon svg')).toHaveAttribute('width', '15');
  });

  it('is rendered in an RTL page without physical-direction attributes on the frame', () => {
    const { container } = render(<div dir="rtl"><RouterProvider routes={[]}><AppSidebar access={installationAdmin} canAccess={() => true} onRetryPermissions={vi.fn()} onNavigate={vi.fn()} mobileOpen /></RouterProvider></div>);
    expect(container.firstElementChild).toHaveAttribute('dir', 'rtl');
    expect(container.querySelector('aside')).toHaveClass('is-open');
  });
});

describe('AppSidebar Trade leaf «طرف‌حساب‌ها» (S2-3)', () => {
  const tradeReady = (permissions: string[], accessMode: 'ReadWrite' | 'ReadOnly' = 'ReadWrite') =>
    createTradeAccess({ status: 'ready', accessMode, permissions });

  function setupTrade(trade: TradeAccess | null) {
    const sidebar = (
      <RouterProvider routes={[]}>
        <AppSidebar access={installationAdmin} canAccess={() => true} onRetryPermissions={vi.fn()} onNavigate={vi.fn()} mobileOpen={false} />
      </RouterProvider>
    );
    return render(trade ? <TradeAccessContext.Provider value={trade}>{sidebar}</TradeAccessContext.Provider> : sidebar);
  }

  const openTrade = async () => {
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'بازرگانی خارجی' }));
    return user;
  };

  it('is hidden by default (no provider), while loading, on error and when the module is unavailable; the honest placeholder stays', async () => {
    for (const trade of [
      null,
      createTradeAccess({ status: 'loading' }),
      createTradeAccess({ status: 'error' }),
      createTradeAccess({ status: 'unavailable', reason: 'module' }),
    ]) {
      const { unmount } = setupTrade(trade);
      await openTrade();
      expect(screen.queryByRole('link', { name: 'طرف‌حساب‌ها' })).not.toBeInTheDocument();
      const placeholder = screen.getByText('واردات و صادرات').closest('[aria-disabled="true"]') as HTMLElement;
      expect(placeholder).toBeTruthy();
      expect(placeholder).toHaveTextContent('به‌زودی');
      expect(placeholder.querySelector('a,button')).toBeNull();
      unmount();
    }
  });

  it('is hidden without trade.form.party.view even when the user holds other Trade codes, and even for an installation administrator without the code', async () => {
    setupTrade(tradeReady(['trade.form.party.create', 'trade.form.party.update']));
    await openTrade();
    expect(screen.queryByRole('link', { name: 'طرف‌حساب‌ها' })).not.toBeInTheDocument();
  });

  it('shows a real link to /trade/parties when capabilities allow the view, in the existing «بازرگانی خارجی» group', async () => {
    setupTrade(tradeReady(['trade.form.party.view']));
    expect(screen.queryByRole('link', { name: 'طرف‌حساب‌ها' })).not.toBeInTheDocument(); // folded until clicked
    await openTrade();
    const link = screen.getByRole('link', { name: 'طرف‌حساب‌ها' });
    expect(link).toHaveAttribute('href', '/app/trade/parties');
    expect(link).not.toHaveAttribute('aria-current');
    expect(screen.getByText('واردات و صادرات').closest('[aria-disabled="true"]')).toBeTruthy();
    // It is shown on a read-only period too (viewing is allowed).
    expect(link.closest('li')?.closest('ul')?.id).toBe(screen.getByRole('button', { name: 'بازرگانی خارجی' }).getAttribute('aria-controls'));
  });

  it('marks the leaf active (aria-current) on the list, details and edit routes', async () => {
    for (const path of ['/app/trade/parties', '/app/trade/parties/3b1f5c70-1111-4000-8000-00000000a001', '/app/trade/parties/3b1f5c70-1111-4000-8000-00000000a001/edit']) {
      window.history.pushState(null, '', path);
      const { unmount } = setupTrade(tradeReady(['trade.form.party.view'], 'ReadOnly'));
      // Direct load never opens a group: the folded owner shows a dot, the click opens it.
      const group = screen.getByRole('button', { name: 'بازرگانی خارجی' });
      expect(group).toHaveAttribute('aria-expanded', 'false');
      expect(group.querySelector('.faranou-nav-group-dot')).not.toBeNull();
      await openTrade();
      const active = screen.getByRole('link', { name: 'طرف‌حساب‌ها' });
      expect(active).toHaveAttribute('aria-current', 'page');
      expect(active).toHaveClass('faranou-nav-active');
      expect(document.querySelectorAll('[aria-current="page"]')).toHaveLength(1);
      unmount();
    }
  });

  it('does not leak the English internal names or the route into the visible labels', async () => {
    setupTrade(tradeReady(['trade.form.party.view']));
    await openTrade();
    expect(screen.queryByText('trade', { exact: true })).not.toBeInTheDocument();
    expect(screen.queryByText('/trade/parties', { exact: true })).not.toBeInTheDocument();
  });
});
