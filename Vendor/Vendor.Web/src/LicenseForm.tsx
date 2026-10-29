import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ManagedSelect, type SelectOption } from '@/components/forms/managed-select';

export type LicenseDraft = {
  id?: string;
  customerId: string;
  contractId: string | null;
  installationId: string | null;
  licenseNumber: string;
  productCode: string;
  licenseKind?: string;
  maxCompanies?: number | null;
  subsystems?: string[];
  graceDays?: number;
  lastIssuedSequence?: number;
  validFromUtc: string;
  validUntilUtc: string;
  status?: string;
  rowVersion?: string;
};

type LicenseFormProps = {
  initial?: LicenseDraft;
  canReadCustomers: boolean;
  canReadContracts: boolean;
  canReadInstallations: boolean;
  onCancel: () => void;
  onSaved: () => void;
};

type Customer = { id: string; customerNumber: string; displayName: string };
type Contract = { id: string; customerId: string; contractNumber: string; status: string };
type Installation = { id: string; customerId: string; installationCode: string; name: string; status: string };
type PageResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};
type ApiError = { error?: string; message?: string };

const noContract: SelectOption = { value: '', label: 'بدون قرارداد' };
const noInstallation: SelectOption = { value: '', label: 'بدون نصب مشخص' };

const subsystemOptions = [
  { code: 'finance', label: 'مالی (finance)' },
  { code: 'sales', label: 'فروش (sales)' },
  { code: 'inventory', label: 'انبار (inventory)' },
  { code: 'treasury', label: 'خزانه (treasury)' },
] as const;

function todayUtcDate() {
  return new Date().toISOString().slice(0, 10);
}

function isValidDateOnly(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const parsed = new Date(`${value}T00:00:00.000Z`);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === value;
}

function asUtcDate(value: string) {
  return new Date(`${value}T00:00:00.000Z`).toISOString();
}

function errorMessage(status: number, code: string): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز لازم برای این عملیات را ندارد.';
  if (status === 400 && code === 'invalid_license') return 'شماره لایسنس، کد محصول یا بازه اعتبار معتبر نیست.';
  if (status === 400 && code === 'customer_not_found') return 'مشتری انتخاب‌شده وجود ندارد یا در دسترس نیست.';
  if (status === 400 && code === 'contract_customer_mismatch') return 'قرارداد انتخاب‌شده متعلق به مشتری انتخاب‌شده نیست.';
  if (status === 400 && code === 'installation_customer_mismatch') return 'نصب انتخاب‌شده متعلق به مشتری انتخاب‌شده نیست.';
  if (status === 400 && code === 'invalid_row_version') return 'نسخه اطلاعات برای ویرایش معتبر نیست؛ فهرست را تازه‌سازی کنید.';
  if (status === 409 && code === 'license_number_exists') return 'این شماره لایسنس قبلاً ثبت شده است.';
  if (status === 409 && code === 'stale_row_version') return 'لایسنس تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.';
  if (status === 409 && code === 'license_not_editable_after_draft') return 'لایسنس از وضعیت پیش‌نویس خارج شده و دیگر قابل ویرایش نیست.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی سامانه رخ داد. جزئیات حساس نمایش داده نمی‌شود.';
  return 'ذخیره اطلاعات لایسنس ناموفق بود؛ دوباره تلاش کنید.';
}

function responseError(status: number, code: string) {
  return new Error(errorMessage(status, code));
}

