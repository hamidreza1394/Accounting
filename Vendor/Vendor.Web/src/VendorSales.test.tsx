import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { VendorSales } from './VendorSales';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function page(items: unknown[]) {
  return { items, page: 1, pageSize: 25, totalCount: items.length, totalPages: 1, hasPreviousPage: false, hasNextPage: false };
}

const collectibleOrder = {
  id: 'order-1',
  customerId: 'customer-1',
  customerNumber: 'PAY-1',
  orderNumber: 'SO-1405-001',
  totalAmount: 1200,
  currencyCode: 'IRR',
  dueAtUtc: '2026-11-15T00:00:00Z',
  createdAtUtc: '2026-11-01T08:00:00Z',
  paymentStatus: 'PartiallyPaid',
  overdue: true,
  verifiedPaidAmount: 300,
  remainingAmount: 900,
  rowVersion: 'AQIDBAUGBwg=',
};

const overpaidOrder = {
  id: 'order-2',
  customerId: 'customer-1',
  customerNumber: 'PAY-1',
  orderNumber: 'SO-1405-002',
  totalAmount: 1000,
  currencyCode: 'IRR',
  dueAtUtc: null,
  createdAtUtc: '2026-11-01T09:00:00Z',
  paymentStatus: 'Paid',
  overdue: false,
  verifiedPaidAmount: 1200,
  remainingAmount: 0,
  rowVersion: 'AQIDBAUGBwg=',
};

function ordersPage(items: Array<Record<string, unknown>>) {
  // List rows arrive without line/payment details; the detail endpoint serves those.
  return page(items.map(item => ({ ...item, lines: [], payments: [] })));
}

const orderDetail = {
  ...collectibleOrder,
  lines: [{ id: 'line-1', productCode: 'ADDON-COMPANY', description: 'افزایش ظرفیت شرکت', quantity: 3, unitAmount: 400, lineAmount: 1200, licenseId: null }],
  payments: [
    {
      id: 'payment-1', orderId: 'order-1', amount: 300, currencyCode: 'IRR', method: 'bank_transfer',
      externalReference: 'RCPT-9', claimedPaidAtUtc: '2026-11-02T10:00:00Z', status: 'Recorded',
      verifiedByUserId: null, verifiedAtUtc: null, rejectionReason: null, rowVersion: 'AQIDBAUGBwg=',
    },
    {
      id: 'payment-2', orderId: 'order-1', amount: 100, currencyCode: 'IRR', method: 'cheque',
      externalReference: 'CHQ-3', claimedPaidAtUtc: '2026-11-03T10:00:00Z', status: 'Rejected',
      verifiedByUserId: null, verifiedAtUtc: null, rejectionReason: 'مرجع رسید تأیید نشد', rowVersion: 'AQIDBAUGBwg=',
    },
  ],
};

function baseFetchMock(overrides: Record<string, (init?: RequestInit) => Promise<unknown>> = {}) {
  const calls: Array<{ url: string; init?: RequestInit | undefined; body?: unknown }> = [];
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const body = init?.body ? (JSON.parse(String(init.body)) as unknown) : undefined;
    calls.push({ url, init, body });
    if (url.startsWith('/api/vendor/sales-orders/?')) return Promise.resolve(response(ordersPage([collectibleOrder, overpaidOrder])));
    if (url.startsWith('/api/vendor/receivables/?')) return Promise.resolve(response(page([{
      id: 'order-1', customerId: 'customer-1', customerNumber: 'PAY-1', orderNumber: 'SO-1405-001',
      totalAmount: 1200, currencyCode: 'IRR', dueAtUtc: '2026-11-15T00:00:00Z',
      paymentStatus: 'PartiallyPaid', overdue: true, verifiedPaidAmount: 300, remainingAmount: 900,
    }])));
    if (url.startsWith('/api/vendor/customers/')) return Promise.resolve(response(page([
      { id: 'customer-1', customerNumber: 'PAY-1', legalName: 'شرکت نمونه', displayName: 'نمونه' },
    ])));
    if (url.startsWith('/api/vendor/contracts/')) return Promise.resolve(response(page([
      { id: 'contract-1', contractNumber: 'CT-1' },
    ])));
    if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-sales' }));
    if (url === `/api/vendor/sales-orders/${collectibleOrder.id}`) return Promise.resolve(response(orderDetail));
    for (const [match, handler] of Object.entries(overrides)) {
      if (url.startsWith(match)) return handler(init);
    }
    return Promise.resolve(response({}, 404));
  });
  return { fetchMock, calls };
}

const allPermissions = ['vendor.payments.read', 'vendor.payments.manage'];

