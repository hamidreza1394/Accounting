import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { RouterProvider } from '@/lib/router';
import { PageBreadcrumbBar, PageBreadcrumbProvider, usePageBreadcrumb } from './PageBreadcrumb';

/** A page that declares its breadcrumb like the real ones do (record name once known). */
function DeclaringPage({ name }: { name: string | null }) {
  usePageBreadcrumb(
    name
      ? [
          { label: 'منابع انسانی' },
          { label: 'کارکنان', to: '/hr/org/employees' },
          { label: name },
        ]
      : [{ label: 'منابع انسانی' }, { label: 'کارکنان', to: '/hr/org/employees' }, { label: 'پروندهٔ کارمند' }],
  );
  return <h1>صفحه</h1>;
}

function Workspace({ name, remountKey = 'a' }: { name: string | null; remountKey?: string }) {
  const [mountPage, setMountPage] = useState(true);
  return (
    <RouterProvider routes={[]}>
      <PageBreadcrumbProvider key={remountKey} fallback={[{ label: 'پروندهٔ کارمند' }]}>
        <PageBreadcrumbBar />
        <button type="button" onClick={() => setMountPage(false)}>حذف صفحه</button>
        {mountPage ? <DeclaringPage name={name} /> : null}
      </PageBreadcrumbProvider>
    </RouterProvider>
  );
}

describe('PageBreadcrumb (the single shell-level breadcrumb bar)', () => {
  it('shows the route-title fallback until a page declares its real path', async () => {
    render(<Workspace name={null} />);
    const bar = screen.getByRole('navigation', { name: 'مسیر صفحه' });
    // First paint is the fallback; the declaration effect follows it.
    expect(await screen.findByText('پروندهٔ کارمند', { selector: 'li span' })).toBeInTheDocument();
    expect(bar).toHaveTextContent('منابع انسانی');
  });

  it('replaces the fallback with the page declaration, including the record name once known', async () => {
    const user = userEvent.setup();
    const view = render(<Workspace name={null} />);
    await screen.findByText('پروندهٔ کارمند', { selector: 'li span' });
    view.rerender(<Workspace name="سارا کریمی" />);
    expect(await screen.findByText('سارا کریمی')).toBeInTheDocument();
    const crumb = screen.getByText('کارکنان', { selector: 'a' });
    expect(crumb).toHaveAttribute('href', '/app/hr/org/employees');
    await user.click(crumb); // no-op here; proves the crumb is a real link element
  });

  it('falls back to the route title again when the declaring page unmounts', async () => {
    const user = userEvent.setup();
    render(<Workspace name="سارا کریمی" />);
    expect(await screen.findByText('سارا کریمی')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'حذف صفحه' }));
    await waitFor(() => expect(screen.queryByText('سارا کریمی')).not.toBeInTheDocument());
    expect(screen.getByText('پروندهٔ کارمند')).toBeInTheDocument();
  });

  it('starts fresh on a remount (per-path provider key), never leaking the previous declaration', async () => {
    const view = render(<Workspace name="سارا کریمی" />);
    expect(await screen.findByText('سارا کریمی')).toBeInTheDocument();
    view.rerender(<Workspace name={null} remountKey="b" />);
    expect(screen.queryByText('سارا کریمی')).not.toBeInTheDocument();
    expect(await screen.findByText('پروندهٔ کارمند', { selector: 'li span' })).toBeInTheDocument();
  });
});
