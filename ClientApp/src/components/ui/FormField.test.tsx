import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { FormField, Input, Select, Textarea } from './FormField';
import { FormSection } from './FormSection';

describe('FormField', () => {
  it('labels the control, shows the required marker and links the hint', () => {
    render(
      <FormField label="نام کاربری" required hint="حداقل سه نویسه">
        <Input />
      </FormField>,
    );
    const input = screen.getByLabelText(/نام کاربری/);
    expect(input).toHaveAttribute('aria-required', 'true');
    expect(input).not.toHaveAttribute('aria-invalid');
    expect(input).toHaveAccessibleDescription('حداقل سه نویسه');
  });

  it('marks the control invalid and describes it by the error', () => {
    render(
      <FormField label="ایمیل" hint="مثال: a@b.ir" error="ایمیل معتبر نیست">
        <Input type="email" />
      </FormField>,
    );
    const input = screen.getByLabelText('ایمیل');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('مثال: a@b.ir ایمیل معتبر نیست');
  });

  it('keeps an id and a describedby set on the control, and works with Select and Textarea', async () => {
    const user = userEvent.setup();
    render(
      <>
        <FormField label="نقش" error="خطا">
          <Select id="role" aria-describedby="extra">
            <option value="a">الف</option>
            <option value="b">ب</option>
          </Select>
        </FormField>
        <FormField label="توضیحات">
          <Textarea />
        </FormField>
        <p id="extra">راهنمای بیرونی</p>
      </>,
    );
    const select = screen.getByLabelText('نقش');
    expect(select).toHaveAttribute('id', 'role');
    expect(select).toHaveAccessibleDescription('راهنمای بیرونی خطا');
    await user.selectOptions(select, 'b');
    expect(select).toHaveValue('b');
    await user.type(screen.getByLabelText('توضیحات'), 'سلام');
    expect(screen.getByLabelText('توضیحات')).toHaveValue('سلام');
  });
});

describe('FormSection', () => {
  it('is a named region with its heading', () => {
    render(
      <FormSection title="مشخصات" description="اطلاعات پایه">
        <p>محتوا</p>
      </FormSection>,
    );
    expect(screen.getByRole('region', { name: 'مشخصات' })).toHaveTextContent('اطلاعات پایه');
  });

  it('renders an h3 by default and honours headingLevel', () => {
    const { rerender } = render(
      <FormSection title="مشخصات">
        <p>محتوا</p>
      </FormSection>,
    );
    expect(screen.getByRole('heading', { level: 3, name: 'مشخصات' })).toBeInTheDocument();
    rerender(
      <FormSection title="مشخصات" headingLevel={2}>
        <p>محتوا</p>
      </FormSection>,
    );
    expect(screen.getByRole('heading', { level: 2, name: 'مشخصات' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 3 })).toBeNull();
    expect(screen.getByRole('region', { name: 'مشخصات' })).toBeInTheDocument();
  });
});
