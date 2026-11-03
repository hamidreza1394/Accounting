import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { json, stubApi } from '../../test/adminUsers';
import { myRequest, pageBody } from '../../test/membership';
import { MyMembershipRequestsPage } from './MyMembershipRequestsPage';

const onUnauthenticated = vi.fn((): boolean => false);

const rows = [
  myRequest(),
  myRequest({
    id: 'req-2',
    destinationCompanyName: 'شرکت دوم',
    status: 'Approved',
    decidedAtUtc: '2026-11-02T09:00:00Z',
    approvedRoleCode: 'ACCOUNTANT',
    approvedRoleName: 'حسابدار',
  }),
  myRequest({
    id: 'req-3',
    destinationCompanyName: 'شرکت سوم',
    status: 'Rejected',
    decidedAtUtc: '2026-11-02T10:00:00Z',
    decisionReason: 'سمت نامرتبط',
  }),
];

describe('MyMembershipRequestsPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists the own requests with status text, role and reason, and says a request grants no access', async () => {
    const { calls } = stubApi(() => json(pageBody(rows)));
    render(<MyMembershipRequestsPage onUnauthenticated={onUnauthenticated} />);

    expect(await screen.findByRole('heading', { level: 1, name: 'درخواست‌های عضویت من' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('هیچ دسترسی‌ای');

    const pending = (await screen.findByRole('cell', { name: 'شرکت مقصد' })).closest('tr') as HTMLElement;
    expect(within(pending).getByText('در انتظار تأیید')).toBeInTheDocument();
    const approved = screen.getByText('شرکت دوم').closest('tr') as HTMLElement;
    expect(within(approved).getByText('تأییدشده')).toBeInTheDocument();
    expect(within(approved).getByText('حسابدار')).toBeInTheDocument();
    const rejected = screen.getByText('شرکت سوم').closest('tr') as HTMLElement;
    expect(within(rejected).getByText('ردشده')).toBeInTheDocument();
    expect(within(rejected).getByText('سمت نامرتبط')).toBeInTheDocument();
    expect(calls.map((call) => call.path)).toEqual(['/api/v1/me/membership-requests']);
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
  });

  it('shows an empty state when there is no request', async () => {
    stubApi(() => json(pageBody([])));
    render(<MyMembershipRequestsPage onUnauthenticated={onUnauthenticated} />);
    expect(await screen.findByText('درخواست عضویتی ثبت نشده است')).toBeInTheDocument();
  });

  it('shows an error and retries', async () => {
    const user = userEvent.setup();
    let fail = true;
    stubApi(() => (fail ? new Response(null, { status: 500 }) : json(pageBody(rows))));
    render(<MyMembershipRequestsPage onUnauthenticated={onUnauthenticated} />);
    const retry = await screen.findByRole('button', { name: /تلاش/ });
    fail = false;
    await user.click(retry);
    expect(await screen.findByText('شرکت دوم')).toBeInTheDocument();
  });

  it('hands a 401 to the unauthenticated flow', async () => {
    onUnauthenticated.mockReturnValue(true);
    stubApi(() => new Response(null, { status: 401 }));
    render(<MyMembershipRequestsPage onUnauthenticated={onUnauthenticated} />);
    await vi.waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });
});
