import type { ProductDetail, ProductWriteBody } from '../api/tradeTypes';
import { validateText } from './partyValidation';

// Client-side mirror of the server rules of `ProductService` (code <= 64, name <= 200, product group code
// <= 64, HS code <= 16 printable ASCII characters kept as TEXT, a base unit of the reference list). The server
// stays authoritative; this only saves a round trip. Messages never echo the value.

export const productLimits = { code: 64, name: 200, productGroupCode: 64, hsCode: 16 } as const;

export type ProductFormState = {
  code: string;
  name: string;
  productGroupCode: string;
  hsCode: string;
  baseUnitCode: string | null;
};

export const emptyProductForm: ProductFormState = { code: '', name: '', productGroupCode: '', hsCode: '', baseUnitCode: null };

export type ProductFieldKey = 'code' | 'name' | 'productGroupCode' | 'hsCode' | 'baseUnitCode';
export type ProductFieldErrors = Partial<Record<ProductFieldKey, string>>;

/** The HS code column is non-Unicode on the server: only printable ASCII (space to tilde) is accepted. */
function hasNonPrintableAscii(value: string): boolean {
  for (const character of value) {
    const code = character.codePointAt(0) ?? 0;
    if (code < 0x20 || code > 0x7e) return true;
  }
  return false;
}

function validateHsCode(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return undefined;
  if (hasNonPrintableAscii(trimmed)) return 'کد تعرفه فقط می‌تواند شامل نویسه‌های لاتین قابل‌چاپ باشد.';
  if (trimmed.length > productLimits.hsCode) return `کد تعرفه نباید بیش از ${productLimits.hsCode.toLocaleString('fa-IR')} نویسه باشد.`;
  return undefined;
}

/** `knownUnitCodes` is the loaded reference list; when it is not loaded yet the server decides the validity. */
export function validateProductForm(form: ProductFormState, knownUnitCodes: ReadonlySet<string> | null): ProductFieldErrors {
  const errors: ProductFieldErrors = {};
  const code = validateText(form.code, 'کد', productLimits.code, true);
  if (code) errors.code = code;
  const name = validateText(form.name, 'نام', productLimits.name, true);
  if (name) errors.name = name;
  const group = validateText(form.productGroupCode, 'کد گروه کالا', productLimits.productGroupCode, false);
  if (group) errors.productGroupCode = group;
  const hs = validateHsCode(form.hsCode);
  if (hs) errors.hsCode = hs;
  if (form.baseUnitCode === null || form.baseUnitCode.trim() === '') errors.baseUnitCode = 'واحد پایه الزامی است.';
  else if (knownUnitCodes !== null && !knownUnitCodes.has(form.baseUnitCode)) errors.baseUnitCode = 'واحد پایه نامعتبر است.';
  return errors;
}

const optional = (value: string): string | null => {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
};

/** The request body, built field by field and trimmed like the server does. Call it only after validation. */
export function buildProductBody(form: ProductFormState): ProductWriteBody {
  return {
    code: form.code.trim(),
    name: form.name.trim(),
    productGroupCode: optional(form.productGroupCode),
    hsCode: optional(form.hsCode),
    baseUnitCode: form.baseUnitCode ?? '',
  };
}

export function productFormFromDetail(product: ProductDetail): ProductFormState {
  return {
    code: product.code,
    name: product.name,
    productGroupCode: product.productGroupCode ?? '',
    hsCode: product.hsCode ?? '',
    baseUnitCode: product.baseUnitCode,
  };
}

/** True when the form differs from the baseline after normalization (trim, optional text). */
export function isProductFormDirty(current: ProductFormState, baseline: ProductFormState): boolean {
  return JSON.stringify(buildProductBody(current)) !== JSON.stringify(buildProductBody(baseline));
}
