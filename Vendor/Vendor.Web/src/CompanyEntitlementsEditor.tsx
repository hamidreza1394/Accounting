import { useRef, useState } from 'react';
import { Button } from '@/components/ui/button';

export type FeatureType = 'enabled' | 'max_count' | 'enum';

export type CompanyFeature = {
  code: string;
  type: FeatureType;
  enabled?: boolean | null;
  maxCount?: number | null;
  enumValue?: string | null;
};

/** Shape of a company entitlement inside the API response. */
export type CompanyEntitlementResponse = {
  companyReference: string;
  descriptor: string;
  modules?: string[] | null;
  maxUsers?: number | null;
  features?: CompanyFeature[] | null;
};

/** Shape of a company entitlement inside create/update request bodies. */
export type CompanyEntitlementRequest = {
  companyReference?: string;
  descriptor: string;
  modules: string[];
  maxUsers: number;
  features: CompanyFeature[];
};

export type FeatureDraft = { code: string; type: FeatureType; enabled: boolean; maxCount: string; enumValue: string };
export type CompanyDraft = {
  key: string;
  companyReference?: string;
  descriptor: string;
  modules: string[];
  maxUsers: string;
  features: FeatureDraft[];
};

export const companyModuleOptions = [
  { code: 'finance', label: 'مالی (finance)' },
  { code: 'sales', label: 'فروش (sales)' },
  { code: 'inventory', label: 'انبار (inventory)' },
  { code: 'treasury', label: 'خزانه (treasury)' },
  { code: 'TRADE_IMPORT', label: 'واردات بازرگانی (TRADE_IMPORT)' },
] as const;

/** Mirror of the server feature catalog; the server stays authoritative. The reserved `enum` type is not offered. */
export const featureCatalog = [
  { code: 'finance.analytic_account_levels', module: 'finance', type: 'max_count', label: 'سطوح حساب تحلیلی' },
  { code: 'finance.cost_centers', module: 'finance', type: 'max_count', label: 'مراکز هزینه' },
  { code: 'finance.budgeting', module: 'finance', type: 'enabled', label: 'بودجه‌بندی' },
  { code: 'inventory.warehouses', module: 'inventory', type: 'max_count', label: 'انبارها' },
  { code: 'sales.pos', module: 'sales', type: 'enabled', label: 'صندوق فروش (POS)' },
] as const satisfies ReadonlyArray<{ code: string; module: string; type: FeatureType; label: string }>;

const descriptorMaxLength = 200;
const maxUsersLimit = 1_000_000;
// eslint-disable-next-line no-control-regex
const controlChars = /[\u0000-\u001F\u007F-\u009F]/;

function catalogEntry(code: string) {
  return featureCatalog.find(entry => entry.code === code);
}

function moduleLabel(code: string) {
  return companyModuleOptions.find(option => option.code === code)?.label ?? code;
}

export function companiesFromResponse(
  companies: CompanyEntitlementResponse[], nextKey: () => string,
): CompanyDraft[] {
  return companies.map(company => ({
    key: nextKey(),
    companyReference: company.companyReference,
    descriptor: company.descriptor ?? '',
    modules: Array.isArray(company.modules) ? [...company.modules] : [],
    maxUsers: company.maxUsers == null ? '' : String(company.maxUsers),
    features: (Array.isArray(company.features) ? company.features : []).map(feature => ({
      code: feature.code,
      type: catalogEntry(feature.code)?.type ?? feature.type,
      enabled: feature.enabled === true,
      maxCount: feature.maxCount == null ? '' : String(feature.maxCount),
      enumValue: feature.enumValue ?? '',
    })),
  }));
}

