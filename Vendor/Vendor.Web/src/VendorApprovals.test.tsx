import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';import { VendorApprovals } from './VendorApprovals';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

describe('Vendor approval queue', () => {
  it('reviews a pending request with a fresh antiforgery token and refreshes the queue', async () => {
    const approval = {
      id: 'approval-1',
      resourceType: 'installation',
      resourceId: 'installation-1',
      targetStatus: 'Suspended',
      requestedByUserId: 'requester-1',
      requestedAtUtc: '2026-10-27T08:00:00Z',
      status: 'Pending',
      rowVersion: 'AQIDBAUGBwg=',
    };
    let listCalls = 0;
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/vendor/approvals/?')) {
        listCalls += 1;
        return Promise.resolve(response({
          items: listCalls === 1 ? [approval] : [],
          page: 1, pageSize: 100, totalCount: listCalls === 1 ? 1 : 0,
          totalPages: listCalls === 1 ? 1 : 0, hasPreviousPage: false, hasNextPage: false,
        }));
      }
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-approval' }));
      if (url === '/api/vendor/approvals/approval-1/decision' && init?.method === 'POST')
        return Promise.resolve(response({ ...approval, status: 'Approved' }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<VendorApprovals permissions={['vendor.approvals.read', 'vendor.approvals.review']} />);
    expect(await screen.findByText('تعلیق')).toBeInTheDocument();
    expect(screen.getByText('مدیر مجاز می‌تواند درخواست ثبت‌شده را خودش تأیید یا رد کند.')).toBeInTheDocument();

    fireEvent.click(await screen.findByRole('button', { name: 'تأیید درخواست approval-1' }));
    expect(await screen.findByRole('status')).toHaveTextContent('درخواست تأیید و تغییر وضعیت اعمال شد.');
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/approvals/approval-1/decision', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-approval' },
      body: JSON.stringify({ decision: 'Approved', rowVersion: 'AQIDBAUGBwg=', comment: null }),
    }));
    expect(await screen.findByText('درخواست در انتظاری وجود ندارد.')).toBeInTheDocument();
  });

  it('renders payment verification requests with their dedicated labels and reviews them', async () => {
    const paymentApproval = {
      id: 'approval-2',
      resourceType: 'salesOrderPayment',
      resourceId: 'payment-9',
      targetStatus: 'Verified',
      requestedByUserId: 'requester-2',
      requestedAtUtc: '2026-11-20T10:00:00Z',
      status: 'Pending',
      rowVersion: 'AQIDBAUGBwg=',
    };
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/vendor/approvals/?')) {
        return Promise.resolve(response({
          items: [paymentApproval],
          page: 1, pageSize: 100, totalCount: 1,
          totalPages: 1, hasPreviousPage: false, hasNextPage: false,
        }));
      }
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-payment' }));
      if (url === '/api/vendor/approvals/approval-2/decision' && init?.method === 'POST')
        return Promise.resolve(response({ ...paymentApproval, status: 'Approved' }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<VendorApprovals permissions={['vendor.approvals.read', 'vendor.approvals.review']} />);
    expect(await screen.findByText('پرداخت سفارش')).toBeInTheDocument();
    expect(screen.getByText('تأیید پرداخت')).toBeInTheDocument();
    expect(screen.queryByText('نصب')).not.toBeInTheDocument();

    fireEvent.click(await screen.findByRole('button', { name: 'تأیید درخواست approval-2' }));
    expect(await screen.findByRole('status')).toHaveTextContent('درخواست تأیید و تغییر وضعیت اعمال شد.');
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/approvals/approval-2/decision', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-payment' },
      body: JSON.stringify({ decision: 'Approved', rowVersion: 'AQIDBAUGBwg=', comment: null }),
    }));
  });

  it('renders license amendment issuance requests with their dedicated labels', async () => {
    const amendmentApproval = {
      id: 'approval-3',
      resourceType: 'licenseAmendment',
      resourceId: 'amendment-7',
      targetStatus: 'Issued',
      requestedByUserId: 'requester-3',
      requestedAtUtc: '2026-11-20T11:00:00Z',
      status: 'Pending',
      rowVersion: 'AQIDBAUGBwg=',
    };
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/vendor/approvals/?')) {
        return Promise.resolve(response({
          items: [amendmentApproval],
          page: 1, pageSize: 100, totalCount: 1,
          totalPages: 1, hasPreviousPage: false, hasNextPage: false,
        }));
      }
      if (url === '/api/auth/antiforgery') return Promise.resolve(response({ requestToken: 'csrf-amendment' }));
      if (url === '/api/vendor/approvals/approval-3/decision' && init?.method === 'POST')
        return Promise.resolve(response({ ...amendmentApproval, status: 'Approved' }));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<VendorApprovals permissions={['vendor.approvals.read', 'vendor.approvals.review']} />);
    expect(await screen.findByText('Amendment لایسنس')).toBeInTheDocument();
    expect(screen.getByText('صدور Amendment')).toBeInTheDocument();
    expect(screen.queryByText('نصب')).not.toBeInTheDocument();
    // Without vendor.licenses.issue the amendment controls stay disabled (server is authoritative).
    expect(screen.getByRole('button', { name: 'تأیید درخواست approval-3' })).toBeDisabled();

    cleanup();
    render(<VendorApprovals permissions={['vendor.approvals.read', 'vendor.approvals.review', 'vendor.licenses.issue']} />);
    fireEvent.click(await screen.findByRole('button', { name: 'تأیید درخواست approval-3' }));
    expect(await screen.findByRole('status')).toHaveTextContent('درخواست تأیید و تغییر وضعیت اعمال شد.');
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/approvals/approval-3/decision', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-amendment' },
      body: JSON.stringify({ decision: 'Approved', rowVersion: 'AQIDBAUGBwg=', comment: null }),
    }));
  });
});
