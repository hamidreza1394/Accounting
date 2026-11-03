import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Root } from './Root';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url === '/api/v1/provisioning/enrollment/antiforgery' || url === '/api/v1/antiforgery/token') {
      return { ok: true, status: 200, json: async () => ({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf' }) };
    }
    throw new Error(`Unexpected request: ${url}`);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('Root', () => {
  it('renders the lazily loaded enrollment page for /enroll', async () => {
    stubFetch();
    render(<Root pathname="/enroll" />);
    expect(await screen.findByLabelText('نام کاربری مدیر')).toBeInTheDocument();
    expect(screen.getByLabelText('توکن یک‌بارمصرف')).toHaveAttribute('dir', 'ltr');
  });

  it('renders the lazily loaded package import page for /provisioning/import', async () => {
    stubFetch();
    render(<Root pathname="/provisioning/import" />);
    expect(await screen.findByLabelText(/انتخاب فایل بسته/)).toBeInTheDocument();
  });

  it('does not render a provisioning page for any other path', () => {
    stubFetch();
    render(<Root pathname="/app/" />);
    expect(screen.queryByLabelText('نام کاربری مدیر')).toBeNull();
    expect(screen.queryByLabelText(/انتخاب فایل بسته/)).toBeNull();
  });

  it('shows a focused alert when the chunk fails to load, and retries with a fresh import', async () => {
    stubFetch();
    const user = userEvent.setup();
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const loadEnrollment = vi
      .fn<() => Promise<{ default: () => never }>>()
      .mockRejectedValueOnce(new Error('Failed to fetch dynamically imported module'))
      .mockImplementation(async () => import('./components/ProvisioningEnrollmentPage').then((m) => ({ default: m.ProvisioningEnrollmentPage as () => never })));
    render(<Root pathname="/enroll" loadEnrollment={loadEnrollment} />);
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('بارگذاری این بخش انجام نشد');
    expect(alert).toHaveFocus();
    expect(screen.getByRole('button', { name: 'بارگذاری مجدد صفحه' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByLabelText('نام کاربری مدیر')).toBeInTheDocument();
    expect(loadEnrollment).toHaveBeenCalledTimes(2);
    expect(screen.queryByText(/بارگذاری این بخش انجام نشد/)).toBeNull();
    consoleError.mockRestore();
  });
});
