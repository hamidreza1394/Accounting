import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react';
import { AlertCircle, LoaderCircle, RefreshCw, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type Installation = {
  id: string;
  installationCode: string;
  name: string;
  status: string;
};
type EligibleLicense = {
  licenseId: string;
  licenseNumber: string;
  productCode: string;
  sequence: number;
  subsystems: string[];
};
type EligibleParent = {
  requestId: string;
  companyName: string;
  companyType: 'parent' | 'subsidiary';
  confirmedAtUtc?: string | null;
};
type ProvisioningRequest = {
  id: string;
  requestId: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  confirmedAtUtc?: string | null;
  acknowledgementResult?: string | null;
  acknowledgedAtUtc?: string | null;
  failureCode?: string | null;
  payloadSha256: string;
  rowVersion: string;
  approvalStatus?: string | null;
};
type PageResponse<T> = { items: T[] };
type Props = {
  customerId: string;
  customerName: string;
  onCancel: () => void;
  onSaved: () => void;
  canRequestApproval?: boolean;
  canExportPackage?: boolean;
};
type ApiError = { error?: string; message?: string };
type CustomerProvisioningPackageFile = { formatVersion: number; keyId: string; algorithm: string; payloadJson: string; signatureBase64: string };

const subsystemOptions = [
  { value: 'finance', label: 'مالی' },
  { value: 'sales', label: 'فروش' },
  { value: 'inventory', label: 'انبار' },
  { value: 'treasury', label: 'خزانه‌داری' },
] as const;

const MAX_ACKNOWLEDGEMENT_FILE_BYTES = 64 * 1024;

const acknowledgementErrorMessages: Record<string, string> = {
  provisioning_acknowledgement_envelope_invalid: 'ساختار فایل پاسخ مشتری معتبر نیست.',
  provisioning_acknowledgement_signature_invalid: 'امضای فایل پاسخ معتبر نیست؛ فایل دست‌کاری یا با کلید نامعتبر امضا شده است.',
  provisioning_acknowledgement_payload_invalid: 'محتوای فایل پاسخ مشتری معتبر نیست.',
  provisioning_acknowledgement_binding_mismatch: 'فایل پاسخ به این درخواست راه‌اندازی تعلق ندارد.',
  provisioning_acknowledgement_trusted_key_invalid: 'پیکربندی کلید مورد اعتماد پاسخ مشتری معتبر نیست.',
  provisioning_acknowledgement_conflict: 'برای این درخواست پاسخ دیگری قبلاً ثبت شده و قابل جایگزینی نیست.',
  provisioning_request_not_delivered: 'ابتدا بستهٔ امضاشده باید صادر و تحویل داده شود.',
};

function acknowledgementUploadError(status: number, code: string): string {
  if (status === 401) return 'کلید امضای فایل پاسخ در فهرست کلیدهای مورد اعتماد Vendor نیست.';
  if (status === 404) return 'درخواست راه‌اندازی یافت نشد.';
  if (status === 429) return 'تعداد تلاش‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  return acknowledgementErrorMessages[code] ?? 'ثبت فایل پاسخ مشتری ناموفق بود.';
}

function isAcknowledgementEnvelope(value: unknown): boolean {
  if (typeof value !== 'object' || value === null) return false;
  const envelope = value as Record<string, unknown>;
  return typeof envelope.formatVersion === 'number'
    && ['keyId', 'algorithm', 'acknowledgementJson', 'signatureBase64'].every(key => typeof envelope[key] === 'string');
}

const acknowledgementResultLabels: Record<string, string> = {
  Applied: 'راه‌اندازی توسط مشتری اعمال شد',
  AlreadyApplied: 'راه‌اندازی قبلاً اعمال شده بود',
  Rejected: 'درخواست توسط مشتری رد شد',
};

const statusLabels: Record<string, string> = {
  AwaitingSigner: 'در انتظار پیکربندی امن امضا',
  PendingDelivery: 'آماده تحویل؛ هنوز تأیید مشتری ثبت نشده',
  Confirmed: 'تأییدشده توسط مصرف‌کننده مشتری',
  Failed: 'ناموفق؛ نیازمند بررسی و تلاش مجدد',
};

function publicError(status: number, code: string): string {
  if (status === 400 && code === 'invalid_antiforgery_token') return 'توکن امنیتی معتبر نیست؛ فرم را تازه‌سازی کنید.';
  if (status === 400 && code === 'invalid_provisioning_subsystems') return 'زیرسیستم‌های انتخابی معتبر نیستند یا گزینهٔ تکراری دارند.';
  if (status === 404 && code === 'provisioning_license_not_found') return 'لایسنس انتخاب‌شده به این مشتری یا نصب تعلق ندارد.';
  if (status === 409 && code === 'provisioning_license_inactive') return 'لایسنس یا نصب مقصد فعال نیست.';
  if (status === 409 && code === 'provisioning_license_outside_validity') return 'بازهٔ اعتبار لایسنس آغاز نشده یا پایان یافته است.';
  if (status === 409 && code === 'provisioning_license_envelope_missing') return 'برای این لایسنس نسخهٔ امضاشده‌ای وجود ندارد.';
  if (status === 409 && code === 'provisioning_license_sequence_stale') return 'نسخهٔ لایسنس تغییر کرده است؛ فهرست لایسنس‌ها را تازه‌سازی کنید.';
  if (status === 409 && code === 'provisioning_subsystem_not_entitled') return 'یکی از زیرسیستم‌های انتخابی در لایسنس امضاشده وجود ندارد.';
  if (status === 503 && code === 'provisioning_license_signing_key_untrusted') return 'کلید عمومی لایسنس در فهرست کلیدهای مورد اعتماد Vendor پیکربندی نشده است.';
  if (status === 400 && code === 'invalid_provisioning_configuration') return 'نام شرکت، نوع شرکت یا نام کاربری مدیر اولیه معتبر نیست.';
  if (status === 400 && code === 'invalid_provisioning_admin_email') return 'نشانی ایمیل مدیر اولیه را به‌درستی وارد کنید.';
  if (status === 400 && code === 'invalid_provisioning_fiscal_period') return 'عنوان و بازه دوره مالی را بررسی کنید؛ تاریخ پایان باید بعد از تاریخ شروع باشد.';
  if (status === 400 && code === 'invalid_provisioning_parent_company_reference') return 'برای شرکت تابعه باید یک شرکت مادر معتبر انتخاب کنید.';
  if (status === 409 && code === 'provisioning_parent_reference_invalid') return 'شرکت مادر انتخاب‌شده در همین مشتری و نصب، اعمال و تأیید نشده یا دیگر واجد شرایط نیست. فهرست را تازه‌سازی کنید.';
  if (status === 400 && code === 'invalid_provisioning_request_id') return 'شناسه درخواست معتبر نیست؛ دوباره تلاش کنید.';
  if (status === 401) return 'نشست شما پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز ثبت درخواست راه‌اندازی را ندارد.';
  if (status === 404) return 'مشتری یا نصب انتخاب‌شده پیدا نشد یا به این مشتری تعلق ندارد.';
  if (status === 409 && code === 'provisioning_request_id_conflict') return 'شناسه درخواست با تنظیمات دیگری استفاده شده است؛ یک درخواست تازه بسازید.';
  if (status === 429) return 'درخواست‌های زیادی ثبت شده است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی رخ داد. اطلاعات حساس نمایش داده نمی‌شود.';
  return 'ثبت درخواست ناموفق بود؛ وضعیت درخواست را بررسی و دوباره تلاش کنید.';
}

function newRequestId(): string {
  return globalThis.crypto.randomUUID();
}

function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('fa-IR', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date);
}

