import { lazy, Suspense, useEffect, useState } from 'react';
import { AlertCircle, ChevronLeft, ChevronRight, LoaderCircle, Pencil, Plus, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import type { CustomerDraft } from './CustomerForm';
import type { InstallationDraft } from './InstallationForm';
import type { ContractDraft } from './ContractForm';
import type { LicenseDraft } from './LicenseForm';

const CustomerForm = lazy(() => import('./CustomerForm').then(module => ({ default: module.CustomerForm })));
const InstallationForm = lazy(() => import('./InstallationForm').then(module => ({ default: module.InstallationForm })));
const ContractForm = lazy(() => import('./ContractForm').then(module => ({ default: module.ContractForm })));
const LicenseForm = lazy(() => import('./LicenseForm').then(module => ({ default: module.LicenseForm })));

export type BusinessSection = 'customers' | 'installations' | 'contracts' | 'licenses';

type PageResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};
type Row = Record<string, unknown>;
type Column = { key: string; label: string; kind?: 'date' | 'status' | 'environment' | 'licenseKind' | 'subsystems' };

const resources: Record<BusinessSection, { title: string; description: string; endpoint: string; columns: Column[] }> = {
  customers: {
    title: 'مشتریان تجاری',
    description: 'فهرست مشتریان ثبت‌شده در سامانه مرکزی فروشنده.',
    endpoint: '/api/vendor/customers/',
    columns: [
      { key: 'customerNumber', label: 'شماره مشتری' },
      { key: 'legalName', label: 'نام حقوقی' },
      { key: 'displayName', label: 'نام نمایشی' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'createdAtUtc', label: 'تاریخ ثبت', kind: 'date' },
    ],
  },
  installations: {
    title: 'نصب‌ها',
    description: 'نصب‌ها و محیط‌های ثبت‌شده برای مشتریان تجاری.',
    endpoint: '/api/vendor/installations/',
    columns: [
      { key: 'installationCode', label: 'کد نصب' },
      { key: 'name', label: 'نام نصب' },
      { key: 'environment', label: 'محیط', kind: 'environment' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'lastSeenAtUtc', label: 'آخرین مشاهده', kind: 'date' },
    ],
  },
  contracts: {
    title: 'قراردادها',
    description: 'قراردادهای تجاری و بازه اعتبار آن‌ها.',
    endpoint: '/api/vendor/contracts/',
    columns: [
      { key: 'contractNumber', label: 'شماره قرارداد' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'startsAtUtc', label: 'شروع اعتبار', kind: 'date' },
      { key: 'endsAtUtc', label: 'پایان اعتبار', kind: 'date' },
      { key: 'currencyCode', label: 'ارز' },
    ],
  },
  licenses: {
    title: 'مجوزها و لایسنس‌ها',
    description: 'مجوزهای ثبت‌شده؛ وضعیت این فهرست به‌معنی صدور یا امضای رمزنگاری‌شده نیست.',
    endpoint: '/api/vendor/licenses/',
    columns: [
      { key: 'licenseNumber', label: 'شماره لایسنس' },
      { key: 'productCode', label: 'محصول' },
      { key: 'licenseKind', label: 'نوع', kind: 'licenseKind' },
      { key: 'subsystems', label: 'زیرسیستم‌های خریداری‌شده', kind: 'subsystems' },
      { key: 'maxCompanies', label: 'سقف شرکت' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'validFromUtc', label: 'شروع اعتبار', kind: 'date' },
      { key: 'validUntilUtc', label: 'پایان اعتبار', kind: 'date' },
    ],
  },
};

const statusLabels: Record<string, string> = {
  Active: 'فعال',
  Draft: 'پیش‌نویس',
  Suspended: 'تعلیق‌شده',
  Expired: 'منقضی',
  Revoked: 'لغوشده',
};
const environmentLabels: Record<string, string> = {
  Production: 'عملیاتی',
  Staging: 'آزمایشی',
  Development: 'توسعه',
};
const licenseKindLabels: Record<string, string> = {
  term: 'مدت‌دار',
  perpetual: 'دائمی',
};
const subsystemLabels: Record<string, string> = {
  finance: 'مالی',
  sales: 'فروش',
  inventory: 'انبار',
  treasury: 'خزانه',
};

