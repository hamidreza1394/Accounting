import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { json, listBody, stubApi, userItem } from '../../test/adminUsers';
import { FormField } from '../ui/FormField';
import { UserPicker, type PickedUser } from './UserPicker';

const onUnauthenticated = vi.fn((): boolean => false);

function Harness({ onPick }: { onPick?: (user: PickedUser | null) => void }) {
  const [value, setValue] = useState<PickedUser | null>(null);
  return (
    <FormField label="کاربر">
      <UserPicker
        value={value}
        onChange={(user) => {
          setValue(user);
          onPick?.(user);
        }}
        onUnauthenticated={onUnauthenticated}
      />
    </FormField>
  );
}

function api(handler: () => Response = () => json(listBody([userItem(1), userItem(2), userItem(3, { firstName: null, lastName: null })]))) {
  const urls: URL[] = [];
  const stub = stubApi((call) => {
    if (call.path !== '/api/v1/admin/users') return new Response(null, { status: 404 });
    urls.push(new URL(String(stub.mock.mock.calls.at(-1)?.[0]), 'http://localhost'));
    return handler();
  });
  return { ...stub, urls };
}

describe('UserPicker', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('is a labelled combobox that does not search below two characters', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    render(<Harness />);
    const input = screen.getByRole('combobox', { name: 'کاربر' });
    expect(input).toHaveAttribute('aria-expanded', 'false');
    await user.type(input, 'a');
    await new Promise((resolve) => setTimeout(resolve, 450));
    expect(urls).toHaveLength(0);
  });

  it('searches the real user list with the typed text (debounced) and shows name, username and status', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    render(<Harness />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    const options = await screen.findAllByRole('option');
    expect(options).toHaveLength(3);
    // typing 4 characters made exactly one request, for the full text
    expect(urls).toHaveLength(1);
    expect(urls[0]?.searchParams.get('search')).toBe('user');
    expect(urls[0]?.searchParams.get('pageSize')).toBe('8');
    expect(options[0]).toHaveTextContent('نام1 خانوادگی1');
    expect(options[0]).toHaveTextContent('user1');
    expect(options[2]).toHaveTextContent('بدون نام ثبت‌شده');
    expect(screen.getByRole('combobox', { name: 'کاربر' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('is operable by keyboard: arrows move the active option, Enter chooses, Escape closes', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    render(<Harness onPick={onPick} />);
    const input = screen.getByRole('combobox', { name: 'کاربر' });
    api();
    await user.type(input, 'user');
    await screen.findAllByRole('option');
    expect(input).toHaveAttribute('aria-activedescendant', expect.stringMatching(/-0$/));
    await user.keyboard('{ArrowDown}');
    expect(input).toHaveAttribute('aria-activedescendant', expect.stringMatching(/-1$/));
    expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{ArrowUp}{ArrowUp}');
    expect(input).toHaveAttribute('aria-activedescendant', expect.stringMatching(/-2$/)); // wraps to the last
    await user.keyboard('{Escape}');
    expect(input).toHaveAttribute('aria-expanded', 'false');
    expect(onPick).not.toHaveBeenCalled();
    await user.keyboard('{ArrowDown}');
    await user.keyboard('{Enter}');
    expect(onPick).toHaveBeenCalledTimes(1);
  });

  it('chooses with the mouse, shows the user with a named remove button, and removes it', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    api();
    render(<Harness onPick={onPick} />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    await user.click(await screen.findByRole('option', { name: /نام2 خانوادگی2/ }));
    expect(onPick).toHaveBeenLastCalledWith({ id: 'user-2', label: 'نام2 خانوادگی2' });
    expect(screen.queryByRole('combobox')).toBeNull();
    expect(screen.getByText('نام2 خانوادگی2')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'حذف کاربر انتخاب‌شده نام2 خانوادگی2' }));
    expect(onPick).toHaveBeenLastCalledWith(null);
    expect(screen.getByRole('combobox', { name: 'کاربر' })).toHaveValue('');
  });

  it('uses the username as the label of a user without a name', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    api();
    render(<Harness onPick={onPick} />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    await user.click((await screen.findAllByRole('option'))[2] as HTMLElement);
    expect(onPick).toHaveBeenLastCalledWith({ id: 'user-3', label: 'user3' });
  });

  it('says when nothing matches and when more results exist than shown', async () => {
    const user = userEvent.setup();
    api(() => json(listBody([], { totalCount: 0 })));
    const first = render(<Harness />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'zzz');
    expect(await screen.findByText('کاربری پیدا نشد.')).toBeInTheDocument();
    first.unmount();

    api(() => json(listBody([userItem(1)], { totalCount: 40 })));
    render(<Harness />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    expect(await screen.findByText(/فقط ۱ نتیجهٔ اول/)).toBeInTheDocument();
  });

  it('explains a 403 and a failure, and hands a 401 to the unauthenticated flow', async () => {
    const user = userEvent.setup();
    api(() => new Response(null, { status: 403 }));
    const first = render(<Harness />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    expect(await screen.findByRole('alert')).toHaveTextContent('فهرست کاربران فقط برای مدیر نصب در دسترس است.');
    first.unmount();

    api(() => new Response(null, { status: 500 }));
    const second = render(<Harness />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    expect(await screen.findByRole('alert')).toHaveTextContent('جستجوی کاربران انجام نشد.');
    second.unmount();

    onUnauthenticated.mockReturnValue(true);
    api(() => new Response(null, { status: 401 }));
    render(<Harness />);
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });

  it('closes the list when the pointer goes outside', async () => {
    const user = userEvent.setup();
    api();
    render(
      <div>
        <Harness />
        <button type="button">بیرون</button>
      </div>,
    );
    await user.type(screen.getByRole('combobox', { name: 'کاربر' }), 'user');
    await screen.findAllByRole('option');
    await user.click(screen.getByRole('button', { name: 'بیرون' }));
    expect(screen.getByRole('combobox', { name: 'کاربر' })).toHaveAttribute('aria-expanded', 'false');
  });
});
