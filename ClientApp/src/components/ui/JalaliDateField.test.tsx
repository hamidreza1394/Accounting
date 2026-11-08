import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { FormField } from './FormField';
import { JalaliDateField, clampToRange, dateToIso, isoToDate } from './JalaliDateField';

// 2026-11-03 is 12 Aban 1405.
function Controlled({ initial = '', min, max, onChange }: { initial?: string; min?: string; max?: string; onChange?: (value: string) => void }) {
  const [value, setValue] = useState(initial);
  return (
    <JalaliDateField
      value={value}
      min={min ?? ''}
      max={max ?? ''}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
    />
  );
}

/** A day button by its Jalali day and month (anchored, so ۱ never matches ۲۱). */
const day = (n: string, month: string) => screen.getByRole('button', { name: new RegExp(`\\s${n}-ام ${month} ۱۴۰۵`) });
const findDay = (n: string, month: string) => screen.findByRole('button', { name: new RegExp(`\\s${n}-ام ${month} ۱۴۰۵`) });

describe('JalaliDateField', () => {
  it('shows a placeholder when empty and the Jalali date with Persian digits when set', () => {
    const { rerender } = render(<JalaliDateField value="" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'انتخاب تاریخ' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'پاک‌کردن تاریخ' })).toBeNull();
    rerender(<JalaliDateField value="2026-11-03" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' })).toBeInTheDocument();
  });

  it('provides direct month and year dropdowns in the Jalali calendar', async () => {
    const user = userEvent.setup();
    render(<Controlled initial="2026-11-03" />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog.querySelectorAll('select')).toHaveLength(2);
  });

  it('opens an RTL Persian calendar on the month of the value, with Persian labels and digits', async () => {
    const user = userEvent.setup();
    render(<Controlled initial="2026-11-03" />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveAttribute('dir', 'rtl');
    expect(dialog).toHaveTextContent('آبان ۱۴۰۵');
    expect(screen.getByRole('button', { name: 'رفتن به ماه بعد' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'رفتن به ماه قبل' })).toBeInTheDocument();
  });

  it('chooses a day and reports it as a Gregorian ISO calendar date, then closes', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled initial="2026-11-03" onChange={onChange} />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    // 15 Aban 1405 = 2026-11-06
    await user.click(await findDay('۱۵', 'آبان'));
    expect(onChange).toHaveBeenCalledExactlyOnceWith('2026-11-06');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۵' })).toBeInTheDocument();
  });

  it('can be moved to another month and picks a day there', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled initial="2026-11-03" onChange={onChange} />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    await user.click(await screen.findByRole('button', { name: 'رفتن به ماه بعد' }));
    expect(await screen.findByRole('dialog')).toHaveTextContent('آذر ۱۴۰۵');
    await user.click(day('۱', 'آذر'));
    expect(onChange).toHaveBeenCalledExactlyOnceWith('2026-11-22');
  });

  it('does not clear the value when the selected day is clicked again', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled initial="2026-11-03" onChange={onChange} />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    await user.click(await findDay('۱۲', 'آبان'));
    expect(onChange).not.toHaveBeenCalledWith('');
  });

  it('clears with a dedicated, named button', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled initial="2026-11-03" onChange={onChange} />);
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن تاریخ' }));
    expect(onChange).toHaveBeenCalledExactlyOnceWith('');
    expect(screen.getByRole('button', { name: 'انتخاب تاریخ' })).toBeInTheDocument();
  });

  it('disables days outside min and max', async () => {
    const user = userEvent.setup();
    render(<Controlled initial="2026-11-03" min="2026-11-01" max="2026-11-10" />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    // 9 Aban = 2026-10-31 (before min), 19 Aban = 2026-11-10 (max), 20 Aban = 2026-11-11 (after max)
    expect(await findDay('۹', 'آبان')).toBeDisabled();
    expect(day('۱۹', 'آبان')).toBeEnabled();
    expect(day('۲۰', 'آبان')).toBeDisabled();
  });

  it('is operable by keyboard: Enter opens, arrows move, Enter chooses, Escape closes and returns focus', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled initial="2026-11-03" onChange={onChange} />);
    const trigger = screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' });
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(onChange).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' })).toHaveFocus());

    await user.keyboard('{Enter}');
    await screen.findByRole('dialog');
    const selected = await findDay('۱۲', 'آبان');
    selected.focus();
    await user.keyboard('{ArrowRight}{Enter}');
    // In an RTL calendar ArrowRight moves to the previous day.
    expect(onChange).toHaveBeenCalledExactlyOnceWith('2026-11-02');
  });

  it('is labelled and described through FormField', () => {
    render(
      <FormField label="از تاریخ" hint="تاریخ شروع" error="تاریخ نامعتبر است.">
        <JalaliDateField value="" onChange={vi.fn()} />
      </FormField>,
    );
    const trigger = screen.getByLabelText('از تاریخ');
    expect(trigger).toBe(screen.getByRole('button', { name: /از تاریخ/ }));
    expect(trigger).toHaveAttribute('aria-invalid', 'true');
    expect(trigger).toHaveAccessibleDescription(/تاریخ نامعتبر است/);
    // The chosen day (or the placeholder) is part of the description, since the label hides the content.
    expect(trigger).toHaveAccessibleDescription(/انتخاب تاریخ/);
  });

  it('does not open or offer clearing when disabled', async () => {
    const user = userEvent.setup();
    render(<JalaliDateField value="2026-11-03" onChange={vi.fn()} disabled />);
    await user.click(screen.getByRole('button', { name: '۱۴۰۵/۰۸/۱۲' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.queryByRole('button', { name: 'پاک‌کردن تاریخ' })).toBeNull();
  });
});

