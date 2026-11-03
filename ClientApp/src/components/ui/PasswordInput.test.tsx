import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { PasswordInput } from './PasswordInput';

describe('PasswordInput', () => {
  it('renders a hidden, left-to-right, spelling-aid-free input', () => {
    render(<PasswordInput aria-label="رمز" fieldName="رمز فعلی" />);
    const input = screen.getByLabelText('رمز');
    expect(input).toHaveAttribute('type', 'password');
    expect(input).toHaveAttribute('dir', 'ltr');
    expect(input).toHaveAttribute('spellcheck', 'false');
    expect(input).toHaveAttribute('autocapitalize', 'none');
    expect(input).toHaveAttribute('autocorrect', 'off');
    expect(input.parentElement).toHaveAttribute('dir', 'ltr');
  });

  it('puts the toggle and the padding on the start side by default', () => {
    render(<PasswordInput aria-label="رمز" fieldName="رمز فعلی" />);
    const input = screen.getByLabelText('رمز');
    const toggle = within(input.parentElement as HTMLElement).getByRole('button');
    expect(toggle).toHaveClass('start-0');
    expect(input).toHaveClass('ps-11', 'text-start');
    expect(toggle).not.toHaveClass('end-0');
    expect(input).not.toHaveClass('pe-11');
  });

  it('puts the toggle and the padding on the end side when asked', () => {
    render(<PasswordInput aria-label="رمز" fieldName="رمز فعلی" togglePlacement="end" />);
    const input = screen.getByLabelText('رمز');
    const toggle = within(input.parentElement as HTMLElement).getByRole('button');
    expect(toggle).toHaveClass('end-0');
    expect(input).toHaveClass('pe-11', 'text-start');
    expect(toggle).not.toHaveClass('start-0');
    expect(input).not.toHaveClass('ps-11');
  });

  it('toggles visibility with a fixed label and aria-pressed, by keyboard too', async () => {
    const user = userEvent.setup();
    render(<PasswordInput aria-label="رمز" fieldName="رمز فعلی" defaultValue="x" />);
    const toggle = screen.getByRole('button', { name: 'نمایش رمز فعلی' });
    expect(toggle).toHaveAttribute('type', 'button');
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    toggle.focus();
    await user.keyboard('{Enter}');
    expect(screen.getByLabelText('رمز')).toHaveAttribute('type', 'text');
    expect(screen.getByRole('button', { name: 'نمایش رمز فعلی' })).toHaveAttribute('aria-pressed', 'true');
    await user.keyboard(' ');
    expect(screen.getByLabelText('رمز')).toHaveAttribute('type', 'password');
  });

  it('forwards props such as id, autoComplete and a caller class to the input', () => {
    render(<PasswordInput id="p1" fieldName="رمز" autoComplete="new-password" className="extra" aria-label="رمز" />);
    const input = screen.getByLabelText('رمز');
    expect(input).toHaveAttribute('id', 'p1');
    expect(input).toHaveAttribute('autocomplete', 'new-password');
    expect(input).toHaveClass('extra');
  });
});
