# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-se-d099-acceptance

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** GLM — Vendor/Licensing/Customer Consumer
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط مستندات؛ کد تولیدی تغییر نکرد)

## هدف و معیارهای پذیرش
- [x] بازبینی D-099 و استخراج پیش‌نیازهای واقعی (P1..P8) با مرجع تصمیم‌ها و ODهای باز
- [x] مشخص‌کردن وضعیت واقعی Software/Hardware signing و تفاوت دو مسیر (TPM در برابر SoftwareProtected/D-107)
- [x] ۱۰ معیار پذیرش آزمون‌پذیر (AC-1..AC-10) با نگاشت به بندهای D-099؛ AC-8 در اصلاحیهٔ این PR به دو زیرمعیار **مستقل** تفکیک شد: AC-8a (`HardwareTpm` fail-closed، بدون fallback خودکار به Software KSP) و AC-8b (`SoftwareProtected` فقط با انتخاب/تأیید صریح سطح assurance؛ نبود TPM به‌تنهایی شکست نیست؛ بدون downgrade خودکار و با سطح قابل‌تشخیص در وضعیت/گزارش‌ها)
- [x] ماتریس تست چهار سطحی (T-A unit / T-B SoftHSM / T-C CNG واقعی در CI / T-D سخت‌افزار واقعی) با قاعدهٔ «هیچ AC کلیدی فقط با mock بسته نشود»
- [x] حفظ fail-closed، D-107/D-055 و عدم ادعای Production Hardware readiness

## خلاصه تغییرات
- سند جدید `docs/D099_HARDWARE_REACTIVATION_ACCEPTANCE.md` — تصمیم جدیدی نمی‌گیرد؛ رفتار Accepted را به معیار آزمون‌پذیر ترجمه می‌کند و مرز mock/سخت‌افزار واقعی را ثبت می‌کند.
- **اصلاحیه ابهام AC-8 (همین PR):** AC-8 واحد که «رفتار یکسان در هر دو سطح» می‌گفت، دو مسیر مستقل را مخلوط می‌کرد؛ اکنون AC-8a/AC-8b رفتار fail-closed متفاوت و درست هر مسیر را جدا تعریف می‌کنند، ماتریس تست (T-C برای AC-8b و T-D برای AC-8a) و قاعدهٔ «هیچ AC کلیدی فقط با mock بسته نشود» هم‌راستا شدند و رفتار موجود کد (`WindowsCngLicenseInstallationIdentityProofVerifier.cs:71-78`، consent در `WindowsCngLicenseInstallationKeyProvisioner.cs:66/105`، نمایش `AssuranceLevel` در `ApiEndpoints.cs:766`) به‌عنوان شاهد ارجاع داده شد. ماتریس تست سطح سند است؛ هیچ تست کدی در این PR اجرا یا ادعا نشده است.

## فایل‌های مهم تغییرکرده
- `docs/D099_HARDWARE_REACTIVATION_ACCEPTANCE.md` — جدید
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-se-d099-acceptance.md` — جدید

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ OD-20/OD-21/OD-06/OD-27 به‌صورت «باز» حفظ شدند و این سند ادعایی روی آن‌ها ندارد. پیاده‌سازی reactivation نیازمند Issue/ADR اجرایی مستقل پس از تأیید مالک است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تطبیق ادعاها با کد (WindowsCng providers، runbookها، وضعیت CI تست‌ها) | انجام شد | همهٔ file:lineها از `main@58a1ccd4` |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر؛ CI PR گزارش خودش را می‌دهد |
| `git diff --check` | موفق | صفر خطا |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی/داده؛ سند صراحتاً هرگونه ادعای Production Hardware Readiness را تا اجرای Key Ceremony و T-D منع می‌کند و امضای لایسنس در CI را ممنوع می‌داند (D-041/G5).