describe('screen reader value', () => {
  it('reads the chosen day after the label of a FormField', () => {
    render(
      <FormField label="تا تاریخ">
        <JalaliDateField value="2026-11-03" onChange={vi.fn()} />
      </FormField>,
    );
    const trigger = screen.getByRole('button', { name: /تا تاریخ/ });
    expect(trigger).toHaveAccessibleDescription('۱۴۰۵/۰۸/۱۲');
  });
});

describe('opening month of an empty field', () => {
  it('opens on today, not on the minimum, when today is allowed', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-03T09:00:00'));
    const user = userEvent.setup();
    render(<Controlled min="2000-01-01" max="2100-12-31" />);
    await user.click(screen.getByRole('button', { name: 'انتخاب تاریخ' }));
    expect(await screen.findByRole('dialog')).toHaveTextContent('آبان ۱۴۰۵');
    vi.useRealTimers();
  });

  it('opens on the nearest allowed month when today is outside the range', () => {
    const min = new Date(2026, 11, 1, 12);
    const max = new Date(2026, 11, 31, 12);
    expect(clampToRange(new Date(2026, 10, 3, 12), min, max)).toBe(min);
    expect(clampToRange(new Date(2027, 2, 3, 12), min, max)).toBe(max);
    const inside = new Date(2026, 11, 10, 12);
    expect(clampToRange(inside, min, max)).toBe(inside);
    expect(clampToRange(inside)).toBe(inside);
  });
});

describe('ISO conversion', () => {
  it('round-trips calendar dates, including a daylight-saving day, and rejects non-dates', () => {
    for (const iso of ['2026-11-03', '2026-03-29', '2026-10-25', '2000-01-01', '2100-12-31', '2024-02-29']) {
      const date = isoToDate(iso);
      expect(date).toBeDefined();
      expect(dateToIso(date as Date)).toBe(iso);
    }
    expect(isoToDate('2026-02-30')).toBeUndefined();
    expect(isoToDate('2026-13-01')).toBeUndefined();
    expect(isoToDate('')).toBeUndefined();
    expect(isoToDate('1405/08/12')).toBeUndefined();
  });

  it('formats years below 1000 with four digits', () => {
    expect(dateToIso(new Date(999, 0, 5))).toBe('0999-01-05');
  });
});
