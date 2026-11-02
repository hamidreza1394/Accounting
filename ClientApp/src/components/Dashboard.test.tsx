import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { Dashboard } from './Dashboard';

vi.mock('./CompanyOverviewPanel', () => ({ CompanyOverviewPanel: () => <div>اطلاعات واقعی شرکت</div> }));

const session: Session = { username: 'demo', fullName: 'کاربر آزمایشی', accountKind: 'User', isAdministrator: false };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: '1234567890', roleName: 'حسابدار' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

describe('Dashboard', () => {
  it('shows the authenticated workspace identity and selected fiscal period', () => {
    render(<Dashboard session={session} context={context} onUnauthenticated={vi.fn(() => false)} />);
    expect(screen.getByRole('heading', { name: /به سامانه یکپارچه فرانو خوش آمدید/ })).toBeInTheDocument();
    expect(screen.getAllByText('شرکت نمونه').length).toBeGreaterThan(0);
    expect(screen.getAllByText('سال مالی ۱۴۰۵').length).toBeGreaterThan(0);
    expect(screen.getAllByText('حسابدار').length).toBeGreaterThan(0);
    expect(screen.getByText('مدیریت هوشمند کسب‌وکار با دقت، شفافیت و امنیت بیشتر.')).toBeInTheDocument();
    expect(screen.getByText('مدیریت هوشمند کسب‌وکار با دقت، شفافیت و امنیت بیشتر.').closest('section')).not.toHaveTextContent('کاربر آزمایشی');
    expect(screen.getByText('اطلاعات واقعی شرکت')).toBeInTheDocument();
  });

  it('uses the local Tehran panorama and the wide welcome composition', () => {
    render(<Dashboard session={session} context={context} onUnauthenticated={vi.fn(() => false)} />);
    const panorama = screen.getByAltText('');
    expect(panorama).toHaveAttribute('src', '/images/dashboard-tehran-panorama.svg');
    expect(panorama).toHaveAttribute('aria-hidden', 'true');
    expect(panorama.closest('section')).toHaveClass('faranou-welcome');
    expect(screen.getByRole('heading', { name: /به سامانه یکپارچه فرانو خوش آمدید/ }).closest('section')).toHaveClass('faranou-welcome');
  });

  it('uses the reference dashboard hierarchy without inventing financial metrics', () => {
    render(<Dashboard session={session} context={context} onUnauthenticated={vi.fn(() => false)} />);
    expect(document.querySelectorAll('.faranou-summary-card')).toHaveLength(4);
    expect(document.querySelectorAll('.faranou-panel')).toHaveLength(3);
    expect(screen.getByText('داده‌ای برای ترسیم نمودار در دسترس نیست')).toBeInTheDocument();
    expect(screen.getAllByText('در حال توسعه').length).toBe(6);
    expect(screen.queryByText(/۱۲۳,۴۵۰,۰۰۰/)).not.toBeInTheDocument();
  });

  it('clearly identifies a closed fiscal period as read-only', () => {
    const readOnlyContext: SelectedContext = { ...context, fiscalPeriod: { ...context.fiscalPeriod, status: 'Closed', accessMode: 'readOnly' } };
    render(<Dashboard session={session} context={readOnlyContext} onUnauthenticated={vi.fn(() => false)} />);
    expect(screen.getByText('فقط مشاهده و گزارش‌گیری')).toBeInTheDocument();
  });
});
