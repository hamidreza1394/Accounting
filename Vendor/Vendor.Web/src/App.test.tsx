import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import App from './App';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body, text: async () => JSON.stringify(body) };
}

describe('Vendor control plane shell', () => {
  it('shows the Persian sign-in screen when no session exists', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({}, 401)));
    render(<App />);
    expect(await screen.findByRole('heading', { name: 'به پنل مرکزی وارد شوید' })).toBeInTheDocument();
    expect(screen.getByLabelText('نام کاربری یا ایمیل')).toBeRequired();
  });

  it('moves to TOTP only after the real password endpoint requests a second factor', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({}, 401))
      .mockResolvedValueOnce(response({ requestToken: 'test-token' }))
      .mockResolvedValueOnce(response({ requiresSecondFactor: true, method: 'totp' }));
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);
    fireEvent.change(await screen.findByLabelText('نام کاربری یا ایمیل'), { target: { value: 'vendor-admin' } });
    fireEvent.change(screen.getByLabelText('رمز عبور'), { target: { value: 'not-a-real-password' } });
    fireEvent.click(screen.getByRole('button', { name: /ادامه و تأیید هویت/ }));
    expect(await screen.findByLabelText('کد احراز هویت')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/password', expect.objectContaining({ method: 'POST' }));
  });
});
