import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AppShell } from '../AppShell';
import { resetFormsForTests, useFormGuard } from '@/lib/formGuard';
import { routeIconOf } from '@/lib/routeIcons';
import type { AccessSnapshot } from '@/lib/permissions';
import type { SelectedContext, Session } from '../../api/types';

const session: Session = { username: 'hamid', fullName: 'حمید رضایی', accountKind: 'User', isAdministrator: false };
const access: AccessSnapshot = { status: 'ready', isInstallationAdministrator: true, companyId: 'company-1', companyCodes: [] };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر سیستم' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

/** A page whose form can be driven through the four lifecycle states. */
function GuardedFormPage({ label }: { label: string }) {
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useFormGuard({ dirty, saving, errorMessage: error, title: label });
  return (
    <div>
      <h1>{label}</h1>
      <button type="button" onClick={() => setDirty(true)}>خراب‌کردن فرم</button>
      <button type="button" onClick={() => setSaving(true)}>شروع ذخیره</button>
      <button type="button" onClick={() => { setSaving(false); setError('ذخیره انجام نشد.'); }}>شکست ذخیره</button>
    </div>
  );
}

const routes = [
  { path: '/', element: () => <h1>داشبورد</h1>, title: 'داشبورد' },
  { path: '/admin/users', element: () => <h1>مدیریت کاربران</h1>, title: 'مدیریت کاربران' },
  { path: '/hr/org/units', element: () => <h1>واحدهای سازمانی</h1>, title: 'واحدهای سازمانی' },
  { path: '/hr/org/employees', element: () => <GuardedFormPage label="کارکنان" />, title: 'کارکنان' },
  { path: '/form', element: () => <GuardedFormPage label="فرم آزمایشی" />, title: 'فرم آزمایشی' },
] as const;

function setup(at = '/app/') {
  window.history.pushState(null, '', at);
  render(
    <AppShell session={session} context={context} access={access} routes={routes} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
      <main>جایگزین</main>
    </AppShell>,
  );
}

const tabbar = () => screen.getByRole('navigation', { name: 'تب‌های کاری باز' });
function tabButton(name: RegExp): HTMLElement {
  const matches = within_tabbar(name);
  if (matches.length !== 1) throw new Error(`expected one tab button for ${String(name)}, found ${matches.length}`);
  return matches[0]!;
}
function within_tabbar(name: RegExp): HTMLElement[] {
  return [...tabbar().querySelectorAll<HTMLButtonElement>('.faranou-tab-button')].filter((button) => name.test(button.textContent ?? ''));
}
async function navigate(to: string) {
  window.history.pushState(null, '', to);
  window.dispatchEvent(new PopStateEvent('popstate'));
  await waitFor(() => expect(window.location.pathname).toBe(to));
}

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
});

describe('workspace tab state indicators (Issue #444 stage 1 completion)', () => {
  it('shows a route icon on every tab and different page kinds get different icons', async () => {
    setup();
    await navigate('/app/admin/users');
    await navigate('/app/hr/org/units');
    const buttons = [...tabbar().querySelectorAll<HTMLButtonElement>('.faranou-tab-button')];
    expect(buttons.length).toBeGreaterThanOrEqual(3);
    const activeTab = buttons.find((button) => button.getAttribute('aria-current') === 'page');
    const inactiveTab = buttons.find((button) => button.getAttribute('aria-current') !== 'page');
    expect(activeTab).toHaveStyle({ backgroundColor: 'var(--color-navy)', color: 'var(--color-primary-foreground)' });
    expect(inactiveTab).toHaveStyle({ backgroundColor: 'var(--color-period)', color: 'var(--color-sub)' });
    // The icons are meaningful per route, not one icon for everything.
    const iconClasses = buttons.map((button) => button.querySelector('svg')?.classList.contains('faranou-tab-icon'));
    expect(iconClasses.every(Boolean)).toBe(true);
    expect(routeIconOf('/admin/users')).not.toBe(routeIconOf('/hr/org/units'));
    expect(routeIconOf('/')).toBe(routeIconOf('/'));
  });

  it('a clean tab shows no marker; dirty/saving/error each announce their Persian status', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));
    const clean = tabButton(/فرم آزمایشی/);
    expect(clean.querySelector('.faranou-tab-dot')).toBeNull();
    expect(clean.querySelector('.faranou-tab-saving')).toBeNull();
    expect(clean.querySelector('.faranou-tab-erroricon')).toBeNull();
    expect(clean.textContent).not.toContain('دارای تغییر ذخیره‌نشده');

    await user.click(screen.getByRole('button', { name: 'خراب‌کردن فرم' }));
    expect(tabButton(/فرم آزمایشی/).textContent).toContain('دارای تغییر ذخیره‌نشده');
    expect(tabButton(/فرم آزمایشی/).querySelector('.faranou-tab-dot')).not.toBeNull();

    await user.click(screen.getByRole('button', { name: 'شروع ذخیره' }));
    expect(tabButton(/فرم آزمایشی/).textContent).toContain('در حال ذخیره');
    expect(tabButton(/فرم آزمایشی/).querySelector('.faranou-tab-saving')).not.toBeNull();

    await user.click(screen.getByRole('button', { name: 'شکست ذخیره' }));
    expect(tabButton(/فرم آزمایشی/).textContent).toContain('خطا در ذخیره');
    expect(tabButton(/فرم آزمایشی/).querySelector('.faranou-tab-erroricon')).not.toBeNull();
    // The tooltip carries the status as well (hover and screen readers alike).
    expect(tabButton(/فرم آزمایشی/)).toHaveAttribute('title', expect.stringContaining('خطا در ذخیره'));
  });

  it('keeps the horizontal strip and the close button independent of the status marker', async () => {
    const user = userEvent.setup();
    setup('/app/form');
    await navigate('/app/hr/org/employees');
    await waitFor(() => tabButton(/کارکنان/));
    await user.click(screen.getByRole('button', { name: 'خراب‌کردن فرم' }));
    // Two tabs, one dirty: the close buttons remain separate from the activation button.
    expect(within_tabbar(/فرم آزمایشی/)).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'بستن فرم آزمایشی' })).toBeInTheDocument();
    expect(tabbar().scrollWidth).toBeGreaterThanOrEqual(tabbar().clientWidth - 2);
  });
});
