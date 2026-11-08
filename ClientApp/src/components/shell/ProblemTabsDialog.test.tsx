import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AppShell } from '../AppShell';
import { resetFormsForTests, useFormGuard } from '@/lib/formGuard';
import type { AccessSnapshot } from '@/lib/permissions';
import type { SelectedContext, Session } from '../../api/types';

const session: Session = { username: 'hamid', fullName: 'حمید رضایی', accountKind: 'User', isAdministrator: false };
const access: AccessSnapshot = { status: 'ready', isInstallationAdministrator: true, companyId: 'company-1', companyCodes: [] };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر سیستم' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

function GuardedFormPage({ label, message }: { label: string; message: string }) {
  const [dirty, setDirty] = useState(false);
  useFormGuard({ dirty, title: label, dirtyMessage: message });
  return (
    <div>
      <h1>{label}</h1>
      <button type="button" onClick={() => setDirty(true)}>خراب‌کردن فرم</button>
    </div>
  );
}

const routes = [
  { path: '/', element: () => <h1>داشبورد</h1>, title: 'داشبورد' },
  { path: '/hr/org/employees', element: () => <GuardedFormPage label="کارکنان" message="فرم کارکنان آزمایشی است." />, title: 'کارکنان' },
  { path: '/form', element: () => <GuardedFormPage label="فرم آزمایشی" message="فرم آزمایشی تغییرات ذخیره‌نشده دارد." />, title: 'فرم آزمایشی' },
] as const;

const tabbar = () => screen.getByRole('navigation', { name: 'تب‌های کاری باز' });
function tabButton(name: RegExp): HTMLElement {
  const matches = [...tabbar().querySelectorAll<HTMLButtonElement>('.faranou-tab-button')].filter((button) => name.test(button.textContent ?? ''));
  if (matches.length !== 1) throw new Error(`expected one tab button for ${String(name)}, found ${matches.length}`);
  return matches[0]!;
}
async function makeDirty(label: RegExp) {
  const pane = document.querySelector(`[data-tab-pane="${CSS.escape(paneOf(label))}"]`);
  const button = pane?.querySelector<HTMLButtonElement>('button:not(.faranou-tab-button)');
  if (!button) throw new Error(`dirty button of ${String(label)} not found`);
  await userEvent.setup().click(button);
}
function paneOf(label: RegExp): string {
  if (label.test('فرم آزمایشی')) return '/form';
  if (label.test('کارکنان')) return '/hr/org/employees';
  return '/';
}

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
});

describe('the problem-tabs dialog (Issue #444 stage 1 completion)', () => {
  it('lists every problem tab once with its Persian status, message and navigation', async () => {
    const user = userEvent.setup();
    window.history.pushState(null, '', '/app/form');
    const onClose = vi.fn();
    render(
      <AppShell
        session={session}
        context={context}
        access={access}
        routes={routes}
        workspaceTabs
        onChangeContext={vi.fn()}
        onLogout={vi.fn().mockResolvedValue(undefined)}
        problemTabsDialog={{ open: true, title: 'تغییر زمینهٔ کاری ممکن نیست', description: 'پیش از تغییر شرکت یا دوره، فرم‌های زیر را تعیین تکلیف کنید.', onClose }}
      >
        <main>جایگزین</main>
      </AppShell>,
    );
    await waitFor(() => tabButton(/فرم آزمایشی/));

    // Open the second tab and dirty its form as well.
    window.history.pushState(null, '', '/app/hr/org/employees');
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => tabButton(/کارکنان/));
    await makeDirty(/فرم آزمایشی/);
    await makeDirty(/کارکنان/);

    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    expect(dialog.textContent).toContain('پیش از تغییر شرکت یا دوره، فرم‌های زیر را تعیین تکلیف کنید.');
    expect(dialog.textContent).toContain('فرم آزمایشی');
    expect(dialog.textContent).toContain('کارکنان');
    expect(dialog.textContent).toContain('دارای تغییر ذخیره‌نشده');
    expect(dialog.textContent).toContain('فرم آزمایشی تغییرات ذخیره‌نشده دارد.');
    // One row per tab even though the count could be larger.
    expect(dialog.querySelectorAll('.faranou-problemtab-title')).toHaveLength(2);

    // Clicking a tab name activates exactly that tab through the shared store (URL follows).
    await user.click(within(dialog).getByRole('button', { name: 'رفتن به تب فرم آزمایشی' }));
    await waitFor(() => expect(window.location.pathname).toBe('/app/form'));
    expect(tabButton(/فرم آزمایشی/)).toHaveAttribute('aria-current', 'page');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows the problem-form count when one tab holds several problem forms', async () => {
    const user = userEvent.setup();
    window.history.pushState(null, '', '/app/form');
    const twoForms = [
      routes[0],
      {
        path: '/form',
        element: () => (
          <div>
            <h1>فرم آزمایشی</h1>
            <GuardedFormPage label="فرم اول" message="فرم اول تغییرات ذخیره‌نشده دارد." />
            <GuardedFormPage label="فرم دوم" message="فرم دوم تغییرات ذخیره‌نشده دارد." />
          </div>
        ),
        title: 'فرم آزمایشی',
      },
    ] as const;
    render(
      <AppShell
        session={session}
        context={context}
        access={access}
        routes={twoForms}
        workspaceTabs
        onChangeContext={vi.fn()}
        onLogout={vi.fn().mockResolvedValue(undefined)}
        problemTabsDialog={{ open: true, title: 'تغییر زمینهٔ کاری ممکن نیست', description: 'بررسی فرم‌ها.', onClose: vi.fn() }}
      >
        <main>جایگزین</main>
      </AppShell>,
    );
    await waitFor(() => tabButton(/فرم آزمایشی/));
    const pane = document.querySelector('[data-tab-pane="/form"]')!;
    const buttons = [...pane.querySelectorAll<HTMLButtonElement>('button')];
    await user.click(buttons[0]!);
    await user.click(buttons[1]!);
    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    expect(dialog.textContent).toContain('۲ فرم');
    expect(dialog.querySelectorAll('.faranou-problemtab-title')).toHaveLength(1);
  });

  it('cancel changes nothing: no navigation, forms and tabs untouched', async () => {
    const user = userEvent.setup();
    window.history.pushState(null, '', '/app/form');
    const onClose = vi.fn();
    render(
      <AppShell
        session={session}
        context={context}
        access={access}
        routes={routes}
        workspaceTabs
        onChangeContext={vi.fn()}
        onLogout={vi.fn().mockResolvedValue(undefined)}
        problemTabsDialog={{ open: true, title: 'تغییر زمینهٔ کاری ممکن نیست', description: 'بررسی فرم‌ها.', onClose }}
      >
        <main>جایگزین</main>
      </AppShell>,
    );
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await makeDirty(/فرم آزمایشی/);
    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    await user.click(within(dialog).getByRole('button', { name: 'انصراف' }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(window.location.pathname).toBe('/app/form');
    expect(tabButton(/فرم آزمایشی/)).toBeInTheDocument();
    expect(tabButton(/فرم آزمایشی/).textContent).toContain('دارای تغییر ذخیره‌نشده');
  });
});
