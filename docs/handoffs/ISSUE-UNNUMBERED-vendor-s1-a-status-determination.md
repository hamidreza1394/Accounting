# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-s1-a-status-determination

- **Issue:** ندارد (مأموریت تعیین تکلیف S1-a به‌دستور مالک؛ Issue اختصاصی بازِ متناظر وجود نداشت و Issue تکراری ساخته نشد. Issues مرتبط باز: [#228](https://github.com/hamidreza1394/Accounting/issues/228)، [#235](https://github.com/hamidreza1394/Accounting/issues/235)، [#392](https://github.com/hamidreza1394/Accounting/issues/392)؛ مرجع نقشهٔ S1: Issue بستهٔ [#696](https://github.com/hamidreza1394/Accounting/issues/696))
- **Agent/مسئول:** GLM (z.ai agent) به دستور مالک محصول
- **تاریخ:** 2026-11-18
- **مبنا:** `main@ab4ddef16f965b98c21720c5784a1fe068538878` (ادغام PR #707 — D-103 V1)
- **وضعیت:** انجام‌شده (فقط مستندات و تعیین تکلیف؛ بدون هیچ تغییر کد)

## هدف و معیارهای پذیرش
- [x] بررسی وضعیت واقعی مخزن از کد/commit/PR/CI، نه از گزارش‌های تاریخی: `main@ab4ddef1` = ادغام PR #707 در `2026-11-18T13:10:04Z`؛ `AGENTS.md`، `INDEX`، `PROJECT_STATUS`، `DECISIONS`، شش سند `LICENSING*`، `AGENT_COORDINATION` و handoffهای #228/#235/#392/#393/#696/#698 خوانده شد.
- [x] تعیین تکلیف هر هفت بند S1-a با شواهد کد/تست/CI واقعی (بخش یافته‌ها).
- [x] هیچ قابلیتی دوباره ساخته نشد؛ Gate دوکلیدی، allow-list، قرارداد ACK/Provisioning، تنظیمات، کلیدها و `CustomerConsumerReady` تغییر نکردند.
- [x] Blockerهای نیازمند تصمیم مالک به‌جای تصمیم‌گیری خودسرانه ثبت شدند.
- [x] پیش‌نیازهای S1-e برای مأموریت بعدی مشخص شد.
- [x] ناسازگاری‌های مستندات با Mergeهای واقعی (#707 و تأیید CI post-merge #699) با کمترین تغییر هماهنگ شد.

## یافته‌ها — تعیین تکلیف S1-a (هر هفت بند)

### 1) allow-list ACK سمت Vendor (گسترش شرکت‌محور) — کامل
- PR #699 (Issue #698) در `2026-11-17T09:51:12Z` با merge commit `755150929b211ac3b952a3c3a7f0856624b11f0d` ادغام شد؛ چهار کد ثابت شرکت‌محور Customer v5 (`provisioning_license_not_installed`، `provisioning_not_entitled_for_company`، `provisioning_company_reference_conflict`، `provisioning_company_capacity_exceeded`) به دو کد قدیمی افزوده شدند — تأیید در `Vendor/Vendor.Host/Authorization/ProvisioningAcknowledgementStateRules.cs` (خطوط ~۴۱..۴۶؛ شش کد).
- تست واحد شش کد (`ProvisioningAcknowledgementStateRulesTests`) و تست SQL Server (`ProvisioningAcknowledgementSqlServerIntegrationTests`: ACK امضاشده → `Failed` + `FailureCode` + metadata/Audit، retry دقیق idempotent، conflict برای ACK متفاوت، کد ناشناخته/PII بدون mutation).
- CI روی HEAD نهایی PR: Vendor CI ۳/۳، Semgrep ۱/۱، .NET CI ۷/۷ (Build/Test، E2E، Publish ویندوز/لینوکس). **تأیید جدید 2026-11-18:** post-merge CI روی merge commit `75515092` در main با API بررسی شد = ۱۵/۱۵ Check Run موفق (e2e، publish، publish-windows، publish-scripts، build-and-test، CodeQL ×۴، build-test-package، client، checkpoint regression ویندوز). این، موفقیتِ تأییدنشدهٔ قبلی در `PROJECT_STATUS.md` است.

### 2) اعتبارسنجی ACK امضاشده و رفتار نامعتبر/تکراری/replay — کامل
- مسیر در `VendorProvisioningEndpoints.cs`: envelope نامعتبر → 400 `provisioning_acknowledgement_envelope_invalid`؛ retry دقیق → audit `vendor.provisioning.acknowledgement-retried` و پاسخ idempotent؛ ACK متفاوت برای درخواست نهایی‌شده → 409 `provisioning_acknowledgement_conflict`؛ payload نامعتبر → 400؛ `request_id` تکراری متفاوت → 409 `provisioning_request_id_conflict`.
- verify-before-parse و trust-store: `ProvisioningAcknowledgementTrustStoreTests`، `ProvisioningPackageSigningTrustStoreTests`، `ProvisioningSigningProviderContractTests`؛ الگوی تاریخی handoff `ISSUE-228-vendor-verify-signature-before-parse`. امضای معتبر به‌تنهایی مجوز ذخیرهٔ ErrorCode نیست (allow-list fail-closed).

### 3) مرز CustomerAccount / Installation / CompanyReference — حفظ‌شده
- هیچ ستون/جدول `CompanyReference` در `Vendor.Persistence` وجود ندارد (تنها match، یک comment است). مقدار مرجع فقط از payload امضاشده اعتبار دارد: `VendorLicensePayloadBuilder` دو مسیر دارد — `FormatVersion: 1` بدون entitlement شرکت‌محور، یا `FormatVersion = LicensePayload.CurrentFormatVersion` (v2) با `CompanyEntitlements` از draft تأییدشدهٔ `VendorLicenseCompanyDraft.TryParse` که در همان تراکنش صدور امضا می‌شود؛ هیچ مسیری مرجع را از نام شرکت، CustomerNumber یا CompanyId استنتاج نمی‌کند.
- تست‌های قاعدهٔ مرجع (از `PROJECT_STATUS.md`): `NewCompanyReceivesServerGeneratedGuidDReference`، `ClientSuppliedReferenceForNewCompanyIsRejected`، `ExistingReferenceIsKeptWhenItMatchesTheSameDraft`.
- Resolver fail-closed: payload v2 بدون mapping شرکت‌محور → 409 `provisioning_company_reference_mapping_required` (اثبات در ماتریس gate در `VendorProvisioningV5IssuanceSqlServerTests`).
- D-103 V1 (PR #707، merge commit `ab4ddef1`): `VendorTenantView` فقط-خواندنی و مشتق‌شده؛ بدون Entity/جدول/Migration/API مستقل/کلید ذخیره‌شده؛ تست نگهبان `404` برای `GET/POST /api/vendor/tenants/`. وضعیت D-103 در `DECISIONS.md` با ادغام واقعی هم‌سو است.

### 4) Gate دوکلیدی صدور v5 — بسته و سالم (عمداً fail-closed)
- `VendorProvisioningCompanyReferenceGate.cs`: `CustomerConsumerReady => false` (interlock سطح کد؛ فقط PR پیاده‌کنندهٔ verification v5 مشتری مجاز به تغییر آن است) + `Provisioning:CompanyReferenceIssuance:Enabled` پیش‌فرض خاموش (فقط `true` صریح قابل parse)؛ `FromConfiguration = requested && CustomerConsumerReady`.
- تست‌ها: `VendorProvisioningCompanyReferenceGateTests` و ماتریس `{null, Disabled, enabled:false}` در `VendorProvisioningV5IssuanceSqlServerTests` — با Gate بسته، حتی binding معتبر هم صدور نمی‌یابد. **در این مأموریت هیچ تغییری در Gate/تنظیم ایجاد نشد و بازکردن آن تصمیم مالک است.**

### 5) تست‌های Integration مربوط به ACK و زنجیرهٔ Provisioning — موجود و CI-verified
- چهارده فایل تست ACK/Provisioning در `tests/Vendor.Host.Tests` شامل `ProvisioningAcknowledgementEndpointTests`، `ProvisioningAcknowledgementSqlServerIntegrationTests`، `VendorProvisioningLicenseResolverSqlServerTests`، `VendorProvisioningV5IssuanceSqlServerTests`، `VendorProvisioningV5PayloadRulesTests`، `VendorProvisioningCompanyReferenceRulesTests`، `VendorProvisioningCompanyReferenceGateTests`.
- تست‌های SQL Server نیازمند `FARANO_VENDOR_TEST_SQLSERVER` و require-guardها هستند و فقط در CI اجرا می‌شوند (CIهای مرجع بند ۱ همگی سبز). اجرای محلی در این مأموریت انجام نشد (SQL Server/Docker در محیط اجرا نیست) — نه موفقیت و نه شکست محلی ادعا نمی‌شود.

### 6) وابستگی‌های Customer در #684/#686/#687 — ادغام‌شده در main؛ بدون تغییر در این مأموریت
- #684 (`5f4baf82`): schema `CompanySetupRecords` و Migration `20261116070805_AddCompanySetupRecords` **فقط در مخزن** — روی دیتابیس واقعی مشتری اجرا نشده.
- #686 (`b0eefa37`): مصرف‌کنندهٔ v5 با Apply اتمیک و ترتیب Entitlement→Duplicate→Capacity و چهار کد رد شرکت‌محور؛ #687 (`f8a98713`): استخراج `CustomerCompanyEntitlementResolver`؛ `maxCompanies` با سیاست block-new شمرده می‌شود.
- سمت Customer خارج از محدودهٔ این مأموریت است؛ هیچ تغییری در `Server/**` یا Migrationها انجام نشد.

### 7) تست دو میزبان و الزامات باقی‌مانده برای Staging
- تست دو میزبان موجود: `VendorCustomerProvisioningDualHostE2ETests` (schemaVersion=4؛ از PR #329، ادغام 2026-10-30 با .NET CI ۷/۷) — دو TestServer مستقل + دو دیتابیس SQL Server مستقل در یک test process؛ پوشش: درخواست → maker-checker → امضا/Export → apply مشتری → replay → enrollment → ACK امضاشده → Confirmed.
- تست دو میزبان شرکت‌محور v5 پس از بازشدن Gate معنا دارد؛ در وضعیت فعلی صدور v5 عملیاتی وجود ندارد.
- **الزامات Staging باقی‌مانده:** E2E دو پردازه/دو ماشین واقعی، KMS/HSM عملیاتی (providerهای Production عمداً fail-closed؛ #236)، توزیع/rotation کلید اعتماد ACK (#235)، bootstrap مدیر Dedicated/Offline (#389). اینها زیرساخت/تصمیم عملیاتی‌اند، نه کار کد Vendor در این مأموریت.

## نتیجهٔ S1-a از سمت Vendor
**کامل.** چهار جزء S1-a در `LICENSING_IMPLEMENTATION_PLAN.md` (گسترش allow-list ACK سمت Vendor، Gate دوکلیدی به‌صورت بسته و امن، تست دو میزبان موجود، مرزها) یا انجام‌شده‌اند یا در وضعیت عمدی fail-closed صحیح‌اند. باقی‌ماندهٔ S1-a صرفاً تصمیم مالک، Migration واقعی مشتری، زیرساخت Staging و کارهای Customer-side است؛ طبق مأموریت هیچ کدنویسی جدیدی انجام نشد و هیچ موردی از S1-b تا S1-g اضافه نشد.

## Blockerهای نیازمند تصمیم/اقدام مالک (بدون صدور مجوز فرضی)
1. تصمیم و اجرای دوکلیدی بازکردن Gate صدور v5 (فقط در مأموریت مستقل با تأیید صریح مالک).
2. اجرای Migration `20261116070805_AddCompanySetupRecords` روی دیتابیس واقعی مشتری با schema approval و رویهٔ عملیاتی.
3. سیاست Restore/Clone و انتقال نصب: OD-06 (`Proposed`/`NeedsApproval`)، T34، anti-rollback در #364 (باز).
4. Staging واقعی دو پردازه/دو ماشین و provider عملیاتی امضا (#236؛ هیچ KMS/HSM provision نشده).
5. bootstrap مدیر Dedicated/Offline (#389) و trust-key distribution/rotation (#235).
6. ریسک ثبت‌شدهٔ `CustomerNumber` قابل‌ویرایش در Vendor در برابر `CustomerReference` صدورشده (تصمیم سیاست اصلاح با مالک).

## پیش‌نیازهای S1-e برای مأموریت بعدی
- **دامنهٔ S1-e (طبق نقشهٔ S1):** انقضای Cloud و recovery — شاخهٔ Cloud در state machine شروع (مسدودسازی ورود عادی + حفظ مسیر تمدید/بازیابی). تصمیم مبنا D-098 (`Accepted`).
- **Issues وابستهٔ باز:** #396 (دروازهٔ license-first در startup مشتری)، #384 (persist activation و enforcement سمت سرور؛ `ICustomerLicenseRuntimeEvaluator` ثبت شده ولی هیچ call-site اجرایی ندارد)، #228/#235 (زنجیرهٔ provisioning/ACK و sync).
- **پیش‌نیازها پیش از شروع:** تثبیت قرارداد مشترک بین‌بخشی Vendor/Customer (در S1-0 مالکیتش ثبت شده ولی تثبیت نشده)، تعیین محدودهٔ دقیق سهم Vendor (داده/قرارداد انقضای Cloud و مسیر sync) در برابر Customer-side، و وضوح تعامل با مسیر تمدید (پیوند با S1-b amendment) — بدون شروع S1-b..S1-g در همان مأموریت.
- **هشدار D-098:** تفاوت انقضای Cloud با Restricted نصب Dedicated/Offline (D-026/D-059) باید در طراحی state machine رعایت شود.

## خلاصه تغییرات
- فقط این handoff + به‌روزرسانی وضعیت در `PROJECT_STATUS.md` (بخش جدید بالای سند + بخش‌های ناسازگار)، `docs/LICENSING.md` (یک جملهٔ وضعیت CI post-merge)، handoff `ISSUE-698` (افزودهٔ تأیید CI)، `WORK_LOG.md` (ورود افزایشی) و `INDEX.md` (لینک همین handoff).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `git diff --check` | موفق | بدون trailing whitespace |
| بازبینی کامل diff | انجام شد | فقط docs/؛ بدون کد |
| `dotnet build` / `dotnet test` | اجرا نشده | تغییر اجرایی وجود ندارد؛ طبق `AGENTS.md` اجرا نشده گزارش می‌شود |
| `python3 scripts/check_static_assets.py` | اجرا نشده | بی‌ارتباط با تغییر مستندات |
| خواندن کد/handoffها و API وضعیت PR/Check-Runها | انجام شد | شواهد SHA/Run در متن؛ از جمله PR #707 (۱۸/۱۸ چک روی HEAD) و post-merge `75515092` (۱۵/۱۵) و `ab4ddef1` (هنگام بازبینی ۱۳/۱۵ موفق؛ e2e و publish-windows در حال اجرا) |

## امنیت، داده و سازگاری
- هیچ secret، کلید، credential یا connection string در مستندات نیست؛ Gate دوکلیدی، تنظیمات، trust store و allow-list تغییر نکردند.
- هیچ Migration/Schema/Customer DB لمس نشد؛ هیچ ادعای Production readiness وجود ندارد.
- با D-022/D-026/D-059/D-087/D-097..D-102 و OD-06/OD-20..OD-26 سازگار است؛ هیچ ADR بازنویسی یا شمارهٔ جدیدی اضافه نشد (تصمیم جدیدی گرفته نشد).

## شناسه‌های Git
- Branch: `agent/s1-a-vendor-status-determination` (از `main@ab4ddef1`)
- Pull Request: مستنداتی؛ وضعیت ادغام و CI واقعی آن در گزارش مأموریت و تاریخچهٔ PR ثبت می‌شود. Merge توسط Maintainer انجام می‌شود.
