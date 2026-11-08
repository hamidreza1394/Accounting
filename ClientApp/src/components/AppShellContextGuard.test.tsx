import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AppShell } from './AppShell';
import { resetFormsForTests, useFormGuard } from '@/lib/formGuard';
import type { AccessSnapshot } from '@/lib/permissions';
import type { SelectedContext, Session } from '../api/types';

const session: Session = { username: 'hamid', fullName: 'حمید رضایی', accountKind: 'User', isAdministrator: false };
const access: AccessSnapshot = { status: 'ready', isInstallationAdministrator: true, companyId: 'company-1', companyCodes: [] };

const companyA: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت الف', nationalId: null, roleName: 'مدیر سیستم' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};
const companyB: SelectedContext = {
  company: { id: 'company-2', name: 'شرکت ب', nationalId: null, roleName: 'مدیر سیستم' },
  fiscalPeriod: { id: 'period-2', title: 'سال مالی ۱۴۰۶', startDate: '2027-03-21', endDate: '2028-03-19', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

function GuardedFormPage({ label }: { label: string }) {
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useFormGuard({ dirty, saving, errorMessage: error, title: label });
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

/** An independent, installation-wide page (its tab must survive a context change). */
function GlobalPage() {
  return <h1>مدیریت کاربران</h1>;
}

const routes = [
  { path: '/', element: () => <h1>داشبورد</h1>, title: 'داشبورد' },
  { path: '/admin/users', element: GlobalPage, title: 'مدیریت کاربران' },
  { path: '/hr/org/employees', element: () => <GuardedFormPage label="کارکنان" />, title: 'کارکنان' },
  { path: '/form', element: () => <GuardedFormPage label="فرم آزمایشی" />, title: 'فرم آزمایشی' },
] as const;

const tabbar = () => screen.getByRole('navigation', { name: 'تب‌های کاری باز' });
function tabNames(): string[] {
  return [...tabbar().querySelectorAll<HTMLButtonElement>('.faranou-tab-button')].map((button) => button.textContent ?? '');
}
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
async function driveForm(paneKey: string, action: 'خراب‌کردن فرم' | 'شروع ذخیره' | 'شکست ذخیره' | 'پاک‌سازی فرم') {
  const pane = document.querySelector(`[data-tab-pane="${CSS.escape(paneKey)}"]`)!;
  const button = [...pane.querySelectorAll<HTMLButtonElement>('button')].find((candidate) => candidate.textContent?.trim() === action);
  if (!button) throw new Error(`${action} not found in ${paneKey}`);
  await userEvent.setup().click(button);
}
const changeButton = () => screen.getByRole('button', { name: /شرکت جاری شرکت الف، تغییر شرکت/ });

type SetupOptions = { context?: SelectedContext; onChangeContext?: () => void };
function setup(options: SetupOptions = {}) {
  window.history.pushState(null, '', '/app/');
  const view = render(
    <AppShell
      session={session}
      context={options.context ?? companyA}
      access={access}
      routes={routes}
      workspaceTabs
      onChangeContext={options.onChangeContext ?? vi.fn()}
      onLogout={vi.fn().mockResolvedValue(undefined)}
    >
      <main>جایگزین</main>
    </AppShell>,
  );
  return view;
}

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
});

describe('the context-change guard (Issue #444 stage 1 completion; D-076 flow)', () => {
  it('a fully clean workspace goes straight to the final confirmation, and confirming hands over', async () => {
    const user = userEvent.setup();
    const onChangeContext = vi.fn();
    setup({ onChangeContext });
    await waitFor(() => tabButton(/داشبورد/));
    await user.click(changeButton());
    const confirm = await screen.findByRole('dialog', { name: 'تأیید نهایی تغییر شرکت یا دورهٔ مالی' });
    await user.click(within(confirm).getByRole('button', { name: 'ادامه و انتخاب زمینهٔ جدید' }));
    expect(onChangeContext).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['Dirty', 'خراب‌کردن فرم'],
    ['Saving', 'شروع ذخیره'],
    ['Error', 'شکست ذخیره'],
  ] as const)('a %s form blocks the context change and nothing is discarded', async (label, action) => {
    const user = userEvent.setup();
    const onChangeContext = vi.fn();
    setup({ onChangeContext });
    await navigate('/app/form');
    await waitFor(() => tabButton(/فرم آزمایشی/));
    await driveForm('/form', action);

    await user.click(changeButton());
    const dialog = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    expect(dialog.textContent).toContain('فرم آزمایشی');
    expect(onChangeContext).not.toHaveBeenCalled();
    // The workspace is untouched: tabs, URL and the form state all remain.
    expect(window.location.pathname).toBe('/app/form');
    expect(tabNames().some((name) => name.includes('فرم آزمایشی'))).toBe(true);

    // Resolve the form, then the final confirmation appears automatically.
    await driveForm('/form', 'پاک‌سازی فرم');
    const confirm = await screen.findByRole('dialog', { name: 'تأیید نهایی تغییر شرکت یا دورهٔ مالی' });
    await user.click(within(confirm).getByRole('button', { name: 'انصراف' }));
    expect(onChangeContext).not.toHaveBeenCalled();
    void label;
  });

  it('cancelling at the final confirmation keeps the company, the period and the tabs', async () => {
    const user = userEvent.setup();
    const onChangeContext = vi.fn();
    setup({ onChangeContext });
    await navigate('/app/hr/org/employees');
    await waitFor(() => tabButton(/کارکنان/));
    await user.click(changeButton());
    const confirm = await screen.findByRole('dialog', { name: 'تأیید نهایی تغییر شرکت یا دورهٔ مالی' });
    await user.click(within(confirm).getByRole('button', { name: 'انصراف' }));
    expect(onChangeContext).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/app/hr/org/employees');
    expect(tabNames()).toEqual(['داشبورد', 'کارکنان']);
  });

  it('after the context actually changed, context-dependent tabs close and global ones survive', async () => {
    const onChangeContext = vi.fn();
    const view = setup({ context: companyA, onChangeContext });
    await navigate('/app/admin/users'); // global (installation-wide)
    await navigate('/app/hr/org/employees'); // company-dependent
    await waitFor(() => tabButton(/کارکنان/));
    expect(tabNames()).toEqual(['داشبورد', 'مدیریت کاربران', 'کارکنان']);

    // The confirmed change succeeded: App keeps the SAME shell mounted under the selection
    // screen and it re-renders with the new context; the janitor closes dependent tabs.
    view.rerender(
      <AppShell
        session={session}
        context={companyB}
        access={access}
        routes={routes}
        workspaceTabs
        onChangeContext={onChangeContext}
        onLogout={vi.fn().mockResolvedValue(undefined)}
      >
        <main>جایگزین</main>
      </AppShell>,
    );

    await waitFor(() => expect(tabNames()).toEqual(['مدیریت کاربران']));
    // The URL followed the surviving global tab (its pane stayed mounted all along).
    expect(window.location.pathname).toBe('/app/admin/users');
    expect(tabButton(/مدیریت کاربران/)).toHaveAttribute('aria-current', 'page');
  });

  it('direct URL navigation and back/forward never start a context change', async () => {
    const onChangeContext = vi.fn();
    setup({ onChangeContext });
    await waitFor(() => tabButton(/داشبورد/));
    await navigate('/app/admin/users');
    expect(tabButton(/مدیریت کاربران/)).toHaveAttribute('aria-current', 'page');
    // URL navigation alone never touches the work context (the server cookie decides).
    expect(onChangeContext).not.toHaveBeenCalled();
    window.history.back();
    window.dispatchEvent(new PopStateEvent('popstate'));
    await waitFor(() => expect(window.location.pathname).toBe('/app/'));
    expect(onChangeContext).not.toHaveBeenCalled();
  });

  it('repeated clicks never start a second change while the flow is open', async () => {
    const user = userEvent.setup();
    const onChangeContext = vi.fn();
    setup({ onChangeContext });
    await waitFor(() => tabButton(/داشبورد/));
    await user.click(changeButton());
    const confirm = await screen.findByRole('dialog', { name: 'تأیید نهایی تغییر شرکت یا دورهٔ مالی' });
    // Clicking the header buttons again while a stage is open does nothing harmful.
    await user.click(changeButton());
    await user.click(changeButton());
    await user.click(within(confirm).getByRole('button', { name: 'ادامه و انتخاب زمینهٔ جدید' }));
    expect(onChangeContext).toHaveBeenCalledTimes(1);
  });
});
