# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-staging-readiness-evidence

- **Issue:** UNNUMBERED — هم‌راستاسازی مستندات آمادگی Vendor و ثبت شواهد چرخهٔ اول Staging
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-23
- **وضعیت:** PR #786 باز و Ready for review است. روی head `911bb1c81618e881639766a144ed1a9fd508b6a1` هر ۱۵ check با نتیجهٔ `success` تکمیل شدند؛ در بازبینی CodeRabbit یک نکتهٔ معتبر دربارهٔ تضمین خاموش‌بودن Gate در صورت باقی‌ماندن تنظیم قبلی `true` یافت شد. اصلاح Runbook و worksheet ثبت می‌شود؛ CI باید روی head اصلاحی دوباره بررسی شود.

## هدف و معیارهای پذیرش
- [x] وضعیت G2 در چک‌لیست با ادغام PR #771 هم‌راستا شود، بدون ادعای Operational/Production verification.
- [x] توپولوژی مورد توافق برای آزمایش اول ثبت شود: یک Windows Server، دو پردازه/سرویس و دو دیتابیس مستقل.
- [x] برگه‌ای برای ثبت اجرای واقعی و شواهد، بدون مجوزدهی ضمنی به Migration یا Runtime Gate، اضافه شود.
- [x] محدودیت آزمایش یک‌میزبانه و نیاز به آزمون آتی دو میزبان مستقل صریح باشد.
- [x] تغییرات روی آخرین main بازاعمال شوند و محتوای PR #780 حفظ شود.

## خلاصه تغییرات
- وضعیت قدیمی G2 که «در حال تکمیل» و CI نهایی را تأییدنشده معرفی می‌کرد، با وضعیت ادغام PR #771 جایگزین شد.
- تفکیک «پیاده‌سازی و CI»، «پذیرش فهرست توسط مالک» و «شواهد Publish عملیاتی» حفظ شد.
- وابستگی قدیمی Runbook به ادغام PR #781 اصلاح شد؛ endpoint در main موجود است اما اجرای واقعی export هنوز Not Run است.
- توپولوژی Staging اولیه یک‌میزبانه و محدودیت آن در اثبات استقلال میزبان ثبت شد.
- برگهٔ ثبت شواهد چرخهٔ اول Staging اضافه و در Index ثبت شد.

## فایل‌های مهم تغییرکرده
- `docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md` — اصلاح snapshot و وضعیت G2.
- `docs/runbooks/FIRST-STAGING-LICENSE-CYCLE.md` — اصلاح وضعیت PR #781، ثبت توپولوژی مصوب و پیوند برگه شواهد.
- `docs/runbooks/FIRST-STAGING-LICENSE-CYCLE-EVIDENCE.md` — برگهٔ ثبت محیط، پیش‌شرط‌های ایمنی، مراحل Vendor/Customer، آزمون‌های منفی و تأیید نهایی.
- `docs/INDEX.md` — ثبت برگه شواهد و این handoff؛ تغییرات موجود PR #780 حفظ شده‌اند.
- `docs/WORK_LOG.md` — ثبت خلاصهٔ کار؛ رکوردهای PR #780 حفظ شده‌اند.
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-staging-readiness-evidence.md` — این handoff.

## تصمیم‌های فنی
- آزمایش نخست روی یک Windows Server با دو پردازه/سرویس، هویت‌های سرویس و دیتابیس‌های مستقل انجام خواهد شد.
- معماری باید نصب Vendor و Customer روی دو Windows Server مستقل را نیز پشتیبانی کند؛ آزمون فعلی جایگزین آزمون دو میزبان نیست.
- هیچ تغییر کد، Schema، Migration، تنظیمات عملیاتی، کلید یا Gate انجام نشده است.
- Runtime Gate خاموش و `DenyAllTradeModuleEntitlement` بدون تغییر باقی می‌ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بازبینی مستندات مبنا در main | انجام شد | main@490224e69542624b8e75a86fc83fc522157e1b29؛ AGENTS، Index، Checklist، Runbook، DECISIONS و WORK_LOG بازبینی شدند. |
| بازاعمال تغییرات روی آخرین main | انجام شد | شاخهٔ جدید ساخته شد؛ PR #780 در Index و WORK_LOG حفظ شده است. |
| بازبینی diff فایل‌های PR | انجام شد | فایل‌های تغییرکرده و patchهای PR از GitHub بررسی شدند. |
| `git diff --check` | اجرا نشد | محیط Git محلی در دسترس نبود. |
| CI | تکمیل‌شده روی head `911bb1c81618e881639766a144ed1a9fd508b6a1` | هر ۱۵ check با نتیجهٔ `success` تکمیل شدند؛ این نتیجه فقط متعلق به همین SHA است. اصلاح جدید Gate باعث اجرای CI روی head بعدی خواهد شد. CI سبز اجرای عملیاتی Staging نیست. |
| Migration / اجرای DB / Key Ceremony / تست سرویس | اجرا نشد | خارج از دامنه و نیازمند تأیید عملیاتی مالک است. |

## امنیت، داده و سازگاری
- هیچ secret، کلید خصوصی، connection string یا دادهٔ مشتری در مستندات درج نشده است.
- هیچ دیتابیس، سرویس، تنظیمات میزبان یا کلید تغییر نکرده است.
- برگهٔ شواهد صریحاً هشدار می‌دهد که درج اطلاعات محرمانه در مستندات/commit ممنوع است.

## ریسک‌ها و کارهای باقی‌مانده
- CI روی head اصلاحی، بازبینی نهایی diff و بازبینی انسانی نهایی باقی است؛ CodeRabbit یک نکتهٔ معتبر دربارهٔ مقدار مؤثر Gate مطرح کرد.
- تأیید مالک برای فهرست allow-list G2 و ثبت شواهد Publish عملیاتی باقی است.
- اجرای Staging واقعی، Backup/Restore، Migrationهای مجاز، Key Ceremony، ACL، trust pinning و تست Restart انجام نشده‌اند.
- آزمون دو Windows Server مستقل، trust distribution/rotation (#235)، Bootstrap مستقل TenantOwner (D-110)، enforcement شرکت‌محور (#391/#392)، ACK/Applied (#393/#394) و Production Audit sink (#107) جداگانه باقی می‌مانند.
- Runtime Gate باید در پیکربندی مؤثر Customer صریحاً OFF باشد؛ این PR مجوز فعال‌سازی آن یا اجرای عملیات Trade نیست.

## شناسه‌های Git
- **Branch:** `docs/vendor-staging-evidence-main-490224e-2026-11-23`
- **PR قبلی متعارض:** #785 (قدیمی، بر مبنای main قبلی؛ جایگزین آن این PR است)
- **Pull Request جایگزین:** #786