describe('Vendor sales and payments workspace', () => {
  it('renders the RTL order list with derived debt, overdue flag and payment statuses', async () => {
    const { fetchMock } = baseFetchMock();
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={allPermissions} />);

    expect(await screen.findByText('SO-1405-001')).toBeInTheDocument();
    const section = document.querySelector('section[aria-labelledby="vendor-sales-title"]');
    expect(section?.getAttribute('dir')).toBe('rtl');

    expect(await screen.findByText('SO-1405-001')).toBeInTheDocument();
    expect(screen.getByText('SO-1405-002')).toBeInTheDocument();
    expect(screen.getAllByText(/نیمه‌پرداخت‌شده/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/معوق/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/پرداخت‌شده/).length).toBeGreaterThan(0);
  });

  it('shows the open receivables report on the second tab', async () => {
    const { fetchMock } = baseFetchMock();
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={allPermissions} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'مطالبات باز' }))[0]!);
    expect(await screen.findByText(/گزارش زمان‌خوان مانده بدهی/)).toBeInTheDocument();
    expect(await screen.findByText('SO-1405-001')).toBeInTheDocument();
  });

  it('creates a sales order with a fresh antiforgery token and matching line totals', async () => {
    const { fetchMock, calls } = baseFetchMock({
      '/api/vendor/sales-orders': () => Promise.resolve(response({ id: 'order-new' }, 201)),
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={allPermissions} />);

    fireEvent.click(await screen.findByRole('button', { name: /سفارش جدید/ }));
    const customerSelect = (await screen.findByLabelText('انتخاب مشتری')) as HTMLSelectElement;
    fireEvent.change(customerSelect, { target: { value: 'customer-1' } });
    fireEvent.change(screen.getByLabelText('شماره سفارش'), { target: { value: 'SO-1405-010' } });
    fireEvent.change(screen.getByLabelText('کد محصول ردیف 1'), { target: { value: 'ADDON-COMPANY' } });
    fireEvent.change(screen.getByLabelText('توضیح ردیف 1'), { target: { value: 'افزایش ظرفیت شرکت' } });
    fireEvent.change(screen.getByLabelText('تعداد ردیف 1'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('مبلغ واحد ردیف 1'), { target: { value: '500' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت سفارش' }));

    await waitFor(() => expect(screen.getByText(/سفارش با موفقیت ثبت شد/)).toBeInTheDocument());
    const create = calls.find(call => call.url === '/api/vendor/sales-orders' && call.init?.method === 'POST');
    expect(create).toBeDefined();
    expect((create!.body as { totalAmount: number }).totalAmount).toBe(1000);
    expect((create!.body as { lines: Array<{ lineAmount: number }> }).lines[0]!.lineAmount).toBe(1000);
    expect(create!.init!.headers).toMatchObject({ 'X-CSRF-TOKEN': 'csrf-sales' });
  });

  it('records a payment with the order currency and requests an independent verification decision', async () => {
    const { fetchMock, calls } = baseFetchMock({
      '/api/vendor/sales-orders/order-1/payments': () => Promise.resolve(response({ id: 'payment-new', status: 'Recorded' }, 201)),
      '/api/vendor/payments/payment-1/approval-request': () => Promise.resolve(response({ id: 'approval-9' }, 202)),
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={allPermissions} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'جزئیات' }))[0]!);
    expect(await screen.findByText('ردیف‌های سفارش')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('مبلغ پرداخت'), { target: { value: '450' } });
    fireEvent.change(screen.getByLabelText('مرجع رسید'), { target: { value: 'RCPT-11' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت پرداخت' }));

    await waitFor(() => expect(screen.getByText(/پرداخت ثبت شد/)).toBeInTheDocument());
    const record = calls.find(call => call.url.endsWith('/payments') && call.init?.method === 'POST');
    expect(record).toBeDefined();
    expect((record!.body as { currencyCode: string }).currencyCode).toBe('IRR');

    fireEvent.click(await screen.findByRole('button', { name: 'درخواست تأیید' }));
    await waitFor(() => expect(screen.getByText(/در صفحه درخواست‌های تأیید انجام می‌شود/)).toBeInTheDocument());
    const decision = calls.find(call => call.url.includes('/approval-request'));
    expect(decision).toBeDefined();
    expect(decision!.body).toMatchObject({ action: 'verify', rowVersion: 'AQIDBAUGBwg=' });
    expect(decision!.init!.headers).toMatchObject({ 'X-CSRF-TOKEN': 'csrf-sales' });
  });

  it('surfaces safe Persian messages for backend rejections (currency mismatch, already decided)', async () => {
    const { fetchMock } = baseFetchMock({
      '/api/vendor/sales-orders/order-1/payments': () => Promise.resolve(response({ error: 'payment_currency_mismatch' }, 400)),
      '/api/vendor/payments/payment-1/approval-request': () => Promise.resolve(response({ error: 'payment_already_decided' }, 409)),
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={allPermissions} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'جزئیات' }))[0]!);
    fireEvent.change(await screen.findByLabelText('مبلغ پرداخت'), { target: { value: '450' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت پرداخت' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('ارز پرداخت با ارز سفارش یکسان نیست.');

    fireEvent.click(await screen.findByRole('button', { name: 'درخواست تأیید' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('این پرداخت قبلاً تصمیم‌گیری شده است');
  });

  it('keeps the workspace read-only without the manage permission and still shows the overpayment state', async () => {
    const { fetchMock } = baseFetchMock();
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={['vendor.payments.read']} />);

    expect(await screen.findByText('SO-1405-002')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /سفارش جدید/ })).not.toBeInTheDocument();
    expect(screen.getAllByText('پرداخت‌شده').length).toBeGreaterThan(0);
  });
});
