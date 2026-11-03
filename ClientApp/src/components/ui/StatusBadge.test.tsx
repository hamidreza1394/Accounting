import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { StatusBadge, type Status } from './StatusBadge';

describe('StatusBadge', () => {
  it.each<[Status, string]>([
    ['active', 'فعال'],
    ['inactive', 'غیرفعال'],
    ['pending', 'در انتظار'],
    ['rejected', 'ردشده'],
    ['readonly', 'فقط‌خواندنی'],
  ])('shows an icon together with the text for %s', (status, text) => {
    const { container } = render(<StatusBadge status={status} />);
    expect(screen.getByText(text)).toBeInTheDocument();
    expect(container.querySelector('svg')).not.toBeNull();
  });

  it('lets the caller replace the text', () => {
    render(<StatusBadge status="pending">در انتظار تأیید مدیر</StatusBadge>);
    expect(screen.getByText('در انتظار تأیید مدیر')).toBeInTheDocument();
  });
});
