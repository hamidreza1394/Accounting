import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { AppShell } from './AppShell';

const session: Session = { username: 'alice', fullName: 'آلیس مثالی', accountKind: 'User', isAdministrator: false };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'کاربر' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

describe('MyAccountPanel', () => {
  it('shows only the authenticated profile and loads its own history on demand', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        username: 'alice', firstName: 'آلیس', lastName: 'مثالی', emailAddress: null, cellPhoneNumber: null,
        isEmailAddressVerified: false, isCellPhoneNumberVerified: false,
        lastLoginDateTime: '2026-11-02T08:00:00Z', insertDateTime: '2026-10-01T09:00:00Z',
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    const trigger = render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای آزمایشی</main></AppShell>);
    fireEvent.click(screen.getByRole('button', { name: 'حساب کاربری من' }));

    const dialog = await screen.findByRole('dialog', { name: 'حساب کاربری من' });
    expect((await screen.findAllByText('آلیس مثالی')).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(await screen.findByText('سابقه‌ای ثبت نشده است')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]?.[0]).toBe('/api/v1/me/login-history?take=50');

    fireEvent.keyDown(dialog, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(trigger.getByRole('button', { name: 'حساب کاربری من' })).toHaveAttribute('aria-expanded', 'false');
  });
});
