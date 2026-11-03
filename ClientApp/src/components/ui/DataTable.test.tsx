import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RouterProvider } from '@/lib/router';
import { DataTable, type DataTableColumn } from './DataTable';

type Row = { id: string; name: string };
const rows: Row[] = [
  { id: '1', name: 'علی' },
  { id: '2', name: 'مریم' },
];
const columns: DataTableColumn<Row>[] = [
  { key: 'name', header: 'نام', sortable: true, cell: (row) => row.name },
  { key: 'id', header: 'شناسه', cell: (row) => row.id },
];

describe('DataTable', () => {
  it('renders an accessible table with caption, column headers and rows', () => {
    render(<DataTable caption="فهرست کاربران" columns={columns} rows={rows} getRowKey={(row) => row.id} />);
    expect(screen.getByRole('table', { name: 'فهرست کاربران' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'نام' })).toBeInTheDocument();
    expect(screen.getAllByRole('row')).toHaveLength(3);
    expect(screen.getByRole('region', { name: 'فهرست کاربران' })).toHaveAttribute('tabindex', '0');
  });

  it('exposes aria-sort and requests sorting through the callback', async () => {
    const user = userEvent.setup();
    const onSortChange = vi.fn();
    const { rerender } = render(
      <DataTable caption="c" columns={columns} rows={rows} getRowKey={(row) => row.id} sort={null} onSortChange={onSortChange} />,
    );
    expect(screen.getByRole('columnheader', { name: 'نام' })).toHaveAttribute('aria-sort', 'none');
    expect(screen.getByRole('columnheader', { name: 'شناسه' })).not.toHaveAttribute('aria-sort');
    await user.click(screen.getByRole('button', { name: 'نام' }));
    expect(onSortChange).toHaveBeenLastCalledWith({ key: 'name', direction: 'asc' });
    rerender(
      <DataTable caption="c" columns={columns} rows={rows} getRowKey={(row) => row.id} sort={{ key: 'name', direction: 'asc' }} onSortChange={onSortChange} />,
    );
    expect(screen.getByRole('columnheader', { name: 'نام' })).toHaveAttribute('aria-sort', 'ascending');
    await user.click(screen.getByRole('button', { name: 'نام' }));
    expect(onSortChange).toHaveBeenLastCalledWith({ key: 'name', direction: 'desc' });
  });

  it('shows skeleton rows with a status while loading, without data rows', () => {
    render(<DataTable caption="c" columns={columns} rows={rows} getRowKey={(row) => row.id} loading />);
    expect(screen.getByRole('status')).toHaveTextContent('در حال بارگذاری…');
    expect(screen.getByRole('table', { name: 'c' })).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByText('علی')).not.toBeInTheDocument();
  });

  it('shows the empty state', () => {
    render(<DataTable caption="c" columns={columns} rows={[]} getRowKey={(row) => row.id} emptyTitle="کاربری نیست" />);
    expect(screen.getByText('کاربری نیست')).toBeInTheDocument();
  });

  it('shows the error state with retry', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<DataTable caption="c" columns={columns} rows={[]} getRowKey={(row) => row.id} error="بارگذاری ناموفق" onRetry={onRetry} />);
    expect(screen.getByRole('alert')).toHaveTextContent('بارگذاری ناموفق');
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('shows the range and total, pages and changes the page size', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    const onPageSizeChange = vi.fn();
    render(
      <DataTable
        caption="c"
        columns={columns}
        rows={rows}
        getRowKey={(row) => row.id}
        pagination={{ page: 2, pageSize: 10, total: 25, onPageChange, onPageSizeChange }}
      />,
    );
    expect(screen.getByText(/نمایش ۱۱ تا ۲۰ از ۲۵/)).toBeInTheDocument();
    expect(screen.getByText(/صفحه ۲ از ۳/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    expect(onPageChange).toHaveBeenLastCalledWith(3);
    await user.click(screen.getByRole('button', { name: 'قبلی' }));
    expect(onPageChange).toHaveBeenLastCalledWith(1);
    await user.selectOptions(screen.getByLabelText('تعداد در صفحه'), '50');
    expect(onPageSizeChange).toHaveBeenCalledWith(50);
  });

  it('disables previous on the first page and next on the last', () => {
    render(
      <DataTable caption="c" columns={columns} rows={rows} getRowKey={(row) => row.id} pagination={{ page: 1, pageSize: 10, total: 5, onPageChange: () => undefined }} />,
    );
    expect(screen.getByRole('button', { name: 'قبلی' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'بعدی' })).toBeDisabled();
  });

  it('makes rows navigable through a real link in the first cell', async () => {
    window.history.pushState(null, '', '/app/admin/users');
    const user = userEvent.setup();
    render(
      <RouterProvider routes={[]}>
        <DataTable caption="c" columns={columns} rows={rows} getRowKey={(row) => row.id} rowHref={(row) => `/admin/users/${row.id}`} />
      </RouterProvider>,
    );
    const link = within(screen.getAllByRole('row')[1] as HTMLElement).getByRole('link', { name: 'علی' });
    expect(link).toHaveAttribute('href', '/app/admin/users/1');
    await user.click(link);
    expect(window.location.pathname).toBe('/app/admin/users/1');
  });
});
