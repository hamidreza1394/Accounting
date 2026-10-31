# تحویل وظیفه — ISSUE-384-customer-license-import

- **Issue:** [#384](https://github.com/hamidreza1394/Accounting/issues/384)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** پیاده‌سازی اولیه در شاخه؛ اصلاح خطاهای CI روی head جدید انجام شده؛ CI نهایی در انتظار

## هدف و معیارهای پذیرش
- [x] استفاده از schema مشتری ادغام‌شده در PR #387؛ بدون تغییر Vendor schema.
- [x] ثبت metadata عمومی هویت نصب فقط از کلید CNG از قبل provision‌شده؛ عدم ساخت خودکار یا export کلید خصوصی.
- [x] ثبت actor/time برای رضایت SoftwareProtected.
- [x] اثبات possession کلید و بررسی OS-installation marker پیش از import.
- [x] import/replace تراکنشی با validator revocation-aware، idempotency و کنترل sequence.
- [x] audit رویدادهای enrollment/import/rejection.
- [ ] اجرای CI کامل و اصلاح همه خطاهای آن.
- [ ] startup reload/revalidation و اتصال enforcement به call-siteهای عملیات تجاری.
- [ ] Cloud authoritative entitlement resolution و tenant isolation.
- [ ] تست عملی دو میزبان و TPM واقعی در انتهای پروژه.

## خلاصه تغییرات
- PR #387 ادغام شده است: commit `65c2cb4762a8f05e3f1deb2592579ac8fba0b6d6`.
- شاخه کاری `feat/issue-384-customer-license-import` شامل enrollment هویت نصب و import/replace لایسنس است.
- APIهای جدید زیر `/api/v1/licensing/installation-identity/enroll` و `/api/v1/licensing/import` هستند؛ هر دو فقط برای AccountKind=Infrastructure، با Anti-forgery، rate limit و no-store.
- InstallationId از `Provisioning:InstallationId` سمت سرور خوانده می‌شود؛ از بدنه درخواست گرفته نمی‌شود.
- طبق D-059، envelope امضاشده با status برابر Restricted پذیرفته و ذخیره می‌شود تا runtime به حالت read/export/recovery برود؛ Grace رد می‌شود چون grace ضمنی پذیرفته نشده است.

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Licensing/CustomerInstallationIdentityEnrollmentService.cs` — ثبت metadata کلید نصب موجود و rotation نسخه‌دار، با تراکنش و audit.
- `Server/Infrastructure/Licensing/CustomerLicenseImportService.cs` — اعتبارسنجی revocation-aware، idempotency، sequence monotonic، جایگزینی و audit.
- `Server/Infrastructure/Licensing/WindowsCngLicenseInstallationIdentityProofVerifier.cs` — بررسی marker و امضای challenge با کلید CNG موجود.
- `Server/Infrastructure/Licensing/ILocalInstallationKeyMetadataProvider.cs` — مرز خواندن metadata کلید نصب بدون ایجاد کلید.
- `Server/Api/ApiEndpoints.cs` — endpointهای operator-only و antiforgery.
- `Server/Program.cs` — DI/options.
- `tests/Template.Tests/CustomerInstallationIdentityEnrollmentServiceTests.cs` — تست enrollment، consent، idempotency و rotation.
- `tests/Template.Tests/CustomerLicenseImportServiceTests.cs` — تست import، idempotency، downgrade، policy و OS-change.
- `tests/Template.Tests/Integration/WindowsCngLicenseInstallationKeyTests.cs` — تست Windows-gated برای proof verifier.
- `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md`, `docs/LICENSING.md` — ثبت وضعیت و مرزهای باقی‌مانده.

## تصمیم‌های فنی
- TPM 2.0 / Microsoft Platform Crypto Provider همچنان پیش‌فرض است؛ Software KSP فقط با consent صریح و ثبت Assurance پایین‌تر.
- enrollment فقط کلید موجود را می‌خواند؛ اگر کلید غایب باشد، خطای fail-closed برمی‌گردد.
- جایگزینی identity مستلزم KeyVersion بالاتر است؛ هویت قبلی Retired و هویت جدید Current می‌شود.
- اعتبارسنجی از `ICustomerLicenseEnvelopeValidator` عبور می‌کند؛ مسیر raw signature validator به مصرف‌کننده تزریق نمی‌شود.
- sequence باید از بیشترین sequence ثبت‌شده برای InstallationId بیشتر باشد؛ LicenseId موجود در جریان replace تغییر نمی‌کند.
- statusهای Valid، ExpiringSoon و Restricted برای import پذیرفته می‌شوند؛ Grace رد می‌شود. پذیرش Restricted به معنی پیاده‌سازی runtime restricted-mode نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `.NET build` | اجرا نشده | در این محیط اجرای محلی .NET انجام نشده است. |
| `dotnet test` | اجرا نشده | تست‌ها نوشته شده‌اند؛ باید CI آن‌ها را اجرا کند. |
| SQL Server migration/integration | اجرا نشده | هیچ migration روی دیتابیس واقعی اجرا نشده است. |
| Windows CNG proof test | اجرا نشده | تست Windows-gated اضافه شده؛ CI باید آن را اجرا کند. |
| physical TPM / two-host E2E | موکول‌شده | طبق درخواست مالک در انتهای پروژه اجرا می‌شود. |

## امنیت، داده و سازگاری
- این branch فقط Customer DB را مصرف می‌کند؛ هیچ Vendor schema تغییر نمی‌کند.
- کلید خصوصی، token یا raw secret در DB، audit یا پاسخ API ذخیره نمی‌شود.
- audit eventها در همان تراکنش state-change ثبت می‌شوند؛ شکست audit باعث عدم موفقیت عملیات می‌شود.
- OS marker mismatch باعث انتقال identity به ReactivationRequired و رد import می‌شود.
- این PR به تنهایی runtime enforcement نیست؛ سرویس هنوز باید به startup validation و مجوزدهی عملیات حسابداری متصل شود.

## ریسک‌ها و کارهای باقی‌مانده
- CI کامل هنوز اجرا/تأیید نشده؛ compile/test errors ممکن است باقی باشند.
- Startup reload/revalidation و middleware/policy/call-site enforcement تکمیل نشده است.
- Cloud server-side authoritative entitlement resolution و tenant isolation تکمیل نشده است.
- OD-03/04/05 و OD-06 همچنان خارج از این slice و نیازمند تصمیم/پیاده‌سازی جداگانه‌اند.
- نصب Linux در این مرحله پشتیبانی عملیاتی ندارد؛ provider Windows CNG fail-closed است.
- تست TPM واقعی، restore/recovery drill و دو میزبان مستقل در انتهای پروژه انجام می‌شود.

## شناسه‌های Git
- **Branch:** `feat/issue-384-customer-license-import`
- **Commit:** `2d184ef20f7baf062a2e241a6abd845ef328cb9c` (آخرین head مشاهده‌شده هنگام به‌روزرسانی این handoff؛ در صورت commit بعدی، PR head ملاک است).
- **Pull Request:** [#388](https://github.com/hamidreza1394/Accounting/pull/388) — باز؛ CI در حال اجرا.
