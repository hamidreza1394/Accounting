import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { CustomerForm } from './CustomerForm';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

describe('Vendor customer form', () => {
  it('creates a commercial customer with a fresh antiforgery token', async () => {
    const onSaved = vi.fn();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ requestToken: 'csrf-create' }))
      .mockResolvedValueOnce(response({ id: 'customer-1' }, 201));
    vi.stubGlobal('fetch', fetchMock);

    render(<CustomerForm onCancel={vi.fn()} onSaved={onSaved} />);
    fireEvent.change(screen.getByLabelText('شماره مشتری'), { target: { value: 'C-100' } });
    fireEvent.change(screen.getByLabelText('نام حقوقی'), { target: { value: 'شرکت نمونه' } });
    fireEvent.change(screen.getByLabelText('نام نمایشی'), { target: { value: 'نمونه' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت مشتری' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/auth/antiforgery', expect.objectContaining({
      credentials: 'same-origin', cache: 'no-store',
    }));
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/vendor/customers/', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-create' },
      body: JSON.stringify({ customerNumber: 'C-100', legalName: 'شرکت نمونه', displayName: 'نمونه' }),
    }));
    expect(onSaved).toHaveBeenCalledOnce();
  });

  it('shows a stable duplicate-number conflict without exposing server details', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ requestToken: 'csrf-create' }))
      .mockResolvedValueOnce(response({ error: 'customer_number_exists', detail: 'database internals' }, 409));
    vi.stubGlobal('fetch', fetchMock);

    render(<CustomerForm onCancel={vi.fn()} onSaved={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('شماره مشتری'), { target: { value: 'C-100' } });
    fireEvent.change(screen.getByLabelText('نام حقوقی'), { target: { value: 'شرکت نمونه' } });
    fireEvent.change(screen.getByLabelText('نام نمایشی'), { target: { value: 'نمونه' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت مشتری' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('این شماره مشتری قبلاً ثبت شده است.');
    expect(screen.queryByText('database internals')).not.toBeInTheDocument();
  });

  it('sends the current RowVersion when editing and explains stale conflicts', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ requestToken: 'csrf-edit' }))
      .mockResolvedValueOnce(response({ error: 'stale_row_version' }, 409));
    vi.stubGlobal('fetch', fetchMock);

    render(<CustomerForm initial={{
      id: 'customer-1', rowVersion: 'AQIDBAUGBwg=', customerNumber: 'C-100',
      legalName: 'شرکت نمونه', displayName: 'نمونه',
    }} onCancel={vi.fn()} onSaved={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('نام نمایشی'), { target: { value: 'نام جدید' } });
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره تغییرات' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('اطلاعات مشتری تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.');
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/vendor/customers/customer-1', expect.objectContaining({
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-edit' },
      body: JSON.stringify({
        customerNumber: 'C-100', legalName: 'شرکت نمونه', displayName: 'نام جدید', rowVersion: 'AQIDBAUGBwg=',
      }),
    }));
  });
});
