# تحویل وظیفه — ISSUE-364-tpm-revocation-state

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/364
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** ناقص؛ persistence slice پیاده‌سازی شده، TPM platform provider و hardware verification باقی است.

## هدف و معیارهای پذیرش
- [x] تصمیم D-057 با تأیید مالک محصول ثبت شد.
- [x] Migration افزایشی فقط در دیتابیس مشتری طراحی شد.
- [x] State store durable با pending/accepted envelope و crash recovery افزوده شد.
- [x] TPM NV Extend abstraction و hash-chain generation binding افزوده شد.
- [x] SQL Server session application lock برای serialization بین پردازه‌ها افزوده شد.
- [x] fail-closed default provider و dedicated revocation public-key resolver ثبت شدند.
- [x] Backend Windows TBS با Microsoft.TSS برای TPM NV Extend اضافه شد.
- [x] Backend Linux local TPM device بدون simulator fallback اضافه شد.
- [x] ابزار create-only provisioning برای Windows/TBS و مستندات ceremony اضافه شد.
- [ ] ابزار provisioning Linux و secret delivery/rotation عملیاتی.
- [ ] اجرای تست‌های SQL Server و CI روی آخرین SHA.
- [ ] تست واقعی روی TPM سخت‌افزاری Windows 10/11، Windows Server و Linux.
- [ ] runtime enforcement در مسیر نهایی LicenseEvaluator/host پس از تأیید provider.

## خلاصه تغییرات
- Entity و جدول CustomerOfflineRevocationStates در Customer Database.
- Migration: 20261031120000_AddCustomerOfflineRevocationState.
- TPM NV Extend hash-chain، نگهداری envelope امضاشده و re-verification هر state read.
- پروتکل prepare → TPM extend/read-back → finalize با recovery دو crash window.
- SQL Server sp_getapplock در سطح Session برای جلوگیری از Extend هم‌زمان در چند process.
- default provider بدون پیکربندی TPM unavailable است؛ در پیکربندی صریح، Windows TBS یا Linux strict local device انتخاب می‌شود و هیچ software/simulator fallback وجود ندارد.

## فایل‌های مهم تغییرکرده
- Domain/CustomerOfflineRevocationState.cs
- Infrastructure/Persistence/Configurations/CustomerOfflineRevocationStateConfiguration.cs
- Infrastructure/Persistence/TpmAnchoredOfflineRevocationStateStore.cs
- Infrastructure/Persistence/Migrations/20261031120000_AddCustomerOfflineRevocationState.cs
- Infrastructure/Persistence/Migrations/20261031120000_AddCustomerOfflineRevocationState.Designer.cs
- Licensing.Contracts/OfflineRevocationConsumer.cs
- Server/Infrastructure/Licensing/OfflineRevocationTrustProviders.cs
- Server/Program.cs
- tests/Template.Tests/Integration/CustomerOfflineRevocationSqlServerTests.cs
- tests/Template.Tests/DatabaseModelTests.cs
- docs/DECISIONS.md (D-057), docs/LICENSING_THREAT_MODEL.md, docs/LICENSING_DATA_MODEL.md, docs/PROJECT_STATUS.md, docs/WORK_LOG.md

## تصمیم‌های فنی
- TPM NV Extend hash-chain به‌جای SQL-only counter استفاده می‌شود؛ هر extension شامل domain separator، generation و snapshot digest است.
- TPM digest فعلی باید با DB برابر باشد و هر state read signature، payload digest، sequence، expiry، revoked IDs و chain را verify می‌کند.
- دیتابیس مشتری و Vendor DB کاملاً جدا هستند.
- هر TPM mismatch، index reset یا نبود provider به Unavailable منجر می‌شود.
- Runtime فقط Read/Extend؛ Define/Undefine در Ceremony جداگانه.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| .NET build | اجرا نشده | محیط این نشست دسترسی به repo clone/.NET runner ندارد |
| Licensing.Contracts tests | اجرا نشده | CI پس از PR لازم است |
| SQL Server integration | اجرا نشده | تست‌ها نوشته شده‌اند؛ نیازمند FARANOU_TEST_SQLSERVER |
| Migration up/down | اجرا نشده | CI باید بررسی کند |
| TPM hardware | اجرا نشده | provider پیاده شده ولی هنوز روی سخت‌افزار واقعی verify نشده |

## امنیت، داده و سازگاری
- هیچ تغییری در VendorDbContext/schema انجام نشده است.
- هیچ private key یا TPM authorization در Git ثبت نشده است.
- migration روی دیتابیس واقعی یا Production اجرا نشده است.
- provider فعلی عمداً unavailable/fail-closed است.

## گام بعدی
1. تکمیل ابزار provisioning Linux و secure auth delivery/rotation.
2. اجرای CI، رفع تمام شکست‌ها و ساخت PR مستقل برای هر مرحله.
3. تست روی TPM واقعی Windows 10/11، Windows Server و Linux.