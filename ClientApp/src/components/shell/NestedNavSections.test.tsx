import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Building2, Network, Users } from 'lucide-react';
import { beforeEach, describe, expect, it } from 'vitest';
import { RouterProvider } from '@/lib/router';
import { NestedNavSections, type NestedNavLeaf } from './NestedNavSections';

const leaves: NestedNavLeaf[] = [
  { label: 'ورودی ساده', icon: Users, to: '/plain' },
  { label: 'واحد', icon: Building2, to: '/unit', section: 'بخش نمونه', sectionIcon: Network },
  { label: 'کارمند', icon: Users, to: '/emp', section: 'بخش نمونه' },
  { label: 'به‌زودی', icon: Users, section: 'بخش نمونه' },
];

function setup(pathname: string, visible: readonly NestedNavLeaf[] = leaves) {
  window.history.pushState(null, '', `/app${pathname}`);
  return render(
    <RouterProvider routes={[]}>
      <ul>
        <NestedNavSections
          groupLabel="گروه"
          leaves={visible}
          pathname={pathname}
          onNavigate={() => undefined}
          DisabledLeaf={({ leaf }) => <div aria-disabled="true">{leaf.label}</div>}
        />
      </ul>
    </RouterProvider>,
  );
}

beforeEach(() => window.history.pushState(null, '', '/app/'));

describe('NestedNavSections', () => {
  it('renders plain leaves, a section toggle with its icon, and keeps disabled leaves honest', () => {
    const { container } = setup('/plain');
    expect(screen.getByRole('link', { name: 'ورودی ساده' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: 'بخش نمونه' })).toHaveAttribute('aria-expanded', 'true');
    expect(container.querySelectorAll('.faranou-nav-section-toggle .faranou-nav-icon--sub')).toHaveLength(1);
    expect(screen.getByText('به‌زودی').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('folds and unfolds a section, even when it contains the active page', async () => {
    const user = userEvent.setup();
    setup('/emp/42');
    const toggle = screen.getByRole('button', { name: 'بخش نمونه' });
    expect(screen.getByRole('link', { name: 'کارمند' })).toHaveAttribute('aria-current', 'page');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'کارمند' })).not.toBeInTheDocument();
    await user.click(toggle);
    expect(screen.getByRole('link', { name: 'کارمند' })).toBeInTheDocument();
  });

  it('renders an all-disabled section as a non-interactive heading without expanding child entries', () => {
    setup('/', [{ label: 'مجوزها', icon: Users, section: 'دسترسی و مجوزها', sectionIcon: Network }]);
    const disabled = screen.getByText('دسترسی و مجوزها').closest('[aria-disabled="true"]');
    expect(disabled).toBeTruthy();
    expect(disabled?.querySelector('button,a')).toBeNull();
    expect(screen.queryByText('مجوزها')).not.toBeInTheDocument();
  });

  it('keeps the section header when its first leaf is filtered out', () => {
    setup('/', leaves.filter((leaf) => leaf.label !== 'واحد'));
    expect(screen.getByRole('button', { name: 'بخش نمونه' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'کارمند' })).toBeInTheDocument();
  });
});
