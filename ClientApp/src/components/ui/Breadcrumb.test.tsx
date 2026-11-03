import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { RouterProvider } from '@/lib/router';
import { Breadcrumb } from './Breadcrumb';

describe('Breadcrumb', () => {
  it('lists the path, links the ancestors and marks the current page', async () => {
    window.history.pushState(null, '', '/app/admin/users/7');
    const user = userEvent.setup();
    render(
      <RouterProvider routes={[]}>
        <Breadcrumb items={[{ label: 'خانه', to: '/' }, { label: 'کاربران', to: '/admin/users' }, { label: 'علی رضایی' }]} />
      </RouterProvider>,
    );
    expect(screen.getByRole('navigation', { name: 'مسیر صفحه' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'کاربران' })).toHaveAttribute('href', '/app/admin/users');
    expect(screen.getByText('علی رضایی')).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', { name: 'علی رضایی' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: 'کاربران' }));
    expect(window.location.pathname).toBe('/app/admin/users');
  });
});
