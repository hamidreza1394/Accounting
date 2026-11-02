import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { AppShell } from './AppShell';

const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};
const admin: Session = { username: 'admin', fullName: 'مدیر سیستم', accountKind: 'User', isAdministrator: true };

describe('permission catalog', () => {
  it('displays only registered permission definitions and supports search', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify([
      { id: 'permission-1', code: 'trade.form.party.view', name: 'مشاهده طرف حساب', description: 'اجازه مشاهده طرف حساب', subsystemCode: 'trade' },
      { id: 'permission-2', code: 'trade.form.party.create', name: 'ایجاد طرف حساب', description: 'اجازه ایجاد طرف حساب', subsystemCode: 'trade' },
    ]), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    render(<AppShell session={admin} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>داشبورد</main></AppShell>);
    fireEvent.click(screen.getByRole('button', { name: 'کاتالوگ و جزئیات مجوزها' }));

    expect(await screen.findByRole('heading', { name: 'کاتالوگ و جزئیات مجوزها' })).toBeInTheDocument();
    expect(await screen.findByText('مشاهده طرف حساب')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('جستجوی مجوز'), { target: { value: 'party.create' } });
    expect(screen.getByText('ایجاد طرف حساب')).toBeInTheDocument();
    expect(screen.queryByText('مشاهده طرف حساب')).toBeNull();
  });
});
