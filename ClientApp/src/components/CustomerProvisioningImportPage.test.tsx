import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { CustomerProvisioningImportPage } from './CustomerProvisioningImportPage';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

function response(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  };
}

describe('Customer provisioning import', () => {
  it('loads a signed package, posts it without caching and reveals the one-time token only after success', async () => {
    const requestId = 'a0b1c2d3-e4f5-4678-9012-abcdefabcdef';
    const packageDocument = {
      formatVersion: 1,
      keyId: 'vendor-key-1',
      algorithm: 'ECDSA_P256_SHA256',
      payloadJson: JSON.stringify({ requestId }),
      signatureBase64: 'signature',
    };
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/v1/antiforgery/token') {
        return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-token' });
      }
      if (url === '/api/v1/provisioning/apply' && init?.method === 'POST') {
        return response({
          acknowledgement: { result: 'Applied', requestId },
          replayed: false,
          enrollmentToken: 'one-time-enrollment-secret',
        });
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<CustomerProvisioningImportPage />);
    const file = new File([JSON.stringify(packageDocument)], 'customer-package.json', { type: 'application/json' });
    fireEvent.change(await screen.findByLabelText('انتخاب فایل بستهٔ JSON'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'اعتبارسنجی و اعمال بسته' }));

    await screen.findByText('one-time-enrollment-secret');
    const applyCall = fetchMock.mock.calls.find(([url]) => String(url) === '/api/v1/provisioning/apply');
    expect(applyCall).toBeDefined();
    const init = applyCall?.[1] as RequestInit;
    expect(init.cache).toBe('no-store');
    expect(init.headers).toEqual({ 'Content-Type': 'application/json', 'X-XSRF-TOKEN': 'csrf-token' });
    expect(JSON.parse(String(init.body))).toEqual(packageDocument);
    expect(screen.getByText(/شناسه درخواست:/)).toBeInTheDocument();
  });

  it('rejects a package with no payload request identifier before calling the API', async () => {
    const fetchMock = vi.fn(async () => response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-token' }));
    vi.stubGlobal('fetch', fetchMock);

    render(<CustomerProvisioningImportPage />);
    const file = new File([JSON.stringify({
      formatVersion: 1,
      keyId: 'vendor-key-1',
      algorithm: 'ECDSA_P256_SHA256',
      payloadJson: JSON.stringify({ requestId: 'not-a-guid' }),
      signatureBase64: 'signature',
    })], 'invalid-package.json', { type: 'application/json' });
    fireEvent.change(await screen.findByLabelText('انتخاب فایل بستهٔ JSON'), { target: { files: [file] } });

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('شناسه درخواست در بسته معتبر نیست.'));
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
