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
  lines: [{ id: 'line-1', productCode: 'addon.company.seat', description: 'افزایش ظرفیت شرکت', quantity: 3, unitAmount: 400, lineAmount: 1200, licenseId: 'license-1' }],
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

  it('previews, records and submits a license amendment for a purchased line', async () => {
    const calls: Array<{ url: string; init?: RequestInit | undefined; body?: unknown }> = [];
    let amendmentCreated = false;
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const body = init?.body ? (JSON.parse(String(init.body)) as unknown) : undefined;
      calls.push({ url, init, body });
      if (url.startsWith('/api/vendor/sales-orders/?')) return Promise.resolve(response(ordersPage([collectibleOrder])));
      if (url.startsWith('/api/vendor/receivables/?')) return Promise.resolve(response(page([])));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-amendment' }));
      if (url === `/api/vendor/sales-orders/${collectibleOrder.id}`) return Promise.resolve(response(orderDetail));
      if (url.includes('/api/vendor/license-amendment-requests/?orderId=')) {
        return Promise.resolve(response(amendmentCreated ? page([{
          id: 'amendment-1', orderId: collectibleOrder.id, orderLineId: 'line-1', licenseId: 'license-1',
          kind: 'maxCompanies', requestedMaxCompanies: 8, requestedSupportUntilUtc: null,
          requestedUpdatesUntilUtc: null, status: 'Requested', failureCode: null, rowVersion: 'AQIDBAUGBwg=',
        }]) : page([])));
      }
      if (url === '/api/vendor/license-amendment-requests/preview')
        return Promise.resolve(response({ sequence: 2, maxCompanies: 8, supportUntilUtc: null, updatesUntilUtc: null }));
      if (url === '/api/vendor/license-amendment-requests') {
        amendmentCreated = true;
        return Promise.resolve(response({ id: 'amendment-1' }, 201));
      }
      if (url === '/api/vendor/license-amendment-requests/amendment-1/approval-request')
        return Promise.resolve(response({ approvalId: 'approval-am' }, 202));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={['vendor.payments.read', 'vendor.payments.manage', 'vendor.licenses.manage', 'vendor.licenses.issue']} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'جزئیات' }))[0]!);
    expect(await screen.findByText('Amendment لایسنس (ردیف خرید)')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: 'درخواست Amendment' }));
    fireEvent.change(await screen.findByLabelText('سقف جدید شرکت‌ها'), { target: { value: '8' } });

    fireEvent.click(screen.getByRole('button', { name: 'پیش‌نمایش' }));
    expect(await screen.findByText(/پیش‌نمایش کاندید: نسخه ۲/)).toBeInTheDocument();
    const preview = calls.find(call => call.url === '/api/vendor/license-amendment-requests/preview');
    expect(preview!.body).toMatchObject({ orderLineId: 'line-1', licenseId: 'license-1', kind: 'maxCompanies', requestedMaxCompanies: 8 });
    expect(preview!.init!.headers).toMatchObject({ 'X-CSRF-TOKEN': 'csrf-amendment' });

    fireEvent.click(screen.getByRole('button', { name: 'ثبت درخواست' }));
    expect(await screen.findByText(/پس از بازبینی، آن را برای تأیید مدیر ارسال کنید/)).toBeInTheDocument();
    const create = calls.find(call => call.url === '/api/vendor/license-amendment-requests' && call.init?.method === 'POST');
    expect(create!.body).toMatchObject({ kind: 'maxCompanies', requestedMaxCompanies: 8 });

    fireEvent.click(await screen.findByRole('button', { name: 'ارسال برای تأیید' }));
    expect(await screen.findByText(/در صفحه درخواست‌های تأیید انجام می‌شود/)).toBeInTheDocument();
    const submitted = calls.find(call => call.url.includes('/approval-request'));
    expect(submitted!.body).toMatchObject({ rowVersion: 'AQIDBAUGBwg=' });
  });

  it('treats a failed amendment page as incomplete, disables actions and recovers via retry', async () => {
    const requestedRow = {
      id: 'amendment-1', orderId: collectibleOrder.id, orderLineId: 'line-1', licenseId: 'license-1',
      kind: 'maxCompanies', requestedMaxCompanies: 8, requestedSupportUntilUtc: null,
      requestedUpdatesUntilUtc: null, status: 'Requested', failureCode: null, rowVersion: 'AQIDBAUGBwg=',
    };
    const issuedRow = { ...requestedRow, id: 'amendment-2', status: 'Issued' };
    let page2Fails = true;
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      void init;
      const url = String(input);
      if (url.startsWith('/api/vendor/sales-orders/?')) return Promise.resolve(response(ordersPage([collectibleOrder])));
      if (url.startsWith('/api/vendor/receivables/?')) return Promise.resolve(response(page([])));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-retry' }));
      if (url === `/api/vendor/sales-orders/${collectibleOrder.id}`) return Promise.resolve(response(orderDetail));
      if (url.includes('/api/vendor/license-amendment-requests/?orderId=')) {
        const pageNumber = Number(url.match(/page=(\d+)/)?.[1]);
        if (pageNumber === 1) return Promise.resolve(response({
          items: [requestedRow], page: 1, pageSize: 50, totalCount: 2, totalPages: 2, hasPreviousPage: false, hasNextPage: true,
        }));
        return page2Fails
          ? Promise.resolve(response({ error: 'boom' }, 500))
          : Promise.resolve(response({
              items: [issuedRow], page: 2, pageSize: 50, totalCount: 2, totalPages: 2, hasPreviousPage: true, hasNextPage: false,
            }));
      }
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={['vendor.payments.read', 'vendor.payments.manage', 'vendor.licenses.read', 'vendor.licenses.manage', 'vendor.licenses.issue']} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'جزئیات' }))[0]!);
    // Page 1 succeeded but page 2 failed: the incomplete list must not look complete.
    expect(await screen.findByRole('alert')).toHaveTextContent('فهرست Amendmentها کامل بارگذاری نشد');
    // The line already has a Requested row, so the only action button is the approval submit —
    // and it must be disabled until the list is complete.
    expect(screen.getByRole('button', { name: 'ارسال برای تأیید' })).toBeDisabled();

    // Retry reloads only this order's data and completes the list.
    page2Fails = false;
    fireEvent.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'ارسال برای تأیید' })).toBeEnabled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('delivers an issued amendment envelope, shows the delivery state and keeps Applied visible', async () => {
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:amendment-envelope');
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    let delivered = false;
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/vendor/sales-orders/?')) return Promise.resolve(response(ordersPage([collectibleOrder])));
      if (url.startsWith('/api/vendor/receivables/?')) return Promise.resolve(response(page([])));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-delivery' }));
      if (url === `/api/vendor/sales-orders/${collectibleOrder.id}`) return Promise.resolve(response(orderDetail));
      if (url.includes('/api/vendor/license-amendment-requests/?orderId=')) {
        return Promise.resolve(response(page([delivered ? {
          id: 'amendment-1', orderId: collectibleOrder.id, orderLineId: 'line-1', licenseId: 'license-1',
          kind: 'maxCompanies', requestedMaxCompanies: 8, requestedSupportUntilUtc: null,
          requestedUpdatesUntilUtc: null, status: 'Delivered', failureCode: null, rowVersion: 'AQIDBAUGBwg=',
          issuedSequence: 2, deliveredAtUtc: '2026-11-29T09:00:00Z',
          acknowledgementResult: null, acknowledgedAtUtc: null,
        } : {
          id: 'amendment-1', orderId: collectibleOrder.id, orderLineId: 'line-1', licenseId: 'license-1',
          kind: 'maxCompanies', requestedMaxCompanies: 8, requestedSupportUntilUtc: null,
          requestedUpdatesUntilUtc: null, status: 'Issued', failureCode: null, rowVersion: 'AQIDBAUGBwg=',
          issuedSequence: 2, deliveredAtUtc: null, acknowledgementResult: null, acknowledgedAtUtc: null,
        }])));
      }
      if (url === '/api/vendor/license-amendment-requests/amendment-1/delivery') {
        delivered = true;
        const envelope = {
          requestId: 'amendment-1', status: 'Delivered', repeated: false, nonce: 'a'.repeat(64),
          payloadSha256: 'b'.repeat(64), sequence: 2, formatVersion: 1, keyId: 'test-license-kid',
          algorithm: 'ES256', payloadJson: '{"sequence":2}', signatureBase64: 'c'.repeat(64),
        };
        return Promise.resolve({ ok: true, status: 200, json: async () => envelope });
      }
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={['vendor.payments.read', 'vendor.licenses.manage', 'vendor.licenses.issue']} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'جزئیات' }))[0]!);
    // The issued amendment offers delivery; the per-line row blocks a second active request.
    fireEvent.click(await screen.findByRole('button', { name: 'تحویل پاکت امضاشده' }));
    expect(await screen.findByText(/تحویل Amendment ثبت شد/)).toBeInTheDocument();
    await waitFor(() => expect(createObjectUrl).toHaveBeenCalledWith(expect.any(Blob)));
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:amendment-envelope');
    // After the refresh the delivered state and its sequence are shown with the re-export action.
    // The delivered state shows in both the per-line row and the delivery panel.
    expect((await screen.findAllByText(/در انتظار رسید امضاشده/)).length).toBeGreaterThan(0);
    expect(screen.getByText(/نسخهٔ ۲/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'دریافت مجدد پاکت' })).toBeEnabled();
  });

  it('keeps amendment actions enabled for a mutation operator when the list request is denied', async () => {
    // The list endpoint needs vendor.licenses.read; manage/issue are independent permissions.
    // A 403 on the list must not gate this operator's server-authorized mutation actions.
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      void init;
      const url = String(input);
      if (url.startsWith('/api/vendor/sales-orders/?')) return Promise.resolve(response(ordersPage([collectibleOrder])));
      if (url.startsWith('/api/vendor/receivables/?')) return Promise.resolve(response(page([])));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-denied' }));
      if (url === `/api/vendor/sales-orders/${collectibleOrder.id}`) return Promise.resolve(response(orderDetail));
      if (url.includes('/api/vendor/license-amendment-requests/?orderId=')) return Promise.resolve(response({}, 403));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={['vendor.payments.read', 'vendor.licenses.manage', 'vendor.licenses.issue']} />);

    fireEvent.click((await screen.findAllByRole('button', { name: 'جزئیات' }))[0]!);
    expect(await screen.findByText('Amendment لایسنس (ردیف خرید)')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'درخواست Amendment' })).toBeEnabled();
  });

  it('discards out-of-order detail responses so a slower earlier order never overwrites the selection', async () => {
    const order2Detail = {
      ...orderDetail,
      id: 'order-2',
      orderNumber: 'SO-1405-002',
      lines: [{ id: 'line-2', productCode: 'addon.company.seat', description: 'سفارش دوم', quantity: 1, unitAmount: 400, lineAmount: 400, licenseId: 'license-1' }],
      payments: [],
    };
    let resolveOrder1Detail: ((value: unknown) => void) | null = null;
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      void init;
      const url = String(input);
      if (url.startsWith('/api/vendor/sales-orders/?')) return Promise.resolve(response(ordersPage([collectibleOrder, overpaidOrder])));
      if (url.startsWith('/api/vendor/receivables/?')) return Promise.resolve(response(page([])));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-race' }));
      if (url === `/api/vendor/sales-orders/${collectibleOrder.id}`) {
        return new Promise(resolve => { resolveOrder1Detail = resolve; });
      }
      if (url === '/api/vendor/sales-orders/order-2') return Promise.resolve(response(order2Detail));
      if (url.includes('license-amendment-requests/?orderId=order-1')) return new Promise(() => { /* hangs: never completes */ });
      if (url.includes('license-amendment-requests/?orderId=order-2')) return Promise.resolve(response(page([])));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<VendorSales permissions={['vendor.payments.read', 'vendor.payments.manage', 'vendor.licenses.manage', 'vendor.licenses.issue']} />);

    const detailButtons = await screen.findAllByRole('button', { name: 'جزئیات' });
    fireEvent.click(detailButtons[0]!);
    fireEvent.click(detailButtons[1]!);
    // The fast order-2 detail (with its completed amendment list) is published.
    expect(await screen.findByText('سفارش دوم')).toBeInTheDocument();
    expect(screen.getByText('بدون درخواست')).toBeInTheDocument();

    // The slow order-1 responses (detail and its still-hanging amendment pages) must never
    // overwrite the current selection's state.
    resolveOrder1Detail!(response(orderDetail));
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('orderId=order-1'), expect.anything()));
    expect(screen.getByText('سفارش دوم')).toBeInTheDocument();
    expect(screen.queryByText('ارسال برای تأیید')).not.toBeInTheDocument();
  });

});
