import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert } from '@/components/ui/Alert';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/Badge';
import { apiGetRaw, apiPostJson } from '@/api/client';

type AntiforgeryResponse = { headerName?: string; requestToken?: string };
type ApiError = { detail?: string; title?: string };

type LicenseSummary = {
	licenseId?: string;
	sequence?: number;
	productCode?: string;
	licenseKind?: string;
	expiresAtUtc?: string;
	maxCompanies?: number;
	subsystems?: string[];
};

type LicensingStatus = {
	state?: string;
	deploymentMode?: string;
	status?: string;
	errorCode?: string;
	license?: LicenseSummary | null;
	resolvedAtUtc?: string;
};

type ImportResult = { alreadyInstalled?: boolean; status?: string };

/** The server rejects anything longer than this; checking client-side avoids a pointless upload. */
const MaximumEnvelopeChars = 300_000;

const stateLabels: Record<string, string> = {
	active: 'فعال',
	restricted: 'محدود',
	unlicensed: 'بدون مجوز',
	invalid: 'نامعتبر',
};

const stateTones: Record<string, 'success' | 'warning' | 'neutral' | 'danger'> = {
	active: 'success',
	restricted: 'warning',
	unlicensed: 'neutral',
	invalid: 'danger',
};

const deploymentLabels: Record<string, string> = {
	dedicated: 'نصب اختصاصی',
	cloud: 'نصب اشتراکی',
};

/** Stable server error codes (ApiEndpoints / CustomerLicenseImportService) mapped to Persian guidance. */
const errorMessages: Record<string, string> = {
	license_import_busy: 'عملیات فعال‌سازی دیگری روی این نصب در جریان است؛ چند لحظه بعد دوباره تلاش کنید.',
	license_import_conflict: 'این مجوز با وضعیت فعلی نصب تعارض دارد؛ فایل مجوز صحیح را ارائه کنید.',
	license_import_concurrency_conflict: 'تعارض هم‌زمانی در ثبت مجوز رخ داد؛ دوباره تلاش کنید.',
	license_sequence_replay_or_downgrade: 'این پاکت قدیمی‌تر یا تکراری نسبت به مجوز نصب‌شده است؛ فایل مجوز جدیدتر را ارائه کنید.',
	license_id_change_requires_reactivation: 'شناسه لایسنس تغییر کرده است؛ فعال‌سازی مجدد طبق فرایند رسمی لازم است.',
	installation_identity_changed_reactivation_required: 'هویت نصب تغییر کرده است؛ فعال‌سازی مجدد لازم است.',
	license_audit_unavailable: 'سرویس Audit نصب در دسترس نیست؛ فعال‌سازی فعلاً انجام نمی‌شود و بعداً تلاش کنید.',
	installation_identity_proof_unavailable: 'سرویس هویت نصب در دسترس نیست؛ فعال‌سازی فعلاً انجام نمی‌شود.',
	offline_revocation_unavailable: 'سرویس ابطال آفلاین در دسترس نیست؛ فعال‌سازی فعلاً انجام نمی‌شود.',
	envelope_invalid: 'محتوای فایل مجوز معتبر نیست؛ پاکت امضاشده را عیناً و کامل وارد کنید.',
};

function errorMessageFor(status: number, detail?: string): string {
	if (status === 401 || status === 403)
		return 'فعال‌سازی مجوز فقط با حساب زیرساخت (Infrastructure) انجام می‌شود؛ با حساب نصب وارد شوید.';
	if (status === 503)
		return 'سرویس‌های امنیتی نصب (Audit / هویت نصب / ابطال آفلاین) در دسترس نیستند؛ فعال‌سازی fail-closed انجام نمی‌شود و بعداً تلاش کنید.';
	if (detail && errorMessages[detail]) return errorMessages[detail];
	if (status === 409) return 'فعال‌سازی مجوز با وضعیت فعلی نصب تعارض دارد؛ فایل مجوز صحیح را ارائه کنید.';
	return 'فعال‌سازی مجوز انجام نشد؛ فایل مجوز و وضعیت نصب را بررسی کنید.';
}

function formatDate(value?: string): string {
	if (!value) return '—';
	const parsed = new Date(value);
	return Number.isNaN(parsed.getTime()) ? '—' : parsed.toLocaleString('fa-IR');
}