export function LicenseForm({
  initial, canReadCustomers, canReadContracts, canReadInstallations, onCancel, onSaved,
}: LicenseFormProps) {
  const editing = Boolean(initial?.id);
  const editable = !editing || initial?.status === 'Draft';
  const [customerId, setCustomerId] = useState(initial?.customerId ?? '');
  const [customerOptions, setCustomerOptions] = useState<SelectOption[]>([]);
  const [selectedCustomerOption, setSelectedCustomerOption] = useState<SelectOption | null>(null);
  const [customerPage, setCustomerPage] = useState(1);
  const [customerPages, setCustomerPages] = useState({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
  const [customerLoading, setCustomerLoading] = useState(false);
  const [customerError, setCustomerError] = useState('');
  const [customerLabel, setCustomerLabel] = useState('');

  const [contractId, setContractId] = useState(initial?.contractId ?? '');
  const [contractOptions, setContractOptions] = useState<SelectOption[]>([noContract]);
  const [selectedContractOption, setSelectedContractOption] = useState<SelectOption | null>(
    initial?.contractId ? { value: initial.contractId, label: `قرارداد فعلی (${initial.contractId})` } : null,
  );
  const [contractPage, setContractPage] = useState(1);
  const [contractPages, setContractPages] = useState({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
  const [contractLoading, setContractLoading] = useState(false);
  const [contractError, setContractError] = useState('');

  const [installationId, setInstallationId] = useState(initial?.installationId ?? '');
  const [installationOptions, setInstallationOptions] = useState<SelectOption[]>([noInstallation]);
  const [selectedInstallationOption, setSelectedInstallationOption] = useState<SelectOption | null>(
    initial?.installationId ? { value: initial.installationId, label: `نصب فعلی (${initial.installationId})` } : null,
  );
  const [installationPage, setInstallationPage] = useState(1);
  const [installationPages, setInstallationPages] = useState({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
  const [installationLoading, setInstallationLoading] = useState(false);
  const [installationError, setInstallationError] = useState('');

  const [licenseNumber, setLicenseNumber] = useState(initial?.licenseNumber ?? '');
  const [productCode, setProductCode] = useState(initial?.productCode ?? '');
  const [licenseKind, setLicenseKind] = useState(initial?.licenseKind ?? 'term');
  const [maxCompanies, setMaxCompanies] = useState(initial?.maxCompanies == null ? '' : String(initial.maxCompanies));
  const [subsystems, setSubsystems] = useState<string[]>(initial?.subsystems ?? []);
  const [graceDays, setGraceDays] = useState(String(initial?.graceDays ?? 30));
  const [validFromUtc, setValidFromUtc] = useState(initial?.validFromUtc?.slice(0, 10) ?? todayUtcDate());
  const [validUntilUtc, setValidUntilUtc] = useState(initial?.validUntilUtc?.slice(0, 10) ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (editing || !canReadCustomers) return;
    let active = true;
    setCustomerLoading(true);
    setCustomerError('');
    fetch(`/api/vendor/customers/?page=${customerPage}&pageSize=100`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw responseError(response.status, '');
      const page = await response.json() as PageResponse<Customer>;
      if (!Array.isArray(page.items) || typeof page.hasNextPage !== 'boolean' ||
          typeof page.hasPreviousPage !== 'boolean' || typeof page.totalCount !== 'number') {
        throw new Error('پاسخ فهرست مشتریان با قرارداد صفحه‌بندی سازگار نیست.');
      }
      if (!active) return;
      const options = page.items.filter(item => typeof item.id === 'string')
        .map(item => ({ value: item.id, label: `${item.customerNumber} — ${item.displayName}` }));
      setCustomerOptions(options);
      setCustomerPages({ hasPreviousPage: page.hasPreviousPage, hasNextPage: page.hasNextPage, totalCount: page.totalCount });
      setCustomerId(current => current || options[0]?.value || '');
      setSelectedCustomerOption(current => current ?? options[0] ?? null);
    }).catch(reason => {
      if (active) {
        setCustomerOptions([]);
        setCustomerError(reason instanceof Error ? reason.message : 'دریافت فهرست مشتریان ناموفق بود.');
      }
    }).finally(() => { if (active) setCustomerLoading(false); });
    return () => { active = false; };
  }, [canReadCustomers, customerPage, editing]);

  useEffect(() => {
    if (!editing || !initial?.customerId || !canReadCustomers) {
      if (editing && initial?.customerId) setCustomerLabel(`شناسه مشتری: ${initial.customerId}`);
      return;
    }
    let active = true;
    fetch(`/api/vendor/customers/${initial.customerId}`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error('نام مشتری مرتبط قابل دریافت نیست.');
      const customer = await response.json() as { customerNumber?: string; displayName?: string };
      if (active) setCustomerLabel([customer.customerNumber, customer.displayName].filter(Boolean).join(' — ') || initial.customerId);
    }).catch(() => { if (active) setCustomerLabel(`شناسه مشتری: ${initial.customerId}`); });
    return () => { active = false; };
  }, [canReadCustomers, editing, initial?.customerId]);

  useEffect(() => {
    if (!customerId) {
      setContractOptions([noContract]);
      setContractPages({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
      return;
    }
    if (!canReadContracts) {
      setContractOptions(editing && initial?.contractId
        ? [{ value: initial.contractId, label: `قرارداد فعلی (${initial.contractId})` }]
        : [noContract]);
      setContractPages({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
      setContractError('');
      return;
    }
    let active = true;
    setContractLoading(true);
    setContractError('');
    fetch(`/api/vendor/contracts/?customerId=${encodeURIComponent(customerId)}&page=${contractPage}&pageSize=100`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw responseError(response.status, '');
      const page = await response.json() as PageResponse<Contract>;
      if (!Array.isArray(page.items) || typeof page.hasNextPage !== 'boolean' ||
          typeof page.hasPreviousPage !== 'boolean' || typeof page.totalCount !== 'number') {
        throw new Error('پاسخ فهرست قراردادها با قرارداد صفحه‌بندی سازگار نیست.');
      }
      if (!active) return;
      setContractOptions([noContract, ...page.items.filter(item => item.customerId === customerId)
        .map(item => ({ value: item.id, label: `${item.contractNumber} — ${item.status}` }))]);
      setContractPages({ hasPreviousPage: page.hasPreviousPage, hasNextPage: page.hasNextPage, totalCount: page.totalCount });
    }).catch(reason => {
      if (active) {
        setContractOptions([noContract]);
        setContractError(reason instanceof Error ? reason.message : 'دریافت قراردادهای مشتری ناموفق بود.');
      }
    }).finally(() => { if (active) setContractLoading(false); });
    return () => { active = false; };
  }, [canReadContracts, contractPage, customerId, editing, initial?.contractId]);

  useEffect(() => {
    if (!editing || !initial?.contractId) {
      setSelectedContractOption(null);
      return;
    }
    if (!canReadContracts) {
      setSelectedContractOption({ value: initial.contractId, label: `قرارداد فعلی (${initial.contractId})` });
      return;
    }
    let active = true;
    fetch(`/api/vendor/contracts/${initial.contractId}`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error('قرارداد مرتبط قابل دریافت نیست.');
      const contract = await response.json() as Contract;
      if (contract.customerId !== initial.customerId) throw new Error('مشتری قرارداد با لایسنس هم‌خوان نیست.');
      if (active) setSelectedContractOption({ value: contract.id, label: `${contract.contractNumber} — ${contract.status}` });
    }).catch(() => {
      if (active) setSelectedContractOption({ value: initial.contractId!, label: `قرارداد فعلی (${initial.contractId})` });
    });
    return () => { active = false; };
  }, [canReadContracts, editing, initial?.contractId, initial?.customerId]);

  useEffect(() => {
    if (!customerId) {
      setInstallationOptions([noInstallation]);
      setInstallationPages({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
      return;
    }
    if (!canReadInstallations) {
      setInstallationOptions(editing && initial?.installationId
        ? [{ value: initial.installationId, label: `نصب فعلی (${initial.installationId})` }]
        : [noInstallation]);
      setInstallationPages({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
      setInstallationError('');
      return;
    }
    let active = true;
    setInstallationLoading(true);
    setInstallationError('');
    fetch(`/api/vendor/installations/?customerId=${encodeURIComponent(customerId)}&page=${installationPage}&pageSize=100`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw responseError(response.status, '');
      const page = await response.json() as PageResponse<Installation>;
      if (!Array.isArray(page.items) || typeof page.hasNextPage !== 'boolean' ||
          typeof page.hasPreviousPage !== 'boolean' || typeof page.totalCount !== 'number') {
        throw new Error('پاسخ فهرست نصب‌ها با قرارداد صفحه‌بندی سازگار نیست.');
      }
      if (!active) return;
      setInstallationOptions([noInstallation, ...page.items.filter(item => item.customerId === customerId)
        .map(item => ({ value: item.id, label: `${item.installationCode} — ${item.name} (${item.status})` }))]);
      setInstallationPages({ hasPreviousPage: page.hasPreviousPage, hasNextPage: page.hasNextPage, totalCount: page.totalCount });
    }).catch(reason => {
      if (active) {
        setInstallationOptions([noInstallation]);
        setInstallationError(reason instanceof Error ? reason.message : 'دریافت نصب‌های مشتری ناموفق بود.');
      }
    }).finally(() => { if (active) setInstallationLoading(false); });
    return () => { active = false; };
  }, [canReadInstallations, customerId, editing, initial?.installationId, installationPage]);

  useEffect(() => {
    if (!editing || !initial?.installationId) {
      setSelectedInstallationOption(null);
      return;
    }
    if (!canReadInstallations) {
      setSelectedInstallationOption({ value: initial.installationId, label: `نصب فعلی (${initial.installationId})` });
      return;
    }
    let active = true;
    fetch(`/api/vendor/installations/${initial.installationId}`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error('نصب مرتبط قابل دریافت نیست.');
      const installation = await response.json() as Installation;
      if (installation.customerId !== initial.customerId) throw new Error('مشتری نصب با لایسنس هم‌خوان نیست.');
      if (active) setSelectedInstallationOption({
        value: installation.id, label: `${installation.installationCode} — ${installation.name} (${installation.status})`,
      });
    }).catch(() => {
      if (active) setSelectedInstallationOption({ value: initial.installationId!, label: `نصب فعلی (${initial.installationId})` });
    });
    return () => { active = false; };
  }, [canReadInstallations, editing, initial?.customerId, initial?.installationId]);

  const customerSelectOptions = selectedCustomerOption &&
    !customerOptions.some(option => option.value === selectedCustomerOption.value)
    ? [selectedCustomerOption, ...customerOptions] : customerOptions;
  const contractSelectOptions = selectedContractOption?.value &&
    !contractOptions.some(option => option.value === selectedContractOption.value)
    ? [selectedContractOption, ...contractOptions] : contractOptions;
  const installationSelectOptions = selectedInstallationOption?.value &&
    !installationOptions.some(option => option.value === selectedInstallationOption.value)
    ? [selectedInstallationOption, ...installationOptions] : installationOptions;

  function changeCustomer(nextCustomerId: string) {
    setCustomerId(nextCustomerId);
    setSelectedCustomerOption(customerSelectOptions.find(option => option.value === nextCustomerId) ?? null);
    setContractId('');
    setSelectedContractOption(null);
    setContractPage(1);
    setContractPages({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
    setInstallationId('');
    setSelectedInstallationOption(null);
    setInstallationPage(1);
    setInstallationPages({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
    setError('');
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !editable) return;
    setBusy(true);
    setError('');
    let saved = false;
    try {
      const number = licenseNumber.trim();
      const product = productCode.trim();
      if (!editing && (!canReadCustomers || !customerId)) {
        throw new Error('برای ثبت لایسنس باید مجوز مشاهده مشتریان و یک مشتری معتبر داشته باشید.');
      }
      const parsedMaxCompanies = maxCompanies.trim() ? Number(maxCompanies) : null;
      const parsedGraceDays = Number(graceDays);
      const allowedSubsystems = new Set<string>(subsystemOptions.map(option => option.code));
      if (!number || number.length > 64 || !product || product.length > 64 ||
          !['term', 'perpetual'].includes(licenseKind) ||
          !isValidDateOnly(validFromUtc) ||
          (licenseKind === 'term' && (!validUntilUtc || !isValidDateOnly(validUntilUtc))) ||
          (licenseKind === 'perpetual' && Boolean(validUntilUtc)) ||
          (validUntilUtc && validUntilUtc < validFromUtc) ||
          (parsedMaxCompanies !== null && (!Number.isInteger(parsedMaxCompanies) || parsedMaxCompanies < 1)) ||
          !Number.isInteger(parsedGraceDays) || parsedGraceDays < 0 || parsedGraceDays > 90 ||
          subsystems.length === 0 || new Set(subsystems).size !== subsystems.length ||
          subsystems.some(code => !allowedSubsystems.has(code))) {
        throw new Error('نوع لایسنس، سقف شرکت، زیرسیستم‌ها یا بازه اعتبار معتبر نیست.');
      }
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin', cache: 'no-store',
      });
      if (!tokenResponse.ok) throw responseError(tokenResponse.status, '');
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const payload = {
        ...(!editing ? { customerId } : {}),
        contractId: canReadContracts ? (contractId || null) : (initial?.contractId ?? null),
        installationId: canReadInstallations ? (installationId || null) : (initial?.installationId ?? null),
        licenseNumber: number,
        productCode: product,
        licenseKind,
        maxCompanies: parsedMaxCompanies,
        subsystems,
        graceDays: parsedGraceDays,
        validFromUtc: asUtcDate(validFromUtc),
        validUntilUtc: validUntilUtc ? asUtcDate(validUntilUtc) : null,
        ...(editing ? { rowVersion: initial?.rowVersion ?? '' } : {}),
      };
      const response = await fetch(editing ? `/api/vendor/licenses/${initial!.id}` : '/api/vendor/licenses/', {
        method: editing ? 'PUT' : 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': tokenBody.requestToken },
        body: JSON.stringify(payload),
      });
      if (!response.ok) {
        let code = '';
        try {
          const body = await response.json() as ApiError;
          code = body.error ?? body.message ?? '';
        } catch {
          // Keep the public error generic when the response is not JSON.
        }
        throw responseError(response.status, code);
      }
      saved = true;
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ذخیره اطلاعات لایسنس ناموفق بود.');
    } finally {
      setBusy(false);
    }
    if (saved) onSaved();
  }

  const currentRelationsCannotBeChanged = editing && ((!canReadContracts && Boolean(initial?.contractId)) || (!canReadInstallations && Boolean(initial?.installationId)));
  return <Card aria-labelledby="license-form-title" dir="rtl">
    <CardHeader className="border-b">
      <CardTitle id="license-form-title">{editing ? 'ویرایش لایسنس پیش‌نویس' : 'ثبت لایسنس جدید'}</CardTitle>
      <CardDescription>
        این فرم فقط metadata لایسنس را ثبت می‌کند و به‌معنی صدور فایل امضاشده یا فعال‌سازی در برنامه مشتری نیست.
        {editing ? ' فقط پیش‌نویس قابل ویرایش است.' : ' لایسنس جدید در وضعیت پیش‌نویس ثبت می‌شود.'}
      </CardDescription>
    </CardHeader>
    <CardContent className="pt-4">
      <form className="space-y-4" onSubmit={submit}>
        {!editable && <p role="alert" className="text-sm text-destructive">فقط لایسنس پیش‌نویس قابل ویرایش است؛ برای تغییر این رکورد از گردش‌کار مجاز استفاده کنید.</p>}
        {editing ? (
          <div className="grid gap-2">
            <span className="text-sm font-semibold">مشتری مرتبط</span>
            <p className="rounded-md border border-input bg-muted/40 px-3 py-2 text-sm" dir="auto">{customerLabel || `شناسه مشتری: ${initial?.customerId}`}</p>
          </div>
        ) : (
          <div className="space-y-2">
            <ManagedSelect name="license-customer" label="مشتری تجاری" options={customerSelectOptions} value={customerId}
              onValueChange={changeCustomer} required disabled={busy || customerLoading || !canReadCustomers || !editable}
              hint={customerLoading ? 'در حال دریافت مشتریان…' : `مجموع مشتریان: ${customerPages.totalCount}`}
              {...(!canReadCustomers ? { error: 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' } : customerError ? { error: customerError } : {})} />
            {canReadCustomers && (customerPages.hasPreviousPage || customerPages.hasNextPage) && (
              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" size="sm" variant="outline" disabled={busy || customerLoading || !customerPages.hasPreviousPage}
                  onClick={() => { setSelectedCustomerOption(customerSelectOptions.find(option => option.value === customerId) ?? null); setCustomerPage(page => Math.max(1, page - 1)); }}>مشتریان قبلی</Button>
                <span className="text-xs text-muted-foreground">صفحه {customerPage}</span>
                <Button type="button" size="sm" variant="outline" disabled={busy || customerLoading || !customerPages.hasNextPage}
                  onClick={() => { setSelectedCustomerOption(customerSelectOptions.find(option => option.value === customerId) ?? null); setCustomerPage(page => page + 1); }}>مشتریان بعدی</Button>
              </div>
            )}
          </div>
        )}

        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor="license-number" className="text-sm font-medium">شماره لایسنس</label>
            <input id="license-number" required maxLength={64} autoComplete="off" dir="ltr"
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={licenseNumber} onChange={event => setLicenseNumber(event.target.value)} disabled={busy || !editable} />
          </div>
          <div className="space-y-2">
            <label htmlFor="license-product" className="text-sm font-medium">کد محصول</label>
            <input id="license-product" required maxLength={64} autoComplete="off" dir="ltr"
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={productCode} onChange={event => setProductCode(event.target.value)} disabled={busy || !editable} />
          </div>
          <div className="space-y-2">
            <label htmlFor="license-kind" className="text-sm font-medium">نوع لایسنس</label>
            <select id="license-kind" value={licenseKind}
              onChange={event => {
                const nextKind = event.target.value;
                setLicenseKind(nextKind);
                if (nextKind === 'perpetual') setValidUntilUtc('');
              }}
              disabled={busy || !editable}
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50">
              <option value="term">مدت‌دار</option>
              <option value="perpetual">دائمی</option>
            </select>
          </div>
          <div className="space-y-2">
            <label htmlFor="license-max-companies" className="text-sm font-medium">حداکثر شرکت (اختیاری)</label>
            <input id="license-max-companies" type="number" min={1} step={1} inputMode="numeric"
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={maxCompanies} onChange={event => setMaxCompanies(event.target.value)} disabled={busy || !editable} />
          </div>
          <div className="space-y-2">
            <label htmlFor="license-grace-days" className="text-sm font-medium">مهلت ارفاق (روز، حداکثر ۹۰)</label>
            <input id="license-grace-days" type="number" min={0} max={90} step={1} inputMode="numeric"
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={graceDays} onChange={event => setGraceDays(event.target.value)} disabled={busy || !editable} />
          </div>
          <fieldset className="space-y-2 rounded-md border border-input p-3 md:col-span-2">
            <legend className="px-1 text-sm font-medium">زیرسیستم‌های خریداری‌شده</legend>
            <p className="text-xs text-muted-foreground">برای جلوگیری از اعطای مجوز ناخواسته، هیچ زیرسیستمی به‌صورت پیش‌فرض انتخاب نشده است.</p>
            <div className="grid gap-3 sm:grid-cols-2">
              {subsystemOptions.map(option => (
                <label key={option.code} className="flex items-center gap-2 text-sm">
                  <input type="checkbox" value={option.code}
                    checked={subsystems.includes(option.code)}
                    onChange={event => setSubsystems(current => event.target.checked
                      ? [...current, option.code]
                      : current.filter(code => code !== option.code))}
                    disabled={busy || !editable} />
                  <span>{option.label}</span>
                </label>
              ))}
            </div>
          </fieldset>
          <div className="space-y-2">
            <label htmlFor="license-valid-from" className="text-sm font-medium">تاریخ شروع اعتبار</label>
            <input id="license-valid-from" type="date" required
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={validFromUtc} onChange={event => setValidFromUtc(event.target.value)} disabled={busy || !editable} />
          </div>
          <div className="space-y-2">
            <label htmlFor="license-valid-until" className="text-sm font-medium">
              {licenseKind === 'term' ? 'تاریخ پایان اعتبار (الزامی)' : 'تاریخ پایان اعتبار (برای لایسنس دائمی استفاده نمی‌شود)'}
            </label>
            <input id="license-valid-until" type="date" min={validFromUtc || undefined}
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={validUntilUtc} onChange={event => setValidUntilUtc(event.target.value)}
              disabled={busy || !editable || licenseKind === 'perpetual'} />
          </div>
          <div className="space-y-2">
            <ManagedSelect name="license-contract" label="قرارداد مرتبط (اختیاری)" options={contractSelectOptions} value={contractId}
              onValueChange={value => { setContractId(value); setSelectedContractOption(contractSelectOptions.find(option => option.value === value) ?? null); }}
              disabled={busy || !editable || contractLoading || !canReadContracts}
              hint={!canReadContracts ? 'مجوز مشاهده قراردادها ندارید؛ رابطه فعلی بدون تغییر حفظ می‌شود.' :
                contractLoading ? 'در حال دریافت قراردادهای این مشتری…' : `قراردادهای مشتری: ${contractPages.totalCount}`}
              {...(contractError ? { error: contractError } : {})} />
            {canReadContracts && (contractPages.hasPreviousPage || contractPages.hasNextPage) && (
              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" size="sm" variant="outline" disabled={busy || contractLoading || !contractPages.hasPreviousPage}
                  onClick={() => { setSelectedContractOption(contractSelectOptions.find(option => option.value === contractId) ?? null); setContractPage(page => Math.max(1, page - 1)); }}>قراردادهای قبلی</Button>
                <span className="text-xs text-muted-foreground">صفحه {contractPage}</span>
                <Button type="button" size="sm" variant="outline" disabled={busy || contractLoading || !contractPages.hasNextPage}
                  onClick={() => { setSelectedContractOption(contractSelectOptions.find(option => option.value === contractId) ?? null); setContractPage(page => page + 1); }}>قراردادهای بعدی</Button>
              </div>
            )}
          </div>
          <div className="space-y-2">
            <ManagedSelect name="license-installation" label="نصب مرتبط (اختیاری)" options={installationSelectOptions} value={installationId}
              onValueChange={value => { setInstallationId(value); setSelectedInstallationOption(installationSelectOptions.find(option => option.value === value) ?? null); }}
              disabled={busy || !editable || installationLoading || !canReadInstallations}
              hint={!canReadInstallations ? 'مجوز مشاهده نصب‌ها ندارید؛ رابطه فعلی بدون تغییر حفظ می‌شود.' :
                installationLoading ? 'در حال دریافت نصب‌های این مشتری…' : `نصب‌های مشتری: ${installationPages.totalCount}`}
              {...(installationError ? { error: installationError } : {})} />
            {canReadInstallations && (installationPages.hasPreviousPage || installationPages.hasNextPage) && (
              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" size="sm" variant="outline" disabled={busy || installationLoading || !installationPages.hasPreviousPage}
                  onClick={() => { setSelectedInstallationOption(installationSelectOptions.find(option => option.value === installationId) ?? null); setInstallationPage(page => Math.max(1, page - 1)); }}>نصب‌های قبلی</Button>
                <span className="text-xs text-muted-foreground">صفحه {installationPage}</span>
                <Button type="button" size="sm" variant="outline" disabled={busy || installationLoading || !installationPages.hasNextPage}
                  onClick={() => { setSelectedInstallationOption(installationSelectOptions.find(option => option.value === installationId) ?? null); setInstallationPage(page => page + 1); }}>نصب‌های بعدی</Button>
              </div>
            )}
          </div>
        </div>

        {currentRelationsCannotBeChanged && <p className="text-xs leading-6 text-muted-foreground">به‌دلیل نبود مجوز مشاهده یکی از فهرست‌های مرتبط، شناسه رابطه موجود در درخواست حفظ می‌شود و قابل تغییر نیست.</p>}
        {error && <p role="alert" className="text-sm leading-6 text-destructive">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={busy || !editable || !licenseNumber.trim() || !productCode.trim() ||
            !isValidDateOnly(validFromUtc) || Boolean(validUntilUtc && (!isValidDateOnly(validUntilUtc) || validUntilUtc < validFromUtc)) ||
            (!editing && (!customerId || !canReadCustomers || customerLoading || Boolean(customerError)))}>
            {busy ? 'در حال ذخیره…' : editing ? 'ذخیره تغییرات' : 'ثبت لایسنس پیش‌نویس'}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel} disabled={busy}>انصراف</Button>
        </div>
      </form>
    </CardContent>
  </Card>;
}
