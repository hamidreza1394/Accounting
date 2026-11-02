import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { AppShell } from './AppShell';

const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};
const admin: Session = { username: 'admin', fullName: 'مدیر سیستم', accountKind: 'User', isAdministrator: true };

describe('access group management', () => {
  it('shows registered permissions and applies an available template in the editor', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: 'role-1', companyId: 'company-1', code: 'TRADE_VIEWER', name: 'مشاهده بازرگانی',
        isSystemRole: false, isActive: true, permissionCount: 1, activeMemberCount: 2,
        permissionCodes: ['trade.form.party.view'],
      }]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([
        { id: 'permission-1', code: 'trade.form.party.view', name: 'مشاهده طرف حساب', description: null, subsystemCode: 'trade' },
        { id: 'permission-2', code: 'trade.form.party.create', name: 'ایجاد طرف حساب', description: null, subsystemCode: 'trade' },
      ]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([
        { code: 'trade.viewer', name: 'بازرگانی — مشاهده', permissionCodes: ['trade.form.party.view'] },
        { code: 'trade.operator', name: 'بازرگانی — کارشناس عملیاتی', permissionCodes: ['trade.form.party.view', 'trade.form.party.create'] },
      ]), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    render(<AppShell session={admin} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>داشبورد</main></AppShell>);
    fireEvent.click(screen.getByRole('button', { name: 'گروه‌های دسترسی' }));

    expect(await screen.findByRole('heading', { name: 'گروه‌های دسترسی' })).toBeInTheDocument();
    expect(await screen.findByText('مشاهده بازرگانی')).toBeInTheDocument();
    expect(fetchMock.mock.calls.map(call => String(call[0]))).toEqual(expect.arrayContaining([
      '/api/v1/admin/companies/company-1/roles',
      '/api/v1/admin/permission-catalog',
      '/api/v1/admin/permission-templates',
    ]));

    fireEvent.click(screen.getByRole('button', { name: 'گروه جدید' }));
    expect(screen.getByRole('dialog', { name: 'ساخت گروه دسترسی' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('الگوی اولیه'), { target: { value: 'trade.operator' } });
    expect(screen.getByLabelText('ایجاد طرف حساب')).toBeChecked();
  });
});