/** نمایش وضعیت مجوز و فعال‌سازی با فایل امضاشده (Issue #396؛ D-060/D-098). */
export function LicenseActivationPage() {
	const [status, setStatus] = useState<LicensingStatus | null>(null);
	const [statusError, setStatusError] = useState('');
	const [envelopeJson, setEnvelopeJson] = useState('');
	const [fileName, setFileName] = useState('');
	const [requestToken, setRequestToken] = useState('');
	const [headerName, setHeaderName] = useState('X-XSRF-TOKEN');
	const [busy, setBusy] = useState(false);
	const [error, setError] = useState('');
	const [result, setResult] = useState<ImportResult | null>(null);
	const fileInputRef = useRef<HTMLInputElement>(null);

	const loadStatus = useCallback(async () => {
		setStatusError('');
		try {
			const response = await apiGetRaw('/licensing/status');
			if (!response.ok) throw new Error('دریافت وضعیت مجوز ناموفق بود؛ بعداً دوباره تلاش کنید.');
			setStatus((response.body ?? {}) as LicensingStatus);
		} catch (reason) {
			setStatus(null);
			setStatusError(reason instanceof Error ? reason.message : 'خطای ارتباط با سرور.');
		}
	}, []);

	useEffect(() => {
		void loadStatus();
		let cancelled = false;
		apiGetRaw('/antiforgery/token').then((tokenResult) => {
			if (cancelled || !tokenResult.ok) return;
			const body = (tokenResult.body ?? {}) as AntiforgeryResponse;
			setRequestToken(body.requestToken ?? '');
			setHeaderName(body.headerName ?? 'X-XSRF-TOKEN');
		}).catch(() => { /* the submit path reports the missing token explicitly */ });
		return () => { cancelled = true; };
	}, [loadStatus]);

	async function readFile(event: ChangeEvent<HTMLInputElement>) {
		setError('');
		setResult(null);
		const file = event.target.files?.[0];
		if (!file) return;
		if (file.size > MaximumEnvelopeChars) {
			setError('حجم فایل مجاز بیش از حد مجاز است (سقف ۳۰۰ هزار نویسه).');
			event.target.value = '';
			return;
		}
		const text = await file.text();
		if (text.length > MaximumEnvelopeChars) {
			setError('محتوای فایل مجاز بیش از حد مجاز است (سقف ۳۰۰ هزار نویسه).');
			return;
		}
		try {
			JSON.parse(text);
		} catch {
			setError('محتوای فایل، JSON معتبر نیست؛ فایل پاکت امضاشده را عیناً انتخاب کنید.');
			return;
		}
		setEnvelopeJson(text);
		setFileName(file.name);
	}

	function clearSelection() {
		setEnvelopeJson('');
		setFileName('');
		if (fileInputRef.current) fileInputRef.current.value = '';
	}

	async function submit(event: React.FormEvent<HTMLFormElement>) {
		event.preventDefault();
		if (busy) return;
		const trimmed = envelopeJson.trim();
		if (!trimmed) {
			setError('ابتدا فایل مجوز امضاشده را انتخاب کنید.');
			return;
		}
		if (!requestToken) {
			setError('توکن امنیتی صفحه آماده نیست؛ صفحه را تازه‌سازی کنید.');
			return;
		}
		setBusy(true);
		setError('');
		setResult(null);
		try {
			const response = await apiPostJson(
				'/licensing/import',
				{ envelopeJson: trimmed },
				{ headerName, requestToken },
			);
			if (!response.ok) {
				const body = (response.body ?? {}) as ApiError;
				throw new Error(errorMessageFor(response.status, body.detail));
			}
			const body = (response.body ?? {}) as ImportResult;
			setResult(body);
			setEnvelopeJson('');
			setFileName('');
			if (fileInputRef.current) fileInputRef.current.value = '';
			await loadStatus();
		} catch (reason) {
			setError(reason instanceof Error ? reason.message : 'خطای ارتباط با سرور.');
		} finally {
			setBusy(false);
		}
	}

	const state = status?.state ?? '';
	const recoveryGuidance = state === 'active'
		? 'مجوز فعال است و همه عملیات عادی در دسترس است.'
		: state === 'restricted'
			? (status?.deploymentMode === 'dedicated'
				? 'وضعیت محدود: در نصب اختصاصی فقط عملیات خواندن باز است و عملیات عادی مسدود می‌شود؛ مسیر تمدید و بازیابی باز می‌ماند.'
				: 'وضعیت محدود: عملیات عادی مسدود است؛ مسیر تمدید و بازیابی باز می‌ماند.')
			: state === 'invalid'
				? 'مجوز نامعتبر است (امضا، دامنه یا انقضا)؛ فایل مجوز معتبر را ارائه کنید یا با پشتیبانی تماس بگیرید.'
				: 'هنوز مجوزی فعال نشده است؛ فایل مجوز امضاشده را در بخش فعال‌سازی ارائه کنید.';

	return (
		<main className="mx-auto flex min-h-screen w-full max-w-3xl flex-col gap-6 p-4 sm:p-6" dir="rtl">
			<h1 className="text-2xl font-bold">فعال‌سازی مجوز فرانو</h1>

			<Card>
				<CardHeader>
					<CardTitle>وضعیت فعلی مجوز</CardTitle>
					<CardDescription>این وضعیت در هر بار خواندن از ارزیابی revocation-aware سمت سرور resolve می‌شود.</CardDescription>
				</CardHeader>
				<CardContent className="flex flex-col gap-4">
					{statusError && <Alert tone="error">{statusError}</Alert>}
					{status && (
						<div className="flex flex-col gap-3">
							<div className="flex flex-wrap items-center gap-2">
								<Badge tone={stateTones[state] ?? 'neutral'}>
									{stateLabels[state] ?? (state || 'نامشخص')}
								</Badge>
								<span className="text-sm text-muted-foreground">
									حالت استقرار: {deploymentLabels[status.deploymentMode ?? ''] ?? status.deploymentMode ?? '—'}
								</span>
							</div>
							{status.errorCode && (
								<p className="text-sm text-destructive">کد وضعیت: <code dir="ltr">{status.errorCode}</code></p>
							)}
							{status.license ? (
								<dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
									<div><dt className="font-medium">شناسه لایسنس</dt><dd dir="ltr" className="break-all">{status.license.licenseId ?? '—'}</dd></div>
									<div><dt className="font-medium">نسخه (Sequence)</dt><dd>{status.license.sequence ?? '—'}</dd></div>
									<div><dt className="font-medium">محصول</dt><dd>{status.license.productCode ?? '—'}</dd></div>
									<div><dt className="font-medium">انقضای مجوز</dt><dd>{formatDate(status.license.expiresAtUtc)}</dd></div>
									<div><dt className="font-medium">سقف شرکت‌ها</dt><dd>{status.license.maxCompanies ?? '—'}</dd></div>
									<div>
										<dt className="font-medium">زیرسیستم‌ها</dt>
										<dd>{status.license.subsystems?.length ? status.license.subsystems.join('، ') : '—'}</dd>
									</div>
								</dl>
							) : (
								<p className="text-sm text-muted-foreground">پاکت مجوز معتبری روی این نصب resolve نشده است.</p>
							)}
							<p className="text-sm text-muted-foreground">{recoveryGuidance}</p>
						</div>
					)}
					<Button type="button" variant="outline" onClick={() => void loadStatus()}>
						به‌روزرسانی وضعیت
					</Button>
				</CardContent>
			</Card>

			<Card>
				<CardHeader>
					<CardTitle>فعال‌سازی با فایل مجوز امضاشده</CardTitle>
					<CardDescription>
						فایل پاکت مجوز صادرشده از Vendor را عیناً ارائه کنید. اعتبارسنجی امضا، هویت نصب و ابطال کامل سمت سرور و fail-closed است.
					</CardDescription>
				</CardHeader>
				<CardContent>
					<form className="flex flex-col gap-4" onSubmit={submit}>
						<div className="flex flex-col gap-2">
							<label htmlFor="license-file" className="text-sm font-medium">فایل مجوز (.json)</label>
							<input
								id="license-file"
								ref={fileInputRef}
								type="file"
								accept=".json,.txt,application/json,text/plain"
								onChange={(event) => void readFile(event)}
								className="rounded-md border border-input bg-background p-2 text-sm file:mr-3"
							/>
							{fileName && (
								<p className="text-sm text-muted-foreground">
									فایل انتخاب‌شده: <code dir="ltr">{fileName}</code>
									<button type="button" className="mr-2 underline" onClick={clearSelection}>حذف</button>
								</p>
							)}
						</div>
						<div className="flex flex-col gap-2">
							<label htmlFor="license-paste" className="text-sm font-medium">یا محتوای پاکت را اینجا بچسبانید</label>
							<textarea
								id="license-paste"
								dir="ltr"
								rows={6}
								value={envelopeJson}
								onChange={(event) => { setEnvelopeJson(event.target.value); setFileName(''); }}
								className="rounded-md border border-input bg-background p-2 font-mono text-xs"
							/>
						</div>
						{error && <Alert tone="error">{error}</Alert>}
						{result && (
							<Alert tone="info">
								{result.alreadyInstalled
									? 'این مجوز از قبل روی این نصب نصب است و تغییری لازم نبود.'
									: `فعال‌سازی با موفقیت انجام شد${result.status ? `؛ وضعیت جدید: ${result.status}` : '.'}`}
							</Alert>
						)}
						<Button type="submit" disabled={busy || !envelopeJson.trim()}>
							{busy ? 'در حال فعال‌سازی…' : 'فعال‌سازی مجوز'}
						</Button>
					</form>
				</CardContent>
			</Card>
		</main>
	);
}
