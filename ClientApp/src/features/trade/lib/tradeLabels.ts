import type { CountryRef, PartyRole } from '../api/tradeTypes';
import type { ComboboxOption } from '@/components/ui/SearchableCombobox';

// Persian UI texts of the Trade Party slice. The API sends only the enum names; the labels live
// here (UI-02: no i18n library yet, so the feature keeps its texts in one neighbouring module).

export const partyRoleLabel: Record<PartyRole, string> = {
  Importer: 'واردکننده',
  Seller: 'فروشنده',
  FxBroker: 'کارگزار ارز',
  Carrier: 'حمل‌کننده',
  Bank: 'بانک',
  Beneficiary: 'ذی‌نفع',
  Inspector: 'بازرس',
  VesselAgent: 'نمایندهٔ کشتیرانی',
  LivestockSupport: 'پشتیبان دام',
};

export function roleLabel(role: string): string {
  return (partyRoleLabel as Record<string, string>)[role] ?? role;
}

/** Display name of a party: the display name when there is one, otherwise the legal name. */
export function partyTitle(party: { legalName: string; displayName: string | null }): string {
  return party.displayName && party.displayName.trim() !== '' ? party.displayName : party.legalName;
}

/** Combobox options of the country list, sorted by Persian name (the server orders by ISO code). */
export function countryOptions(countries: readonly CountryRef[]): ComboboxOption[] {
  return [...countries]
    .sort((left, right) => left.nameFa.localeCompare(right.nameFa, 'fa'))
    .map((country) => ({ value: country.code, code: country.code, label: country.nameFa, description: country.nameEn }));
}

export const tradeTexts = {
  moduleUnavailableTitle: 'ماژول بازرگانی در دسترس نیست',
  moduleUnavailableBody:
    'ماژول بازرگانی برای شرکت جاری فعال نشده است. برای فعال‌سازی با مدیر سیستم یا پشتیبانی فرانو تماس بگیرید.',
  contextRequiredBody: 'شرکت یا دورهٔ مالی کاری معتبر نیست. زمینهٔ کاری را دوباره انتخاب کنید.',
  forbiddenTitle: 'دسترسی ندارید',
  forbiddenBody: 'شما مجوز این بخش از ماژول بازرگانی را ندارید. برای دریافت مجوز با مدیر شرکت تماس بگیرید.',
  readOnlyBody: 'دورهٔ مالی جاری فقط‌خواندنی است؛ ثبت و ویرایش طرف‌حساب ممکن نیست.',
  capabilitiesError: 'دریافت دسترسی‌های ماژول بازرگانی انجام نشد؛ تا آن زمان دسترسی به این صفحه داده نمی‌شود.',
  loadingAccess: 'در حال بررسی دسترسی به ماژول بازرگانی…',
  networkError: 'ارتباط با سرور برقرار نشد. اتصال خود را بررسی کنید و دوباره تلاش کنید.',
} as const;