function parsePositiveInteger(value: string): number | null {
  const trimmed = value.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  const parsed = Number(trimmed);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

/** Returns a Persian message for the first problem found, or the request payload. */
export function buildCompanyPayload(
  companies: CompanyDraft[],
): { error: string } | { companies: CompanyEntitlementRequest[] } {
  if (companies.length === 0) return { error: 'برای لایسنس شرکت‌محور حداقل یک شرکت لازم است.' };
  const result: CompanyEntitlementRequest[] = [];
  for (const [index, company] of companies.entries()) {
    const row = `شرکت ${index + 1}`;
    const descriptor = company.descriptor.trim();
    if (!descriptor || descriptor.length > descriptorMaxLength || controlChars.test(descriptor)) {
      return { error: `${row}: شناسه توصیفی الزامی است، حداکثر ${descriptorMaxLength} نویسه و بدون نویسه کنترلی.` };
    }
    const maxUsers = parsePositiveInteger(company.maxUsers);
    if (maxUsers === null || maxUsers < 1 || maxUsers > maxUsersLimit) {
      return { error: `${row}: حداکثر کاربر باید عدد صحیح بین ۱ تا ۱٬۰۰۰٬۰۰۰ باشد.` };
    }
    if (new Set(company.modules).size !== company.modules.length ||
        company.modules.some(code => !companyModuleOptions.some(option => option.code === code))) {
      return { error: `${row}: ماژول‌های انتخاب‌شده معتبر نیست.` };
    }
    if (new Set(company.features.map(feature => feature.code)).size !== company.features.length) {
      return { error: `${row}: قابلیت تکراری مجاز نیست.` };
    }
    const features: CompanyFeature[] = [];
    for (const feature of company.features) {
      const entry = catalogEntry(feature.code);
      if (!entry) return { error: `${row}: قابلیت ${feature.code} در فهرست مجاز نیست.` };
      if (!company.modules.includes(entry.module)) {
        return { error: `${row}: قابلیت ${entry.label} به ماژول ${moduleLabel(entry.module)} نیاز دارد؛ ابتدا آن ماژول را انتخاب کنید.` };
      }
      if (entry.type === 'max_count') {
        const maxCount = parsePositiveInteger(feature.maxCount);
        if (maxCount === null || maxCount < 1) {
          return { error: `${row}: سقف «${entry.label}» باید عدد صحیح مثبت باشد.` };
        }
        features.push({ code: entry.code, type: 'max_count', maxCount });
      } else {
        features.push({ code: entry.code, type: 'enabled', enabled: feature.enabled });
      }
    }
    result.push({
      ...(company.companyReference ? { companyReference: company.companyReference } : {}),
      descriptor,
      modules: company.modules,
      maxUsers,
      features,
    });
  }
  return { companies: result };
}

const inputClass = 'h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50';

export function CompanyEntitlementsReadOnly({ companies }: { companies: CompanyEntitlementResponse[] }) {
  return <section aria-labelledby="license-companies-readonly-title" className="space-y-3 rounded-md border border-input p-3">
    <h3 id="license-companies-readonly-title" className="text-sm font-semibold">شرکت‌های لایسنس (فقط‌خواندنی)</h3>
    <p className="text-xs leading-6 text-muted-foreground">
      شناسه توصیفی فقط برای شناسایی توسط اپراتور است و هیچ مفهوم مالکیتی ندارد. مجوزهای نهایی هر شرکت فقط در محتوای امضاشده نگهداری می‌شود.
    </p>
    <ul className="space-y-2">
      {companies.map(company => <li key={company.companyReference} className="rounded-md border border-input bg-muted/40 p-2 text-sm">
        <div dir="auto">{company.descriptor}</div>
        <div className="text-xs text-muted-foreground">شناسه مرجع (opaque، تولیدشده توسط Vendor):</div>
        <code dir="ltr" className="block select-all break-all text-left text-xs">{company.companyReference}</code>
      </li>)}
    </ul>
  </section>;
}

type EditorProps = {
  companies: CompanyDraft[];
  onChange: (next: CompanyDraft[]) => void;
  disabled: boolean;
  maxCompanies: number | null;
};

export function CompanyEntitlementsEditor({ companies, onChange, disabled, maxCompanies }: EditorProps) {
  const counter = useRef(0);
  const [pendingFeature, setPendingFeature] = useState<Record<string, string>>({});

  function newKey() {
    counter.current += 1;
    return `new-company-${counter.current}`;
  }
  function update(key: string, patch: Partial<CompanyDraft>) {
    onChange(companies.map(company => company.key === key ? { ...company, ...patch } : company));
  }
  function updateFeature(key: string, code: string, patch: Partial<FeatureDraft>) {
    onChange(companies.map(company => company.key === key
      ? { ...company, features: company.features.map(feature => feature.code === code ? { ...feature, ...patch } : feature) }
      : company));
  }
  function addFeature(company: CompanyDraft) {
    const code = pendingFeature[company.key];
    const entry = catalogEntry(code ?? '');
    if (!entry) return;
    update(company.key, {
      features: [...company.features, { code: entry.code, type: entry.type, enabled: true, maxCount: '', enumValue: '' }],
    });
    setPendingFeature(current => ({ ...current, [company.key]: '' }));
  }

  return <section aria-labelledby="license-companies-title" className="space-y-3 rounded-md border border-input p-3 md:col-span-2">
    <h3 id="license-companies-title" className="text-sm font-semibold">شرکت‌های لایسنس</h3>
    <p className="text-xs leading-6 text-muted-foreground">
      شناسه توصیفی فقط برای شناسایی توسط اپراتور است و هیچ مفهوم مالکیتی ندارد. شناسه مرجع پس از ذخیره توسط Vendor تولید می‌شود.
    </p>
    {maxCompanies !== null && companies.length > maxCompanies && (
      <p role="status" className="text-xs leading-6 text-amber-700">
        تعداد شرکت‌ها از «حداکثر شرکت» ({maxCompanies}) بیشتر است؛ سرور این درخواست را رد خواهد کرد.
      </p>
    )}
    {companies.length === 0 && <p className="text-xs text-muted-foreground">هنوز شرکتی اضافه نشده است.</p>}
    {companies.map((company, index) => {
      const id = company.key;
      const available = featureCatalog.filter(entry => !company.features.some(feature => feature.code === entry.code));
      return <fieldset key={company.key} className="space-y-3 rounded-md border border-input bg-muted/20 p-3" disabled={disabled}>
        <legend className="px-1 text-sm font-medium">شرکت {index + 1}</legend>
        {company.companyReference && <div className="space-y-1">
          <span className="text-xs text-muted-foreground">شناسه مرجع (opaque، تولیدشده توسط Vendor)</span>
          <code dir="ltr" className="block select-all break-all text-left text-xs">{company.companyReference}</code>
        </div>}
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor={`${id}-descriptor`} className="text-sm font-medium">شناسه توصیفی شرکت {index + 1}</label>
            <input id={`${id}-descriptor`} maxLength={descriptorMaxLength} autoComplete="off" dir="auto" aria-required="true"
              className={inputClass} value={company.descriptor}
              onChange={event => update(company.key, { descriptor: event.target.value })} />
          </div>
          <div className="space-y-2">
            <label htmlFor={`${id}-max-users`} className="text-sm font-medium">حداکثر کاربر شرکت {index + 1}</label>
            <input id={`${id}-max-users`} type="number" inputMode="numeric" aria-required="true" className={inputClass}
              value={company.maxUsers} onChange={event => update(company.key, { maxUsers: event.target.value })} />
          </div>
        </div>
        <fieldset className="space-y-2">
          <legend className="text-sm font-medium">ماژول‌های شرکت {index + 1}</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {companyModuleOptions.map(option => <label key={option.code} className="flex items-center gap-2 text-sm">
              <input type="checkbox" value={option.code} checked={company.modules.includes(option.code)}
                aria-label={`${option.label} — شرکت ${index + 1}`}
                onChange={event => update(company.key, {
                  modules: event.target.checked ? [...company.modules, option.code] : company.modules.filter(code => code !== option.code),
                })} />
              <span>{option.label}</span>
            </label>)}
          </div>
        </fieldset>
        <div className="space-y-2">
          <span className="text-sm font-medium">قابلیت‌ها</span>
          {company.features.length === 0 && <p className="text-xs text-muted-foreground">قابلیت اضافه‌ای انتخاب نشده است.</p>}
          {company.features.map(feature => {
            const entry = catalogEntry(feature.code);
            const label = entry?.label ?? feature.code;
            return <div key={feature.code} className="flex flex-wrap items-center gap-2 text-sm">
              <span className="min-w-40">{label} <code dir="ltr" className="text-xs text-muted-foreground">{feature.code}</code></span>
              {feature.type === 'max_count' && <input type="number" inputMode="numeric" className={`${inputClass} max-w-40`}
                aria-label={`سقف ${label} — شرکت ${index + 1}`} value={feature.maxCount}
                onChange={event => updateFeature(company.key, feature.code, { maxCount: event.target.value })} />}
              {feature.type === 'enabled' && <label className="flex items-center gap-2">
                <input type="checkbox" checked={feature.enabled}
                  onChange={event => updateFeature(company.key, feature.code, { enabled: event.target.checked })} />
                <span>فعال ({label} — شرکت {index + 1})</span>
              </label>}
              <Button type="button" size="sm" variant="outline"
                aria-label={`حذف قابلیت ${label} — شرکت ${index + 1}`}
                onClick={() => update(company.key, { features: company.features.filter(item => item.code !== feature.code) })}>حذف</Button>
            </div>;
          })}
          {available.length > 0 && <div className="flex flex-wrap items-center gap-2">
            <label htmlFor={`${id}-feature-pick`} className="text-sm">افزودن قابلیت به شرکت {index + 1}</label>
            <select id={`${id}-feature-pick`} className={`${inputClass} max-w-64`} value={pendingFeature[company.key] ?? ''}
              onChange={event => setPendingFeature(current => ({ ...current, [company.key]: event.target.value }))}>
              <option value="">انتخاب قابلیت…</option>
              {available.map(entry => <option key={entry.code} value={entry.code}>{entry.label} ({entry.code})</option>)}
            </select>
            <Button type="button" size="sm" variant="outline" onClick={() => addFeature(company)}
              aria-label={`افزودن قابلیت انتخاب‌شده — شرکت ${index + 1}`}>افزودن</Button>
          </div>}
        </div>
        <Button type="button" size="sm" variant="outline" aria-label={`حذف شرکت ${index + 1}`}
          onClick={() => onChange(companies.filter(item => item.key !== company.key))}>حذف شرکت</Button>
      </fieldset>;
    })}
    <Button type="button" size="sm" variant="outline" disabled={disabled}
      onClick={() => onChange([...companies, { key: newKey(), descriptor: '', modules: [], maxUsers: '1', features: [] }])}>
      افزودن شرکت
    </Button>
  </section>;
}
