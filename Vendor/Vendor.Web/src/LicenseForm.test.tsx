import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { LicenseForm } from './LicenseForm';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function page<T>(items: T[]) {
  return { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: items.length ? 1 : 0,
    hasPreviousPage: false, hasNextPage: false };
}

const customerPage = page([{ id: 'customer-1', customerNumber: 'C-001', displayName: 'شرکت نمونه' }]);
const contractPage = page([{ id: 'contract-1', customerId: 'customer-1', contractNumber: 'CTR-001', status: 'Draft' }]);
const installationPage = page([{ id: 'installation-1', customerId: 'customer-1', installationCode: 'INST-001', name: 'نصب اصلی', status: 'Active' }]);

describe('Vendor license metadata form', () => {
  it('keeps native browser constraint validation enabled', () => {
    render(<LicenseForm canReadCustomers canReadContracts canReadInstallations onCancel={vi.fn()} onSaved={vi.fn()} />);

    const form = screen.getByRole('button', { name: 'ثبت لایسنس پیش‌نویس' }).closest('form');
    expect(form).not.toHaveAttribute('novalidate');
  });

  it('creates a Draft license with customer-owned optional references, UTC dates and fresh antiforgery', async () => {
    const onSaved = vi.fn();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/vendor/customers/?')) return Promise.resolve(response(customerPage));
      if (url.startsWith('/api/vendor/contracts/?')) return Promise.resolve(response(contractPage));
      if (url.startsWith('/api/vendor/installations/?')) return Promise.resolve(response(installationPage));
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-license' }));
      if (url === '/api/vendor/licenses/' && init?.method === 'POST') {
        return Promise.resolve(response({ id: 'license-1', status: 'Draft' }, 201));
      }
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<LicenseForm canReadCustomers canReadContracts canReadInstallations onCancel={vi.fn()} onSaved={onSaved} />);
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'مشتری تجاری' })).toHaveValue('customer-1'));
    const contractSelect = await screen.findByRole('combobox', { name: 'قرارداد مرتبط (اختیاری)' });
    const installationSelect = await screen.findByRole('combobox', { name: 'نصب مرتبط (اختیاری)' });
    await waitFor(() => expect(screen.getByRole('option', { name: 'CTR-001 — Draft' })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('option', { name: 'INST-001 — نصب اصلی (Active)' })).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText('شماره لایسنس'), { target: { value: 'LIC-001' } });
    fireEvent.change(screen.getByLabelText('کد محصول'), { target: { value: 'FARANOU-BASE' } });
    expect(screen.getByLabelText('نوع لایسنس')).toHaveValue('term');
    expect(screen.getByLabelText('مالی (finance)')).not.toBeChecked();
    fireEvent.change(screen.getByLabelText('حداکثر شرکت (اختیاری)'), { target: { value: '25' } });
    fireEvent.click(screen.getByLabelText('مالی (finance)'));
    fireEvent.click(screen.getByLabelText('فروش (sales)'));
    fireEvent.change(screen.getByLabelText('تاریخ شروع اعتبار'), { target: { value: '2026-10-27' } });
    fireEvent.change(screen.getByLabelText('تاریخ پایان اعتبار (الزامی)'), { target: { value: '2027-10-26' } });
    fireEvent.change(contractSelect, { target: { value: 'contract-1' } });
    fireEvent.change(installationSelect, { target: { value: 'installation-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت لایسنس پیش‌نویس' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/contracts/?customerId=customer-1&page=1&pageSize=100',
      expect.objectContaining({ credentials: 'same-origin', cache: 'no-store' }));
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100',
      expect.objectContaining({ credentials: 'same-origin', cache: 'no-store' }));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/antiforgery', expect.objectContaining({
      credentials: 'same-origin', cache: 'no-store',
    }));
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/licenses/', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-license' },
      body: JSON.stringify({
        customerId: 'customer-1',
        contractId: 'contract-1',
        installationId: 'installation-1',
        licenseNumber: 'LIC-001',
        productCode: 'FARANOU-BASE',
        licenseKind: 'term',
        maxCompanies: 25,
        subsystems: ['finance', 'sales'],
        graceDays: 30,
        validFromUtc: '2026-10-27T00:00:00.000Z',
        validUntilUtc: '2027-10-26T00:00:00.000Z',
      }),
    }));
  });

  it('preserves existing references and RowVersion when the operator cannot read reference lists', async () => {
    const onSaved = vi.fn();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-edit-license' }));
      if (url === '/api/vendor/licenses/license-1' && init?.method === 'PUT') {
        return Promise.resolve(response({ id: 'license-1', status: 'Draft' }));
      }
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<LicenseForm initial={{
      id: 'license-1', customerId: 'customer-1', contractId: 'contract-1', installationId: 'installation-1',
      rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-001', productCode: 'FARANOU-BASE',
      licenseKind: 'perpetual', maxCompanies: 10, subsystems: ['finance'], graceDays: 30,
      validFromUtc: '2026-01-01T00:00:00Z', validUntilUtc: '', status: 'Draft',
    }} canReadCustomers={false} canReadContracts={false} canReadInstallations={false}
      onCancel={vi.fn()} onSaved={onSaved} />);

    fireEvent.change(screen.getByLabelText('شماره لایسنس'), { target: { value: 'LIC-UPDATED' } });
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره تغییرات' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/licenses/license-1', expect.objectContaining({
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-edit-license' },
      body: JSON.stringify({
        contractId: 'contract-1',
        installationId: 'installation-1',
        licenseNumber: 'LIC-UPDATED',
        productCode: 'FARANOU-BASE',
        licenseKind: 'perpetual',
        maxCompanies: 10,
        subsystems: ['finance'],
        graceDays: 30,
        validFromUtc: '2026-01-01T00:00:00.000Z',
        validUntilUtc: null,
        rowVersion: 'AQIDBAUGBwg=',
      }),
    }));
  });

  it('prevents creation without customer-read permission and rejects reversed validity dates', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    render(<LicenseForm canReadCustomers={false} canReadContracts={false} canReadInstallations={false}
      onCancel={vi.fn()} onSaved={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'ثبت لایسنس پیش‌نویس' })).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent('برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rejects impossible calendar dates before sending a mutation', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      if (String(input).startsWith('/api/vendor/customers/?')) return Promise.resolve(response(customerPage));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<LicenseForm canReadCustomers canReadContracts={false} canReadInstallations={false}
      onCancel={vi.fn()} onSaved={vi.fn()} />);

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'مشتری تجاری' })).toHaveValue('customer-1'));
    fireEvent.change(screen.getByLabelText('تاریخ شروع اعتبار'), { target: { value: '2026-02-31' } });

    expect(screen.getByRole('button', { name: 'ثبت لایسنس پیش‌نویس' })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalledWith('/api/auth/antiforgery', expect.anything());
    expect(fetchMock).not.toHaveBeenCalledWith('/api/vendor/licenses/', expect.anything());
  });

  it('explains stale RowVersion conflicts without claiming the license was saved', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-stale' }));
      if (url === '/api/vendor/licenses/license-1' && init?.method === 'PUT') {
        return Promise.resolve(response({ error: 'stale_row_version' }, 409));
      }
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<LicenseForm initial={{
      id: 'license-1', customerId: 'customer-1', contractId: null, installationId: null,
      rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-001', productCode: 'FARANOU-BASE',
      licenseKind: 'perpetual', subsystems: ['finance'], graceDays: 30,
      validFromUtc: '2026-01-01T00:00:00Z', validUntilUtc: '', status: 'Draft',
    }} canReadCustomers={false} canReadContracts={false} canReadInstallations={false}
      onCancel={vi.fn()} onSaved={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('کد محصول'), { target: { value: 'FARANOU-PLUS' } });
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره تغییرات' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('لایسنس تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.');
  });

  it('does not permit metadata edits after Draft', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    render(<LicenseForm initial={{
      id: 'license-1', customerId: 'customer-1', contractId: null, installationId: null,
      rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-001', productCode: 'FARANOU-BASE',
      validFromUtc: '2026-01-01T00:00:00Z', validUntilUtc: '', status: 'Active',
    }} canReadCustomers={false} canReadContracts={false} canReadInstallations={false}
      onCancel={vi.fn()} onSaved={vi.fn()} />);

    expect(screen.getByRole('alert')).toHaveTextContent('فقط لایسنس پیش‌نویس قابل ویرایش است');
    expect(screen.getByRole('button', { name: 'ذخیره تغییرات' })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  describe('company-scoped (v2) entitlements', () => {
    const createButton = 'ثبت لایسنس پیش‌نویس';

    function createFetch(onPost?: (init: RequestInit) => unknown, postStatus = 201) {
      return vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (url.startsWith('/api/vendor/customers/?')) return Promise.resolve(response(customerPage));
        if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-v2' }));
        if (url === '/api/vendor/licenses/' && init?.method === 'POST') {
          return Promise.resolve(response(onPost ? onPost(init) : { id: 'l1' }, postStatus));
        }
        return Promise.resolve(response({}, 404));
      });
    }

    async function renderCreate(onSaved = vi.fn()) {
      render(<LicenseForm canReadCustomers canReadContracts={false} canReadInstallations={false}
        onCancel={vi.fn()} onSaved={onSaved} />);
      await waitFor(() => expect(screen.getByRole('combobox', { name: 'مشتری تجاری' })).toHaveValue('customer-1'));
      fireEvent.change(screen.getByLabelText('شماره لایسنس'), { target: { value: 'LIC-V2' } });
      fireEvent.change(screen.getByLabelText('کد محصول'), { target: { value: 'FARANOU-BASE' } });
      fireEvent.change(screen.getByLabelText('تاریخ پایان اعتبار (الزامی)'), { target: { value: '2999-01-01' } });
      fireEvent.click(screen.getByLabelText('شرکت‌محور (v2)'));
      return onSaved;
    }

    function postedBody(fetchMock: ReturnType<typeof createFetch>) {
      const call = fetchMock.mock.calls.find(([url, init]) => String(url) === '/api/vendor/licenses/' && init?.method === 'POST');
      return call ? JSON.parse(String(call[1]?.body)) : undefined;
    }

    it('sends companies without subsystems on create and no reference for new companies', async () => {
      const fetchMock = createFetch();
      vi.stubGlobal('fetch', fetchMock);
      const onSaved = await renderCreate();
      expect(screen.queryByText('زیرسیستم‌های خریداری‌شده')).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      fireEvent.change(screen.getByLabelText('شناسه توصیفی شرکت 1'), { target: { value: ' شعبه مرکزی ' } });
      fireEvent.change(screen.getByLabelText('حداکثر کاربر شرکت 1'), { target: { value: '15' } });
      fireEvent.click(screen.getByLabelText('مالی (finance) — شرکت 1'));
      fireEvent.change(screen.getByLabelText('افزودن قابلیت به شرکت 1'), { target: { value: 'finance.cost_centers' } });
      fireEvent.click(screen.getByRole('button', { name: 'افزودن قابلیت انتخاب‌شده — شرکت 1' }));
      fireEvent.change(screen.getByLabelText('سقف مراکز هزینه — شرکت 1'), { target: { value: '7' } });
      fireEvent.change(screen.getByLabelText('افزودن قابلیت به شرکت 1'), { target: { value: 'finance.budgeting' } });
      fireEvent.click(screen.getByRole('button', { name: 'افزودن قابلیت انتخاب‌شده — شرکت 1' }));
      fireEvent.click(screen.getByRole('button', { name: createButton }));

      await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
      const body = postedBody(fetchMock);
      expect(body).not.toHaveProperty('subsystems');
      expect(body.companies).toEqual([{
        descriptor: 'شعبه مرکزی', modules: ['finance'], maxUsers: 15,
        features: [
          { code: 'finance.cost_centers', type: 'max_count', maxCount: 7 },
          { code: 'finance.budgeting', type: 'enabled', enabled: true },
        ],
      }]);
      expect(body.companies[0]).not.toHaveProperty('companyReference');
    });

    it('rejects an empty company list and invalid maxUsers before any request', async () => {
      const fetchMock = createFetch();
      vi.stubGlobal('fetch', fetchMock);
      await renderCreate();
      fireEvent.click(screen.getByRole('button', { name: createButton }));
      expect(await screen.findByRole('alert')).toHaveTextContent('حداقل یک شرکت');

      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      fireEvent.change(screen.getByLabelText('شناسه توصیفی شرکت 1'), { target: { value: 'الف' } });
      for (const bad of ['0', '1000001', '1.5', '']) {
        fireEvent.change(screen.getByLabelText('حداکثر کاربر شرکت 1'), { target: { value: bad } });
        fireEvent.click(screen.getByRole('button', { name: createButton }));
        expect(await screen.findByRole('alert')).toHaveTextContent('حداکثر کاربر');
      }
      expect(fetchMock).not.toHaveBeenCalledWith('/api/auth/antiforgery', expect.anything());
      expect(postedBody(fetchMock)).toBeUndefined();
    });

    it('requires the owning module for a feature and a descriptor', async () => {
      const fetchMock = createFetch();
      vi.stubGlobal('fetch', fetchMock);
      await renderCreate();
      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      fireEvent.click(screen.getByRole('button', { name: createButton }));
      expect(await screen.findByRole('alert')).toHaveTextContent('شناسه توصیفی');

      fireEvent.change(screen.getByLabelText('شناسه توصیفی شرکت 1'), { target: { value: 'ب' } });
      fireEvent.change(screen.getByLabelText('افزودن قابلیت به شرکت 1'), { target: { value: 'sales.pos' } });
      fireEvent.click(screen.getByRole('button', { name: 'افزودن قابلیت انتخاب‌شده — شرکت 1' }));
      fireEvent.click(screen.getByRole('button', { name: createButton }));
      expect(await screen.findByRole('alert')).toHaveTextContent('نیاز دارد');
      expect(postedBody(fetchMock)).toBeUndefined();

      fireEvent.click(screen.getByLabelText('فروش (sales) — شرکت 1'));
      fireEvent.click(screen.getByRole('button', { name: createButton }));
      await waitFor(() => expect(postedBody(fetchMock)).toBeDefined());
    });

    it('warns when companies exceed maxCompanies', async () => {
      vi.stubGlobal('fetch', createFetch());
      await renderCreate();
      fireEvent.change(screen.getByLabelText('حداکثر شرکت (اختیاری)'), { target: { value: '1' } });
      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      expect(screen.getByRole('status')).toHaveTextContent('بیشتر است');
    });

    it('round-trips companyReference on update, omits it for new companies and locks the scope', async () => {
      const onSaved = vi.fn();
      const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-u' }));
        if (url === '/api/vendor/licenses/license-2' && init?.method === 'PUT') return Promise.resolve(response({ id: 'license-2' }));
        return Promise.resolve(response({}, 404));
      });
      vi.stubGlobal('fetch', fetchMock);
      render(<LicenseForm initial={{
        id: 'license-2', customerId: 'customer-1', contractId: null, installationId: null,
        rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-2', productCode: 'P', licenseKind: 'perpetual',
        graceDays: 30, validFromUtc: '2026-01-01T00:00:00Z', validUntilUtc: '', status: 'Draft',
        companies: [{ companyReference: 'ref-abc', descriptor: 'قدیمی', modules: ['inventory'], maxUsers: 4,
          features: [{ code: 'inventory.warehouses', type: 'max_count', maxCount: 3 }] }],
      }} canReadCustomers={false} canReadContracts={false} canReadInstallations={false}
        onCancel={vi.fn()} onSaved={onSaved} />);

      expect(screen.getByLabelText('شرکت‌محور (v2)')).toBeChecked();
      expect(screen.getByLabelText('شرکت‌محور (v2)')).toBeDisabled();
      expect(screen.getByLabelText('سطح نصب (زیرسیستم‌ها، نسخه قدیمی)')).toBeDisabled();
      expect(screen.getByText('ref-abc')).toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      fireEvent.change(screen.getByLabelText('شناسه توصیفی شرکت 2'), { target: { value: 'جدید' } });
      fireEvent.click(screen.getByRole('button', { name: 'ذخیره تغییرات' }));
      await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
      const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!;
      const body = JSON.parse(String(put[1]?.body));
      expect(body).not.toHaveProperty('subsystems');
      expect(body.companies).toEqual([
        { companyReference: 'ref-abc', descriptor: 'قدیمی', modules: ['inventory'], maxUsers: 4,
          features: [{ code: 'inventory.warehouses', type: 'max_count', maxCount: 3 }] },
        { descriptor: 'جدید', modules: [], maxUsers: 1, features: [] },
      ]);
    });

    it('shows Issued company entitlements read-only (descriptor and opaque reference only)', () => {
      vi.stubGlobal('fetch', vi.fn());
      render(<LicenseForm initial={{
        id: 'license-3', customerId: 'customer-1', contractId: null, installationId: null,
        rowVersion: 'AQIDBAUGBwg=', licenseNumber: 'LIC-3', productCode: 'P', status: 'Active',
        validFromUtc: '2026-01-01T00:00:00Z', validUntilUtc: '',
        companies: [{ companyReference: 'ref-issued', descriptor: 'صادرشده', modules: null, maxUsers: null, features: null }],
      }} canReadCustomers={false} canReadContracts={false} canReadInstallations={false}
        onCancel={vi.fn()} onSaved={vi.fn()} />);
      expect(screen.getByText('صادرشده')).toBeInTheDocument();
      expect(screen.getByText('ref-issued')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'افزودن شرکت' })).not.toBeInTheDocument();
    });

    it.each([
      ['invalid_company_entitlements', 'مجوزهای شرکت‌ها معتبر نیست'],
      ['company_entitlements_too_large', 'حجم مجوزهای شرکت‌ها'],
      ['company_reference_not_assignable', 'شناسه مرجع یکی از شرکت‌ها'],
      ['company_entitlements_required', 'باید فهرست شرکت‌ها را بفرستید'],
      ['subsystems_not_allowed_for_company_license', 'زیرسیستم‌های سطح نصب مجاز نیست'],
    ])('maps server error %s to a Persian message', async (code, text) => {
      vi.stubGlobal('fetch', createFetch(() => ({ error: code }), 400));
      await renderCreate();
      fireEvent.click(screen.getByRole('button', { name: 'افزودن شرکت' }));
      fireEvent.change(screen.getByLabelText('شناسه توصیفی شرکت 1'), { target: { value: 'ج' } });
      fireEvent.click(screen.getByRole('button', { name: createButton }));
      expect(await screen.findByRole('alert')).toHaveTextContent(text);
    });

    it('keeps the legacy payload free of a companies key', async () => {
      const fetchMock = createFetch();
      vi.stubGlobal('fetch', fetchMock);
      const onSaved = vi.fn();
      render(<LicenseForm canReadCustomers canReadContracts={false} canReadInstallations={false}
        onCancel={vi.fn()} onSaved={onSaved} />);
      await waitFor(() => expect(screen.getByRole('combobox', { name: 'مشتری تجاری' })).toHaveValue('customer-1'));
      fireEvent.change(screen.getByLabelText('شماره لایسنس'), { target: { value: 'L' } });
      fireEvent.change(screen.getByLabelText('کد محصول'), { target: { value: 'P' } });
      fireEvent.change(screen.getByLabelText('تاریخ پایان اعتبار (الزامی)'), { target: { value: '2999-01-01' } });
      fireEvent.click(screen.getByLabelText('مالی (finance)'));
      fireEvent.click(screen.getByRole('button', { name: createButton }));
      await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
      const body = postedBody(fetchMock);
      expect(body).not.toHaveProperty('companies');
      expect(body.subsystems).toEqual(['finance']);
    });
  });
});
