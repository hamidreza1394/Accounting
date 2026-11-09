import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Tabs } from './Tabs';

const items = [
  { id: 'a', label: 'عمومی', content: <p>محتوای عمومی</p> },
  { id: 'b', label: 'امنیت', content: <p>محتوای امنیت</p> },
  { id: 'c', label: 'غیرفعال', disabled: true, content: <p>پنهان</p> },
  { id: 'd', label: 'سابقه', content: <p>محتوای سابقه</p> },
];

function Host({ dir = 'rtl' }: { dir?: 'rtl' | 'ltr' }) {
  const [value, setValue] = useState('a');
  return (
    <div dir={dir}>
      <Tabs label="بخش‌های حساب" items={items} value={value} onValueChange={setValue} />
    </div>
  );
}

describe('Tabs', () => {
  it('exposes tablist, tabs and the labelled panel', () => {
    render(<Host />);
    expect(screen.getByRole('tablist', { name: 'بخش‌های حساب' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'عمومی' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'امنیت' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tabpanel', { name: 'عمومی' })).toHaveTextContent('محتوای عمومی');
    expect(screen.getByRole('tab', { name: 'عمومی' })).toHaveClass('bg-navy', 'text-white', 'border-gold');
    expect(screen.getByRole('tab', { name: 'امنیت' })).toHaveClass('bg-cool', 'text-sub');
  });

  it('moves to the next tab with ArrowLeft in RTL and to the previous with ArrowRight', async () => {
    const user = userEvent.setup();
    render(<Host />);
    screen.getByRole('tab', { name: 'عمومی' }).focus();
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'امنیت' })).toHaveFocus();
    expect(screen.getByRole('tabpanel', { name: 'امنیت' })).toBeInTheDocument();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'عمومی' })).toHaveFocus();
  });

  it('skips disabled tabs, wraps around and supports Home and End', async () => {
    const user = userEvent.setup();
    render(<Host />);
    screen.getByRole('tab', { name: 'عمومی' }).focus();
    await user.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'سابقه' })).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'عمومی' })).toHaveFocus();
    await user.keyboard('{End}');
    expect(screen.getByRole('tab', { name: 'سابقه' })).toHaveFocus();
    await user.keyboard('{Home}');
    expect(screen.getByRole('tab', { name: 'عمومی' })).toHaveFocus();
  });

  it('reverses the arrows in LTR and selects with a click', async () => {
    const user = userEvent.setup();
    render(<Host dir="ltr" />);
    screen.getByRole('tab', { name: 'عمومی' }).focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'امنیت' })).toHaveFocus();
    await user.click(screen.getByRole('tab', { name: 'سابقه' }));
    expect(screen.getByRole('tabpanel')).toHaveTextContent('محتوای سابقه');
  });
});
