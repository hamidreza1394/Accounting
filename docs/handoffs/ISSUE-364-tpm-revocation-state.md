# تحویل وظیفه — ISSUE-364-tpm-revocation-state

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/364
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** پیاده‌سازی نرم‌افزاری PR #366 در main ادغام شده و .NET CI، Vendor CI و Semgrep روی SHA نهایی سبز هستند؛ hardware verification و runtime enforcement هنوز باز است.

## هدف و معیارهای پذیرش
- [x] تصمیم D-057 با تأیید مالک محصول ثبت شد.
- [x] Migration افزایشی فقط در دیتابیس مشتری طراحی شد.
- [x] State store durable با pending/accepted envelope و crash recovery افزوده شد.
- [x] TPM NV Extend abstraction و hash-chain generation binding افزوده شد.
- [x] SQL Server session application lock برای serialization بین پردازه‌ها افزوده شد.
- [x] fail-closed default provider و dedicated revocation public-key resolver ثبت شدند.
- [x] customer license trust resolver و DI برای RevocationAwareLicenseEnvelopeValidator ثبت شدند.
- [x] Backend Windows TBS با Microsoft.TSS برای TPM NV Extend اضافه شد.
- [x] Backend Linux local TPM device بدون simulator fallback اضافه شد.
- [x] ابزار create-only provisioning برای Windows/TBS اضافه شد.
- [x] اسکریپت create-only provisioning لینوکس با tpm2-tools و TCTI محلی اضافه شد.
- [ ] اجرای provisioning و secret delivery/rotation عملیاتی روی سخت‌افزار واقعی.
- [x] اجرای CI روی SHA نهایی PR #366: .NET CI (شامل build/test، publish، E2E و Windows publish)، Vendor CI و Semgrep موفق.
- [ ] اجرای مستقل/میدانی SQL Server migration/restore روی محیط عملیاتی؛ سبز بودن CI جایگزین این آزمون است.
- [ ] تست واقعی روی TPM سخت‌افزاری Windows 10/11، Windows Server و Linux.
- [ ] انتقال همهٔ call-siteهای runtime مشتری به RevocationAwareLicenseEnvelopeValidator و اثبات enforcement.

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
- Server/Infrastructure/Licensing/ConfigurationLicenseTrustStore.cs
- Server/Infrastructure/Licensing/Tpm2NvExtendRollbackAnchor.cs
- Server/Program.cs
- tools/TpmNvIndexProvisioner/Program.cs
- scripts/provision-offline-revocation-nv-index.sh
- Directory.Packages.props, Template.sln, THIRD_PARTY_NOTICES.md
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
1. اجرای ابزارهای provisioning و بررسی policy/auth در TPM واقعی؛ تکمیل secret delivery/rotation.
2. اجرای CI، رفع تمام شکست‌ها و ساخت PR مستقل برای هر مرحله.
3. تست روی TPM واقعی Windows 10/11، Windows Server و Linux.

## CI feedback and regression coverage — 2026-10-31

- CI initially found that the generated migration Designer contained one extra blank line; the file was aligned with the current model generator output.
- The migration-guard mutation test still used a fixture timestamp 20261030000000 older than the newly added migration 20261031120000, so the test no longer exercised a newer migration. The fixture was moved to 20261101000000; the guard itself was not weakened.
- A second review confirmed GetCurrentAsync already acquires the same SQL Server session application lock as TryApplyAsync before entering recovery logic. No production locking code was changed. Added a SQL Server regression test that blocks an in-flight TPM Extend and verifies a concurrent GetCurrentAsync waits rather than performing a second Extend.
- PR #366 با SHA d2005b618528b416c3773ed7632d09a8627685db در main ادغام شد. آخرین .NET CI run 41175579282، Vendor CI 41175579288 و Semgrep 41175579255 هر سه با conclusion=success تمام شدند.
- .NET CI jobs: client، secret-scan، build-and-test، publish-scripts، e2e، publish و publish-windows همگی موفق‌اند.
- این نتایج اجرای فیزیکی TPM روی Windows/Linux یا provisioning/secret delivery عملیاتی را اثبات نمی‌کنند. همچنین migration روی دیتابیس واقعی اجرا نشده است.


## وضعیت پس از ادغام PR #366 — 2026-10-31

- کد state store و TPM anti-rollback در main قرار دارد؛ این milestone نرم‌افزاری تکمیل شد.
- CI نهایی هر سه pipeline موفق است؛ لینک‌ها: [.NET CI](https://github.com/hamidreza1394/Accounting/actions/runs/41175579282)، [Vendor CI](https://github.com/hamidreza1394/Accounting/actions/runs/41175579288)، [Semgrep](https://github.com/hamidreza1394/Accounting/actions/runs/41175579255).
- Issue #364 همچنان باز می‌ماند تا آزمون روی TPM واقعی، provisioning NV index و مدیریت secret، و اتصال call-site واقعی مشتری به RevocationAwareLicenseEnvelopeValidator انجام و شواهد ثبت شود.


## 2026-10-31 — narrow the customer runtime DI bypass surface

- Added `ICustomerLicenseEnvelopeValidator` as the only customer runtime validation contract; `RevocationAwareLicenseEnvelopeValidator` implements it.
- Removed direct DI registration of raw `LicenseEnvelopeValidator`. Server DI now creates the raw validator only as an internal dependency of the revocation-aware pipeline and exposes only `ICustomerLicenseEnvelopeValidator` to runtime consumers. This prevents a future consumer from accidentally resolving the raw signature/time validator and bypassing revocation checks.
- Updated a contract test to consume the safe interface and still assert fail-closed behavior when no current revocation snapshot exists.
- **Static architecture audit:** no production call-site currently consumes `LicenseEnvelopeValidator`, `RevocationAwareLicenseEnvelopeValidator`, `LicenseEvaluator` or `ICustomerLicenseEnvelopeValidator`. There is also no customer license-envelope file store, DbSet/table, import endpoint or shared authorization policy. `docs/LICENSING.md` correctly marks the installation license module as `NotImplemented`.
- **Why full enforcement is not safe to invent in this slice:** D-025 describes local-file licensing or cloud central storage, but no approved file path/import/admin workflow is implemented. OD-03/04 restrictions after expiry are still `NeedsApproval`; a middleware that blocks all requests or turns expired states into read-only access would invent commercial policy and could lock out initial setup.
- **Remaining required design/implementation:** agree the customer license envelope source (local signed file vs a new customer DB table), import/replace authorization and audit, installation identity binding, shared authorization policy for valid/grace/restricted/missing states, and real endpoint call-sites. Then add integration tests proving revoked/missing/expired snapshot denies protected operations.
- TPM NV state persistence/adapter remains implemented but hardware provisioning, secret delivery/rotation and real Windows/Linux TPM verification remain NotVerified.

- Added a dual-host integration assertion that resolves `ICustomerLicenseEnvelopeValidator` from the actual Customer Server DI container and confirms `LicenseEnvelopeValidator` is not directly resolvable.
