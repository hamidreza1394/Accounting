/**
 * The shared Persian UI texts of the workspace stage (Issue #444 stage 1 completion).
 * A deliberately small module — not an i18n framework (UI-02: the library decision stays
 * `NeedsApproval`); new texts of this stage live here so the dialogs and the tab markers
 * stay consistent and no English leaks into the UI.
 */

export const uiTexts = {
  // Lifecycle statuses (also in formGuard.formStatusLabel — kept there by design).
  unsavedChanges: 'تغییرات ذخیره‌نشده',
  saving: 'ذخیره در حال انجام است',
  saveFailed: 'ذخیره ناموفق بود',
  saveOutcomeUnknown: 'نتیجهٔ ذخیره نامشخص است؛ پیش از ادامه، وضعیت رکورد را بررسی کنید',
  savingHint: 'ذخیره در حال انجام است؛ تا پایان عملیات امکان انصراف نیست.',

  // Exit / discard
  exitDialogTitle: 'لغو تغییرات و بازگشت',
  exitDialogMessage: 'اطلاعات واردشده هنوز ذخیره نشده‌اند. آیا مایلید بدون ذخیره به صفحهٔ قبلی بازگردید؟',
  exitContinue: 'ادامهٔ ویرایش',
  exitDiscard: 'لغو تغییرات و بازگشت',

  // Tab close guard
  closeSavingTitle: 'بستن این تب در حال حاضر ممکن نیست',
  closeSavingMessage: (label: string) => `ذخیرهٔ «${label}» در حال انجام است. تا پایان عملیات منتظر بمانید؛ پس از ذخیرهٔ موفق می‌توانید تب را ببندید.`,
  closeErrorTitle: 'بستن این تب تا رفع خطا ممکن نیست',
  closeErrorMessage: (label: string) => `«${label}» خطا در ذخیره دارد. ابتدا خطا را برطرف کنید، تغییرات را ذخیره یا آگاهانه لغو کنید؛ سپس تب را ببندید.`,

  // Context change guard
  contextBlockedTitle: 'تغییر زمینهٔ کاری ممکن نیست',
  contextBlockedDescription: 'پیش از تغییر شرکت یا دورهٔ مالی، همهٔ فرم‌های زیر باید ذخیره شوند یا آگاهانه لغو شوند. با انتخاب نام هر تب به همان تب بروید.',
  contextConfirmTitle: 'تأیید نهایی تغییر شرکت یا دورهٔ مالی',
  contextConfirmDescription: 'با تأیید، به صفحهٔ انتخاب زمینه می‌روید. پس از انتخاب زمینهٔ جدید، تب‌های وابسته به شرکت و دورهٔ فعلی بسته و داده‌های آن‌ها بی‌اعتبار می‌شوند؛ تب‌های عمومی باز می‌مانند.',
  contextConfirmAction: 'ادامه و انتخاب زمینهٔ جدید',
  goToTab: (label: string) => `رفتن به تب ${label}`,

  // Generic
  cancel: 'انصراف',
  acknowledge: 'متوجه شدم',
  networkError: 'خطای شبکه؛ اتصال خود را بررسی کنید و دوباره تلاش کنید.',
  accessDenied: 'شما مجوز دسترسی به این بخش را ندارید.',
} as const;
