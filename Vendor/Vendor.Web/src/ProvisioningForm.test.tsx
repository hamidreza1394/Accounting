import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ProvisioningForm } from './ProvisioningForm';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

describe('Vendor customer provisioning form', () => {
  it('submits only setup metadata with antiforgery and shows the truthful awaiting-signer state', async () => {
    let created = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100') {
        return response({ items: [{ id: 'install-1', installationCode: 'INSTALL-01', name: 'نصب اصلی', status: 'Active' }] });
      }
      if (url === '/api/vendor/provisioning-requests?customerId=customer-1&page=1&pageSize=10') {
        return response({ items: created ? [{
          id: 'provision-1', requestId: 'request-1', status: 'AwaitingSigner',
          createdAtUtc: '2026-10-28T12:00:00Z', updatedAtUtc: '2026-10-28T12:00:00Z',
          payloadSha256: 'a'.repeat(64),
        }] : [] });
      }
      if (url === '/api/auth/antiforgery') return response({ requestToken: 'csrf-provisioning' });
      if (url === '/api/vendor/provisioning-requests/' && init?.method === 'POST') {
        created = true;
        return response({ id: 'provision-1', status: 'AwaitingSigner' }, 201);
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValueOnce('request-1').mockReturnValue('request-2') });

    const onSaved = vi.fn();
    render(<ProvisioningForm customerId="customer-1" customerName="مشتری نمونه" onCancel={vi.fn()} onSaved={onSaved} />);

    const form = screen.getByRole('button', { name: 'ذخیره درخواست راه‌اندازی' }).closest('form');
    expect(form).not.toHaveAttribute('novalidate');

    await waitFor(() => expect(screen.getByLabelText('نصب مقصد')).toHaveValue('install-1'));
    expect(screen.getByLabelText('نوع شرکت')).toHaveValue('independent');
    expect(screen.getByLabelText('مالی')).toBeChecked();

    fireEvent.change(screen.getByLabelText('نام شرکت حسابداری'), { target: { value: 'شرکت حسابداری نمونه' } });
    fireEvent.change(screen.getByLabelText('نام کاربری مدیر اولیه'), { target: { value: 'initial-admin' } });
    fireEvent.change(screen.getByLabelText('ایمیل مدیر اولیه'), { target: { value: 'admin@example.com' } });
    fireEvent.click(screen.getByLabelText('فروش'));
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره درخواست راه‌اندازی' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    await screen.findByText('در انتظار پیکربندی امن امضا');

    const post = fetchMock.mock.calls.find(([url, init]) =>
      String(url) === '/api/vendor/provisioning-requests/' && init?.method === 'POST');
    expect(post).toBeDefined();
    const init = post?.[1] as RequestInit;
    expect(init.headers).toEqual({
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': 'csrf-provisioning',
    });
    const body = JSON.parse(String(init.body)) as Record<string, unknown>;
    expect(body).toMatchObject({
      requestId: 'request-1',
      customerId: 'customer-1',
      installationId: 'install-1',
      companyName: 'شرکت حسابداری نمونه',
      companyType: 'independent',
      initialAdminUserName: 'initial-admin',
      initialAdminEmail: 'admin@example.com',
      subsystems: ['finance', 'sales'],
      fiscalPeriodTitle: 'سال مالی ۱۴۰۵',
      fiscalPeriodStartDate: '2026-03-21',
      fiscalPeriodEndDate: '2027-03-20',
      parentCompanyReference: null,
    });
    expect(body).not.toHaveProperty('password');
    expect(body).not.toHaveProperty('passwordHash');
    expect(screen.getByText(/هنوز بسته‌ای امضا یا به مشتری تحویل نشده/)).toBeInTheDocument();
  });

  it('requires a parent-company reference for a subsidiary and sends it in schema v2 metadata', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100') {
        return response({ items: [{ id: 'install-1', installationCode: 'INSTALL-01', name: 'نصب اصلی', status: 'Active' }] });
      }
      if (url === '/api/vendor/provisioning-requests?customerId=customer-1&page=1&pageSize=10') return response({ items: [] });
      if (url === '/api/auth/antiforgery') return response({ requestToken: 'csrf-subsidiary' });
      if (url === '/api/vendor/provisioning-requests/' && init?.method === 'POST') {
        return response({ id: 'provision-2', status: 'AwaitingSigner' }, 201);
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValue('request-subsidiary') });

    render(<ProvisioningForm customerId="customer-1" customerName="مشتری نمونه"
      onCancel={vi.fn()} onSaved={vi.fn()} />);
    await waitFor(() => expect(screen.getByLabelText('نصب مقصد')).toHaveValue('install-1'));
    fireEvent.change(screen.getByLabelText('نوع شرکت'), { target: { value: 'subsidiary' } });
    expect(screen.getByLabelText('مرجع شرکت مادر در نصب مشتری')).toBeRequired();
    fireEvent.change(screen.getByLabelText('نام شرکت حسابداری'), { target: { value: 'شرکت تابعه نمونه' } });
    fireEvent.change(screen.getByLabelText('نام کاربری مدیر اولیه'), { target: { value: 'initial-admin' } });
    fireEvent.change(screen.getByLabelText('ایمیل مدیر اولیه'), { target: { value: 'admin@example.com' } });
    fireEvent.change(screen.getByLabelText('مرجع شرکت مادر در نصب مشتری'), { target: { value: 'parent-company-001' } });
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره درخواست راه‌اندازی' }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/vendor/provisioning-requests/', expect.objectContaining({ method: 'POST' })));
    const post = fetchMock.mock.calls.find(([url, init]) =>
      String(url) === '/api/vendor/provisioning-requests/' && init?.method === 'POST');
    const body = JSON.parse(String((post?.[1] as RequestInit).body)) as Record<string, unknown>;
    expect(body).toMatchObject({
      companyType: 'subsidiary',
      parentCompanyReference: 'parent-company-001',
      fiscalPeriodTitle: 'سال مالی ۱۴۰۵',
      fiscalPeriodStartDate: '2026-03-21',
      fiscalPeriodEndDate: '2027-03-20',
    });
  });

  it('requests two-person approval with the persisted RowVersion and shows pending review state', async () => {
    let approvalRequested = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100') {
        return response({ items: [{ id: 'install-1', installationCode: 'INSTALL-01', name: 'نصب اصلی', status: 'Active' }] });
      }
      if (url === '/api/vendor/provisioning-requests?customerId=customer-1&page=1&pageSize=10') {
        return response({ items: [{
          id: 'provision-1', requestId: 'request-1', status: 'AwaitingSigner',
          createdAtUtc: '2026-10-28T12:00:00Z', updatedAtUtc: '2026-10-28T12:00:00Z',
          payloadSha256: 'a'.repeat(64), rowVersion: 'AQIDBAUGBwg=',
          approvalStatus: approvalRequested ? 'Pending' : null,
        }] });
      }
      if (url === '/api/auth/antiforgery') return response({ requestToken: 'csrf-approval' });
      if (url === '/api/vendor/provisioning-requests/provision-1/approval-request' && init?.method === 'POST') {
        approvalRequested = true;
        return response({ id: 'approval-1', status: 'Pending', rowVersion: 'CAcGBQQDAgE=' }, 202);
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValue('unused-request-id') });

    render(<ProvisioningForm customerId="customer-1" customerName="مشتری نمونه"
      onCancel={vi.fn()} onSaved={vi.fn()} canRequestApproval />);

    fireEvent.click(await screen.findByRole('button', { name: 'ارسال برای تأیید دو نفره' }));
    await screen.findByText('در انتظار بررسی دو نفره');
    await screen.findByText(/درخواست برای بررسی دو نفره ارسال شد/);
    expect(screen.queryByRole('button', { name: 'ارسال برای تأیید دو نفره' })).not.toBeInTheDocument();

    const post = fetchMock.mock.calls.find(([url, init]) =>
      String(url) === '/api/vendor/provisioning-requests/provision-1/approval-request' && init?.method === 'POST');
    expect(post).toBeDefined();
    const init = post?.[1] as RequestInit;
    expect(init.headers).toEqual({
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': 'csrf-approval',
    });
    expect(JSON.parse(String(init.body))).toEqual({ rowVersion: 'AQIDBAUGBwg=' });
  });

  it('exports only a signed PendingDelivery package through the antiforgery-protected endpoint', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100') {
        return response({ items: [{ id: 'install-1', installationCode: 'INSTALL-01', name: 'نصب اصلی', status: 'Active' }] });
      }
      if (url === '/api/vendor/provisioning-requests?customerId=customer-1&page=1&pageSize=10') {
        return response({ items: [{
          id: 'provision-1', requestId: 'request-1', status: 'PendingDelivery',
          createdAtUtc: '2026-10-28T12:00:00Z', updatedAtUtc: '2026-10-28T12:00:00Z',
          payloadSha256: 'a'.repeat(64), rowVersion: 'AQIDBAUGBwg=',
        }] });
      }
      if (url === '/api/auth/antiforgery') return response({ requestToken: 'csrf-export' });
      if (url === '/api/vendor/provisioning-requests/provision-1/package/export' && init?.method === 'POST') {
        return response({
          formatVersion: 1, keyId: 'vendor-key-v1', algorithm: 'ECDSA-P256-SHA256',
          payloadJson: '{"schemaVersion":1}', signatureBase64: 'c2lnbmF0dXJl',
        });
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValue('unused-request-id') });
    const OriginalURL = globalThis.URL;
    class TestURL extends OriginalURL {
      static override createObjectURL = vi.fn((_blob: Blob | MediaSource) => 'blob:provisioning-package');
      static override revokeObjectURL = vi.fn((_url: string) => undefined);
    }
    vi.stubGlobal('URL', TestURL);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    render(<ProvisioningForm customerId="customer-1" customerName="مشتری نمونه"
      onCancel={vi.fn()} onSaved={vi.fn()} canExportPackage />);

    fireEvent.click(await screen.findByRole('button', { name: 'دانلود بستهٔ امضاشده' }));
    await screen.findByText(/بستهٔ امضاشده صادر و برای انتقال آفلاین دانلود شد/);

    const post = fetchMock.mock.calls.find(([url, init]) =>
      String(url) === '/api/vendor/provisioning-requests/provision-1/package/export' && init?.method === 'POST');
    expect(post).toBeDefined();
    const init = post?.[1] as RequestInit;
    expect(init.headers).toEqual({
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': 'csrf-export',
    });
    expect(JSON.parse(String(init.body))).toEqual({});
    expect(TestURL.createObjectURL).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
  });
  it('offers only active installations and selects the first active installation by default', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100') {
        return response({ items: [
          { id: 'install-disabled', installationCode: 'INSTALL-OFF', name: 'نصب غیرفعال', status: 'Inactive' },
          { id: 'install-active', installationCode: 'INSTALL-ON', name: 'نصب فعال', status: 'Active' },
        ] });
      }
      if (url === '/api/vendor/provisioning-requests?customerId=customer-1&page=1&pageSize=10') {
        return response({ items: [] });
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValue('unused-request-id') });

    render(<ProvisioningForm customerId="customer-1" customerName="مشتری نمونه"
      onCancel={vi.fn()} onSaved={vi.fn()} />);

    const installationSelect = await screen.findByLabelText('نصب مقصد');
    await waitFor(() => expect(installationSelect).toHaveValue('install-active'));
    expect(screen.queryByRole('option', { name: /نصب غیرفعال/ })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: /نصب فعال/ })).toBeInTheDocument();
  });

  it('shows the verified customer outcome and acknowledgement time in the request history', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/vendor/installations/?customerId=customer-1&page=1&pageSize=100') {
        return response({ items: [{ id: 'install-1', installationCode: 'INSTALL-01', name: 'نصب اصلی', status: 'Active' }] });
      }
      if (url === '/api/vendor/provisioning-requests?customerId=customer-1&page=1&pageSize=10') {
        return response({ items: [{
          id: 'provision-1',
          requestId: 'request-1',
          status: 'Confirmed',
          createdAtUtc: '2026-10-29T10:00:00Z',
          updatedAtUtc: '2026-10-29T11:00:00Z',
          confirmedAtUtc: '2026-10-29T11:00:00Z',
          acknowledgementResult: 'Applied',
          acknowledgedAtUtc: '2026-10-29T11:00:00Z',
          payloadSha256: 'a'.repeat(64),
        }] });
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValue('unused-request-id') });

    render(<ProvisioningForm customerId="customer-1" customerName="مشتری نمونه"
      onCancel={vi.fn()} onSaved={vi.fn()} />);

    await screen.findByText('تأییدشده توسط مصرف‌کننده مشتری');
    expect(screen.getByText('راه‌اندازی توسط مشتری اعمال شد')).toBeInTheDocument();
    const expectedTime = new Intl.DateTimeFormat('fa-IR', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date('2026-10-29T11:00:00Z'));
    expect(screen.getByText(expectedTime)).toBeInTheDocument();
  });

});
