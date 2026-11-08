import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
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

function setup(access: AccessSnapshot = installationAdmin, collapsed = false) {
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
        collapsed={collapsed}
        onToggleCollapsed={vi.fn()}
        mobileOpen={false}
      />
    </RouterProvider>,
  );
}

describe('AppSidebar (Issue #444 stage 1 shell)', () => {
  it('groups the catalog subsystems as folded first-level groups with collapsible leaves', async () => {
    const user = userEvent.setup();
    setup();
    // The eight first-level subsystem groups of UI-05 (all future work, folded).
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

  it('keeps the HR pages under system administration with Persian sections (UI-08)', () => {
    setup();
    // The independent HR group is gone; its pages live in the Persian sections of مدیریت سیستم.
    expect(screen.queryByText('منابع انسانی')).not.toBeInTheDocument();
    expect(screen.getByText('ساختار سازمانی و کارکنان')).toBeInTheDocument();
    // «شرکت‌ها و عضویت‌ها» appears only with the company membership codes (fail-closed);
    // for the installation administrator the two visible sections are checked here.
    expect(screen.getByText('دسترسی و مجوزها')).toBeInTheDocument();
    expect(screen.getByText('سوابق امنیتی و مدیریتی')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'واحدهای سازمانی' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'گروه‌های دسترسی' })).toBeInTheDocument();
  });

  it('opens the real groups by default and keeps every entry Persian (no internal English names)', () => {
    setup();
    expect(screen.getByRole('link', { name: 'مدیریت کاربران' })).toBeInTheDocument();
    // No internal route names surface to the user.
    for (const internal of ['hr', 'reporting', 'trade', 'roles', 'users', 'consolidation', '/admin', '/hr']) {
      expect(screen.queryByText(internal, { exact: true })).not.toBeInTheDocument();
    }
  });

  it('hides every gated page when neither the installation flag nor any code exists', () => {
    setup(companyMember([]));
    expect(screen.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'مدیریت کاربران' })).not.toBeInTheDocument();
  });

  it('folds into the icon rail with accessible names and no visible labels', () => {
    setup(installationAdmin, true);
    expect(screen.getByLabelText('باز کردن کامل منو')).toHaveAttribute('aria-pressed', 'true');
    const dashboard = screen.getByRole('link', { name: 'داشبورد' });
    expect(dashboard).toHaveTextContent('داشبورد'); // sr-only text keeps the accessible name
    expect(screen.queryByText('منوی اصلی')).not.toBeInTheDocument();
    // Group buttons keep their real expanded state; opening one re-expands the rail.
    expect(screen.getByRole('button', { name: 'مدیریت سیستم (باز)' })).toBeInTheDocument();
  });

  it('shows the custom Persian tooltip on rail focus and drops it when labels are visible', async () => {
    // The rail tooltip is the project's own component (hover AND focus, Escape closes) —
    // no native `title`, no external library.
    const rail = setup(installationAdmin, true);
    const dashboard = rail.getByRole('link', { name: 'داشبورد' });
    expect(dashboard).not.toHaveAttribute('title');
    // Group children are hidden in the rail; the tooltip belongs to the group icon itself.
    expect(rail.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();

    // Focus opens the tooltip (portal, role=tooltip, described-by wiring).
    const user = userEvent.setup();
    await user.click(dashboard);
    // The click navigated nowhere (no routes): focus stays on the link, tooltip open.
    expect(await screen.findByRole('tooltip', { name: 'داشبورد' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('tooltip', { name: 'داشبورد' })).not.toBeInTheDocument());
    rail.unmount();

    const expanded = setup();
    expect(expanded.getByRole('link', { name: 'داشبورد' })).not.toHaveAttribute('title');
    expect(expanded.getByRole('link', { name: 'مدیریت کاربران' })).toBeInTheDocument();
    expanded.unmount();
  });

  it('auto-opens the owning group for a deep-linked route', () => {
    window.history.pushState(null, '', '/app/hr/org/positions/abc');
    setup();
    // The HR group is open by default; assert the deep-linked leaf is marked active.
    expect(screen.getByRole('link', { name: 'سمت‌های سازمانی' })).toHaveAttribute('aria-current', 'page');
    return waitFor(() => expect(window.location.pathname).toBe('/app/hr/org/positions/abc'));
  });
});
