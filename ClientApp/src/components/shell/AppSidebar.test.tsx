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
    expect(screen.queryByText('منابع انسانی')).not.toBeInTheDocument();
    expect(screen.getByText('ساختار سازمانی و کارکنان')).toBeInTheDocument();
    expect(screen.getByText('دسترسی و مجوزها')).toBeInTheDocument();
    expect(screen.getByText('سوابق امنیتی و مدیریتی')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'واحدهای سازمانی' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'گروه‌های دسترسی' })).toBeInTheDocument();
  });

  it('lets the user fold and expand the nested organizational section without affecting sibling groups', async () => {
    const user = userEvent.setup();
    setup();
    const section = screen.getByRole('button', { name: 'ساختار سازمانی و کارکنان' });
    expect(section).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'کارکنان' })).toBeInTheDocument();
    await user.click(section);
    expect(section).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'کارکنان' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'مدیریت سیستم' })).toHaveAttribute('aria-expanded', 'true');
    await user.click(section);
    expect(screen.getByRole('link', { name: 'کارکنان' })).toBeInTheDocument();
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

  it('folds into the icon rail with accessible names and no visible labels', () => {
    setup(installationAdmin, true);
    expect(screen.getByLabelText('باز کردن کامل منو')).toHaveAttribute('aria-pressed', 'true');
    const dashboard = screen.getByRole('link', { name: 'داشبورد' });
    expect(dashboard).toHaveTextContent('داشبورد');
    expect(screen.queryByText('منوی اصلی')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'مدیریت سیستم (باز)' })).toBeInTheDocument();
  });

  it('shows the custom Persian tooltip on rail focus and drops it when labels are visible', async () => {
    const rail = setup(installationAdmin, true);
    const dashboard = rail.getByRole('link', { name: 'داشبورد' });
    expect(dashboard).not.toHaveAttribute('title');
    expect(rail.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(dashboard);
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
    expect(screen.getByRole('link', { name: 'سمت‌های سازمانی' })).toHaveAttribute('aria-current', 'page');
    return waitFor(() => expect(window.location.pathname).toBe('/app/hr/org/positions/abc'));
  });
});
