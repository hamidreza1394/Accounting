import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { InstallationForm } from './InstallationForm';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

const customerPage = {
  items: [{ id: 'customer-1', customerNumber: 'C-001', displayName: 'شرکت نمونه' }],
  page: 1, pageSize: 100, totalCount: 1, totalPages: 1,
  hasPreviousPage: false, hasNextPage: false,
};

describe('Vendor installation form', () => {
  it('creates an installation with a real customer, default environment and fresh antiforgery token', async () => {
    const onSaved = vi.fn();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(customerPage))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-install' }))
      .mockResolvedValueOnce(response({ id: 'installation-1' }, 201));
    vi.stubGlobal('fetch', fetchMock);

    render(<InstallationForm canReadCustomers onCancel={vi.fn()} onSaved={onSaved} />);
    const customerSelect = await screen.findByRole('combobox', { name: 'مشتری تجاری' });
    expect(customerSelect).toHaveValue('customer-1');
    expect(screen.getByRole('combobox', { name: 'محیط نصب' })).toHaveValue('Production');
    fireEvent.change(screen.getByLabelText('کد نصب'), { target: { value: 'INST-001' } });
    fireEvent.change(screen.getByLabelText('نام نصب'), { target: { value: 'نصب عملیاتی' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت نصب' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/auth/antiforgery', expect.objectContaining({
      credentials: 'same-origin', cache: 'no-store',
    }));
    expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/vendor/installations/', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-install' },
      body: JSON.stringify({
        customerId: 'customer-1',
        installationCode: 'INST-001',
        name: 'نصب عملیاتی',
        environment: 'Production',
      }),
    }));
  });

  it('does not allow creation when customer-read permission is absent', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    render(<InstallationForm canReadCustomers={false} onCancel={vi.fn()} onSaved={vi.fn()} />);
    expect(screen.getByRole('alert')).toHaveTextContent('برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.');
    expect(screen.getByRole('button', { name: 'ثبت نصب' })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('sends the existing RowVersion on update and shows stale conflicts', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ customerNumber: 'C-001', displayName: 'شرکت نمونه' }))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-update' }))
      .mockResolvedValueOnce(response({ error: 'stale_row_version' }, 409));
    vi.stubGlobal('fetch', fetchMock);

    render(<InstallationForm initial={{
      id: 'installation-1', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=',
      installationCode: 'INST-001', name: 'نصب قدیمی', environment: 'Staging', status: 'Active',
    }} canReadCustomers onCancel={vi.fn()} onSaved={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('نام نصب'), { target: { value: 'نصب جدید' } });
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره تغییرات' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('اطلاعات نصب تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.');
    expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/vendor/installations/installation-1', expect.objectContaining({
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-update' },
      body: JSON.stringify({
        installationCode: 'INST-001', name: 'نصب جدید', environment: 'Staging', rowVersion: 'AQIDBAUGBwg=',
      }),
    }));
  });
});
