import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { AppShell } from './AppShell';

const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

const adminSession: Session = { username: 'admin', fullName: 'مدیر سیستم', accountKind: 'User', isAdministrator: true };

describe('user administration navigation', () => {
  it('loads the server-paged user list and selected user details', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        items: [{
          id: 'user-1', username: 'alice', firstName: 'آلیس', lastName: 'مثالی',
          emailAddress: 'alice@example.test', cellPhoneNumber: null, isActive: true,
          lastLoginDateTime: '2026-11-02T08:00:00Z', insertDateTime: '2026-10-01T09:00:00Z',
          activeCompanyCount: 1,
        }],
        totalCount: 1, page: 1, pageSize: 20,
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: 'user-1', username: 'alice', firstName: 'آلیس', lastName: 'مثالی',
        emailAddress: 'alice@example.test', cellPhoneNumber: null, isEmailAddressVerified: true,
        isCellPhoneNumberVerified: false, isActive: true, lastLoginDateTime: '2026-11-02T08:00:00Z',
        insertDateTime: '2026-10-01T09:00:00Z', updateDateTime: '2026-11-01T09:00:00Z',
        legacyRoleName: 'Accountant',
        memberships: [{
          companyId: 'company-1', companyName: 'شرکت نمونه', roleCode: 'ACCOUNTANT',
          roleName: 'حسابدار', isActive: true, createdAtUtc: '2026-10-01T09:00:00Z',
        }],
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    render(<AppShell session={adminSession} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>داشبورد</main></AppShell>);
    fireEvent.click(screen.getByRole('button', { name: 'مدیریت کاربران' }));

    expect(await screen.findByRole('heading', { name: 'مدیریت کاربران' })).toBeInTheDocument();
    expect(await screen.findByText('آلیس مثالی')).toBeInTheDocument();
    expect(await screen.findByText('حسابدار · ACCOUNTANT')).toBeInTheDocument();
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/v1/admin/users?');
    expect(String(fetchMock.mock.calls[1]?.[0])).toBe('/api/v1/admin/users/user-1');
    fireEvent.click(screen.getByRole('button', { name: 'کاربر جدید' }));
    expect(screen.getByRole('dialog', { name: 'ایجاد کاربر جدید' })).toBeInTheDocument();
    expect(screen.getByLabelText('رمز عبور اولیه')).toBeRequired();
    expect(screen.getByLabelText('تکرار رمز عبور')).toBeRequired();
  });

  it('does not show installation administration navigation to a regular user', () => {
    const regular: Session = { ...adminSession, isAdministrator: false };
    render(<AppShell session={regular} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>داشبورد</main></AppShell>);
    expect(screen.queryByRole('button', { name: 'مدیریت سیستم' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'مدیریت کاربران' })).toBeNull();
  });
});
