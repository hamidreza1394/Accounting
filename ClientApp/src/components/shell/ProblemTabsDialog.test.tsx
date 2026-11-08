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

function setup(onChangeContext = vi.fn()) {
  window.history.pushState(null, '', '/app/form');
  render(
    <AppShell session={session} context={context} access={access} routes={routes} workspaceTabs onChangeContext={onChangeContext} onLogout={vi.fn().mockResolvedValue(undefined)}>
      <main>جایگزین</main>
    </AppShell>,
  );
}

const tabbar = () => screen.getByRole('navigation', { name: 'تب‌های کاری باز' });
function tabButton(name: RegExp): HTMLElement {
  const matches = [...tabbar().querySelectorAll<HTMLButtonElement>('.faranou-tab-button')].filter((button) => name.test(button.textContent ?? ''));
  if (matches.length !== 1) throw new Error(`expected one tab button for ${String(name)}, found ${matches.length}`);
  return matches[0]!;
}
async function navigate(to: string) {
  window.history.pushState(null, '', to);
  window.dispatchEvent(new PopStateEvent('popstate'));
  await waitFor(() => expect(window.location.pathname).toBe(to));
}
async function makeDirty(paneKey: string, buttonLabel: string) {
  const pane = document.querySelector(`[data-tab-pane="${CSS.escape(paneKey)}"]`)!;
  const button = [...pane.querySelectorAll<HTMLButtonElement>('button')].find((candidate) => candidate.textContent?.includes(buttonLabel));
  if (!button) throw new Error(`button ${buttonLabel} not found in ${paneKey}`);
  await userEvent.setup().click(button);
}
const changeCompanyButton = () => screen.getByRole('button', { name: /شرکت جاری شرکت نمونه، تغییر شرکت/ });

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
});

describe('the problem-tabs dialog of the context-change guard (Issue #444 completion)', () => {
  it('lists every problem tab once with its Persian status, message and navigation', async () => {
    const user = userEvent.setup();
    const onChangeContext = vi.fn();
    setup(onChangeContext);
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await navigate('/app/hr/org/employees');
    await makeDirty('/form', 'خراب‌کردن فرم');
    await makeDirty('/hr/org/employees', 'خراب‌کردن فرم');

    await user.click(changeCompanyButton());
    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    expect(dialog.textContent).toContain('پیش از تغییر شرکت یا دورهٔ مالی، همهٔ فرم‌های زیر باید ذخیره شوند یا آگاهانه لغو شوند.');
    expect(dialog.textContent).toContain('فرم آزمایشی');
    expect(dialog.textContent).toContain('کارکنان');
    expect(dialog.textContent).toContain('دارای تغییر ذخیره‌نشده');
    expect(dialog.textContent).toContain('فرم آزمایشی تغییرات ذخیره‌نشده دارد.');
    expect(dialog.querySelectorAll('.faranou-problemtab-title')).toHaveLength(2);
    // The change itself is blocked while any form is unresolved.
    expect(onChangeContext).not.toHaveBeenCalled();

    // Jumping to a tab activates it through the shared store and keeps the flow pending.
    await user.click(within(dialog).getByRole('button', { name: 'رفتن به تب فرم آزمایشی' }));
    await waitFor(() => expect(window.location.pathname).toBe('/app/form'));
    expect(tabButton(/فرم آزمایشی/)).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' })).not.toBeInTheDocument();
    expect(onChangeContext).not.toHaveBeenCalled();
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
      <AppShell session={session} context={context} access={access} routes={twoForms} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
        <main>جایگزین</main>
      </AppShell>,
    );
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await makeDirty('/form', 'خراب‌کردن فرم');
    const pane = document.querySelector('[data-tab-pane="/form"]')!;
    const buttons = [...pane.querySelectorAll<HTMLButtonElement>('button')].filter((candidate) => candidate.textContent?.includes('خراب‌کردن فرم'));
    await user.click(buttons[1]!);

    await user.click(changeCompanyButton());
    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    expect(dialog.textContent).toContain('۲ فرم');
    expect(dialog.querySelectorAll('.faranou-problemtab-title')).toHaveLength(1);
  });

  it('cancel aborts the flow: no navigation, no context change, forms and tabs untouched', async () => {
    const user = userEvent.setup();
    const onChangeContext = vi.fn();
    setup(onChangeContext);
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await makeDirty('/form', 'خراب‌کردن فرم');
    await user.click(changeCompanyButton());
    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    await user.click(within(dialog).getByRole('button', { name: 'انصراف' }));
    expect(onChangeContext).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/app/form');
    expect(tabButton(/فرم آزمایشی/)).toBeInTheDocument();
    expect(tabButton(/فرم آزمایشی/).textContent).toContain('دارای تغییر ذخیره‌نشده');
  });
});
