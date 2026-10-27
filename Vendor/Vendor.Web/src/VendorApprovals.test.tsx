import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { VendorApprovals } from './VendorApprovals';

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

    fireEvent.click(await screen.findByRole('button', { name: 'تأیید درخواست approval-1' }));
    expect(await screen.findByRole('status')).toHaveTextContent('درخواست با موفقیت تأیید و اعمال شد.');
    expect(fetchMock).toHaveBeenCalledWith('/api/vendor/approvals/approval-1/decision', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': 'csrf-approval' },
      body: JSON.stringify({ decision: 'Approved', rowVersion: 'AQIDBAUGBwg=', comment: null }),
    }));
    expect(await screen.findByText('درخواست در انتظاری وجود ندارد.')).toBeInTheDocument();
  });
});
