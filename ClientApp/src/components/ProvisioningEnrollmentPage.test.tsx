import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ProvisioningEnrollmentPage } from './ProvisioningEnrollmentPage';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body?: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  };
}

describe('Customer provisioning enrollment', () => {
  it('sets the first administrator password using the one-time token and no-store requests', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/v1/provisioning/enrollment/antiforgery') {
        return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-enroll' });
      }
      if (url === '/api/v1/provisioning/enrollment/complete' && init?.method === 'POST') {
        return response(undefined, 204);
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<ProvisioningEnrollmentPage />);
    fireEvent.change(await screen.findByLabelText('نام کاربری مدیر'), { target: { value: 'initial-admin' } });
    fireEvent.change(screen.getByLabelText('توکن یک‌بارمصرف'), { target: { value: 'a'.repeat(43) } });
    fireEvent.change(screen.getByLabelText('گذرواژه جدید (حداقل ۱۲ نویسه)'), { target: { value: 'Strong-Passphrase-2026' } });
    fireEvent.change(screen.getByLabelText('تکرار گذرواژه'), { target: { value: 'Strong-Passphrase-2026' } });
    fireEvent.click(screen.getByRole('button', { name: 'مصرف توکن و فعال‌سازی حساب' }));

    await screen.findByText('حساب مدیر فعال شد. توکن مصرف شده و دیگر قابل استفاده نیست. اکنون می‌توانید وارد فرانو شوید.');
    const call = fetchMock.mock.calls.find(([url, init]) =>
      String(url) === '/api/v1/provisioning/enrollment/complete' && init?.method === 'POST');
    expect(call).toBeDefined();
    const init = call?.[1] as RequestInit;
    expect(init.cache).toBe('no-store');
    expect(init.headers).toEqual({ 'Content-Type': 'application/json', 'X-XSRF-TOKEN': 'csrf-enroll' });
    expect(JSON.parse(String(init.body))).toEqual({
      username: 'initial-admin',
      enrollmentToken: 'a'.repeat(43),
      newPassword: 'Strong-Passphrase-2026',
    });
  });

  it('does not submit when password confirmation differs', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) =>
      String(input) === '/api/v1/provisioning/enrollment/antiforgery'
        ? response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-enroll' })
        : response(undefined, 204));
    vi.stubGlobal('fetch', fetchMock);

    render(<ProvisioningEnrollmentPage />);
    fireEvent.change(await screen.findByLabelText('نام کاربری مدیر'), { target: { value: 'initial-admin' } });
    fireEvent.change(screen.getByLabelText('توکن یک‌بارمصرف'), { target: { value: 'a'.repeat(43) } });
    fireEvent.change(screen.getByLabelText('گذرواژه جدید (حداقل ۱۲ نویسه)'), { target: { value: 'Strong-Passphrase-2026' } });
    fireEvent.change(screen.getByLabelText('تکرار گذرواژه'), { target: { value: 'Different-Passphrase-2026' } });
    fireEvent.click(screen.getByRole('button', { name: 'مصرف توکن و فعال‌سازی حساب' }));

    await screen.findByText('تکرار گذرواژه با گذرواژه یکسان نیست.');
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
