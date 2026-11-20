import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { LicenseActivationPage } from './LicenseActivationPage';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

function response(body?: unknown, status = 200) {
	return {
		ok: status >= 200 && status < 300,
		status,
		json: async () => body,
	};
}

const unlicensedStatus = { state: 'unlicensed', deploymentMode: 'dedicated', status: null, errorCode: null, license: null };
const activeStatus = {
	state: 'active',
	deploymentMode: 'dedicated',
	status: 'Active',
	errorCode: null,
	license: { licenseId: 'license-1', sequence: 2, productCode: 'finance', expiresAtUtc: '2027-03-20T00:00:00Z', maxCompanies: 3, subsystems: ['finance'] },
};

describe('License activation page', () => {
	it('shows the current status and its guidance on load', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/antiforgery/token') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);

		expect(await screen.findByText('بدون مجوز')).toBeTruthy();
		expect(screen.getByText(/فایل مجوز امضاشده را در بخش فعال‌سازی ارائه کنید/)).toBeTruthy();
		expect(screen.getByRole('button', { name: 'به‌روزرسانی وضعیت' })).toBeTruthy();
	});

	it('imports a pasted signed envelope, posts it with the antiforgery token and refreshes the status', async () => {
		const postedBodies: unknown[] = [];
		let statusPayload: unknown = unlicensedStatus;
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(statusPayload);
			if (url === '/api/v1/antiforgery/token') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') {
				postedBodies.push(JSON.parse(String(init.body)));
				expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('csrf-activation');
				statusPayload = activeStatus;
				return response({ alreadyInstalled: false, status: 'Active' });
			}
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1,"signatureBase64":"sig"}' } });

		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText(/فعال‌سازی با موفقیت انجام شد/)).toBeTruthy();
		expect(postedBodies).toEqual([{ envelopeJson: '{"formatVersion":1,"signatureBase64":"sig"}' }]);
		// The status refresh turns the state badge into فعال.
		await waitFor(() => expect(screen.getByText('فعال')).toBeTruthy());
		expect(screen.getByText('شناسه لایسنس')).toBeTruthy();
	});

	it('rejects a non-JSON file locally without any network POST', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/antiforgery/token') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import') throw new Error('import must never be called for an invalid file');
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const input = (await screen.findByLabelText('فایل مجوز (.json)')) as HTMLInputElement;
		fireEvent.change(input, { target: { files: [new File(['this is not json'], 'broken.json', { type: 'application/json' })] } });

		expect(await screen.findByText(/JSON معتبر نیست/)).toBeTruthy();
		expect(fetchMock.mock.calls.filter(([url]) => String(url) === '/api/v1/licensing/import')).toHaveLength(0);
	});

	it('maps a downgrade conflict to its Persian guidance', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(activeStatus);
			if (url === '/api/v1/antiforgery/token') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') {
				return response({ detail: 'license_sequence_replay_or_downgrade', title: 'فعال‌سازی مجوز انجام نشد.' }, 409);
			}
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText(/قدیمی‌تر یا تکراری نسبت به مجوز نصب‌شده/)).toBeTruthy();
	});

	it('maps an unavailable security service to the fail-closed guidance', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/antiforgery/token') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') {
				return response({ detail: 'license_audit_unavailable', title: 'فعال‌سازی مجوز انجام نشد.' }, 503);
			}
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText(/fail-closed انجام نمی‌شود/)).toBeTruthy();
	});
});
