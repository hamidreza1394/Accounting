import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { AppShell } from './AppShell';

const session: Session = { username: 'hamid', fullName: 'حمید رضایی', accountKind: 'User', isAdministrator: false };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر مالی' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

describe('AppShell', () => {
  it('places the Faranou brand, company and fiscal period selectors, and user identity in the header', () => {
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    expect(screen.getByRole('img', { name: 'نشان فرانو' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تغییر شرکت' })).toHaveTextContent('شرکت نمونه');
    expect(screen.getByRole('button', { name: 'تغییر دوره مالی' })).toHaveTextContent('سال مالی ۱۴۰۵');
    expect(screen.getByText('حمید رضایی')).toBeInTheDocument();
    expect(screen.getByText('مدیر مالی')).toBeInTheDocument();
  });

  it('opens the personal account center, loads only self data, and supports accessible tabs', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        username: 'alice',
        firstName: 'آلیس',
        lastName: 'مثالی',
        emailAddress: 'alice@example.test',
        cellPhoneNumber: '09120000000',
        isEmailAddressVerified: true,
        isCellPhoneNumberVerified: false,
        lastLoginDateTime: '2026-11-02T08:00:00Z',
        insertDateTime: '2026-10-01T09:00:00Z',
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    const trigger = screen.getByRole('button', { name: 'حساب کاربری من' });
    fireEvent.click(trigger);

    const dialog = await screen.findByRole('dialog', { name: 'حساب کاربری من' });
    expect(await screen.findByText('آلیس مثالی')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'مشخصات من' })).toHaveAttribute('aria-selected', 'true');

    fireEvent.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(await screen.findByText('سابقه‌ای ثبت نشده است')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]?.[0]).toBe('/api/v1/me/login-history?take=50');

    fireEvent.keyDown(dialog, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('submits the authenticated user password change and invokes reauthentication', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        username: 'alice', firstName: 'آلیس', lastName: 'مثالی', emailAddress: null, cellPhoneNumber: null,
        isEmailAddressVerified: false, isCellPhoneNumberVerified: false,
        lastLoginDateTime: null, insertDateTime: '2026-10-01T09:00:00Z',
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ headerName: 'X-XSRF-TOKEN', requestToken: 'token-123' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    const onPasswordChanged = vi.fn();

    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)} onPasswordChanged={onPasswordChanged}><main>محتوای صفحه</main></AppShell>);
    fireEvent.click(screen.getByRole('button', { name: 'حساب کاربری من' }));
    await screen.findByRole('dialog', { name: 'حساب کاربری من' });
    fireEvent.click(screen.getByRole('tab', { name: 'امنیت حساب' }));

    fireEvent.change(screen.getByLabelText('رمز فعلی'), { target: { value: 'Current-Phrase-1405!' } });
    fireEvent.change(screen.getByLabelText('رمز جدید'), { target: { value: 'New-Long-Phrase-1406!' } });
    fireEvent.change(screen.getByLabelText('تکرار رمز جدید'), { target: { value: 'New-Long-Phrase-1406!' } });
    fireEvent.click(screen.getByRole('button', { name: 'تغییر رمز و خروج' }));

    await waitFor(() => expect(onPasswordChanged).toHaveBeenCalledOnce());
    expect(fetchMock.mock.calls[2]?.[0]).toBe('/api/v1/me/password');
  });

  it('keeps unfinished navigation items disabled and the current dashboard active', () => {
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    expect(screen.getByRole('link', { name: /داشبورد/ })).toHaveAttribute('aria-current', 'page');
    const accountingLabels = screen.getAllByText('حسابداری');
    expect(accountingLabels.some((label) => label.closest('[aria-disabled="true"]'))).toBe(true);
  });

  it('uses the premium dark shell matching the approved mockup', () => {
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    expect(document.querySelector('.faranou-topbar')).toHaveClass('faranou-topbar--login-palette');
    expect(document.querySelector('.faranou-sidebar')).toHaveClass('faranou-sidebar--premium');
    expect(document.querySelector('.faranou-sidebar')).not.toHaveClass('faranou-sidebar--light');
    const sidebar = document.querySelector('.faranou-sidebar');
    expect(sidebar?.querySelector('nav > p')).toBeTruthy();
    expect(sidebar?.querySelector('[aria-disabled="true"]')).toBeTruthy();
    expect(sidebar?.querySelector('.faranou-sidebar-footer p')).toBeTruthy();
  });

  it('nests user-management items under system settings as disabled placeholders', () => {
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    expect(screen.getByRole('button', { name: /تنظیمات سیستم/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /مدیریت کاربری/ })).toBeInTheDocument();
    expect(screen.getByText('مدیریت کاربران').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(screen.getByText('مدیریت دسترسی‌ها').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(screen.getByText('مشاهده ورود و خروج کاربران').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(screen.getByText('لیست کاربران فعال').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('keeps full nested menu labels readable instead of truncating them', () => {
    // Note (Issue #442 review, Finding 4): JSDOM has no real layout engine, so this test
    // is a proxy — it confirms the label keeps its full text and the `truncate`/
    // `whitespace-normal` classes that drive wrapping, not that the text actually avoids
    // clipping in a rendered browser at narrow widths. That was confirmed separately with
    // a real Playwright screenshot of the built app's sidebar (see handoff for Issue #442).
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    const longLabel = screen.getByText('مشاهده ورود و خروج کاربران');
    expect(longLabel).toHaveTextContent('مشاهده ورود و خروج کاربران');
    expect(longLabel.className).not.toContain('truncate');
    expect(longLabel.className).toContain('whitespace-normal');
  });

  it('keeps the "coming soon" badge visible (not hover/screen-reader-only) on nested disabled items', () => {
    // Issue #442 review (Finding 3): a bare dot that only surfaced its meaning via a
    // `title` tooltip or a visually-hidden span was invisible to touch users. Nested rows
    // now carry the same always-visible text badge as top-level rows, just smaller.
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    const nestedItem = screen.getByText('مدیریت کاربران').closest('[aria-disabled="true"]') as HTMLElement;
    expect(nestedItem).toBeTruthy();
    expect(nestedItem).toHaveTextContent('به‌زودی');
    expect(nestedItem.querySelector('.faranou-nav-soon-dot')).toBeFalsy();

    const topLevelItem = screen.getByText('حسابداری').closest('[aria-disabled="true"]') as HTMLElement;
    expect(topLevelItem).toHaveTextContent('به‌زودی');
  });

  it('shows the full read-only and reporting-only access explanation', () => {
    const readOnlyContext: SelectedContext = {
      ...context,
      fiscalPeriod: { ...context.fiscalPeriod, accessMode: 'readOnly' },
    };

    render(<AppShell session={session} context={readOnlyContext} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);

    expect(screen.getByLabelText('زمینه کاری جاری')).toHaveTextContent('فقط مشاهده و گزارش‌گیری');
  });
});
