import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import App from './App';
import { BusinessLists } from './BusinessLists';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body, text: async () => JSON.stringify(body) };
}

describe('Vendor control plane shell', () => {
  it('shows the Persian sign-in screen when no session exists', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({}, 401)));
    render(<App />);
    expect(await screen.findByRole('heading', { name: 'به پنل مرکزی وارد شوید' })).toBeInTheDocument();
    expect(screen.getByLabelText('نام کاربری یا ایمیل')).toBeRequired();
    expect(screen.getByLabelText('نام کاربری یا ایمیل')).toHaveAttribute('dir', 'auto');
    expect(screen.getByLabelText('رمز عبور')).toHaveAttribute('dir', 'ltr');
  });

  it('moves to TOTP only after the real password endpoint requests a second factor', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({}, 401))
      .mockResolvedValueOnce(response({ requestToken: 'test-token' }))
      .mockResolvedValueOnce(response({ requiresSecondFactor: true, method: 'totp' }));
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);
    fireEvent.change(await screen.findByLabelText('نام کاربری یا ایمیل'), { target: { value: 'vendor-admin' } });
    fireEvent.change(screen.getByLabelText('رمز عبور'), { target: { value: 'not-a-real-password' } });
    fireEvent.click(screen.getByRole('button', { name: /ادامه و تأیید هویت/ }));
    expect(await screen.findByLabelText('کد احراز هویت')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/password', expect.objectContaining({ method: 'POST' }));
  });

  it('offers passkey sign-in and explains when WebAuthn is unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({}, 401)));
    render(<App />);
    expect(await screen.findByRole('button', { name: /ورود با کلید عبور/ })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /ورود با کلید عبور/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent('HTTPS');
  });

  it('requires the initial password change before showing the management workspace', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ userName: 'vendor-admin', totpEnabled: true, passkeyCount: 0, mustChangePassword: true }))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-token' }))
      .mockResolvedValueOnce(response({ changed: true, mustChangePassword: false }))
      .mockResolvedValueOnce(response({ userName: 'vendor-admin', totpEnabled: true, passkeyCount: 0, mustChangePassword: false }));
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'تغییر گذرواژه اولیه' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('گذرواژه فعلی'), { target: { value: 'Secure-Vendor#2026' } });
    fireEvent.change(screen.getByLabelText('گذرواژه جدید'), { target: { value: 'Changed-Vendor#2026-Strong' } });
    fireEvent.change(screen.getByLabelText('تکرار گذرواژه جدید'), { target: { value: 'Changed-Vendor#2026-Strong' } });
    fireEvent.click(screen.getByRole('button', { name: 'تغییر گذرواژه و ادامه' }));

    expect(await screen.findByRole('heading', { name: 'مرکز کنترل فرانو' })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/change-password', expect.objectContaining({ method: 'POST', cache: 'no-store' }));
  });

  it('keeps TOTP setup secrets in the active UI flow and confirms enrollment through the API', async () => {
    const storageWrite = vi.spyOn(Storage.prototype, 'setItem');
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ userName: 'vendor-admin', totpEnabled: false, passkeyCount: 1 }))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-one' }))
      .mockResolvedValueOnce(response({ sharedKey: 'TESTSHAREDKEY', authenticatorUri: 'otpauth://totp/test' }))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-two' }))
      .mockResolvedValueOnce(response({ enabled: true }))
      .mockResolvedValueOnce(response({ userName: 'vendor-admin', totpEnabled: true, passkeyCount: 1 }));
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);
    fireEvent.click(await screen.findByRole('button', { name: /راه‌اندازی TOTP/ }));
    expect(await screen.findByText('TESTSHAREDKEY')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('کد شش‌رقمی برای تأیید'), { target: { value: '123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'تأیید و فعال‌سازی TOTP' }));
    expect(await screen.findByText('احراز هویت TOTP فعال شد. کلید مشترک را از این صفحه خارج نکنید.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/totp/confirm', expect.objectContaining({ method: 'POST', cache: 'no-store' }));
    expect(storageWrite).not.toHaveBeenCalled();
  });
  it('loads real customer data and follows server pagination metadata', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/auth/me') return Promise.resolve(response({ userName: 'vendor-admin', totpEnabled: true }));
      if (url.startsWith('/api/vendor/customers/')) {
        const page = Number(new URL(url, 'http://localhost').searchParams.get('page'));
        return Promise.resolve(response({
          items: [{ id: 'customer-1', customerNumber: 'C-001', legalName: 'شرکت نمونه', displayName: 'نمونه', status: 'Active', createdAtUtc: '2026-10-27T08:00:00Z' }],
          page, pageSize: 25, totalCount: 26, totalPages: 2, hasPreviousPage: page > 1, hasNextPage: page < 2,
        }));
      }
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'مشتریان تجاری' }));
    expect(await screen.findByText('شرکت نمونه')).toBeInTheDocument();
    expect(screen.getByText(/تعداد کل:/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /بعدی/ }));
    await screen.findByText('شرکت نمونه');
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/customers/?page=2&pageSize=25', expect.objectContaining({ credentials: 'same-origin', cache: 'no-store' }));
  });

  it('submits installation status changes for approval with a fresh token and RowVersion', async () => {
    const installationPage = {
      items: [{ id: 'installation-1', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=', installationCode: 'INS-001',
        name: 'نصب عملیاتی', environment: 'Production', status: 'Active', createdAtUtc: '2026-10-27T08:00:00Z', lastSeenAtUtc: null }],
      page: 1, pageSize: 25, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    };
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/vendor/approvals/status-change') return Promise.resolve(response({ id: 'approval-1', status: 'Pending' }, 202));
      if (url.startsWith('/api/vendor/installations/')) return Promise.resolve(response(installationPage));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-status' }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<BusinessLists section="installations" permissions={['vendor.installations.manage', 'vendor.approvals.request']} />);

    fireEvent.click(await screen.findByRole('button', { name: 'تعلیق نصب نصب عملیاتی' }));
    expect(await screen.findByRole('status')).toHaveTextContent('درخواست تغییر وضعیت نصب برای تأیید مدیر دیگر ثبت شد.');
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/approvals/status-change', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-status' },
      body: JSON.stringify({ resourceType: 'installation', resourceId: 'installation-1', targetStatus: 'Suspended', rowVersion: 'AQIDBAUGBwg=' }),
    }));
  });

  it('explains stale RowVersion when an installation status change conflicts', async () => {
    const installationPage = {
      items: [{ id: 'installation-1', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=', installationCode: 'INS-001',
        name: 'نصب عملیاتی', environment: 'Production', status: 'Active', createdAtUtc: '2026-10-27T08:00:00Z', lastSeenAtUtc: null }],
      page: 1, pageSize: 25, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    };
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/vendor/approvals/status-change') return Promise.resolve(response({ error: 'stale_row_version' }, 409));
      if (url.startsWith('/api/vendor/installations/')) return Promise.resolve(response(installationPage));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-status' }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<BusinessLists section="installations" permissions={['vendor.installations.manage', 'vendor.approvals.request']} />);

    fireEvent.click(await screen.findByRole('button', { name: 'تعلیق نصب نصب عملیاتی' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('اطلاعات نصب تغییر کرده است؛ فهرست را تازه‌سازی کنید.');
  });

  it('allows contract edits only for Draft records and requires customer-read to create', async () => {
    const contractPage = {
      items: [
        { id: 'contract-draft', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=', contractNumber: 'CTR-DRAFT',
          status: 'Draft', startsAtUtc: '2026-10-27T00:00:00Z', endsAtUtc: null, currencyCode: 'IRR', createdAtUtc: '2026-10-27T08:00:00Z' },
        { id: 'contract-active', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=', contractNumber: 'CTR-ACTIVE',
          status: 'Active', startsAtUtc: '2026-10-27T00:00:00Z', endsAtUtc: null, currencyCode: 'IRR', createdAtUtc: '2026-10-27T08:00:00Z' },
      ],
      page: 1, pageSize: 25, totalCount: 2, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response(contractPage)));
    render(<BusinessLists section="contracts" permissions={['vendor.contracts.manage']} />);

    expect(await screen.findByRole('button', { name: 'ثبت قرارداد' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'ویرایش قرارداد CTR-DRAFT' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ویرایش قرارداد CTR-ACTIVE' })).not.toBeInTheDocument();
  });

  it('shows license edit only for Draft records when the manager permission is present', async () => {
    const licensePage = {
      items: [
        { id: 'license-draft', customerId: 'customer-1', contractId: null, installationId: null, rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-DRAFT', productCode: 'FARANOU-BASE', status: 'Draft', validFromUtc: '2026-10-27T00:00:00Z', validUntilUtc: null, createdAtUtc: '2026-10-27T08:00:00Z' },
        { id: 'license-active', customerId: 'customer-1', contractId: null, installationId: null, rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-ACTIVE', productCode: 'FARANOU-BASE', status: 'Active', validFromUtc: '2026-10-27T00:00:00Z', validUntilUtc: null, createdAtUtc: '2026-10-27T08:00:00Z' },
      ],
      page: 1, pageSize: 25, totalCount: 2, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response(licensePage)));
    render(<BusinessLists section="licenses" permissions={['vendor.licenses.manage', 'vendor.customers.read']} />);
    expect(await screen.findByRole('button', { name: 'ثبت لایسنس' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'ویرایش لایسنس LIC-DRAFT' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ویرایش لایسنس LIC-ACTIVE' })).not.toBeInTheDocument();
  });

  it('shows a permission-specific message when a business list returns 403', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/auth/me') return Promise.resolve(response({ userName: 'vendor-admin', totpEnabled: true }));
      if (url.startsWith('/api/vendor/contracts/')) return Promise.resolve(response({}, 403));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'اشتراک‌ها و قراردادها' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('حساب شما مجوز مشاهده این بخش را ندارد.');
  });

  it('shows a truthful empty state when the API returns no records', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/auth/me') return Promise.resolve(response({ userName: 'vendor-admin', totpEnabled: true }));
      if (url.startsWith('/api/vendor/licenses/')) return Promise.resolve(response({
        items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false,
      }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'مجوزها و لایسنس‌ها' }));
    expect(await screen.findByText('رکوردی برای نمایش وجود ندارد')).toBeInTheDocument();
  });

  it('loads the installations section from its own API endpoint', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/auth/me') return Promise.resolve(response({ userName: 'vendor-admin', totpEnabled: true }));
      if (url.startsWith('/api/vendor/installations/')) return Promise.resolve(response({
        items: [{ id: 'installation-1', installationCode: 'INS-001', name: 'نصب عملیاتی', environment: 'Production', status: 'Active', createdAtUtc: '2026-10-27T08:00:00Z', lastSeenAtUtc: null }],
        page: 1, pageSize: 25, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
      }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'نصب‌ها' }));
    expect(await screen.findByText('INS-001')).toBeInTheDocument();
    expect(screen.getByText('عملیاتی')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/installations/?page=1&pageSize=25', expect.objectContaining({ credentials: 'same-origin' }));
  });

  it('hides customer write controls when the session lacks the manage permission', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({
      items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false,
    })));
    render(<BusinessLists section="customers" permissions={['vendor.customers.read']} />);

    expect(await screen.findByText('رکوردی برای نمایش وجود ندارد')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ثبت مشتری' })).not.toBeInTheDocument();
  });

  it('shows customer create and edit controls only with the manage permission', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({
      items: [{ id: 'customer-1', rowVersion: 'AQIDBAUGBwg=', customerNumber: 'C-001', legalName: 'شرکت نمونه', displayName: 'نمونه', status: 'Active' }],
      page: 1, pageSize: 25, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false,
    })));
    render(<BusinessLists section="customers" permissions={['vendor.customers.manage']} />);

    expect(await screen.findByRole('button', { name: 'ثبت مشتری' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ویرایش مشتری نمونه' })).toBeInTheDocument();
  });

});
