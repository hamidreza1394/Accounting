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
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
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
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
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
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
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
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
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
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
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

	it('maps an authorization failure to the infrastructure-account guidance', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') {
				return response({ title: 'Forbidden' }, 403);
			}
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText(/حساب زیرساخت \(Infrastructure\)/)).toBeTruthy();
	});

	it('shows a connection failure message when the network request fails', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') throw new TypeError('network down');
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText('خطای ارتباط با سرور.')).toBeTruthy();
	});

	it('sends exactly one POST even when the submit button is clicked repeatedly while busy', async () => {
		let resolveImport: ((value: Response) => void) | undefined;
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') {
				return await new Promise((resolve) => { resolveImport = resolve as (value: Response) => void; });
			}
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });

		const submit = screen.getByRole('button', { name: 'فعال‌سازی مجوز' }) as HTMLButtonElement;
		fireEvent.click(submit);
		fireEvent.click(submit);
		fireEvent.click(submit);
		expect(submit.disabled).toBe(true);

		resolveImport?.(response({ alreadyInstalled: false, status: 'Active' }) as unknown as Response);
		expect(await screen.findByText(/فعال‌سازی با موفقیت انجام شد/)).toBeTruthy();
		expect(fetchMock.mock.calls.filter(([url]) => String(url) === '/api/v1/licensing/import')).toHaveLength(1);
	});

	it('rejects an oversized file locally without any network POST', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import') throw new Error('import must never be called for an oversized file');
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const input = (await screen.findByLabelText('فایل مجوز (.json)')) as HTMLInputElement;
		const oversized = new File(['x'.repeat(300_001)], 'huge.json', { type: 'application/json' });
		fireEvent.change(input, { target: { files: [oversized] } });

		expect(await screen.findByText(/بیش از حد مجاز است/)).toBeTruthy();
		expect(fetchMock.mock.calls.filter(([url]) => String(url) === '/api/v1/licensing/import')).toHaveLength(0);
	});

	it('degrades to the offline guidance when the status response is not usable', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(null, 500);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);

		expect(await screen.findByText('دریافت وضعیت مجوز ناموفق بود؛ بعداً دوباره تلاش کنید.')).toBeTruthy();
		expect(screen.getByRole('button', { name: 'به‌روزرسانی وضعیت' })).toBeTruthy();
	});

	it('renders the page right-to-left with Persian headings', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		const { container } = render(<LicenseActivationPage />);

		const main = container.querySelector('main');
		expect(main?.getAttribute('dir')).toBe('rtl');
		expect(await screen.findByRole('heading', { level: 1, name: 'فعال‌سازی مجوز فرانو' })).toBeTruthy();
	});

	it('fetches the antiforgery token from the always-allowed provisioning route', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import' && init?.method === 'POST') return response({ alreadyInstalled: false, status: 'Active' });
			// The generic token route is denied by the runtime gate in recovery states; the
			// wizard must never call it.
			if (url === '/api/v1/antiforgery/token') throw new Error('the gate-denied token route must not be called');
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText(/فعال‌سازی با موفقیت انجام شد/)).toBeTruthy();
		expect(fetchMock.mock.calls.filter(([url]) => String(url) === '/api/v1/antiforgery/token')).toHaveLength(0);
	});

	it('ignores a stale status response that finishes after a newer refresh', async () => {
		let resolveFirstStatus: ((value: Response) => void) | undefined;
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/status') {
				if (!resolveFirstStatus) {
					// The initial status call stays pending until the import has finished.
					return await new Promise((resolve) => { resolveFirstStatus = resolve as (value: Response) => void; });
				}
				return response(activeStatus);
			}
			if (url === '/api/v1/licensing/import') return response({ alreadyInstalled: false, status: 'Active' });
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));
		expect(await screen.findByText(/فعال‌سازی با موفقیت انجام شد/)).toBeTruthy();
		await waitFor(() => expect(screen.getByText('فعال')).toBeTruthy());

		// The pre-import status response (unlicensed) arrives now and must not overwrite
		// the newer state.
		resolveFirstStatus?.(response(unlicensedStatus) as unknown as Response);
		await waitFor(() => expect(screen.getByText('فعال')).toBeTruthy());
	});

	it('validates pasted non-JSON content locally without any network POST', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import') throw new Error('import must never be called for invalid pasted content');
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: 'this is not json' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect(await screen.findByText(/محتوای پاکت، JSON معتبر نیست/)).toBeTruthy();
		expect(fetchMock.mock.calls.filter(([url]) => String(url) === '/api/v1/licensing/import')).toHaveLength(0);
	});

	it('invalidates the previous envelope when a malformed replacement file is selected', async () => {
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import') throw new Error('import must never be called after a malformed replacement');
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید');
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1,"signatureBase64":"sig"}' } });

		const input = screen.getByLabelText('فایل مجوز (.json)') as HTMLInputElement;
		fireEvent.change(input, { target: { files: [new File(['this is not json'], 'broken.json', { type: 'application/json' })] } });

		expect(await screen.findByText(/محتوای فایل، JSON معتبر نیست/)).toBeTruthy();
		const submit = screen.getByRole('button', { name: 'فعال‌سازی مجوز' }) as HTMLButtonElement;
		expect(submit.disabled).toBe(true);
		fireEvent.click(submit);
		expect(fetchMock.mock.calls.filter(([url]) => String(url) === '/api/v1/licensing/import')).toHaveLength(0);
	});

	it('keeps the envelope inputs disabled while an import is in flight', async () => {
		let resolveImport: ((value: Response) => void) | undefined;
		const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url === '/api/v1/licensing/status') return response(unlicensedStatus);
			if (url === '/api/v1/provisioning/enrollment/antiforgery') return response({ headerName: 'X-XSRF-TOKEN', requestToken: 'csrf-activation' });
			if (url === '/api/v1/licensing/import') {
				return await new Promise((resolve) => { resolveImport = resolve as (value: Response) => void; });
			}
			throw new Error(`Unexpected request: ${url}`);
		});
		vi.stubGlobal('fetch', fetchMock);

		render(<LicenseActivationPage />);
		const envelope = (await screen.findByLabelText('یا محتوای پاکت را اینجا بچسبانید')) as HTMLTextAreaElement;
		fireEvent.change(envelope, { target: { value: '{"formatVersion":1}' } });
		fireEvent.click(screen.getByRole('button', { name: 'فعال‌سازی مجوز' }));

		expect((screen.getByLabelText('فایل مجوز (.json)') as HTMLInputElement).disabled).toBe(true);
		expect((screen.getByLabelText('یا محتوای پاکت را اینجا بچسبانید') as HTMLTextAreaElement).disabled).toBe(true);

		resolveImport?.(response({ alreadyInstalled: false, status: 'Active' }) as unknown as Response);
		await waitFor(() => expect((screen.getByLabelText('یا محتوای پاکت را اینجا بچسبانید') as HTMLTextAreaElement).disabled).toBe(false));
	});
});
