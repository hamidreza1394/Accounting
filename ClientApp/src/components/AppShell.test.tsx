import { render, screen } from '@testing-library/react';
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
    expect(screen.getByText('حسابداری').closest('[aria-disabled="true"]')).toBeInTheDocument();
  });
});
