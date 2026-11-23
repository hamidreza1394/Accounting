# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-staging-readiness-evidence

- **Issue:** UNNUMBERED — هم‌راستاسازی مستندات آمادگی Vendor و ثبت شواهد چرخهٔ اول Staging
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-23
- **وضعیت:** انجام‌شده در شاخه؛ PR به‌صورت Draft باز است و CI/بازبینی هنوز نهایی نشده است.

## هدف و معیارهای پذیرش
- [x] وضعیت G2 در چک‌لیست با ادغام PR #771 هم‌راستا شود، بدون ادعای Operational/Production verification.
- [x] توپولوژی مورد توافق برای آزمایش اول ثبت شود: یک Windows Server، دو پردازه/سرویس و دو دیتابیس مستقل.
- [x] برگه‌ای برای ثبت اجرای واقعی و شواهد، بدون مجوزدهی ضمنی به Migration یا Runtime Gate، اضافه شود.
- [x] محدودیت آزمایش یک‌میزبانه و نیاز به آزمون آتی دو میزبان مستقل صریح باشد.

## خلاصه تغییرات
- وضعیت قدیمی G2 که «در حال تکمیل» و CI نهایی را تأییدنشده معرفی می‌کرد، با وضعیت ادغام PR #771 اصلاح شد.
- تفکیک «پیاده‌سازی و CI»، «پذیرش فهرست توسط مالک» و «شواهد Publish عملیاتی» حفظ شد.
- برگهٔ ثبت شواهد چرخهٔ اول Staging اضافه شد؛ همهٔ گام‌های اجرایی در حالت Not Run باقی مانده‌اند.
- تصمیم محیط یک‌میزبانه برای آزمایش اولیه و محدودیت آن در اثبات استقلال میزبان ثبت شد.

## فایل‌های مهم تغییرکرده
- `docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md` — اصلاح snapshot و وضعیت G2.
- `docs/runbooks/FIRST-STAGING-LICENSE-CYCLE-EVIDENCE.md` — برگهٔ ثبت محیط، پیش‌شرط‌های ایمنی، مراحل Vendor/Customer، آزمون‌های منفی و تأیید نهایی.
- `docs/INDEX.md` — ثبت برگهٔ شواهد در فهرست مستندات.
- `docs/WORK_LOG.md` — ثبت خلاصهٔ این کار.
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-staging-readiness-evidence.md` — این handoff.

## تصمیم‌های فنی
- آزمایش نخست روی یک Windows Server با دو پردازه/سرویس، هویت‌های سرویس و دیتابیس‌های مستقل انجام خواهد شد.
- معماری باید نصب Vendor و Customer روی دو Windows Server مستقل را نیز پشتیبانی کند؛ آزمون فعلی جایگزین آزمون دو میزبان نیست.
- هیچ تغییر کد، Schema، Migration، تنظیمات عملیاتی، کلید یا Gate انجام نشده است.
- Runtime Gate خاموش و `DenyAllTradeModuleEntitlement` بدون تغییر باقی می‌ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بازبینی مستندات مبنا در main | انجام شد | Runbook چرخهٔ اول، handoff Customer، چک‌لیست آمادگی، DECISIONS و AGENTS/INDEX بررسی شدند. |
| بازبینی diff فایل‌های PR | انجام شد | فقط تغییرات مستنداتی؛ دو فایل اصلی در diff اولیه و handoff/index/log در تکمیل مستندات. |
| `git diff --check` | اجرا نشد | تغییر از طریق GitHub Contents API انجام شده؛ محیط Git محلی در این وظیفه در دسترس نبود. |
| CI | در انتظار | PR #785 Draft است؛ هیچ CI سبزی ادعا نمی‌شود. |
| Migration / اجرای DB / Key Ceremony / تست سرویس | اجرا نشد | عمداً خارج از دامنه و نیازمند تأیید عملیاتی مالک است. |

## امنیت، داده و سازگاری
- هیچ secret، کلید خصوصی، connection string یا دادهٔ مشتری در مستندات درج نشده است.
- هیچ دیتابیس، سرویس، تنظیمات میزبان یا کلید تغییر نکرده است.
- برگهٔ شواهد صریحاً هشدار می‌دهد که درج خروجی حساس در مستندات/commit ممنوع است.

## ریسک‌ها و کارهای باقی‌مانده
- CI و بازبینی PR باید تکمیل شود.
- تأیید مالک برای فهرست allow-list G2 و ثبت شواهد Publish عملیاتی باقی است.
- اجرای Staging واقعی، Backup/Restore، Migrationهای مجاز، Key Ceremony، ACL، trust pinning و تست Restart انجام نشده‌اند.
- آزمون دو Windows Server مستقل، trust distribution/rotation (#235)، Bootstrap مستقل TenantOwner (D-110)، enforcement شرکت‌محور (#391/#392)، ACK/Applied (#393/#394) و Production Audit sink (#107) جداگانه باقی می‌مانند.
- Runtime Gate همچنان OFF است و این PR مجوز فعال‌سازی آن یا اجرای عملیات Trade نیست.

## شناسه‌های Git
- **Branch:** `docs/vendor-staging-readiness-and-evidence-2026-11-23`
- **Commitهای اولیه:** `2e100c4d` و `7437e461` (تکمیل handoff/index/log در ادامهٔ همین PR)
- **Pull Request:** https://github.com/hamidreza1394/Accounting/pull/785