function formatValue(value: unknown, kind?: Column['kind']): string {
  if (value === null || value === undefined || value === '') return '—';
  const raw = String(value);
  if (kind === 'status') return statusLabels[raw] ?? raw;
  if (kind === 'environment') return environmentLabels[raw] ?? raw;
  if (kind === 'licenseKind') return licenseKindLabels[raw] ?? raw;
  if (kind === 'subsystems') {
    if (!Array.isArray(value)) return '—';
    const labels = value.filter((code): code is string => typeof code === 'string').map(code => subsystemLabels[code] ?? code);
    return labels.length ? labels.join('، ') : 'بدون زیرسیستم';
  }
  if (kind === 'date') {
    const date = new Date(raw);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(date);
  }
  return raw;
}

function errorFor(status: number): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز مشاهده این بخش را ندارد.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی سامانه رخ داد. شناسه یا جزئیات حساس نمایش داده نمی‌شود.';
  return 'دریافت اطلاعات ناموفق بود. وضعیت درخواست را بررسی و دوباره تلاش کنید.';
}

export function BusinessLists({ section, permissions = [] }: { section: BusinessSection; permissions?: string[] }) {
  const resource = resources[section];
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [data, setData] = useState<PageResponse<Row> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [showCustomerForm, setShowCustomerForm] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<CustomerDraft | null>(null);
  const [showInstallationForm, setShowInstallationForm] = useState(false);
  const [editingInstallation, setEditingInstallation] = useState<InstallationDraft | null>(null);
  const [showContractForm, setShowContractForm] = useState(false);
  const [editingContract, setEditingContract] = useState<ContractDraft | null>(null);
  const [showLicenseForm, setShowLicenseForm] = useState(false);
  const [editingLicense, setEditingLicense] = useState<LicenseDraft | null>(null);
  const [statusBusyId, setStatusBusyId] = useState<string | null>(null);
  const [notice, setNotice] = useState('');
  const canManageCustomers = section === 'customers' && permissions.includes('vendor.customers.manage');
  const canManageInstallations = section === 'installations' && permissions.includes('vendor.installations.manage');
  const canRequestApprovals = permissions.includes('vendor.approvals.request');
  const canManageContracts = section === 'contracts' && permissions.includes('vendor.contracts.manage');
  const canManageLicenses = section === 'licenses' && permissions.includes('vendor.licenses.manage');
  const canReadCustomers = permissions.includes('vendor.customers.read');
  const canReadContracts = permissions.includes('vendor.contracts.read');
  const canReadInstallations = permissions.includes('vendor.installations.read');

  function openCreateCustomer() {
    setEditingCustomer(null);
    setShowCustomerForm(true);
    setNotice('');
  }

  function openEditCustomer(row: Row) {
    if (typeof row.id !== 'string' || typeof row.rowVersion !== 'string') {
      setError('نسخه اطلاعات مشتری موجود نیست؛ فهرست را تازه‌سازی کنید.');
      return;
    }
    setEditingCustomer({
      id: row.id,
      rowVersion: row.rowVersion,
      customerNumber: String(row.customerNumber ?? ''),
      legalName: String(row.legalName ?? ''),
      displayName: String(row.displayName ?? ''),
    });
    setShowCustomerForm(true);
    setNotice('');
  }

  function closeCustomerForm() {
    setShowCustomerForm(false);
    setEditingCustomer(null);
  }

  function customerSaved() {
    setShowCustomerForm(false);
    setEditingCustomer(null);
    setNotice(editingCustomer ? 'اطلاعات مشتری به‌روزرسانی شد.' : 'مشتری تجاری ثبت شد.');
    setPage(1);
    setData(null);
    setLoading(true);
    setReloadKey(current => current + 1);
  }

  function openCreateInstallation() {
    setEditingInstallation(null);
    setShowInstallationForm(true);
    setNotice('');
  }

  function openEditInstallation(row: Row) {
    if (typeof row.id !== 'string' || typeof row.customerId !== 'string' || typeof row.rowVersion !== 'string') {
      setError('شناسه مشتری یا نسخه اطلاعات نصب موجود نیست؛ فهرست را تازه‌سازی کنید.');
      return;
    }
    setEditingInstallation({
      id: row.id,
      customerId: row.customerId,
      rowVersion: row.rowVersion,
      installationCode: String(row.installationCode ?? ''),
      name: String(row.name ?? ''),
      environment: String(row.environment ?? 'Production'),
      status: String(row.status ?? ''),
    });
    setShowInstallationForm(true);
    setNotice('');
  }

  function closeInstallationForm() {
    setShowInstallationForm(false);
    setEditingInstallation(null);
  }

  function installationSaved() {
    setShowInstallationForm(false);
    setEditingInstallation(null);
    setNotice(editingInstallation ? 'اطلاعات نصب به‌روزرسانی شد.' : 'نصب جدید ثبت شد.');
    setPage(1);
    setData(null);
    setLoading(true);
    setReloadKey(current => current + 1);
  }

  function openCreateContract() {
    setEditingContract(null);
    setShowContractForm(true);
    setNotice('');
  }

  function openEditContract(row: Row) {
    if (row.status !== 'Draft') {
      setError('فقط قرارداد پیش‌نویس قابل ویرایش است.');
      return;
    }
    if (typeof row.id !== 'string' || typeof row.customerId !== 'string' || typeof row.rowVersion !== 'string') {
      setError('شناسه مشتری یا نسخه اطلاعات قرارداد موجود نیست؛ فهرست را تازه‌سازی کنید.');
      return;
    }
    setEditingContract({
      id: row.id,
      customerId: row.customerId,
      rowVersion: row.rowVersion,
      contractNumber: String(row.contractNumber ?? ''),
      startsAtUtc: String(row.startsAtUtc ?? ''),
      endsAtUtc: String(row.endsAtUtc ?? ''),
      currencyCode: String(row.currencyCode ?? 'IRR'),
      status: String(row.status ?? ''),
    });
    setShowContractForm(true);
    setNotice('');
  }

  function closeContractForm() {
    setShowContractForm(false);
    setEditingContract(null);
  }

  function contractSaved() {
    setShowContractForm(false);
    setEditingContract(null);
    setNotice(editingContract ? 'قرارداد پیش‌نویس به‌روزرسانی شد.' : 'قرارداد پیش‌نویس ثبت شد.');
    setPage(1);
    setData(null);
    setLoading(true);
    setReloadKey(current => current + 1);
  }

  function openCreateLicense() {
    setEditingLicense(null);
    setShowLicenseForm(true);
    setNotice('');
  }

  function openEditLicense(row: Row) {
    if (row.status !== 'Draft') {
      setError('فقط لایسنس پیش‌نویس قابل ویرایش است.');
      return;
    }
    if (typeof row.id !== 'string' || typeof row.customerId !== 'string' || typeof row.rowVersion !== 'string') {
      setError('شناسه مشتری یا نسخه اطلاعات لایسنس موجود نیست؛ فهرست را تازه‌سازی کنید.');
      return;
    }
    setEditingLicense({
      id: row.id,
      customerId: row.customerId,
      contractId: typeof row.contractId === 'string' ? row.contractId : null,
      installationId: typeof row.installationId === 'string' ? row.installationId : null,
      rowVersion: row.rowVersion,
      licenseNumber: String(row.licenseNumber ?? ''),
      productCode: String(row.productCode ?? ''),
      licenseKind: String(row.licenseKind ?? (row.validUntilUtc ? 'term' : 'perpetual')),
      maxCompanies: typeof row.maxCompanies === 'number' ? row.maxCompanies : null,
      subsystems: Array.isArray(row.subsystems) ? row.subsystems.filter((item): item is string => typeof item === 'string') : [],
      graceDays: typeof row.graceDays === 'number' ? row.graceDays : 30,
      lastIssuedSequence: typeof row.lastIssuedSequence === 'number' ? row.lastIssuedSequence : 0,
      validFromUtc: String(row.validFromUtc ?? ''),
      validUntilUtc: String(row.validUntilUtc ?? ''),
      status: String(row.status ?? ''),
    });
    setShowLicenseForm(true);
    setNotice('');
  }

  function closeLicenseForm() {
    setShowLicenseForm(false);
    setEditingLicense(null);
  }

  function licenseSaved() {
    setShowLicenseForm(false);
    setEditingLicense(null);
    setNotice(editingLicense ? 'اطلاعات لایسنس پیش‌نویس به‌روزرسانی شد.' : 'لایسنس پیش‌نویس ثبت شد.');
    setPage(1);
    setData(null);
    setLoading(true);
    setReloadKey(current => current + 1);
  }

  async function changeInstallationStatus(row: Row) {
    if (statusBusyId) return;
    if (typeof row.id !== 'string' || typeof row.rowVersion !== 'string') {
      setError('نسخه اطلاعات نصب موجود نیست؛ فهرست را تازه‌سازی کنید.');
      return;
    }
    const currentStatus = String(row.status ?? '');
    const nextStatus = currentStatus === 'Active' ? 'Suspended' : currentStatus === 'Suspended' ? 'Active' : '';
    if (!nextStatus) {
      setError('فقط نصب فعال یا تعلیق‌شده را می‌توان تغییر وضعیت داد.');
      return;
    }
    setStatusBusyId(row.id);
    setError('');
    setNotice('');
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', { credentials: 'same-origin', cache: 'no-store' });
      if (!tokenResponse.ok) throw new Error(errorFor(tokenResponse.status));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');
      const response = await fetch('/api/vendor/approvals/status-change', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': tokenBody.requestToken },
        body: JSON.stringify({ resourceType: 'installation', resourceId: row.id, targetStatus: nextStatus, rowVersion: row.rowVersion }),
      });
      if (!response.ok) {
        let code = '';
        try {
          const body = await response.json() as { error?: string; message?: string };
          code = body.error ?? body.message ?? '';
        } catch {
          // Keep the public error generic when the response is not JSON.
        }
        if (response.status === 409 && code === 'stale_row_version') throw new Error('اطلاعات نصب تغییر کرده است؛ فهرست را تازه‌سازی کنید.');
        if (response.status === 409 && code === 'invalid_status_transition') throw new Error('این تغییر وضعیت مجاز نیست.');
        if (response.status === 409 && code === 'approval_already_pending') throw new Error('برای این نصب درخواست تأیید در انتظار وجود دارد.');
        if (response.status === 403) throw new Error('حساب شما مجوز تغییر وضعیت نصب را ندارد.');
        throw new Error(errorFor(response.status));
      }
      setNotice('درخواست تغییر وضعیت نصب برای تأیید مدیر دیگر ثبت شد.');
      setData(null);
      setLoading(true);
      setReloadKey(current => current + 1);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'تغییر وضعیت نصب ناموفق بود.');
    } finally {
      setStatusBusyId(null);
    }
  }

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    fetch(`${resource.endpoint}?page=${page}&pageSize=${pageSize}`, {
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error(errorFor(response.status));
      const result = await response.json() as PageResponse<Row>;
      if (!Array.isArray(result.items) || typeof result.totalCount !== 'number' ||
          typeof result.page !== 'number' || typeof result.pageSize !== 'number' ||
          typeof result.totalPages !== 'number' || typeof result.hasNextPage !== 'boolean' ||
          typeof result.hasPreviousPage !== 'boolean') {
        throw new Error('پاسخ سرور با قرارداد صفحه‌بندی مورد انتظار سازگار نیست.');
      }
      if (active) setData(result);
    }).catch(reason => {
      if (active) {
        setData(null);
        setError(reason instanceof Error ? reason.message : 'دریافت اطلاعات ناموفق بود.');
      }
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [resource.endpoint, page, pageSize, reloadKey]);

  function changePageSize(value: string) {
    const next = Number(value);
    if ([25, 50, 100].includes(next)) {
      setPageSize(next);
      setPage(1);
    }
  }

  return <section className="space-y-5" aria-labelledby="business-page-title" dir="rtl">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <span className="eyebrow">مدیریت تجاری</span>
        <h1 id="business-page-title" className="mt-1 text-2xl font-bold text-[#193b57]">{resource.title}</h1>
        <p className="mt-2 text-sm leading-7 text-muted-foreground">{resource.description}</p>
      </div>
      <div className="flex flex-wrap gap-2">
        {canManageCustomers && <Button type="button" onClick={openCreateCustomer}><Plus size={16}/> ثبت مشتری</Button>}
        {canManageInstallations && <Button type="button" onClick={openCreateInstallation} disabled={!canReadCustomers} {...(!canReadCustomers ? { title: 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' } : {})}><Plus size={16}/> ثبت نصب</Button>}
        {canManageContracts && <Button type="button" onClick={openCreateContract} disabled={!canReadCustomers} {...(!canReadCustomers ? { title: 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' } : {})}><Plus size={16}/> ثبت قرارداد</Button>}
        {canManageLicenses && <Button type="button" onClick={openCreateLicense} disabled={!canReadCustomers} {...(!canReadCustomers ? { title: 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' } : {})}><Plus size={16}/> ثبت لایسنس</Button>}
        <Button type="button" variant="outline" onClick={() => { setLoading(true); setError(''); setData(null); setReloadKey(current => current + 1); }} disabled={loading}>
          <RefreshCw size={16}/> تازه‌سازی
        </Button>
      </div>
    </div>

    {notice && <p role="status" className="text-sm text-muted-foreground">{notice}</p>}
    <Suspense fallback={<p role="status" className="py-3 text-sm text-muted-foreground">در حال بارگذاری فرم…</p>}>
      {showCustomerForm && canManageCustomers && <CustomerForm {...(editingCustomer ? { initial: editingCustomer } : {})} onCancel={closeCustomerForm} onSaved={customerSaved} />}
      {showInstallationForm && canManageInstallations && <InstallationForm {...(editingInstallation ? { initial: editingInstallation } : {})} canReadCustomers={canReadCustomers} onCancel={closeInstallationForm} onSaved={installationSaved} />}
      {showContractForm && canManageContracts && <ContractForm {...(editingContract ? { initial: editingContract } : {})} canReadCustomers={canReadCustomers} onCancel={closeContractForm} onSaved={contractSaved} />}
      {showLicenseForm && canManageLicenses && <LicenseForm {...(editingLicense ? { initial: editingLicense } : {})}
        canReadCustomers={canReadCustomers} canReadContracts={canReadContracts} canReadInstallations={canReadInstallations}
        onCancel={closeLicenseForm} onSaved={licenseSaved} />}
    </Suspense>

    <Card>
      <CardHeader className="border-b">
        <CardTitle>{resource.title}</CardTitle>
        <CardDescription>اطلاعات از API سامانه خوانده می‌شود؛ دادهٔ نمونه یا ساختگی نمایش داده نمی‌شود.</CardDescription>
      </CardHeader>
      <CardContent className="pt-4">
        {loading ? <div className="flex min-h-36 items-center justify-center gap-3 text-sm text-muted-foreground" role="status"><LoaderCircle className="animate-spin" size={20}/> در حال دریافت اطلاعات…</div>
        : error ? <div className="flex min-h-36 flex-col items-center justify-center gap-3 text-center" role="alert"><AlertCircle className="text-destructive" size={26}/><p className="max-w-xl text-sm leading-7 text-destructive">{error}</p><Button type="button" variant="outline" onClick={() => { setError(''); setLoading(true); setReloadKey(current => current + 1); }}>تلاش دوباره</Button></div>
        : !data || data.items.length === 0 ? <div className="flex min-h-36 flex-col items-center justify-center gap-2 text-center"><span className="text-sm font-semibold">رکوردی برای نمایش وجود ندارد</span><p className="text-sm text-muted-foreground">پس از ثبت اطلاعات، موارد در این فهرست نمایش داده می‌شوند.</p></div>
        : <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] border-separate border-spacing-0 text-right text-sm">
            <thead><tr>{resource.columns.map(column => <th key={column.key} scope="col" className="border-b border-border bg-muted/60 px-4 py-3 font-semibold text-muted-foreground">{column.label}</th>)}{(canManageCustomers || canManageInstallations || canManageContracts || canManageLicenses) && <th scope="col" className="border-b border-border bg-muted/60 px-4 py-3 font-semibold text-muted-foreground">عملیات</th>}</tr></thead>
            <tbody>{data.items.map((row, index) => <tr key={String(row.id ?? row.customerNumber ?? row.installationCode ?? row.contractNumber ?? row.licenseNumber ?? index)} className="hover:bg-muted/30">
              {resource.columns.map(column => <td key={column.key} className="border-b border-border px-4 py-3 text-foreground">{formatValue(row[column.key], column.kind)}</td>)}
              {(canManageCustomers || canManageInstallations || canManageContracts || canManageLicenses) && <td className="border-b border-border px-4 py-3"><div className="flex flex-wrap gap-2">
                {canManageCustomers && <Button type="button" variant="outline" size="sm" onClick={() => openEditCustomer(row)} aria-label={`ویرایش مشتری ${String(row.displayName ?? row.customerNumber ?? '')}`}><Pencil size={15}/> ویرایش</Button>}
                {canManageInstallations && <Button type="button" variant="outline" size="sm" onClick={() => openEditInstallation(row)} aria-label={`ویرایش نصب ${String(row.name ?? row.installationCode ?? '')}`}><Pencil size={15}/> ویرایش</Button>}
                {canManageInstallations && canRequestApprovals && (row.status === 'Active' || row.status === 'Suspended') && <Button type="button" variant="outline" size="sm" disabled={statusBusyId === row.id || Boolean(statusBusyId)} onClick={() => changeInstallationStatus(row)} aria-label={row.status === 'Active' ? `تعلیق نصب ${String(row.name ?? '')}` : `فعال‌سازی نصب ${String(row.name ?? '')}`}>{statusBusyId === row.id ? 'در حال ثبت…' : row.status === 'Active' ? 'تعلیق' : 'فعال‌سازی'}</Button>}
                {canManageContracts && row.status === 'Draft' && <Button type="button" variant="outline" size="sm" onClick={() => openEditContract(row)} aria-label={`ویرایش قرارداد ${String(row.contractNumber ?? '')}`}><Pencil size={15}/> ویرایش</Button>}
                {canManageLicenses && row.status === 'Draft' && <Button type="button" variant="outline" size="sm" onClick={() => openEditLicense(row)} aria-label={`ویرایش لایسنس ${String(row.licenseNumber ?? '')}`}><Pencil size={15}/> ویرایش</Button>}
              </div></td>}
            </tr>)}</tbody>
          </table>
        </div>}
        {!loading && !error && data && <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-border pt-4">
          <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
            <span>تعداد کل: <b className="text-foreground">{new Intl.NumberFormat('fa-IR').format(data.totalCount)}</b></span>
            <label className="flex items-center gap-2">تعداد در صفحه
              <select aria-label="تعداد در صفحه" className="h-9 rounded-md border border-input bg-background px-2 text-foreground" value={pageSize} onChange={event => changePageSize(event.target.value)}>
                <option value={25}>۲۵</option><option value={50}>۵۰</option><option value={100}>۱۰۰</option>
              </select>
            </label>
            <span>صفحه {data.totalPages === 0 ? '—' : new Intl.NumberFormat('fa-IR').format(data.page)} از {data.totalPages === 0 ? '—' : new Intl.NumberFormat('fa-IR').format(data.totalPages)}</span>
          </div>
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" size="sm" disabled={!data.hasPreviousPage || loading} onClick={() => setPage(current => Math.max(1, current - 1))}><ChevronRight size={16}/> قبلی</Button>
            <Button type="button" variant="outline" size="sm" disabled={!data.hasNextPage || loading} onClick={() => setPage(current => current + 1)}>بعدی <ChevronLeft size={16}/></Button>
          </div>
        </div>}
      </CardContent>
    </Card>
  </section>;
}
