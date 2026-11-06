import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider } from '@/lib/router';
import { describe, expect, it } from 'vitest';
import { HrUnitTree } from './HrUnitTree';
import { subtreeNode, unitId } from '../test/hrFixtures';

const detailHref = (id: string) => `/hr/org/units/${id}`;

const sample = [
  subtreeNode(
    { id: unitId, code: 'HQ', name: 'دفتر مرکزی', isActive: true, activeAssignmentCount: 3 },
    [
      subtreeNode(
        { id: 'b1b2c3d4-4444-4000-8000-00000000ef02', code: 'SALES', name: 'فروش', isActive: false, activeAssignmentCount: 0 },
        [
          subtreeNode({ id: 'c2c3d4e5-5555-4000-8000-00000000ef03', code: 'SALES.INT', name: 'فروش داخلی', isActive: true, activeAssignmentCount: 2 }),
        ],
      ),
    ],
  ),
];

function renderTree() {
  return render(
    <RouterProvider routes={[]}>
      <HrUnitTree nodes={sample} detailHref={detailHref} />
    </RouterProvider>,
  );
}

describe('HrUnitTree', () => {
  it('renders the hierarchy with codes, inactive badges and active assignment counts', () => {
    renderTree();
    expect(screen.getByRole('link', { name: /دفتر مرکزی/ })).toHaveAttribute('href', `/app/hr/org/units/${unitId}`);
    expect(screen.getByText('HQ')).toBeInTheDocument();
    expect(screen.getByText(/۳ انتساب فعال/)).toBeInTheDocument();
    // The first level is open by default; deeper levels start collapsed.
    expect(screen.getByText('فروش')).toBeInTheDocument();
    expect(screen.queryByText('فروش داخلی')).not.toBeInTheDocument();
  });

  it('expands nested branches and links to the unit detail pages', async () => {
    const user = userEvent.setup();
    renderTree();
    await user.click(screen.getByRole('button', { name: /باز کردن|نمایش زیرمجموعه‌های فروش/ }));
    expect(screen.getByText('فروش داخلی')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /فروش داخلی/ })).toHaveAttribute('href', '/app/hr/org/units/c2c3d4e5-5555-4000-8000-00000000ef03');
    expect(screen.getAllByText('غیرفعال').length).toBeGreaterThan(0);
  });

  it('shows the empty state for a company without units', () => {
    render(
      <RouterProvider routes={[]}>
        <HrUnitTree nodes={[]} detailHref={detailHref} emptyLabel="هنوز واحدی ثبت نشده است" />
      </RouterProvider>,
    );
    expect(screen.getByText('هنوز واحدی ثبت نشده است')).toBeInTheDocument();
  });

  it('exposes the expander state to assistive technology', async () => {
    const user = userEvent.setup();
    renderTree();
    const rootExpander = screen.getByRole('button', { name: /زیرمجموعه‌های دفتر مرکزی/ });
    expect(rootExpander).toHaveAttribute('aria-expanded', 'true');
    await user.click(rootExpander);
    expect(rootExpander).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByText('فروش')).not.toBeInTheDocument();
  });
});
