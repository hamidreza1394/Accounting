import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Button } from './button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from './card';

describe('shadcn/ui primitives', () => {
  it('supports accessible keyboard activation for the Button', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(<Button onClick={onClick}>ادامه</Button>);

    const button = screen.getByRole('button', { name: 'ادامه' });
    button.focus();
    await user.keyboard('{Enter}');

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('does not activate a disabled Button', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(<Button disabled onClick={onClick}>غیرفعال</Button>);

    const button = screen.getByRole('button', { name: 'غیرفعال' });
    expect(button).toBeDisabled();
    await user.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it('renders Card content with semantic heading and description', () => {
    render(
      <Card>
        <CardHeader>
          <CardTitle>مشخصات شرکت</CardTitle>
          <CardDescription>اطلاعات شرکت انتخاب‌شده</CardDescription>
        </CardHeader>
        <CardContent>دوره مالی جاری</CardContent>
      </Card>,
    );

    expect(screen.getByText('مشخصات شرکت')).toBeInTheDocument();
    expect(screen.getByText('اطلاعات شرکت انتخاب‌شده')).toBeInTheDocument();
    expect(screen.getByText('دوره مالی جاری')).toBeInTheDocument();
  });
});
