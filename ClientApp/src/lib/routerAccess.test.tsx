import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AccessStatus } from './permissions';
import { RouterProvider, type Route } from './router';

const routes: Route[] = [
  { path: '/', element: () => <h1>خانه</h1>, title: 'خانه' },
  { path: '/admin/roles', element: () => <h1>نقش‌ها</h1>, title: 'نقش‌ها', permission: 'admin.roles.view' },
  { path: '/account/membership-requests', element: () => <h1>درخواست‌های من</h1>, title: 'درخواست‌ها', permission: 'account.view' },
];

function setup(path: string, accessStatus: AccessStatus, allowed: boolean, onRetryAccess = vi.fn()) {
  window.history.replaceState(null, '', path);
  render(<RouterProvider routes={routes} canAccess={(permission) => permission === 'account.view' || allowed} accessStatus={accessStatus} onRetryAccess={onRetryAccess} />);
  return onRetryAccess;
}

describe('route guard while permissions are not known (fail-closed)', () => {
  beforeEach(() => window.history.replaceState(null, '', '/app/'));

  it('shows a loading state, not the page and not "forbidden", while permissions load', () => {
    setup('/app/admin/roles', 'loading', false);
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'نقش‌ها' })).not.toBeInTheDocument();
    expect(screen.queryByText('دسترسی ندارید')).not.toBeInTheDocument();
  });

  it('does not render the page when permissions failed, and offers a retry', async () => {
    const retry = setup('/app/admin/roles', 'error', false);
    expect(screen.queryByRole('heading', { name: 'نقش‌ها' })).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('دریافت مجوزها');
    await userEvent.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(retry).toHaveBeenCalledOnce();
  });

  it('shows forbidden once permissions are known and deny the route', () => {
    setup('/app/admin/roles', 'ready', false);
    expect(screen.getByText('شما مجوز مشاهدهٔ این صفحه را ندارید.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'نقش‌ها' })).not.toBeInTheDocument();
  });

  it('renders the page once permissions are known and allow the route', () => {
    setup('/app/admin/roles', 'ready', true);
    expect(screen.getByRole('heading', { name: 'نقش‌ها' })).toBeInTheDocument();
  });

  it('never blocks pages that need no company permission', () => {
    setup('/app/', 'loading', false);
    expect(screen.getByRole('heading', { name: 'خانه' })).toBeInTheDocument();
  });

  it('keeps account pages available while permissions load or fail', () => {
    setup('/app/account/membership-requests', 'error', false);
    expect(screen.getByRole('heading', { name: 'درخواست‌های من' })).toBeInTheDocument();
  });
});
