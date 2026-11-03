import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

  it('shows the focused system-administration menu and removes redundant user-list shortcuts', () => {
    render(<AppShell session={{ ...session, isAdministrator: true }} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    expect(screen.getByRole('button', { name: /مدیریت سیستم/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'مدیریت کاربران' })).toHaveAttribute('href', '/app/admin/users');
    expect(screen.getByRole('link', { name: 'گروه‌های دسترسی' })).toHaveAttribute('href', '/app/admin/roles');
    expect(screen.getByRole('link', { name: 'کاتالوگ و جزئیات مجوزها' })).toHaveAttribute('href', '/app/admin/permissions');
    for (const label of ['ساختار سازمانی و سمت‌ها', 'گزارش امنیت و سوابق', 'سیاست‌های امنیتی']) {
      expect(screen.getByText(label).closest('[aria-disabled="true"]')).toBeTruthy();
    }
    expect(screen.queryByText('لیست کاربران فعال')).toBeNull();
    expect(screen.queryByText('مشاهده ورود و خروج کاربران')).toBeNull();
  });

  it('keeps the new administration labels readable without truncation', () => {
    // JSDOM cannot measure visual wrapping, but it can guard the full label and wrapping classes.
    render(<AppShell session={{ ...session, isAdministrator: true }} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    const longLabel = screen.getByText('ساختار سازمانی و سمت‌ها');
    expect(longLabel).toHaveTextContent('ساختار سازمانی و سمت‌ها');
    expect(longLabel.className).not.toContain('truncate');
    expect(longLabel.className).toContain('whitespace-normal');
  });

  it('keeps the "coming soon" badge visible (not hover/screen-reader-only) on nested disabled items', () => {
    // Issue #442 review (Finding 3): a bare dot that only surfaced its meaning via a
    // `title` tooltip or a visually-hidden span was invisible to touch users. Nested rows
    // now carry the same always-visible text badge as top-level rows, just smaller.
    render(<AppShell session={{ ...session, isAdministrator: true }} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای صفحه</main></AppShell>);
    const nestedItem = screen.getByText('ساختار سازمانی و سمت‌ها').closest('[aria-disabled="true"]') as HTMLElement;
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

  it('offers a skip link as the first focusable element that moves focus to the main content', async () => {
    const user = userEvent.setup();
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><p>محتوای صفحه</p></AppShell>);
    const skip = screen.getByRole('link', { name: 'پرش به محتوای اصلی' });
    expect(skip).toHaveAttribute('href', '#main-content');
    // Hidden until focused (sr-only), visible on focus.
    expect(skip).toHaveClass('sr-only', 'focus:not-sr-only');
    await user.tab();
    expect(skip).toHaveFocus();
    await user.keyboard('{Enter}');
    const main = document.getElementById('main-content');
    expect(main?.tagName).toBe('MAIN');
    expect(main).toHaveFocus();
    expect(window.location.hash).toBe('');
  });

  it('shows a logout failure in the shared error Alert', async () => {
    const user = userEvent.setup();
    render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockRejectedValue(new Error('خروج ناموفق بود'))}><p>محتوای صفحه</p></AppShell>);
    await user.click(screen.getByRole('button', { name: 'خروج' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('خروج ناموفق بود');
  });
});