export function ProvisioningForm({ customerId, customerName, onCancel, onSaved, canRequestApproval = false, canExportPackage = false }: Props) {
  const [installations, setInstallations] = useState<Installation[]>([]);
  const [requests, setRequests] = useState<ProvisioningRequest[]>([]);
  const [eligibleLicenses, setEligibleLicenses] = useState<EligibleLicense[]>([]);
  const [licenseLoading, setLicenseLoading] = useState(false);
  const [eligibleParents, setEligibleParents] = useState<EligibleParent[]>([]);
  const [parentsLoading, setParentsLoading] = useState(false);
  const [installationId, setInstallationId] = useState('');
  const [licenseId, setLicenseId] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [companyType, setCompanyType] = useState('independent');
  const [fiscalPeriodTitle, setFiscalPeriodTitle] = useState('سال مالی ۱۴۰۵');
  const [fiscalPeriodStartDate, setFiscalPeriodStartDate] = useState('2026-03-21');
  const [fiscalPeriodEndDate, setFiscalPeriodEndDate] = useState('2027-03-20');
  const [parentCompanyReference, setParentCompanyReference] = useState('');
  const [adminUserName, setAdminUserName] = useState('');
  const [adminEmail, setAdminEmail] = useState('');
  // Empty means "all entitlements" from the selected signed license revision.
  const [subsystems, setSubsystems] = useState<string[]>([]);
  const [requestId, setRequestId] = useState(newRequestId);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [approvalBusyId, setApprovalBusyId] = useState<string | null>(null);
  const [exportBusyId, setExportBusyId] = useState<string | null>(null);
  const [acknowledgementBusyId, setAcknowledgementBusyId] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError('');
      try {
        const [installationResponse, requestResponse] = await Promise.all([
          fetch(`/api/vendor/installations/?customerId=${encodeURIComponent(customerId)}&page=1&pageSize=100`, {
            credentials: 'same-origin', cache: 'no-store',
          }),
          fetch(`/api/vendor/provisioning-requests?customerId=${encodeURIComponent(customerId)}&page=1&pageSize=10`, {
            credentials: 'same-origin', cache: 'no-store',
          }),
        ]);
        if (!installationResponse.ok) throw new Error(publicError(installationResponse.status, ''));
        if (!requestResponse.ok) throw new Error(publicError(requestResponse.status, ''));
        const installationBody = await installationResponse.json() as PageResponse<Installation>;
        const requestBody = await requestResponse.json() as PageResponse<ProvisioningRequest>;
        if (cancelled) return;
        const nextInstallations = Array.isArray(installationBody.items) ? installationBody.items : [];
        const activeInstallations = nextInstallations.filter(item => item.status === 'Active');
        setInstallations(activeInstallations);
        setRequests(Array.isArray(requestBody.items) ? requestBody.items : []);
        setInstallationId(current => activeInstallations.some(item => item.id === current)
          ? current
          : activeInstallations[0]?.id ?? '');
      } catch (reason) {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'دریافت اطلاعات ناموفق بود.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => { cancelled = true; };
  }, [customerId, refreshKey]);

  useEffect(() => {
    let cancelled = false;
    async function loadEligibleLicenses() {
      if (!installationId) {
        setEligibleLicenses([]);
        setLicenseId('');
        setSubsystems([]);
        setLicenseLoading(false);
        return;
      }

      setLicenseLoading(true);
      try {
        const response = await fetch(
          `/api/vendor/provisioning-requests/eligible-licenses?customerId=${encodeURIComponent(customerId)}&installationId=${encodeURIComponent(installationId)}`,
          { credentials: 'same-origin', cache: 'no-store' },
        );
        if (!response.ok) throw new Error(publicError(response.status, ''));
        const body = await response.json() as PageResponse<EligibleLicense>;
        if (cancelled) return;
        const next = Array.isArray(body.items) ? body.items : [];
        setEligibleLicenses(next);
        setLicenseId(current => next.some(item => item.licenseId === current)
          ? current
          : next[0]?.licenseId ?? '');
      } catch (reason) {
        if (!cancelled) {
          setEligibleLicenses([]);
          setLicenseId('');
          setError(reason instanceof Error ? reason.message : 'دریافت لایسنس‌های معتبر ناموفق بود.');
        }
      } finally {
        if (!cancelled) setLicenseLoading(false);
      }
    }

    void loadEligibleLicenses();
    return () => { cancelled = true; };
  }, [customerId, installationId, refreshKey]);

  useEffect(() => {
    let cancelled = false;
    async function loadEligibleParents() {
      if (companyType !== 'subsidiary' || !installationId) {
        setEligibleParents([]);
        setParentCompanyReference('');
        setParentsLoading(false);
        return;
      }

      setParentsLoading(true);
      try {
        const response = await fetch(
          `/api/vendor/provisioning-requests/eligible-parents?customerId=${encodeURIComponent(customerId)}&installationId=${encodeURIComponent(installationId)}`,
          { credentials: 'same-origin', cache: 'no-store' },
        );
        if (!response.ok) throw new Error(publicError(response.status, ''));
        const body = await response.json() as PageResponse<EligibleParent>;
        if (cancelled) return;
        const items = Array.isArray(body.items) ? body.items : [];
        setEligibleParents(items);
        setParentCompanyReference(current => items.some(item => item.requestId === current) ? current : '');
      } catch (reason) {
        if (!cancelled) {
          setEligibleParents([]);
          setParentCompanyReference('');
          setError(reason instanceof Error ? reason.message : 'دریافت شرکت‌های مادر واجد شرایط ناموفق بود.');
        }
      } finally {
        if (!cancelled) setParentsLoading(false);
      }
    }

    void loadEligibleParents();
    return () => { cancelled = true; };
  }, [customerId, installationId, companyType]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin', cache: 'no-store',
      });
      if (!tokenResponse.ok) throw new Error(publicError(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const response = await fetch('/api/vendor/provisioning-requests/', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': tokenBody.requestToken },
        body: JSON.stringify({
          requestId,
          customerId,
          installationId,
          licenseId,
          companyName: companyName.trim(),
          companyType,
          initialAdminUserName: adminUserName.trim(),
          initialAdminEmail: adminEmail.trim(),
          subsystems: selectedLicense && subsystems.length === selectedLicense.subsystems.length &&
            selectedLicense.subsystems.every(item => subsystems.includes(item))
            ? null
            : [...subsystems].sort(),
          fiscalPeriodTitle: fiscalPeriodTitle.trim(),
          fiscalPeriodStartDate,
          fiscalPeriodEndDate,
          parentCompanyReference: companyType === 'subsidiary' ? parentCompanyReference.trim() : null,
        }),
      });
      if (!response.ok) {
        let code = '';
        try {
          const body = await response.json() as ApiError;
          code = body.error ?? body.message ?? '';
        } catch {
          // Keep errors generic if the API does not return JSON.
        }
        throw new Error(publicError(response.status, code));
      }

      setNotice('تنظیمات در سامانه مرکزی ذخیره شد. هنوز بسته‌ای امضا یا به مشتری تحویل نشده و ایجاد شرکت/مدیر مشتری تأیید نشده است.');
      setRequestId(newRequestId());
      setCompanyName('');
      setAdminUserName('');
      setAdminEmail('');
      setCompanyType('independent');
      setFiscalPeriodTitle('سال مالی ۱۴۰۵');
      setFiscalPeriodStartDate('2026-03-21');
      setFiscalPeriodEndDate('2027-03-20');
      setParentCompanyReference('');
      setSubsystems([]);
      setRefreshKey(current => current + 1);
      onSaved();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ثبت درخواست ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function requestApproval(item: ProvisioningRequest) {
    if (!canRequestApproval || approvalBusyId) return;
    setApprovalBusyId(item.id);
    setError('');
    setNotice('');
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin', cache: 'no-store',
      });
      if (!tokenResponse.ok) throw new Error(publicError(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const response = await fetch(`/api/vendor/provisioning-requests/${encodeURIComponent(item.id)}/approval-request`, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': tokenBody.requestToken },
        body: JSON.stringify({ rowVersion: item.rowVersion }),
      });
      if (!response.ok) {
        let code = '';
        try {
          const body = await response.json() as ApiError;
          code = body.error ?? body.message ?? '';
        } catch {
          // Do not expose raw server errors.
        }
        throw new Error(response.status === 409 && code === 'approval_already_pending'
          ? 'این درخواست قبلاً برای بررسی دو نفره ارسال شده است.'
          : publicError(response.status, code));
      }
      setNotice('درخواست برای بررسی دو نفره ارسال شد؛ بسته هنوز امضا یا به مشتری تحویل نشده است.');
      setRefreshKey(current => current + 1);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ارسال برای بررسی ناموفق بود.');
    } finally {
      setApprovalBusyId(null);
    }
  }

  async function exportPackage(item: ProvisioningRequest) {
    if (!canExportPackage || exportBusyId) return;
    setExportBusyId(item.id);
    setError('');
    setNotice('');
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin', cache: 'no-store',
      });
      if (!tokenResponse.ok) throw new Error(publicError(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const response = await fetch(`/api/vendor/provisioning-requests/${encodeURIComponent(item.id)}/package/export`, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': tokenBody.requestToken },
        body: JSON.stringify({}),
      });
      if (!response.ok) {
        let code = '';
        try {
          const body = await response.json() as ApiError;
          code = body.error ?? body.message ?? '';
        } catch {
          // Keep the public error generic.
        }
        throw new Error(publicError(response.status, code));
      }

      const packageBody = await response.json() as CustomerProvisioningPackageFile;
      const blob = new Blob([JSON.stringify(packageBody, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `faranou-provisioning-${item.requestId}.json`;
      anchor.style.display = 'none';
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      setNotice('بستهٔ امضاشده صادر و برای انتقال آفلاین دانلود شد؛ تأیید مشتری هنوز ثبت نشده است.');
      setRefreshKey(current => current + 1);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'دریافت بسته ناموفق بود.');
    } finally {
      setExportBusyId(null);
    }
  }

  async function submitAcknowledgementFile(item: ProvisioningRequest, event: ChangeEvent<HTMLInputElement>) {
    const input = event.target;
    const file = input.files?.[0];
    if (!file || acknowledgementBusyId) return;
    setAcknowledgementBusyId(item.id);
    setError('');
    setNotice('');
    try {
      if (file.size > MAX_ACKNOWLEDGEMENT_FILE_BYTES) throw new Error('حجم فایل پاسخ مشتری بیش از حد مجاز است.');
      let envelope: unknown;
      try {
        envelope = JSON.parse(await file.text());
      } catch {
        throw new Error('فایل پاسخ مشتری JSON معتبر نیست.');
      }
      if (!isAcknowledgementEnvelope(envelope)) throw new Error('ساختار فایل پاسخ مشتری معتبر نیست.');

      // Machine-to-machine route: the signature, verified against the Vendor trust store, is the proof.
      const response = await fetch(`/api/vendor/provisioning-requests/${encodeURIComponent(item.requestId)}/acknowledgement`, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(envelope),
      });
      if (!response.ok) {
        let code = '';
        try {
          const body = await response.json() as ApiError;
          code = body.error ?? body.message ?? '';
        } catch {
          // Keep the public error generic.
        }
        throw new Error(acknowledgementUploadError(response.status, code));
      }
      const body = await response.json().catch(() => ({})) as { repeated?: boolean };
      setNotice(body.repeated
        ? 'این فایل پاسخ قبلاً ثبت شده بود؛ وضعیت تغییری نکرد.'
        : 'فایل پاسخ مشتری با کلید مورد اعتماد تأیید و ثبت شد.');
      setRefreshKey(current => current + 1);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ثبت فایل پاسخ مشتری ناموفق بود.');
    } finally {
      input.value = '';
      setAcknowledgementBusyId(null);
    }
  }

  const selectedLicense = eligibleLicenses.find(item => item.licenseId === licenseId);

  useEffect(() => {
    setSubsystems(selectedLicense?.subsystems ?? []);
  }, [licenseId, eligibleLicenses]);

  function toggleSubsystem(value: string, checked: boolean) {
    setSubsystems(current => checked
      ? (current.includes(value) ? current : [...current, value])
      : current.filter(item => item !== value));
  }

  return <Card dir="rtl" aria-labelledby="provisioning-form-title">
    <CardHeader className="border-b">
      <CardTitle id="provisioning-form-title">تنظیم راه‌اندازی مشتری</CardTitle>
      <CardDescription>مشتری: {customerName}. این مرحله فقط درخواست را در دیتابیس مستقل Vendor ثبت می‌کند؛ به دیتابیس حسابداری مشتری متصل نمی‌شود.</CardDescription>
    </CardHeader>
    <CardContent className="space-y-5 pt-4">
      <div className="flex items-start gap-3 rounded-md border border-amber-500/30 bg-amber-500/5 p-3 text-sm leading-6">
        <ShieldCheck size={20} className="mt-0.5 shrink-0 text-amber-700" />
        <p>رمز عبور مدیر مشتری در این فرم دریافت یا ذخیره نمی‌شود. درخواست تا اتصال امضاکننده مورد اعتماد در وضعیت انتظار می‌ماند؛ هنوز راه‌اندازی مشتری انجام نشده است.</p>
      </div>

      {error && <div role="alert" className="flex items-start gap-2 text-sm text-destructive"><AlertCircle size={18} className="mt-0.5 shrink-0"/><p>{error}</p></div>}
      {notice && <p role="status" className="text-sm leading-6 text-muted-foreground">{notice}</p>}

      <form className="space-y-4" onSubmit={submit}>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor="provisioning-installation" className="text-sm font-medium">نصب مقصد</label>
            <select id="provisioning-installation" required value={installationId}
              onChange={event => setInstallationId(event.target.value)} disabled={loading || busy || installations.length === 0}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
              <option value="">انتخاب نصب</option>
              {installations.map(item => <option key={item.id} value={item.id}>{item.name} ({item.installationCode})</option>)}
            </select>
            {!loading && installations.length === 0 && <p className="text-xs text-muted-foreground">برای این مشتری نصب فعالی وجود ندارد؛ ابتدا یک نصب فعال ثبت یا فعال کنید.</p>}
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-license" className="text-sm font-medium">لایسنس صادرشده</label>
            <select id="provisioning-license" required value={licenseId}
              onChange={event => { setLicenseId(event.target.value); setSubsystems([]); }}
              disabled={busy || licenseLoading || eligibleLicenses.length === 0}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
              <option value="">انتخاب لایسنس</option>
              {eligibleLicenses.map(item => <option key={item.licenseId} value={item.licenseId}>
                {item.licenseNumber} — {item.productCode} — نسخه {item.sequence}
              </option>)}
            </select>
            {licenseLoading ? <p className="text-xs text-muted-foreground">در حال بررسی امضای لایسنس‌ها…</p>
              : eligibleLicenses.length === 0
                ? <p className="text-xs text-muted-foreground">برای این نصب لایسنس امضاشده، فعال و معتبر پیدا نشد.</p>
                : <p className="text-xs text-muted-foreground">اعتبار و زیرسیستم‌ها در سرور از آخرین نسخهٔ امضاشده استخراج و دوباره بررسی می‌شوند.</p>}
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-company-name" className="text-sm font-medium">نام شرکت حسابداری</label>
            <input id="provisioning-company-name" required maxLength={200} autoComplete="organization"
              value={companyName} onChange={event => setCompanyName(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-company-type" className="text-sm font-medium">نوع شرکت</label>
            <select id="provisioning-company-type" value={companyType} onChange={event => {
                const nextType = event.target.value;
                setCompanyType(nextType);
                if (nextType !== 'subsidiary') setParentCompanyReference('');
              }} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
              <option value="independent">مستقل</option>
              <option value="parent">مادر</option>
              <option value="subsidiary">تابعه</option>
            </select>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-fiscal-period-title" className="text-sm font-medium">عنوان دوره مالی</label>
            <input id="provisioning-fiscal-period-title" required maxLength={100} value={fiscalPeriodTitle}
              onChange={event => setFiscalPeriodTitle(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-fiscal-period-start" className="text-sm font-medium">تاریخ شروع دوره مالی</label>
            <input id="provisioning-fiscal-period-start" type="date" required value={fiscalPeriodStartDate}
              onChange={event => setFiscalPeriodStartDate(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-fiscal-period-end" className="text-sm font-medium">تاریخ پایان دوره مالی</label>
            <input id="provisioning-fiscal-period-end" type="date" required value={fiscalPeriodEndDate}
              onChange={event => setFiscalPeriodEndDate(event.target.value)} disabled={busy}
              min={fiscalPeriodStartDate || undefined}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
          {companyType === 'subsidiary' && <div className="space-y-2">
            <label htmlFor="provisioning-parent-company-reference" className="text-sm font-medium">شرکت مادر تأییدشده در همین نصب</label>
            <select id="provisioning-parent-company-reference" required dir="ltr"
              value={parentCompanyReference} onChange={event => setParentCompanyReference(event.target.value)}
              disabled={busy || parentsLoading || eligibleParents.length === 0}
              aria-describedby="provisioning-parent-company-help"
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50">
              <option value="">{parentsLoading ? 'در حال دریافت فهرست…' : 'انتخاب شرکت مادر اعمال‌شده'}</option>
              {eligibleParents.map(item => <option key={item.requestId} value={item.requestId}>
                {item.companyName} — {item.companyType === 'parent' ? 'مادر' : 'تابعه'} — {item.requestId}
              </option>)}
            </select>
            <p id="provisioning-parent-company-help" className="text-xs leading-5 text-muted-foreground">
              فقط درخواست‌های مادر که در همین مشتری و نصب اعمال و acknowledgement آن‌ها تأیید شده باشد نمایش داده می‌شوند.
              {eligibleParents.length === 0 && !parentsLoading ? ' هنوز گزینهٔ واجد شرایطی وجود ندارد.' : ''}
            </p>
          </div>}
          <div className="space-y-2">
            <label htmlFor="provisioning-admin-username" className="text-sm font-medium">نام کاربری مدیر اولیه</label>
            <input id="provisioning-admin-username" required minLength={3} maxLength={64} autoComplete="off"
              value={adminUserName} onChange={event => setAdminUserName(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-admin-email" className="text-sm font-medium">ایمیل مدیر اولیه</label>
            <input id="provisioning-admin-email" type="email" required maxLength={254} autoComplete="email" dir="ltr"
              value={adminEmail} onChange={event => setAdminEmail(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
            <p className="text-xs leading-5 text-muted-foreground">این ایمیل برای هویت مدیر در نصب مشتری استفاده می‌شود؛ رمز عبور در بسته قرار نمی‌گیرد.</p>
          </div>
        </div>

        <fieldset className="space-y-3">
          <legend className="text-sm font-semibold">زیرسیستم‌های مجاز در لایسنس انتخاب‌شده</legend>
          {!selectedLicense ? <p className="text-xs text-muted-foreground">ابتدا یک لایسنس معتبر انتخاب کنید.</p>
            : <>
              <p className="text-xs leading-5 text-muted-foreground">همهٔ موارد به‌صورت پیش‌فرض انتخاب شده‌اند. برای محدودکردن بسته، فقط زیرمجموعهٔ موردنیاز از entitlementهای امضاشده را نگه دارید.</p>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {subsystemOptions.filter(option => selectedLicense.subsystems.includes(option.value)).map(option => <label key={option.value} className="flex items-center gap-2 rounded-md border border-border p-3 text-sm">
                  <input type="checkbox"
                    checked={subsystems.includes(option.value)}
                    onChange={event => toggleSubsystem(option.value, event.target.checked)} disabled={busy}/>
                  <span>{option.label}</span>
                </label>)}
              </div>
            </>}
        </fieldset>

        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={busy || loading || !installationId || licenseLoading || !licenseId || !companyName.trim() || !adminUserName.trim() || !adminEmail.trim() ||
            !fiscalPeriodTitle.trim() || !fiscalPeriodStartDate || !fiscalPeriodEndDate ||
            (fiscalPeriodStartDate >= fiscalPeriodEndDate) ||
            (companyType === 'subsidiary' && (!parentCompanyReference.trim() || parentsLoading || eligibleParents.length === 0)) || subsystems.length === 0}>
            {busy ? <><LoaderCircle size={16} className="animate-spin"/> در حال ثبت…</> : 'ذخیره درخواست راه‌اندازی'}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel} disabled={busy}>بستن</Button>
          <Button type="button" variant="outline" onClick={() => setRefreshKey(current => current + 1)} disabled={busy || loading}>
            <RefreshCw size={16}/> تازه‌سازی وضعیت
          </Button>
        </div>
      </form>

      <div className="space-y-3 border-t border-border pt-4">
        <div>
          <h3 className="font-semibold">وضعیت درخواست‌های راه‌اندازی</h3>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">وضعیت «تأییدشده» فقط پس از دریافت تأیید از مصرف‌کننده مورد اعتماد مشتری معنا دارد.</p>
        </div>
        {loading ? <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground"><LoaderCircle size={16} className="animate-spin"/> در حال دریافت وضعیت…</p>
          : requests.length === 0 ? <p className="text-sm text-muted-foreground">هنوز درخواستی ثبت نشده است.</p>
          : <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-right text-sm">
              <thead><tr>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">وضعیت</th>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">نتیجه پاسخ مشتری</th>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">زمان ثبت</th>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">زمان پاسخ مشتری</th>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">شناسه درخواست</th>
                {(canRequestApproval || canExportPackage) && <th className="border-b px-3 py-2 font-semibold text-muted-foreground">عملیات</th>}
              </tr></thead>
              <tbody>{requests.map(item => <tr key={item.id}>
                <td className="border-b px-3 py-2">{statusLabels[item.status] ?? item.status}</td>
                <td className="border-b px-3 py-2">{item.acknowledgementResult
                  ? acknowledgementResultLabels[item.acknowledgementResult] ?? item.acknowledgementResult
                  : <span className="text-muted-foreground">—</span>}</td>
                <td className="border-b px-3 py-2 tabular-nums">{formatDate(item.createdAtUtc)}</td>
                <td className="border-b px-3 py-2 tabular-nums">{formatDate(item.acknowledgedAtUtc)}</td>
                <td className="border-b px-3 py-2 font-mono text-xs" dir="ltr">{item.requestId}</td>
                {(canRequestApproval || canExportPackage) && <td className="border-b px-3 py-2">
                  {canExportPackage && item.status === 'PendingDelivery'
                    ? <div className="flex flex-wrap items-center gap-2">
                      <Button type="button" size="sm" variant="outline" onClick={() => void exportPackage(item)}
                        disabled={busy || exportBusyId !== null || acknowledgementBusyId !== null}>
                        {exportBusyId === item.id ? <><LoaderCircle size={14} className="animate-spin"/> در حال دریافت…</> : 'دانلود بستهٔ امضاشده'}
                      </Button>
                      <label className="inline-flex h-8 cursor-pointer items-center gap-1 rounded-md border border-input bg-background px-3 text-xs font-medium hover:bg-muted">
                        {acknowledgementBusyId === item.id ? <><LoaderCircle size={14} className="animate-spin"/> در حال بررسی…</> : 'ثبت فایل پاسخ مشتری'}
                        <input type="file" accept=".json,application/json" className="sr-only"
                          aria-label={`ثبت فایل پاسخ مشتری برای ${item.requestId}`}
                          disabled={busy || exportBusyId !== null || acknowledgementBusyId !== null}
                          onChange={event => void submitAcknowledgementFile(item, event)}/>
                      </label>
                    </div>
                    : canRequestApproval && item.approvalStatus === 'Pending'
                      ? <span className="text-xs text-muted-foreground">در انتظار بررسی دو نفره</span>
                      : canRequestApproval && item.status === 'AwaitingSigner' && <Button type="button" size="sm" variant="outline"
                        onClick={() => void requestApproval(item)} disabled={busy || approvalBusyId !== null || exportBusyId !== null || !item.rowVersion}>
                        {approvalBusyId === item.id ? <><LoaderCircle size={14} className="animate-spin"/> در حال ارسال…</> : 'ارسال برای تأیید دو نفره'}
                      </Button>}
                </td>}
              </tr>)}</tbody>
            </table>
          </div>}
      </div>
    </CardContent>
  </Card>;
}
