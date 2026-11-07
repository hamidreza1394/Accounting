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
  it('groups subsystems under logical Persian labels with collapsible groups', async () => {
    const user = userEvent.setup();
    setup();
    const accounting = screen.getByRole('button', { name: /حسابداری و مالی/ });
    expect(accounting).toHaveAttribute('aria-expanded', 'false');
    // Folded future groups hide their leaves until unfolded.
    expect(screen.queryByText('خزانه‌داری')).not.toBeInTheDocument();
    await user.click(accounting);
    expect(accounting).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('خزانه‌داری')).toBeInTheDocument();
    await user.click(accounting);
    expect(screen.queryByText('خزانه‌داری')).not.toBeInTheDocument();
  });

  it('opens the real groups by default and keeps every entry Persian (no internal English names)', () => {
    setup();
    // Real pages: HR and system administration groups start unfolded.
    expect(screen.getByRole('link', { name: 'واحدهای سازمانی' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'گروه‌های دسترسی' })).toBeInTheDocument();
    // No internal route names surface to the user.
    for (const internal of ['hr', 'reporting', 'trade', 'roles', 'users', 'consolidation', '/admin', '/hr']) {
      expect(screen.queryByText(internal, { exact: true })).not.toBeInTheDocument();
    }
  });

  it('hides the whole HR group when neither the installation flag nor any hr code exists', () => {
    setup(companyMember([]));
    expect(screen.queryByText('منابع انسانی')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
  });

  it('folds into the icon rail with accessible names and no visible labels', () => {
    setup(installationAdmin, true);
    expect(screen.getByLabelText('باز کردن کامل منو')).toHaveAttribute('aria-pressed', 'true');
    const dashboard = screen.getByRole('link', { name: 'داشبورد' });
    expect(dashboard).toHaveTextContent('داشبورد'); // sr-only text keeps the accessible name
    expect(screen.queryByText('منوی اصلی')).not.toBeInTheDocument();
    // Group buttons keep their real expanded state; opening one re-expands the rail.
    expect(screen.getByRole('button', { name: 'منابع انسانی (باز)' })).toBeInTheDocument();
  });

  it('keeps native hover tooltips in the rail and drops them when labels are visible', () => {
    // Closes the WARN recorded in artifacts/ui-preview/stage-1/results-pr-integration.json
    // («tooltip با سلکتورهای عمومی پیدا نشد»): the rail tooltip is the native `title`
    // attribute — invisible in screenshots by design — combined with sr-only names.
    const rail = setup(installationAdmin, true);
    expect(rail.getByRole('link', { name: 'داشبورد' })).toHaveAttribute('title', 'داشبورد');
    expect(rail.getByRole('button', { name: 'منابع انسانی (باز)' })).toHaveAttribute('title', 'منابع انسانی');
    // Group children are hidden in the rail; the tooltip belongs to the group icon itself.
    expect(rail.queryByRole('link', { name: 'واحدهای سازمانی' })).not.toBeInTheDocument();
    rail.unmount();

    const expanded = setup();
    expect(expanded.getByRole('link', { name: 'داشبورد' })).not.toHaveAttribute('title');
    expect(expanded.getByRole('button', { name: /^منابع انسانی/ })).not.toHaveAttribute('title');
    expect(expanded.getByRole('link', { name: 'واحدهای سازمانی' })).toBeInTheDocument();
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
