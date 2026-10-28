import { useEffect, useState, type FormEvent } from 'react';
import { AlertCircle, LoaderCircle, RefreshCw, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type Installation = {
  id: string;
  installationCode: string;
  name: string;
  status: string;
};
type ProvisioningRequest = {
  id: string;
  requestId: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  confirmedAtUtc?: string | null;
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
};
type ApiError = { error?: string; message?: string };

const subsystemOptions = [
  { value: 'finance', label: 'مالی' },
  { value: 'sales', label: 'فروش' },
  { value: 'inventory', label: 'انبار' },
  { value: 'treasury', label: 'خزانه‌داری' },
] as const;

const statusLabels: Record<string, string> = {
  AwaitingSigner: 'در انتظار پیکربندی امن امضا',
  PendingDelivery: 'آماده تحویل؛ هنوز تأیید مشتری ثبت نشده',
  Confirmed: 'تأییدشده توسط مصرف‌کننده مشتری',
  Failed: 'ناموفق؛ نیازمند بررسی و تلاش مجدد',
};

function publicError(status: number, code: string): string {
  if (status === 400 && code === 'invalid_antiforgery_token') return 'توکن امنیتی معتبر نیست؛ فرم را تازه‌سازی کنید.';
  if (status === 400 && code === 'invalid_provisioning_subsystems') return 'حداقل یک زیرسیستم مجاز انتخاب کنید و گزینه‌های تکراری را حذف کنید.';
  if (status === 400 && code === 'invalid_provisioning_configuration') return 'نام شرکت، نوع شرکت یا نام کاربری مدیر اولیه معتبر نیست.';
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

export function ProvisioningForm({ customerId, customerName, onCancel, onSaved, canRequestApproval = false }: Props) {
  const [installations, setInstallations] = useState<Installation[]>([]);
  const [requests, setRequests] = useState<ProvisioningRequest[]>([]);
  const [installationId, setInstallationId] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [companyType, setCompanyType] = useState('independent');
  const [adminUserName, setAdminUserName] = useState('');
  const [subsystems, setSubsystems] = useState<string[]>(['finance']);
  const [requestId, setRequestId] = useState(newRequestId);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [approvalBusyId, setApprovalBusyId] = useState<string | null>(null);
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
        setInstallations(nextInstallations);
        setRequests(Array.isArray(requestBody.items) ? requestBody.items : []);
        setInstallationId(current => nextInstallations.some(item => item.id === current)
          ? current
          : nextInstallations[0]?.id ?? '');
      } catch (reason) {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'دریافت اطلاعات ناموفق بود.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => { cancelled = true; };
  }, [customerId, refreshKey]);

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
          companyName: companyName.trim(),
          companyType,
          initialAdminUserName: adminUserName.trim(),
          subsystems: [...subsystems].sort(),
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
      setCompanyType('independent');
      setSubsystems(['finance']);
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

      <form className="space-y-4" onSubmit={submit} noValidate>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor="provisioning-installation" className="text-sm font-medium">نصب مقصد</label>
            <select id="provisioning-installation" required value={installationId}
              onChange={event => setInstallationId(event.target.value)} disabled={loading || busy || installations.length === 0}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
              <option value="">انتخاب نصب</option>
              {installations.map(item => <option key={item.id} value={item.id}>{item.name} ({item.installationCode})</option>)}
            </select>
            {!loading && installations.length === 0 && <p className="text-xs text-muted-foreground">ابتدا یک نصب برای این مشتری ثبت کنید.</p>}
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-company-name" className="text-sm font-medium">نام شرکت حسابداری</label>
            <input id="provisioning-company-name" required maxLength={200} autoComplete="organization"
              value={companyName} onChange={event => setCompanyName(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-company-type" className="text-sm font-medium">نوع شرکت</label>
            <select id="provisioning-company-type" value={companyType} onChange={event => setCompanyType(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
              <option value="independent">مستقل</option>
              <option value="parent">مادر</option>
              <option value="subsidiary">تابعه</option>
            </select>
          </div>
          <div className="space-y-2">
            <label htmlFor="provisioning-admin-username" className="text-sm font-medium">نام کاربری مدیر اولیه</label>
            <input id="provisioning-admin-username" required minLength={3} maxLength={64} autoComplete="off"
              value={adminUserName} onChange={event => setAdminUserName(event.target.value)} disabled={busy}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"/>
          </div>
        </div>

        <fieldset className="space-y-3">
          <legend className="text-sm font-semibold">زیرسیستم‌های خریداری‌شده</legend>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {subsystemOptions.map(option => <label key={option.value} className="flex items-center gap-2 rounded-md border border-border p-3 text-sm">
              <input type="checkbox" checked={subsystems.includes(option.value)} onChange={event => toggleSubsystem(option.value, event.target.checked)} disabled={busy}/>
              <span>{option.label}</span>
            </label>)}
          </div>
        </fieldset>

        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={busy || loading || !installationId || !companyName.trim() || !adminUserName.trim() || subsystems.length === 0}>
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
            <table className="w-full min-w-[520px] text-right text-sm">
              <thead><tr>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">وضعیت</th>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">زمان ثبت</th>
                <th className="border-b px-3 py-2 font-semibold text-muted-foreground">شناسه درخواست</th>
                {canRequestApproval && <th className="border-b px-3 py-2 font-semibold text-muted-foreground">عملیات</th>}
              </tr></thead>
              <tbody>{requests.map(item => <tr key={item.id}>
                <td className="border-b px-3 py-2">{statusLabels[item.status] ?? item.status}</td>
                <td className="border-b px-3 py-2 tabular-nums">{formatDate(item.createdAtUtc)}</td>
                <td className="border-b px-3 py-2 font-mono text-xs" dir="ltr">{item.requestId}</td>
                {canRequestApproval && <td className="border-b px-3 py-2">
                  {item.approvalStatus === 'Pending'
                    ? <span className="text-xs text-muted-foreground">در انتظار بررسی دو نفره</span>
                    : item.status === 'AwaitingSigner' && <Button type="button" size="sm" variant="outline"
                      onClick={() => void requestApproval(item)} disabled={busy || approvalBusyId !== null || !item.rowVersion}>
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
