import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FormField } from './FormField';
import {
  SearchableCombobox,
  filterComboboxOptions,
  normalizeSearchText,
  type ComboboxOption,
  type SearchableComboboxProps,
} from './SearchableCombobox';

const countries: ComboboxOption[] = [
  { value: 'DE', code: 'DE', label: 'آلمان', description: 'Germany' },
  { value: 'IR', code: 'IR', label: 'ایران', description: 'Iran' },
  { value: 'AE', code: 'AE', label: 'امارات متحدهٔ عربی', description: 'United Arab Emirates' },
  { value: 'CN', code: 'CN', label: 'چین', description: 'China' },
];

function Harness({ initial = null, onChange, ...rest }: { initial?: string | null; onChange?: (value: string | null) => void } & Partial<SearchableComboboxProps>) {
  const [value, setValue] = useState<string | null>(initial);
  return (
    <form onSubmit={(event) => event.preventDefault()}>
      <FormField label="کشور" hint="راهنما">
        <SearchableCombobox
          value={value}
          options={countries}
          onChange={(next) => {
            setValue(next);
            onChange?.(next);
          }}
          {...rest}
        />
      </FormField>
    </form>
  );
}

describe('normalizeSearchText / filterComboboxOptions', () => {
  it('treats Arabic and Persian letter/digit variants and case as equal', () => {
    expect(normalizeSearchText(' كشور ي ')).toBe('کشور ی');
    expect(normalizeSearchText('۱۲٣')).toBe('123');
    expect(normalizeSearchText('Iran')).toBe('iran');
  });

  it('ranks an exact code before a prefix before a contains match and keeps the order inside a rank', () => {
    const list: ComboboxOption[] = [
      { value: 'a', code: 'ZIR', label: 'الف' },
      { value: 'b', code: 'IR', label: 'ایران' },
      { value: 'c', code: 'IRQ', label: 'عراق' },
    ];
    expect(filterComboboxOptions(list, 'ir').map((option) => option.value)).toEqual(['b', 'c', 'a']);
    expect(filterComboboxOptions(list, '')).toHaveLength(3);
    expect(filterComboboxOptions(list, 'نامعلوم')).toEqual([]);
  });

  it('matches by English name and keywords', () => {
    expect(filterComboboxOptions(countries, 'germ').map((option) => option.value)).toEqual(['DE']);
    expect(filterComboboxOptions([{ value: 'x', label: 'الف', keywords: ['کلید'] }], 'کلید')).toHaveLength(1);
  });
});

