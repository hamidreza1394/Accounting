import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ContractForm } from './ContractForm';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

const customerPage = {
  items: [{ id: 'customer-1', customerNumber: 'C-001', displayName: 'شرکت نمونه' }],
  page: 1, pageSize: 100, totalCount: 1, totalPages: 1,
  hasPreviousPage: false, hasNextPage: false,
};

describe('Vendor contract form', () => {
  it('creates a Draft contract with a real customer, UTC dates, and a fresh antiforgery token', async () => {
    const onSaved = vi.fn();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(customerPage))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-contract' }))
      .mockResolvedValueOnce(response({ id: 'contract-1', status: 'Draft' }, 201));
    vi.stubGlobal('fetch', fetchMock);

    render(<ContractForm canReadCustomers onCancel={vi.fn()} onSaved={onSaved} />);
    const customerSelect = await screen.findByRole('combobox', { name: 'مشتری تجاری' });
    await waitFor(() => expect(customerSelect).toHaveValue('customer-1'));
    expect(await screen.findByRole('combobox', { name: 'ارز قرارداد' })).toHaveValue('IRR');
    fireEvent.change(screen.getByLabelText('شماره قرارداد'), { target: { value: 'CTR-001' } });
    fireEvent.change(screen.getByLabelText('تاریخ شروع'), { target: { value: '2026-10-27' } });
    fireEvent.change(screen.getByLabelText('تاریخ پایان (اختیاری)'), { target: { value: '2027-10-26' } });
    fireEvent.change(await screen.findByRole('combobox', { name: 'ارز قرارداد' }), { target: { value: 'IRR' } });
    fireEvent.click(screen.getByRole('button', { name: 'ثبت قرارداد' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/auth/antiforgery', expect.objectContaining({
      credentials: 'same-origin', cache: 'no-store',
    }));
    expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/vendor/contracts/', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-contract' },
      body: JSON.stringify({
        customerId: 'customer-1',
        contractNumber: 'CTR-001',
        startsAtUtc: '2026-10-27T00:00:00.000Z',
        endsAtUtc: '2027-10-26T00:00:00.000Z',
        currencyCode: 'IRR',
      }),
    }));
  });

  it('preserves an existing currency outside the common quick-select list', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({
      customerNumber: 'C-001', displayName: 'شرکت نمونه',
    })));

    render(<ContractForm initial={{
      id: 'contract-1', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=',
      contractNumber: 'CTR-001', startsAtUtc: '2026-01-01T00:00:00Z', endsAtUtc: '',
      currencyCode: 'CAD', status: 'Draft',
    }} canReadCustomers onCancel={vi.fn()} onSaved={vi.fn()} />);

    const currencySelect = await screen.findByRole('combobox', { name: 'ارز قرارداد' });
    expect(currencySelect).toHaveValue('CAD');
    expect(screen.getByRole('option', { name: 'ارز فعلی ثبت‌شده (CAD)' })).toBeInTheDocument();
  });

  it('does not allow creation without customer-read permission or with reversed dates', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    render(<ContractForm canReadCustomers={false} onCancel={vi.fn()} onSaved={vi.fn()} />);

    expect(screen.getByRole('alert')).toHaveTextContent('برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.');
    expect(screen.getByRole('button', { name: 'ثبت قرارداد' })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('sends the existing RowVersion when editing and explains stale conflicts', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ customerNumber: 'C-001', displayName: 'شرکت نمونه' }))
      .mockResolvedValueOnce(response({ requestToken: 'csrf-contract-edit' }))
      .mockResolvedValueOnce(response({ error: 'stale_row_version' }, 409));
    vi.stubGlobal('fetch', fetchMock);

    render(<ContractForm initial={{
      id: 'contract-1', customerId: 'customer-1', rowVersion: 'AQIDBAUGBwg=',
      contractNumber: 'CTR-001', startsAtUtc: '2026-01-01T00:00:00Z', endsAtUtc: '',
      currencyCode: 'IRR', status: 'Draft',
    }} canReadCustomers onCancel={vi.fn()} onSaved={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('شماره قرارداد'), { target: { value: 'CTR-002' } });
    fireEvent.click(screen.getByRole('button', { name: 'ذخیره تغییرات' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('قرارداد تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.');
    expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/vendor/contracts/contract-1', expect.objectContaining({
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-contract-edit' },
      body: JSON.stringify({
        contractNumber: 'CTR-002',
        startsAtUtc: '2026-01-01T00:00:00.000Z',
        endsAtUtc: null,
        currencyCode: 'IRR',
        rowVersion: 'AQIDBAUGBwg=',
      }),
    }));
  });
});
