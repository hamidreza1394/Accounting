import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmDialog } from './ConfirmDialog';

function Host({ onConfirm, tone }: { onConfirm: () => void | Promise<void>; tone?: 'danger' | 'default' }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        باز کن
      </button>
      <ConfirmDialog
        open={open}
        title="حذف کاربر"
        description="این عمل قابل بازگشت نیست."
        confirmLabel="حذف"
        {...(tone ? { tone } : {})}
        onConfirm={onConfirm}
        onCancel={() => setOpen(false)}
      />
    </>
  );
}

describe('ConfirmDialog', () => {
  it('is a named modal dialog described by its text', async () => {
    const user = userEvent.setup();
    render(<Host onConfirm={() => undefined} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'باز کن' }));
    const dialog = screen.getByRole('dialog', { name: 'حذف کاربر' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAccessibleDescription('این عمل قابل بازگشت نیست.');
    expect(screen.getByRole('button', { name: 'حذف' })).toHaveFocus();
  });

  it('uses alertdialog and focuses Cancel first for the danger tone', async () => {
    const user = userEvent.setup();
    render(<Host tone="danger" onConfirm={() => undefined} />);
    await user.click(screen.getByRole('button', { name: 'باز کن' }));
    expect(screen.getByRole('alertdialog', { name: 'حذف کاربر' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'انصراف' })).toHaveFocus();
  });

  it('closes with Escape and returns focus to the opener', async () => {
    const user = userEvent.setup();
    render(<Host onConfirm={() => undefined} />);
    const opener = screen.getByRole('button', { name: 'باز کن' });
    await user.click(opener);
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(opener).toHaveFocus();
  });

  it('traps Tab and Shift+Tab inside the dialog', async () => {
    const user = userEvent.setup();
    render(<Host onConfirm={() => undefined} />);
    await user.click(screen.getByRole('button', { name: 'باز کن' }));
    const cancel = screen.getByRole('button', { name: 'انصراف' });
    const confirm = screen.getByRole('button', { name: 'حذف' });
    expect(confirm).toHaveFocus();
    await user.tab();
    expect(cancel).toHaveFocus();
    await user.tab({ shift: true });
    expect(confirm).toHaveFocus();
    await user.tab();
    await user.tab();
    expect(confirm).toHaveFocus();
  });

  it('prevents a double submit while the action runs', async () => {
    const user = userEvent.setup();
    let finish: () => void = () => undefined;
    const onConfirm = vi.fn(() => new Promise<void>((resolve) => { finish = resolve; }));
    render(<Host onConfirm={onConfirm} />);
    await user.click(screen.getByRole('button', { name: 'باز کن' }));
    const confirm = screen.getByRole('button', { name: 'حذف' });
    await user.click(confirm);
    await user.click(confirm);
    await user.keyboard('{Enter}');
    expect(onConfirm).toHaveBeenCalledTimes(1);
    finish();
    await waitFor(() => undefined);
  });

  it('ignores confirm, cancel and Escape while busy and keeps focus inside', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(<ConfirmDialog open busy title="حذف" onConfirm={onConfirm} onCancel={onCancel} />);
    const confirm = screen.getByRole('button', { name: 'در حال انجام…' });
    expect(confirm).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByRole('dialog')).toHaveAttribute('aria-busy', 'true');
    confirm.focus();
    await user.keyboard('{Enter}');
    await user.keyboard('{Escape}');
    expect(onConfirm).not.toHaveBeenCalled();
    expect(onCancel).not.toHaveBeenCalled();
    expect(confirm).toHaveFocus();
  });

  it('keeps Tab and Shift+Tab inside the dialog while busy', async () => {
    const user = userEvent.setup();
    render(
      <>
        <button type="button">بیرون</button>
        <ConfirmDialog open busy title="حذف" onConfirm={() => undefined} onCancel={() => undefined} />
      </>,
    );
    const dialog = screen.getByRole('dialog');
    const cancel = screen.getByRole('button', { name: 'انصراف' });
    const confirm = screen.getByRole('button', { name: 'در حال انجام…' });
    confirm.focus();
    await user.tab();
    expect(cancel).toHaveFocus();
    await user.tab({ shift: true });
    expect(confirm).toHaveFocus();
    await user.tab();
    await user.tab();
    expect(dialog).toContainElement(document.activeElement as HTMLElement);
  });

  it('closes on Escape even when focus is outside the dialog, but not while busy', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    const { rerender } = render(<ConfirmDialog open title="حذف" onConfirm={() => undefined} onCancel={onCancel} />);
    (document.activeElement as HTMLElement).blur();
    await user.keyboard('{Escape}');
    expect(onCancel).toHaveBeenCalledTimes(1);
    rerender(<ConfirmDialog open busy title="حذف" onConfirm={() => undefined} onCancel={onCancel} />);
    await user.keyboard('{Escape}');
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['rejecting', () => Promise.reject(new Error('fail'))],
    [
      'synchronously throwing',
      () => {
        throw new Error('fail');
      },
    ],
  ])('stays usable after a %s onConfirm and leaks no rejection', async (_name, behaviour) => {
    const user = userEvent.setup();
    // Vitest fails the run on an unhandled rejection, so a leak would surface here.
    {
      const onConfirm = vi.fn(behaviour as () => Promise<void>);
      render(<ConfirmDialog open title="حذف" confirmLabel="حذف" onConfirm={onConfirm} onCancel={() => undefined} />);
      const confirm = screen.getByRole('button', { name: 'حذف' });
      await user.click(confirm);
      await user.click(confirm);
      expect(onConfirm).toHaveBeenCalledTimes(2);
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
  });
});
