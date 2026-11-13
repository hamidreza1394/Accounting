import { ApiError } from '@/api/client';
import { TradeContractError } from '../api/tradeTypes';
import type { PartyFieldKey } from './partyValidation';
import type { ProductFieldKey } from './productValidation';
import { tradeTexts } from './tradeLabels';

/**
 * What a failed Trade request means for the page. The 409s differ only by the machine code in the
 * ProblemDetails `detail` (exposed as `ApiError.problemCode`), so each one gets its own Persian
 * message here; the raw server text is never shown. Nothing here ever includes a submitted value.
 */
export type TradeFailureKind =
  | 'validation'
  | 'duplicate-code'
  | 'concurrency'
  | 'period-read-only'
  | 'context-required'
  | 'module-unavailable'
  | 'forbidden'
  | 'too-large'
  | 'invalid-body'
  | 'not-found'
  | 'network'
  | 'generic';

export type TradeFailure<K extends string = PartyFieldKey> = {
  kind: TradeFailureKind;
  message: string;
  /** Messages for the fields of the form (400 validation, and the duplicate code on `code`). */
  fieldErrors: Partial<Record<K, string>>;
  status: number | null;
};

/** What differs between the Trade forms: the form's fields and the texts/codes that name the record. */
type FailureSubject<K extends string> = {
  fieldKeys: readonly K[];
  duplicateProblemCode: string;
  duplicateMessage: string;
  concurrencyMessage: string;
  notFoundMessage: string;
};

const partyFieldKeys: readonly PartyFieldKey[] = ['code', 'legalName', 'displayName', 'legalIdentifier', 'countryCode', 'roles'];

export const tradeMessages = {
  duplicateCode: 'این کد پیش‌تر برای طرف‌حساب دیگری ثبت شده است. کد دیگری وارد کنید.',
  concurrency:
    'این طرف‌حساب در فاصلهٔ باز بودن فرم توسط کاربر دیگری تغییر کرده است. اطلاعات تازه را بارگذاری کنید و تغییرات را دوباره اعمال کنید.',
  periodReadOnly: tradeTexts.readOnlyBody,
  contextRequired: tradeTexts.contextRequiredBody,
  moduleUnavailable: tradeTexts.moduleUnavailableTitle,
  forbidden: 'شما مجوز انجام این عملیات را ندارید.',
  notFound: 'طرف‌حساب پیدا نشد یا به آن دسترسی ندارید.',
  tooLarge: 'حجم اطلاعات ارسالی بیش از حد مجاز است. مقادیر را کوتاه‌تر کنید و دوباره تلاش کنید.',
  invalidBody: 'درخواست قابل پردازش نبود. صفحه را بررسی کنید و دوباره تلاش کنید.',
  validation: 'برخی فیلدها نامعتبرند. خطاها را بررسی و اصلاح کنید.',
  generic: 'عملیات انجام نشد. لطفاً دوباره تلاش کنید.',
} as const;

export const productMessages = {
  duplicateCode: 'این کد پیش‌تر برای کالای دیگری ثبت شده است. کد دیگری وارد کنید.',
  concurrency:
    'این کالا در فاصلهٔ باز بودن فرم توسط کاربر دیگری تغییر کرده است. اطلاعات تازه را بارگذاری کنید و تغییرات را دوباره اعمال کنید.',
  notFound: 'کالا پیدا نشد یا به آن دسترسی ندارید.',
} as const;

const partySubject: FailureSubject<PartyFieldKey> = {
  fieldKeys: partyFieldKeys,
  duplicateProblemCode: 'trade_party_code_duplicate',
  duplicateMessage: tradeMessages.duplicateCode,
  concurrencyMessage: tradeMessages.concurrency,
  notFoundMessage: tradeMessages.notFound,
};

const productSubject: FailureSubject<ProductFieldKey> = {
  fieldKeys: ['code', 'name', 'productGroupCode', 'hsCode', 'baseUnitCode'],
  duplicateProblemCode: 'trade_product_code_duplicate',
  duplicateMessage: productMessages.duplicateCode,
  concurrencyMessage: productMessages.concurrency,
  notFoundMessage: productMessages.notFound,
};

function pickFieldErrors<K extends string>(error: ApiError, fieldKeys: readonly K[]): Partial<Record<K, string>> {
  const result: Partial<Record<K, string>> = {};
  for (const key of fieldKeys) {
    const first = error.fieldErrors?.[key]?.[0];
    if (first) result[key] = first;
  }
  return result;
}

export function mapTradeFailure(cause: unknown): TradeFailure {
  return mapFailure(cause, partySubject);
}

export function mapProductFailure(cause: unknown): TradeFailure<ProductFieldKey> {
  return mapFailure(cause, productSubject);
}

function mapFailure<K extends string>(cause: unknown, subject: FailureSubject<K>): TradeFailure<K> {
  if (cause instanceof ApiError) {
    const base = { fieldErrors: {} as Partial<Record<K, string>>, status: cause.status };
    switch (cause.status) {
      case 413:
        return { kind: 'too-large', message: tradeMessages.tooLarge, ...base };
      case 400: {
        if (cause.problemCode === 'trade_invalid_request_body') return { kind: 'invalid-body', message: tradeMessages.invalidBody, ...base };
        const fieldErrors = pickFieldErrors(cause, subject.fieldKeys);
        // A 400 without a known field is a malformed request or an invalid anti-forgery token: show the server's text.
        return Object.keys(fieldErrors).length > 0
          ? { kind: 'validation', message: tradeMessages.validation, fieldErrors, status: 400 }
          : { kind: 'generic', message: cause.message, ...base };
      }
      case 403:
        return cause.problemCode === 'trade_module_unavailable'
          ? { kind: 'module-unavailable', message: tradeMessages.moduleUnavailable, ...base }
          : { kind: 'forbidden', message: tradeMessages.forbidden, ...base };
      case 404:
        return { kind: 'not-found', message: subject.notFoundMessage, ...base };
      case 409:
        switch (cause.problemCode) {
          case subject.duplicateProblemCode:
            return {
              kind: 'duplicate-code',
              message: subject.duplicateMessage,
              fieldErrors: { code: subject.duplicateMessage } as Partial<Record<K, string>>,
              status: 409,
            };
          case 'trade_concurrency_conflict':
            return { kind: 'concurrency', message: subject.concurrencyMessage, ...base };
          // Defensive: Party writes are allowed in a closed period now, but another gate may still answer this.
          case 'trade_period_read_only':
            return { kind: 'period-read-only', message: tradeMessages.periodReadOnly, ...base };
          case 'trade_context_required':
            return { kind: 'context-required', message: tradeMessages.contextRequired, ...base };
          default:
            return { kind: 'generic', message: tradeMessages.generic, ...base };
        }
      default:
        return { kind: 'generic', message: cause.message, ...base };
    }
  }
  if (cause instanceof TradeContractError) {
    return { kind: 'generic', message: 'پاسخ سرور قابل‌فهم نبود. لطفاً دوباره تلاش کنید.', fieldErrors: {}, status: null };
  }
  return { kind: 'network', message: tradeTexts.networkError, fieldErrors: {}, status: null };
}

/** The kinds that mean "the module is not usable here at all" (a friendly state instead of an error box). */
export function isModuleBlocked(failure: TradeFailure<string> | null): boolean {
  return failure?.kind === 'module-unavailable' || failure?.kind === 'context-required';
}
