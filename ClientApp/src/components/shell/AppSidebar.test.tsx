import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AccessSnapshot, Permission } from '@/lib/permissions';
import { RouterProvider } from '@/lib/router';
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

  it('keeps the section icon when only a later leaf of the section is permitted', () => {
    const access = companyMember(['hr.employees.list']);
    const { container } = render(
      <RouterProvider routes={[]}>
        <AppSidebar
          access={access}
          canAccess={(permission: Permission) => permission === 'hr.employees.view'}
          onRetryPermissions={vi.fn()}
          onNavigate={vi.fn()}
          mobileOpen={false}
        />
      </RouterProvider>,
    );
    const toggle = screen.getByRole('button', { name: 'ساختار سازمانی' });
    expect(screen.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'کارکنان' })).toBeInTheDocument();
    expect(toggle.querySelector('svg')).not.toBeNull();
    expect(container).toBeTruthy();
  });

  it('keeps the organizational pages under system administration in a section named «ساختار سازمانی»', () => {
    setup();
    expect(screen.queryByText('منابع انسانی')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ساختار سازمانی' })).toBeInTheDocument();
    expect(screen.queryByText('ساختار سازمانی و کارکنان')).not.toBeInTheDocument();
    expect(screen.getByText('دسترسی و مجوزها')).toBeInTheDocument();
    expect(screen.getByText('سوابق امنیتی و مدیریتی')).toBeInTheDocument();
    for (const [name, href] of [
      ['واحدهای سازمانی', '/app/hr/org/units'],
      ['سمت‌های سازمانی', '/app/hr/org/positions'],
      ['کارکنان', '/app/hr/org/employees'],
      ['گروه‌های دسترسی', '/app/admin/roles'],
      ['کاتالوگ و جزئیات مجوزها', '/app/admin/permissions'],
      ['مدیریت کاربران', '/app/admin/users'],
      ['مجوزهای دسترسی', '/app/admin/access'],
    ] as const) {
      expect(screen.getByRole('link', { name })).toHaveAttribute('href', href);
    }
    const section = screen.getByRole('button', { name: 'ساختار سازمانی' }).closest('li') as HTMLElement;
    expect(within(section).getAllByRole('link').map((link) => link.textContent)).toEqual(['واحدهای سازمانی', 'سمت‌های سازمانی', 'کارکنان']);
  });

  it('links «مجوزهای دسترسی» to its real page, directly under the user management entry', () => {
    setup();
    const links = screen.getAllByRole('link').map((link) => link.textContent);
    expect(links.indexOf('مجوزهای دسترسی')).toBe(links.indexOf('مدیریت کاربران') + 1);
    expect(screen.getByRole('link', { name: 'مجوزهای دسترسی' })).toHaveAttribute('href', '/app/admin/access');
  });

  it('hides «مجوزهای دسترسی» from a member without role permissions (hint only; the server decides)', () => {
    setup(companyMember([]));
    expect(screen.queryByRole('link', { name: 'مجوزهای دسترسی' })).not.toBeInTheDocument();
  });

  it('never creates a link for a page that has no route (placeholders stay disabled)', () => {
    setup();
    expect(screen.getByText('سیاست‌های امنیتی').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('lets the user fold and expand the organizational section without affecting sibling groups', async () => {
    const user = userEvent.setup();
    setup();
    const section = screen.getByRole('button', { name: 'ساختار سازمانی' });
    expect(section).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'کارکنان' })).toBeInTheDocument();
    await user.click(section);
    expect(section).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'کارکنان' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'دسترسی و مجوزها' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', { name: 'مدیریت سیستم' })).toHaveAttribute('aria-expanded', 'true');
    await user.click(section);
    expect(screen.getByRole('link', { name: 'کارکنان' })).toBeInTheDocument();
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

  it('uses one icon style: a coloured chip for groups, a uniform plain icon for sub-menu items', () => {
    const { container } = setup();
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

  it('opens the real groups by default and keeps every entry Persian (no internal English names)', () => {
    setup();
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

  it('marks the active page with aria-current, including deep routes, and only that page', () => {
    window.history.pushState(null, '', '/app/admin/users/123');
    setup();
    const active = screen.getByRole('link', { name: 'مدیریت کاربران' });
    expect(active).toHaveAttribute('aria-current', 'page');
    expect(active).toHaveClass('faranou-nav-active');
    expect(document.querySelectorAll('[aria-current="page"]')).toHaveLength(1);
  });

  it('auto-opens the owning group and section for a deep-linked route', () => {
    window.history.pushState(null, '', '/app/hr/org/units');
    setup(installationAdmin);
    const units = screen.getByRole('link', { name: 'واحدهای سازمانی' });
    expect(units).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: 'مدیریت سیستم' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', { name: 'ساختار سازمانی' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('lets the user fold the system group by hand while the dashboard stays the active page', async () => {
    window.history.pushState(null, '', '/');
    const user = userEvent.setup();
    setup();
    const system = screen.getByRole('button', { name: 'مدیریت سیستم' });
    await user.click(system);
    expect(system).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('link', { name: 'داشبورد' })).toHaveAttribute('aria-current', 'page');
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

  it('keeps icon sizes fixed: groups 16px, sub-menu items 15px', () => {
    const { container } = setup();
    expect(container.querySelector('.faranou-nav-group .faranou-nav-icon svg')).toHaveAttribute('width', '16');
    expect(container.querySelector('.faranou-nav-sub .faranou-nav-icon svg')).toHaveAttribute('width', '15');
  });

  it('is rendered in an RTL page without physical-direction attributes on the frame', () => {
    const { container } = render(<div dir="rtl"><RouterProvider routes={[]}><AppSidebar access={installationAdmin} canAccess={() => true} onRetryPermissions={vi.fn()} onNavigate={vi.fn()} mobileOpen /></RouterProvider></div>);
    expect(container.firstElementChild).toHaveAttribute('dir', 'rtl');
    expect(container.querySelector('aside')).toHaveClass('is-open');
  });
});
