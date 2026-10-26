import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ManagedSelect } from './managed-select';

const options = [
  { value: 'independent', label: 'شرکت مستقل' },
  { value: 'parent', label: 'شرکت مادر' },
  { value: 'subsidiary', label: 'شرکت زیرمجموعه' },
];

describe('ManagedSelect', () => {
  it('uses the first option as the default and renders the field in RTL', () => {
    render(<ManagedSelect label="نوع شرکت" options={options} name="companyType" />);

    const select = screen.getByRole('combobox', { name: 'نوع شرکت' });
    expect(select).toHaveValue('independent');
    expect(select).toHaveAttribute('dir', 'rtl');
  });

  it('uses a valid explicit default and reports subsequent selections', () => {
    const onValueChange = vi.fn();
    render(
      <ManagedSelect
        label="نوع شرکت"
        options={options}
        defaultValue="parent"
        onValueChange={onValueChange}
      />,
    );

    const select = screen.getByRole('combobox', { name: 'نوع شرکت' });
    expect(select).toHaveValue('parent');
    fireEvent.change(select, { target: { value: 'subsidiary' } });
    expect(select).toHaveValue('subsidiary');
    expect(onValueChange).toHaveBeenCalledWith('subsidiary');
  });

  it('does not expose option management without explicit permission and callback', () => {
    const onAddOption = vi.fn();
    const { rerender } = render(
      <ManagedSelect label="نوع شرکت" options={options} onAddOption={onAddOption} />,
    );
    expect(screen.queryByRole('button', { name: 'افزودن گزینه' })).not.toBeInTheDocument();

    rerender(
      <ManagedSelect
        label="نوع شرکت"
        options={options}
        canManageOptions
        onAddOption={onAddOption}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'افزودن گزینه' }));
    expect(onAddOption).toHaveBeenCalledOnce();
  });

  it('shows an accessible validation error and disables an empty list', () => {
    render(<ManagedSelect label="نوع شرکت" options={[]} error="انتخاب نوع شرکت الزامی است." />);

    expect(screen.getByRole('alert')).toHaveTextContent('انتخاب نوع شرکت الزامی است.');
    expect(screen.getByRole('combobox', { name: 'نوع شرکت' })).toBeDisabled();
  });
});