describe('SearchableCombobox', () => {
  it('exposes the ARIA combobox contract and is named by the FormField label and hint', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox', { name: 'کشور' });
    expect(input).toHaveAttribute('aria-expanded', 'false');
    expect(input).toHaveAttribute('aria-autocomplete', 'list');
    expect(input).toHaveAttribute('aria-haspopup', 'listbox');
    expect(input).toHaveAccessibleDescription('راهنما');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('opens on click, shows code + name, and lets the mouse choose an option', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    const input = screen.getByRole('combobox');
    await user.click(input);
    expect(input).toHaveAttribute('aria-expanded', 'true');
    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(4);
    expect(options[1]).toHaveTextContent('IR');
    expect(options[1]).toHaveTextContent('ایران');
    await user.click(options[1] as HTMLElement);
    expect(onChange).toHaveBeenCalledWith('IR');
    expect(input).toHaveValue('IR — ایران');
    expect(input).toHaveAttribute('aria-expanded', 'false');
  });

  it('filters while typing by Persian name, English name and code', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    const input = screen.getByRole('combobox');
    await user.type(input, 'چین');
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([expect.stringContaining('چین')]);
    await user.clear(input);
    await user.type(input, 'germ');
    expect(screen.getByRole('option')).toHaveTextContent('آلمان');
    await user.clear(input);
    await user.type(input, 'ae');
    expect(screen.getAllByRole('option')[0]).toHaveTextContent('امارات');
  });

  it('replaces the chosen label when typing again, whether by click or by keyboard (browser smoke regression)', async () => {
    const user = userEvent.setup();
    render(<Harness initial="IR" />);
    const input = screen.getByRole('combobox');
    expect(input).toHaveValue('IR — ایران');

    // Click into the already-focused input, then type: the text must be replaced, not appended to.
    input.focus();
    await user.click(input);
    await user.keyboard('chi');
    expect(input).toHaveValue('chi');
    expect(screen.getByRole('option')).toHaveTextContent('چین');
    await user.keyboard('{Escape}');
    expect(input).toHaveValue('IR — ایران');

    // Keyboard only (no click): same behaviour.
    await user.keyboard('ger');
    expect(input).toHaveValue('ger');
  });

  it('shows an empty message in a status region when nothing matches', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.type(screen.getByRole('combobox'), 'zzzz');
    expect(screen.queryByRole('option')).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('موردی پیدا نشد');
  });

  it('supports the keyboard: ArrowDown opens, arrows move aria-activedescendant, Enter chooses without submitting, Escape closes', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: { preventDefault: () => void }) => event.preventDefault());
    const onChange = vi.fn();
    function Form() {
      const [value, setValue] = useState<string | null>(null);
      return (
        <form onSubmit={onSubmit}>
          <label htmlFor="c">کشور</label>
          <SearchableCombobox id="c" value={value} options={countries} onChange={(next) => { setValue(next); onChange(next); }} />
        </form>
      );
    }
    render(<Form />);
    const input = screen.getByRole('combobox');
    input.focus();
    await user.keyboard('{ArrowDown}');
    expect(input).toHaveAttribute('aria-expanded', 'true');
    const first = screen.getAllByRole('option')[0] as HTMLElement;
    expect(input).toHaveAttribute('aria-activedescendant', first.id);
    await user.keyboard('{ArrowDown}{ArrowDown}');
    const third = screen.getAllByRole('option')[2] as HTMLElement;
    expect(input).toHaveAttribute('aria-activedescendant', third.id);
    await user.keyboard('{ArrowUp}');
    expect(input).toHaveAttribute('aria-activedescendant', (screen.getAllByRole('option')[1] as HTMLElement).id);
    await user.keyboard('{Enter}');
    expect(onChange).toHaveBeenCalledWith('IR');
    expect(onSubmit).not.toHaveBeenCalled();
    expect(input).toHaveAttribute('aria-expanded', 'false');

    // Closed: Enter is a normal key again (the form may submit).
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(input).toHaveValue('IR — ایران');
  });

  it('keeps the chosen value when the user leaves (Tab) after typing, and restores the text', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial="IR" onChange={onChange} />);
    const input = screen.getByRole('combobox');
    expect(input).toHaveValue('IR — ایران');
    await user.click(input);
    await user.keyboard('xx');
    await user.tab();
    expect(onChange).not.toHaveBeenCalled();
    expect(input).toHaveValue('IR — ایران');
  });

  it('clears the choice with the clear button and returns focus to the input', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial="IR" onChange={onChange} />);
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن انتخاب' }));
    expect(onChange).toHaveBeenCalledWith(null);
    expect(screen.getByRole('combobox')).toHaveValue('');
    expect(screen.getByRole('combobox')).toHaveFocus();
    expect(screen.queryByRole('button', { name: 'پاک‌کردن انتخاب' })).not.toBeInTheDocument();
  });

  it('does not open or offer clearing when disabled', async () => {
    const user = userEvent.setup();
    render(<Harness initial="IR" disabled />);
    expect(screen.getByRole('combobox')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'پاک‌کردن انتخاب' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('combobox'));
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('marks the field invalid through FormField and keeps the error in its description', () => {
    render(
      <FormField label="کشور" error="کد کشور نامعتبر است.">
        <SearchableCombobox value={null} options={countries} onChange={vi.fn()} />
      </FormField>,
    );
    const input = screen.getByRole('combobox');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('کد کشور نامعتبر است.');
  });

  it('marks the chosen option as selected and does not choose a disabled option', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SearchableCombobox
        aria-label="انتخاب"
        value="DE"
        options={[...countries, { value: 'XX', label: 'غیرفعال', disabled: true }]}
        onChange={onChange}
      />,
    );
    await user.click(screen.getByRole('combobox', { name: 'انتخاب' }));
    expect(screen.getByRole('option', { name: /آلمان/ })).toHaveAttribute('aria-selected', 'true');
    await user.click(screen.getByRole('option', { name: /غیرفعال/ }));
    expect(onChange).not.toHaveBeenCalled();
  });

  it('loads from an async source (debounced), shows loading, results, empty and error with retry', async () => {
    const user = userEvent.setup();
    const calls: string[] = [];
    let failNext = false;
    const loadOptions = vi.fn(async (query: string) => {
      calls.push(query);
      if (failNext) throw new Error('boom');
      return countries.filter((option) => option.label.includes(query));
    });
    function Remote() {
      const [value, setValue] = useState<string | null>(null);
      return <SearchableCombobox aria-label="کشور" value={value} loadOptions={loadOptions} onChange={setValue} searchDelayMs={10} />;
    }
    render(<Remote />);
    const input = screen.getByRole('combobox');
    await user.click(input);
    expect(await screen.findAllByRole('option')).toHaveLength(4);
    await user.type(input, 'ایر');
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1));
    expect(calls.at(-1)).toBe('ایر');
    await user.clear(input);
    await user.type(input, 'نیست');
    expect(await screen.findByText('موردی پیدا نشد')).toBeInTheDocument();

    failNext = true;
    await user.clear(input);
    await user.type(input, 'چ');
    expect(await screen.findByText('دریافت فهرست انجام نشد.')).toBeInTheDocument();
    failNext = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1));
  });

  it('never chooses a stale option while the list reloads for a changed query (Enter or click)', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    let release: (options: ComboboxOption[]) => void = () => undefined;
    const loadOptions = vi.fn((query: string) =>
      query === '' ? Promise.resolve(countries) : new Promise<readonly ComboboxOption[]>((resolve) => { release = resolve; }),
    );
    render(<SearchableCombobox aria-label="کشور" value={null} loadOptions={loadOptions} onChange={onChange} searchDelayMs={5} />);
    const input = screen.getByRole('combobox');
    await user.click(input);
    expect(await screen.findAllByRole('option')).toHaveLength(4);
    // Type and press Enter at once: the visible rows still belong to the OLD (empty) query.
    await user.type(input, 'چین{Enter}');
    expect(onChange).not.toHaveBeenCalled();
    for (const option of screen.getAllByRole('option')) expect(option).toHaveAttribute('aria-disabled', 'true');
    await user.click(screen.getAllByRole('option')[0] as HTMLElement);
    expect(onChange).not.toHaveBeenCalled();
    await waitFor(() => expect(loadOptions).toHaveBeenCalledWith('چین', expect.anything()));
    release([countries[3] as ComboboxOption]);
    await waitFor(() => expect(screen.getByRole('option')).not.toHaveAttribute('aria-disabled'));
    await user.keyboard('{Enter}');
    expect(onChange).toHaveBeenCalledWith('CN', expect.objectContaining({ value: 'CN' }));
  });

  it('shows the chosen option of an async source from selectedOption even before it is loaded', () => {
    render(
      <SearchableCombobox
        aria-label="کشور"
        value="IR"
        selectedOption={{ value: 'IR', code: 'IR', label: 'ایران' }}
        loadOptions={async () => []}
        onChange={vi.fn()}
      />,
    );
    expect(screen.getByRole('combobox')).toHaveValue('IR — ایران');
  });

  it('caps the rendered options and says so', async () => {
    const user = userEvent.setup();
    const many = Array.from({ length: 30 }, (_, index) => ({ value: `v${index}`, label: `مورد ${index}` }));
    render(<SearchableCombobox aria-label="x" value={null} options={many} maxVisible={10} onChange={vi.fn()} />);
    await user.click(screen.getByRole('combobox'));
    expect(screen.getAllByRole('option')).toHaveLength(10);
    expect(screen.getByText(/فقط .* مورد نخست نمایش داده شد/)).toBeInTheDocument();
  });
});
