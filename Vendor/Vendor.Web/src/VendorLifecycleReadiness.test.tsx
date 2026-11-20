import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { VendorLifecycleReadiness } from './VendorLifecycleReadiness';

const customersPage = (items: unknown[]) => ({
  items, page: 1, pageSize: 100, totalCount: items.length,
  totalPages: 1, hasPreviousPage: false, hasNextPage: false,
});

const customer = { id: 'c-1', customerNumber: 'C-1001', legalName: 'شرکت نمونه', displayName: null };

const readinessResponse = {
  customerId: 'c-1',
  customerNumber: 'C-1001',
  displayName: 'شرکت نمونه',
  generatedAtUtc: '2026-11-20T12:00:00Z',
  stages: [
    { key: 'customerRegistration', status: 'Ready', reason: null, referenceId: null, changedAtUtc: null, correctiveAction: null },
    { key: 'payment', status: 'Pending', reason: 'پرداخت ثبت شده اما هنوز تأیید مستقل نشده است.', referenceId: null, changedAtUtc: null, correctiveAction: 'درخواست تأیید پرداخت را ارسال کنید.' },
    { key: 'amendment', status: 'Failed', reason: 'آخرین Amendment رد شد؛ وضعیت پرداخت مستقل باقی مانده است.', referenceId: null, changedAtUtc: '2026-11-20T10:00:00Z', correctiveAction: 'درخواست اصلاح‌شده جدیدی برای همان ردیف ثبت کنید.' },
    { key: 'runtimeGate', status: 'Blocked', reason: 'گیت CompanyReference بسته است (#392): فقط اقلام Installation-scoped قابل صدور هستند و Company-scoped هرگز Ready اعلام نمی‌شود.', referenceId: null, changedAtUtc: null, correctiveAction: 'پس از حل #392 و تأیید مالک، گیت را فعال کنید.' },
  ],
};

function response(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status, headers: { 'Content-Type': 'application/json' },
  });
}

describe('Vendor lifecycle readiness diagnostics', () => {
  beforeEach(() => { vi.unstubAllGlobals(); });
  afterEach(() => { vi.unstubAllGlobals(); });

  it('renders the RTL stage table with real statuses, reasons and corrective actions', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => { void init;
      const url = String(input);
      if (url.startsWith('/api/vendor/customers/?')) return Promise.resolve(response(customersPage([customer])));
      if (url === '/api/vendor/customers/c-1/readiness') return Promise.resolve(response(readinessResponse));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorLifecycleReadiness permissions={['vendor.customers.read']} />);

    const section = document.querySelector('section[aria-labelledby="vendor-readiness-title"]');
    expect(section?.getAttribute('dir')).toBe('rtl');

    fireEvent.click(await screen.findByText('شرکت نمونه'));
    expect(await screen.findByText('چرخه شرکت نمونه')).toBeInTheDocument();
    expect(screen.getByText('آماده')).toBeInTheDocument();
    expect(screen.getByText('در جریان')).toBeInTheDocument();
    expect(screen.getByText('ناموفق')).toBeInTheDocument();
    expect(screen.getByText('مسدود')).toBeInTheDocument();
    // The #392 gate must never look Ready while it is closed.
    expect(screen.getByText(/گیت CompanyReference بسته است/)).toBeInTheDocument();
    expect(screen.getByText(/درخواست اصلاح‌شده جدیدی/)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/vendor/customers/c-1/readiness',
      expect.objectContaining({ credentials: 'same-origin' }),
    );
  });

  it('keeps the page read-only and explains missing permission or customer', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => { void init;
      const url = String(input);
      if (url.startsWith('/api/vendor/customers/?')) return Promise.resolve(response(customersPage([customer])));
      if (url === '/api/vendor/customers/c-1/readiness') return Promise.resolve(response({ error: 'x' }, 404));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorLifecycleReadiness permissions={['vendor.customers.read']} />);

    fireEvent.click(await screen.findByText('شرکت نمونه'));
    expect(await screen.findByRole('alert')).toHaveTextContent('مشتری موردنظر یافت نشد');
    // Read-only: only GET requests are ever issued by this page.
    expect(fetchMock.mock.calls.every(([, init]) => !(init as RequestInit | undefined)?.method)).toBe(true);

    cleanup();
    render(<VendorLifecycleReadiness permissions={['vendor.payments.read']} />);
    expect(await screen.findByText('مجوز مشاهده مشتریان برای این صفحه لازم است.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'شرکت نمونه' })).not.toBeInTheDocument();
  });
});
