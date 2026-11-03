import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { catalogItems, perm } from '../../test/roles';
import { PermissionTree, type PermissionTreeProps } from './PermissionTree';

function Harness({ initial = [], onChange, ...rest }: { initial?: string[]; onChange?: (next: string[]) => void } & Partial<PermissionTreeProps>) {
  const [value, setValue] = useState<string[]>(initial);
  return (
    <PermissionTree
      permissions={catalogItems}
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
      {...rest}
    />
  );
}

const leaf = (name: string) => screen.getByRole('checkbox', { name });
const group = (name: string) => screen.getByRole('checkbox', { name: `انتخاب همهٔ مجوزهای ${name}` });

describe('PermissionTree', () => {
  it('groups by subsystem and resource with accessible names, codes and descriptions', () => {
    render(<Harness />);
    expect(screen.getByRole('button', { name: /مدیریت سیستم/ })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', { name: /بازرگانی/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: 'reports.company' })).toBeInTheDocument();
    const row = leaf('حذف سفارش');
    expect(row).toHaveAccessibleDescription(/trade\.orders\.delete/);
    expect(row).toHaveAccessibleDescription(/حذف سفارش پیش‌نویس/);
    expect(screen.getByText('trade.orders.delete')).toHaveAttribute('dir', 'ltr');
  });

  it('shows UI-derived operation badges only for the six known operations', () => {
    render(<Harness />);
    const deleteRow = leaf('حذف سفارش').closest('label') as HTMLElement;
    expect(within(deleteRow).getByText('حذف')).toBeInTheDocument();
    const approveRow = leaf('تأیید سفارش').closest('label') as HTMLElement;
    expect(within(approveRow).queryByText(/^(مشاهده|فهرست|ایجاد|ویرایش|حذف|مدیریت)$/)).toBeNull();
    expect(within(leaf('مدیریت گروه‌های دسترسی').closest('label') as HTMLElement).getByText('مدیریت')).toBeInTheDocument();
  });

  it('keeps tri-state parents in sync with the selection and toggles a whole group', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial={['trade.orders.view']} onChange={onChange} />);
    // Native `indeterminate` is exposed as the mixed state; no redundant aria-checked is set.
    expect(group('بازرگانی')).toHaveProperty('indeterminate', true);
    expect(group('بازرگانی')).not.toHaveAttribute('aria-checked');
    expect(group('orders')).toHaveProperty('indeterminate', true);
    expect(group('reports.company')).toHaveProperty('indeterminate', false);
    expect(group('reports.company')).not.toBeChecked();
    expect(group('مدیریت سیستم')).not.toBeChecked();

    await user.click(group('بازرگانی'));
    expect(group('بازرگانی')).toBeChecked();
    expect(group('بازرگانی')).toHaveProperty('indeterminate', false);
    expect(onChange).toHaveBeenLastCalledWith([
      'trade.orders.approve',
      'trade.orders.create',
      'trade.orders.delete',
      'trade.orders.view',
      'trade.reports.company.view',
    ]);

    await user.click(leaf('گزارش شرکتی'));
    expect(group('بازرگانی')).toHaveProperty('indeterminate', true);
    expect(group('reports.company')).not.toBeChecked();

    await user.click(group('orders'));
    expect(onChange).toHaveBeenLastCalledWith([]);
    expect(group('بازرگانی')).not.toBeChecked();
    expect(group('بازرگانی')).toHaveProperty('indeterminate', false);
  });

  it('reports counts of selected permissions in total and per group', async () => {
    const user = userEvent.setup();
    render(<Harness initial={['admin.users.view', 'trade.orders.view']} />);
    expect(screen.getByText('۲ از ۱۰ مجوز انتخاب شده است.')).toBeInTheDocument();
    // The count is visible but is not a live region, so toggling does not announce every change.
    expect(screen.getByText('۲ از ۱۰ مجوز انتخاب شده است.').closest('[aria-live]')).toBeNull();
    expect(screen.getByRole('button', { name: /مدیریت سیستم/ })).toHaveTextContent('۱ از ۵');
    await user.click(leaf('ایجاد سفارش'));
    expect(screen.getByRole('button', { name: /بازرگانی/ })).toHaveTextContent('۲ از ۵');
  });

  it('filters by code, name or description (debounced), opens matches and counts them', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی مجوزها' }), 'پیش‌نویس');
    await waitFor(() => expect(screen.queryByRole('checkbox', { name: 'ایجاد سفارش' })).toBeNull());
    expect(leaf('حذف سفارش')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('۱ مجوز با جستجو مطابقت دارد');
    expect(screen.getByRole('button', { name: /بازرگانی/ })).toHaveTextContent('۰ از ۱ نتیجه');
    expect(screen.queryByRole('button', { name: /مدیریت سیستم/ })).toBeNull();

    await user.clear(screen.getByRole('searchbox', { name: 'جستجوی مجوزها' }));
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی مجوزها' }), 'zzz');
    expect(await screen.findByText('مجوزی با این جستجو پیدا نشد.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن جستجوی مجوزها' }));
    expect(leaf('ایجاد سفارش')).toBeInTheDocument();
  });

  it('applies group select-all and clear only to the permissions matching the search', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی مجوزها' }), 'orders.view');
    await waitFor(() => expect(screen.queryByRole('checkbox', { name: 'حذف سفارش' })).toBeNull());
    await user.click(group('بازرگانی'));
    expect(onChange).toHaveBeenLastCalledWith(['trade.orders.view']);
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن نتایج' }));
    expect(onChange).toHaveBeenLastCalledWith([]);
  });

  it('supports keyboard use: arrows between group headers, Enter to collapse, Space on a checkbox', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    const admin = screen.getByRole('button', { name: /مدیریت سیستم/ });
    const trade = screen.getByRole('button', { name: /بازرگانی/ });
    admin.focus();
    await user.keyboard('{ArrowDown}');
    expect(trade).toHaveFocus();
    await user.keyboard('{ArrowUp}');
    expect(admin).toHaveFocus();
    await user.keyboard('{End}');
    expect(trade).toHaveFocus();
    await user.keyboard('{Home}');
    expect(admin).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(admin).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('checkbox', { name: 'مشاهدهٔ کاربران' })).toBeNull();
    await user.keyboard(' ');
    expect(admin).toHaveAttribute('aria-expanded', 'true');

    leaf('مشاهدهٔ کاربران').focus();
    await user.keyboard(' ');
    expect(onChange).toHaveBeenLastCalledWith(['admin.users.view']);
  });

  it('is read-only: items stay focusable but nothing can be changed; groups can still be opened', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial={['admin.users.view']} readOnly onChange={onChange} />);
    expect(leaf('مشاهدهٔ کاربران')).toBeChecked();
    expect(leaf('مشاهدهٔ کاربران')).not.toBeDisabled();
    expect(leaf('مشاهدهٔ کاربران')).toHaveAttribute('aria-readonly', 'true');
    leaf('مشاهدهٔ کاربران').focus();
    expect(leaf('مشاهدهٔ کاربران')).toHaveFocus();
    expect(screen.getByRole('button', { name: 'انتخاب همه' })).toBeDisabled();
    expect(screen.getByText(/فقط‌خواندنی/)).toBeInTheDocument();
    await user.click(leaf('مشاهدهٔ کاربران'));
    await user.keyboard(' ');
    await user.click(group('بازرگانی'));
    expect(onChange).not.toHaveBeenCalled();
    expect(leaf('مشاهدهٔ کاربران')).toBeChecked();
    expect(leaf('ایجاد کاربر')).not.toBeChecked();
    await user.click(screen.getByRole('button', { name: /بازرگانی/ }));
    expect(screen.getByRole('button', { name: /بازرگانی/ })).toHaveAttribute('aria-expanded', 'false');
  });

  it('lists codes unknown to the catalog and lets the user remove them', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial={['legacy.thing.view', 'admin.users.view']} onChange={onChange} />);
    expect(screen.getByRole('heading', { level: 3, name: /مجوزهای ناشناخته \(۱\)/ })).toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', { name: 'مجوز ناشناخته' }));
    expect(onChange).toHaveBeenLastCalledWith(['admin.users.view']);
    expect(screen.queryByRole('heading', { name: /مجوزهای ناشناخته/ })).toBeNull();
  });

  it('is locked while disabled (submitting)', () => {
    render(<Harness disabled />);
    for (const box of screen.getAllByRole('checkbox')) expect(box).toBeDisabled();
  });

  it('keeps codes unknown to the catalog and emits the rest in catalog order', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial={['legacy.thing.view']} onChange={onChange} />);
    await user.click(leaf('مشاهدهٔ کاربران'));
    await user.click(leaf('مدیریت گروه‌های دسترسی'));
    expect(onChange).toHaveBeenLastCalledWith(['admin.roles.manage', 'admin.users.view', 'legacy.thing.view']);
  });

  it('starts collapsed for a large catalog and stays responsive with about 200 permissions', async () => {
    const user = userEvent.setup();
    const big = Array.from({ length: 200 }, (_, index) => perm(`mod${index % 5}.res${Math.floor(index / 10) % 4}.act${index}`, `مجوز ${index}`, `توضیح ${index}`));
    const onChange = vi.fn();
    render(<PermissionTree permissions={big} value={[]} onChange={onChange} />);
    // Only the five collapsed group checkboxes exist; no leaf is rendered yet.
    expect(screen.getAllByRole('checkbox')).toHaveLength(5);
    expect(screen.getByRole('button', { name: /mod0/ })).toHaveAttribute('aria-expanded', 'false');
    await user.click(screen.getByRole('button', { name: 'بازکردن همه' }));
    expect(screen.getAllByRole('checkbox').length).toBeGreaterThan(200);
    await user.click(screen.getByRole('button', { name: 'انتخاب همه' }));
    expect(onChange).toHaveBeenLastCalledWith(big.map((item) => item.code));
  });
});
