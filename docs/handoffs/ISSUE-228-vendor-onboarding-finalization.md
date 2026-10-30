# تحویل وظیفه — ISSUE-228-vendor-onboarding-finalization

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT / GitHub integration
- **تاریخ:** 2026-10-30
- **وضعیت:** اصلاح داشبورد در PR #354 ادغام و CI آن سبز شد؛ Issue #228 به‌دلیل موانع مستقل Production باز می‌ماند.

## هدف و معیارهای پذیرش
- [x] متن داشبورد با قابلیت‌های واقعی Vendor در main هم‌تراز شود.
- [x] Provisioning نسخه ۴، گردش maker-checker، export بسته و intake پاسخ امضاشده از قابلیت‌های موجود معرفی شوند.
- [x] نبود HSM واقعی و fail-closed بودن امضای Production به‌صورت شفاف نمایش داده شود.
- [x] متن قدیمی «نسخه اولیه رابط» و ادعای نادرست «عملیات نوشتن در مراحل بعدی تکمیل می‌شود» حذف شود.
- [ ] تأمین HSM واقعی برای امضای Production؛ با بودجه فعلی صفر، Blocked.
- [ ] checkpoint مستقل Audit با failure-domain و credential جدا؛ Issue #107.
- [ ] revocation آفلاین end-to-end و anti-rollback؛ Issue #214.
- [ ] آزمون میدانی TPM و deployment/backup/restore؛ نیازمند میزبان واقعی.

## خلاصه تغییرات
- متن Overview از وضعیت قدیمی «نسخه اولیه/در حال توسعه» به وضعیت واقعی قابلیت‌های پیاده‌سازی‌شده تغییر کرد.
- توضیح شد که Provisioning v4، تأیید دو نفره، export بسته و دریافت ACK امضاشده در کد وجود دارند.
- محدودیت امضای بسته و لایسنس در Production تا تهیه HSM واقعی صریح شد؛ هیچ ادعای Production-ready اضافه نشد.
- آزمون رگرسیون به App.test.tsx اضافه شد تا متن دقیق وضعیت و نبود برچسب قدیمی را بررسی کند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Web/src/App.tsx` — متن وضعیت و جریان‌های اصلی داشبورد.
- `Vendor/Vendor.Web/src/App.test.tsx` — آزمون متن وضعیت و جلوگیری از regression.
  
## تصمیم‌های فنی
- هیچ تصمیم معماری جدیدی گرفته نشد.
- رمز اولیه مشتری در Vendor جمع‌آوری یا ذخیره نمی‌شود؛ رمز توسط مدیر مشتری در مسیر Enrollment امن تعیین می‌شود. انتقال رمز خام یا hash مشتری به Vendor خلاف مرز امنیتی #228/#235 است.
- بدون HSM واقعی، signer پیش‌فرض unavailable/fail-closed باقی می‌ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor CI | موفق | run `41095191395`؛ typecheck، Vitest، build، SQL Server integration، EF snapshot guard و publish |
| .NET CI | موفق | run `41095191238`؛ build/test، migration guards، E2E، publish Linux و Windows و Windows CNG tests |
| Semgrep | موفق | run `41095191239` |
| CodeQL | موفق | run `41095184027`؛ C#, JavaScript/TypeScript, Python و Actions |
| اجرای محلی | اجرا نشد | محیط فعلی GitHub-only است |

## امنیت، داده و سازگاری
- هیچ endpoint، schema، migration، secret، private key یا customer DB تغییر نکرد.
- هیچ credential اولیه یا رمز قابل استفاده مجدد در Vendor ذخیره نمی‌شود.
- UI وضعیت امضای Production را صادقانه و fail-closed نمایش می‌دهد.

## ریسک‌ها و کارهای باقی‌مانده
- Issue #228/#235 از نظر جریان نرم‌افزاری تا مرحلهٔ امضا/تحویل و دریافت ACK پیاده‌سازی شده‌اند، اما امضای Production با HSM واقعی هنوز فعال نیست.
- دو TestServer در یک process، جایگزین آزمون دو ماشین/دو OS process واقعی نیست.
- Issue #107، #214 و آزمون عملیاتی Windows TPM همچنان موانع جداگانه‌اند.

## شناسه‌های Git
- Branch: `fix/vendor-dashboard-readiness-copy`
- Commit: پس از اجرای CI ثبت می‌شود.
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/354 (merged)
- Merge commit: `31f1873ce9f26405beaf773e876bf99155c43fa2`
- Post-merge CI روی main: .NET `41096833025`، Vendor `41096832981` و Semgrep `41096832996` در زمان ثبت این handoff در حال اجرا هستند؛ موفقیت پس از merge هنوز اعلام نمی‌شود.
