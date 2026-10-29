# تحویل وظیفه — ISSUE-235-provisioning-payload-v2

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-29
- **وضعیت:** مصرف‌کننده در حال تکمیل؛ trust verifier و Migration مدل/اسکلت claim اضافه شده‌اند، اما provisioning اتمیک و enrollment هنوز کامل نیست

## هدف و معیارهای پذیرش
- [x] افزودن نسخهٔ قرارداد provisioning که دورهٔ مالی اولیه را صریح حمل کند.
- [x] حمل مرجع شرکت مادر برای شرکت فرعی، بدون اتصال مستقیم Vendor به دیتابیس مشتری.
- [x] حفظ canonical JSON و اعتبار امضای payload نسخهٔ ۱.
- [x] تست منفی برای تاریخ نامعتبر، نبود دورهٔ مالی و شرکت فرعی بدون مرجع مادر.
- [ ] پیاده‌سازی handler واقعی که در یک تراکنش شرکت، دوره مالی، نقش، عضویت مدیر، claim و audit را اعمال کند.
- [ ] claim اتمی RequestId/Nonce در مسیر واقعی provisioning؛ فعلاً مدل و unique index آماده‌اند اما مصرف‌کننده آنها را فراخوانی نمی‌کند.
- [ ] enrollment یک‌بارمصرف مدیر و اجبار تغییر رمز در اولین ورود.
- [x] Migration مستقل سمت مشتری برای claim و replay metadata در PR جداگانه تولید شده است.
- [ ] تست SQL Server واقعی برای rollback، replay هم‌زمان و migration ارتقا/بازگشت.

## خلاصه تغییرات
- CustomerProvisioningPayload فیلدهای نسخه‌دار عنوان/تاریخ شروع/پایان دورهٔ مالی و ParentCompanyReference دارد.
- نسخهٔ ۱ دقیقاً همان فیلدهای canonical قبلی را تولید می‌کند؛ فیلدهای جدید فقط در SchemaVersion=2 به JSON امضاشده افزوده می‌شوند.
- Validator هر دو schema v1 و v2 را می‌پذیرد، اما در v2 دورهٔ مالی معتبر را اجباری می‌کند و ParentCompanyReference را فقط برای subsidiary می‌پذیرد.
- هیچ رمز، password hash، secret، connection string یا private key به payload اضافه نشده است.

## فایل‌های مهم تغییرکرده
- Licensing.Contracts/CustomerProvisioningPackage.cs — فیلدهای نسخهٔ ۲ و canonical serialization سازگار با v1.
- Licensing.Contracts/CustomerProvisioningPackageValidator.cs — قواعد نسخهٔ ۱/۲ و اعتبارسنجی دوره/رابطهٔ والد.
- tests/Licensing.Contracts.Tests/CustomerProvisioningPackageValidatorTests.cs — پوشش v1، امضای v2 و سناریوهای نامعتبر.

## تصمیم‌های فنی
- قالب envelope همچنان نسخهٔ ۱ است؛ نسخهٔ schema payload مستقل است.
- قرارداد v1 حفظ شده چون وضعیت نصب‌های Production از مخزن قابل اثبات نیست؛ فیلد جدیدی به canonical JSON نسخهٔ ۱ افزوده نمی‌شود.
- دورهٔ مالی در v2 لازم است. شرکت subsidiary باید مرجع مادر داشته باشد؛ independent و parent نباید مرجع مادر داشته باشند.
- رمز اولیه از بسته منتقل نمی‌شود. مسیر enrollment امن باید token تصادفی یک‌بارمصرف را فقط یک بار به اپراتور/مشتری نشان دهد، hash آن را ذخیره کند و اولین نشست را محدود به تغییر رمز کند.
- برای ParentCompanyReference هنوز نگاشت مرجع بیرونی به شرکت داخلی تعریف نشده؛ تا روشن‌شدن قرارداد نگاشت، شرکت تابعه نباید اعمال شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| اجرای محلی dotnet test برای Licensing.Contracts.Tests | اجرا نشده | این نشست checkout محلی متصل به مخزن و SDK .NET ندارد؛ CI روی PR باید اجرا شود. |
| تست‌های verifier | افزوده شد؛ CI نهایی روی HEAD جدید لازم است | امضای معتبر/نامعتبر، کلید ناشناخته، customer/installation mismatch، expiry و tampering. |
| تولید Migration | موفق | run 40909292917؛ migration ID `20261029161402_AddCustomerProvisioningClaims` تولید و به شاخه commit شد. |
| SQL Server integration tests | اجرا نشده | تست rollback/replay/concurrency و اجرای migration روی دیتابیس واقعی هنوز اضافه نشده است. |
| git diff --check | اجرا نشده | تغییرات از طریق GitHub Contents API ثبت شده‌اند؛ CI و بازبینی نهایی لازم است. |

## امنیت، داده و سازگاری
- این PR فقط قرارداد مشترک و تست‌های واحد آن را تغییر می‌دهد؛ هیچ Server/**، schema مشتری، Migration یا Vendor DB تغییر نکرده است.
- اعتبارسنجی امضا نیازمند trust store مستقل در مصرف‌کننده است؛ این PR مصرف‌کننده یا replay protection را ادعا نمی‌کند.
- payload نباید شامل اطلاعات محرمانه یا اطلاعات تجاری حسابداری مشتری باشد.

## ریسک‌ها و کارهای باقی‌مانده
- تولیدکنندهٔ Vendor باید در PR جداگانه v2 را صادر کند؛ مصرف‌کننده نباید آن را حدس بزند.
- مصرف‌کننده باید RequestId و hash nonce را با unique constraint و claim اتمی در همان تراکنش provisioning ذخیره کند.
- پیش از ادغام Migration مشتری، بررسی مدل فعلی، rollback و SQL Server integration tests الزامی است.
- enrollment مدیر اولیه باید با secret تصادفی یک‌بارمصرف و محدود به زمان انجام شود؛ هیچ رمز یا secret قابل‌بازیابی در بسته قرار نگیرد.
- KMS/HSM و مقصد مستقل ممیزی همچنان وابستگی عملیاتی جداگانه‌اند.

## شناسه‌های Git
- Branch: `feat/customer-provisioning-consumer`
- Migration: `20261029161402_AddCustomerProvisioningClaims`
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/302 (Draft; do not merge yet).

## Schema v3 follow-up — 2026-10-29
- Added `InitialAdminEmail` to schema v3 only. Canonical JSON for schema v1/v2 is unchanged.
- Vendor onboarding now collects the initial administrator email, validates it server-side, includes it in the signed v3 payload, and compares it during same-request idempotency checks.
- This closes a concrete contract gap: the customer `Users.EmailAddress` field is required, so the consumer must not invent a placeholder email.
- The v3 email field is not a password or bootstrap secret. No credentials are sent or persisted by this change.
- Added contract, Vendor validation and UI submission regression tests. Awaiting CI for the latest head.
- Still not complete: transactional apply, secure one-time enrollment delivery, forced password-change enforcement, subsidiary parent-reference resolution, signed acknowledgement generation, and SQL Server replay/concurrency/rollback integration coverage.
