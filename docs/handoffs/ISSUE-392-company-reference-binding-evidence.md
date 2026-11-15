# تحویل وظیفه — ISSUE-392-company-reference-binding-evidence

- **Issue:** [#392](https://github.com/hamidreza1394/Accounting/issues/392) (والد #389 / D-059؛ هماهنگی با #228، #235، #384، #391، #393، #394، #396)
- **Agent/مسئول:** Claude (بررسی خواندنی)
- **تاریخ:** 2026-11-15
- **مبنا:** `main@990e8152b701d39b55cee502bc1508458582d635`
- **وضعیت:** انجام‌شده (فقط مستندات و شواهد)

> **این سند هیچ تصمیم معماری را مصوب نمی‌کند.** همهٔ گزینه‌ها و ترجیح‌ها `Proposed` هستند و تأیید مالک لازم دارند. هیچ کد، Migration، Schema یا فایل تصمیم‌گیری موجود (`DECISIONS.md` و مانند آن) تغییر نکرد.

## هدف و معیارهای پذیرش
- [x] دامنه و وضعیت واقعی #392 با کد و Issueها تطبیق داده شود.
- [x] محل تولید، نگهداری و انتشار `CompanyReference` در Vendor و قرارداد مشخص شود.
- [x] enforcement واقعی `maxCompanies`، مسیرهای ساخت Company و تهدیدهای Replay/Restore/Clone بررسی شود.
- [x] امکان enforcement بدون Schema، بدون Tenant و بدون `CompanyReference` ارزیابی شود.
- [x] گزینه‌های الف، ب، ج، د مقایسه شوند؛ ترتیب PRهای Tenant و Provisioning با Issueها تطبیق داده شود.

## یافته‌ها (با ارجاع)

### F1. `CompanyReference` در قرارداد امضاشدهٔ فعلی
- **در `LicensePayload` v2 وجود دارد:** `Licensing.Contracts/CompanyLicenseEntitlement.cs` (`CompanyReference`)، `LicensePayload.CurrentFormatVersion = 2`، اعتبارسنجی قالب و یکتایی در `StructuralValidation.cs` (خط ~۱۵۵ تا ۱۵۷، `IdPattern`)، و `companyEntitlements` ≤ `maxCompanies` (خط ~۱۴۳). `docs/LICENSING_DATA_MODEL.md` (حدود سطر ۳۱۸ تا ۳۳۱).
- **در `CustomerProvisioningPayload` وجود ندارد:** `CustomerProvisioningPackage.cs` فقط `CustomerReference` و `ParentCompanyReference` دارد؛ `CurrentPayloadSchemaVersion = 4` (`CustomerProvisioningPackageValidator.cs` خط ۱۶). `ParentCompanyReference` همان `RequestId` والد است، نه `CompanyReference` (`docs/PROJECT_STATUS.md` بند #228).
- **نتیجه:** افزودن `CompanyReference` به بستهٔ Provisioning به **نسخهٔ جدید schema (v5 یا معادل)** نیاز دارد. الگوی موجود همین است: فیلدهای هر نسخه با شرط `SchemaVersion >= n` به canonical JSON اضافه می‌شوند و نسخه‌های قبلی ثابت می‌مانند (`CustomerProvisioningPackage.cs` سطرهای ~۷۲ تا ۹۵؛ پیام Issue #392: «additively with a versioned field»). این الگو را من تأیید می‌کنم، اما تصمیم برای نسخهٔ جدید با مالک است.

### F2. Vendor: تولید، نگهداری، انتشار
- **تولید:** هیچ کدی در `Vendor/**` مقدار `CompanyReference` نمی‌سازد. `VendorLicenseAmendmentPayloadBuilder.cs` (سطر ~۱۲۱) صریحاً می‌گوید «never creates a company reference» و فقط روی payload v2 **موجود و امضاشده** کار می‌کند.
- **صدور پایه فقط v1 است:** `VendorLicensePayloadBuilder.cs` (سطر ~۶۶) `FormatVersion: 1` و `Limits(MaxCompanies, Subsystems)` می‌سازد؛ `companyEntitlements` ندارد. پس در `main` هیچ مسیر Vendor برای صدور v2 از صفر وجود ندارد.
- **نگهداری:** `VendorDbContext` مجموعه‌های `Customers`، `Installations`، `Contracts`، `Licenses`، `ApprovalRequests`، `SignedLicenseEnvelopes`، `SignedRevocationSnapshots`، `ProvisioningRequests` را دارد. هیچ جدول/ستونی برای `CompanyReference` یا entitlement هر Company نیست (جست‌وجوی `CompanyReference` در `Vendor.Persistence` نتیجه‌ای نداد).
- **انتشار:** `VendorProvisioningLicenseResolver.cs` (خط ~۱۵۰ تا ۱۵۵) برای payload v2 با `provisioning_company_reference_mapping_required` fail-closed می‌شود (PR #546).
- **شکاف مالکیت (مهم):** `LICENSING_THREAT_MODEL.md` (سطر ۲۰۳) «issuance v2» را به #393 می‌دهد؛ #392 مالک نگاشت مشتری است؛ اما «چه کسی و کجا مقدار `CompanyReference` را در Vendor می‌سازد و نگه می‌دارد» در هیچ سند یا Issue صراحتاً مالک ندارد (سطر ۲۰۹ فقط «مراقبت در تولید Reference» می‌گوید). این شکاف باید پیش از طراحی Schema مشتری بسته شود.

### F3. #391 و `maxCompanies`
- #391 بسته است، ولی دامنهٔ آن Seat، Module و Feature است؛ `maxCompanies` در بدنهٔ Issue نیست. PRهای #534 (`EntitlementPolicyEngine`) و #539 در main هستند.
- `EntitlementPolicyEngine` (`Licensing.Contracts/EntitlementPolicyEngine.cs`) توابع `EvaluateCompany`، `EvaluateModule`، `EvaluateFeature`، `EvaluateSeatCapacity`، `EvaluateLegacySubsystem` را دارد؛ **ارزیابی سقف `maxCompanies` ندارد**. در آزمون‌ها `maxCompanies` فقط در داده‌ی تست (`EntitlementPolicyEngineTests.cs` خط ۲۲) و اعتبارسنجی ساختاری (`CompanyEntitlementContractTests.cs` خط ۲۷۱) دیده شد.
- **اتصال runtime انجام نشده:** `ICustomerLicenseRuntimeEvaluator` در `Program.cs` (خط ۲۵۱) ثبت شده، ولی هیچ فراخوانی در `Server/**` ندارد. `docs/PROJECT_STATUS.md` (سطر ۱۵۳) و `docs/ROADMAP.md` (سطر ۲۱۵) هم همین را می‌گویند («اتصال runtime انجام نشده»).
- **نتیجه:** enforcement واقعی `maxCompanies` در سمت مشتری **وجود ندارد**؛ نه در #391 و نه جای دیگر.

### F4. مسیرهای ساخت Company
- **تنها مسیر غیرآزمونی:** `CustomerProvisioningApplyService.ApplyAsync` (`Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs`، سطر ~۲۸۷ `_database.Companies.Add`). بقیهٔ `Companies.Add` ها در `tests/**` و seed E2E (`tests/e2e/seed.mjs`) هستند.
- **اولین اجرا در `Program.cs`:** فقط راه‌اندازی دیتابیس (`/DatabaseSetup` با توکن یک‌بارمصرف) و Migration؛ هیچ ساخت Company یا User ندارد. `InfrastructureMasterBootstrap.cs` فقط Hash اعتبار Master را تولید می‌کند و جدول Users را Seed نمی‌کند (D-044 زمینه).
- **Apply از لایسنس مستقل است:** شناسهٔ نصب از `CustomerProvisioningSettings.InstallationId` (پیکربندی استقرار) می‌آید و با `payload.InstallationId` مقایسه می‌شود (`CustomerProvisioningPackageVerifier.cs` سطر ~۵۹)، نه از `InstallationIdentity`/`InstalledLicense`. هیچ بررسی `maxCompanies` یا وضعیت لایسنس در Apply نیست.
- **هم‌زمانی از قبل حل است:** Apply داخل تراکنش `Serializable` و قفل `sp_getapplock` (`faranou:provisioning:apply`) اجرا می‌شود (سطرهای ~۵۹، ۱۱۶، ۱۱۷، ۵۸۰). هر نقطهٔ enforcement جدید در همین مسیر از این قفل بهره می‌برد.
- **نویسندهٔ بدون Tenant:** Apply فعلی Company بدون `TenantId` می‌سازد. S3-0h (ردیف PR-D1) می‌گوید چنین نویسنده‌هایی باید پیش از Backfill متوقف یا Tenant-aware شوند (PR-6).

### F5. D-044 و حالت اعمال
- D-044 (`Proposed` / `NeedsApproval`): ایجاد اولین Company در حالت `Missing` مجاز است؛ سقف `maxCompanies` پس از وجود لایسنس اعمال می‌شود؛ `Licensing:EnforcementMode = Off|Observe|Enforce`.
- `EnforcementMode` در کد وجود ندارد (جست‌وجوی `.cs` و `.json` نتیجه‌ای نداد). پس سیاست «Observe» برای نصب‌های موجود پیاده‌سازی نشده است.
- تناقض متنی: `LICENSING_OPEN_DECISIONS.md` (سطر ۱۴۵ تا ۱۴۹) OD-14 را `Proposed` نگه داشته، در حالی که بند بعدی همان سند (سطر ~۱۹۳) bootstrap را تحت D-059 پذیرفته‌شده می‌داند. (این سند اصلاح نشد.)

### F6. تهدیدها (Threat Model و D-044)
| تهدید | کنترل مستند | وضعیت برای نگاشت `CompanyReference` |
|---|---|---|
| جعل `CompanyReference` | امضای payload و اعتبارسنجی canonical (`LICENSING_THREAT_MODEL.md` سطر ۲۰۶) | کنترل قرارداد هست؛ **اعمال نگاشت** در سند به #391 داده شده ولی Issue #392 مالک آن است (ناهماهنگی مستند) |
| Replay بستهٔ Provisioning | `RequestId`/`Nonce` یکتا در DB (`ProvisioningRequestClaimConfiguration.cs` سطرهای ۲۶ و ۲۷) | جدول Claim داخل همان دیتابیس مشتری است |
| Replay/downgrade Amendment | sequence افزایشی (T39، سطر ۱۷۹) | ذخیره و مقایسهٔ اتمیک sequence در مشتری هنوز انجام نشده |
| Restore دیتابیس به وضعیت قدیمی | T34 (سطر ۱۶۷)، anti-rollback در #364 (باز) | **استنباط (نه شاهد مستقیم):** با Restore، ردیف‌های Claim و هر جدول نگاشت جدید هم به عقب برمی‌گردند؛ ممکن است بستهٔ قبلاً اعمال‌شده دوباره قابل اعمال شود. سند موجود این حالت را برای Claim یا نگاشت صراحتاً بررسی نکرده است |
| Clone کامل VM/میزبان | T6 «قابل جلوگیری نیست؛ پذیرفته‌شده» (سطر ۱۷)، T32 (سطر ۱۶۵) | همان `CompanyReference` در دو نسخهٔ کپی استفاده می‌شود؛ Vendor فقط با sync (#394، باز) می‌تواند ببیند |
| استفاده از لایسنس نصب دیگر | `installationId` و اثرانگشت کلید (T3/T17) | Apply از پیکربندی، نه هویت رمزنگاری، استفاده می‌کند (F4) |
| درخواست آفلاین تکراری/منقضی | T49 (سطر ۱۹۳) | وابسته به #394 |

### F7. ترتیب وابستگی PRها و Issueها
- **ترتیب D-059** (`DECISIONS.md` بخش «ترتیب پیاده‌سازی مصوب»): #395 → #390 → #391 و #392 → #393 → #394.
- **S3-0h** (`ISSUE-397-s3-0h-…` جدول PR): PR-4 (`Tenants` + `Companies.TenantId` nullable) ← PR-5 (ثبت Tenant اپراتوری و Backfill dry-run) ← PR-6 (Provisioning آگاه از Tenant) ← PR-D1 (Backfill). گیت‌ها: G3، G4 برای PR-4؛ G5 برای Backfill.
- **ROADMAP** (سطرهای ۲۰۷ تا ۲۲۱): #392، #228، #384، #401 فقط فعال‌سازی Production را مسدود می‌کنند، نه توسعهٔ S2/S3.
- **تعارض‌های شناسایی‌شده:**
  1. D-059 می‌گوید #392 پیش از #393؛ اما Threat Model (سطر ۲۰۳) صدور v2 را به #393 می‌دهد و #392 برای نگاشت به مرجع امضاشده نیاز دارد (چرخهٔ وابستگی احتمالی؛ F2).
  2. #392 پیش از PR-6 هر نویسندهٔ Company را Tenant-agnostic نگه می‌دارد؛ با D-089 («Company فقط داخل Tenant موجود») و PR-6 هم‌زمان نمی‌شود.
  3. بدنهٔ #396 می‌گوید «Company records are local accounting tenants»؛ این عبارت با D-087 (Company ≠ Tenant) هم‌خوان نیست (اصطلاح؛ Issue تغییر نکرد).
  4. Apply به `InstalledLicense` وابسته نیست، پس هر enforcement واقعی به اتصال runtime #384/#391 نیاز دارد (هر دو در عمل ناتمام).

### F8. آیا گزینهٔ «د-مرحلهٔ ۲» (enforcement بدون Schema، Tenant و `CompanyReference`) درست و امن است؟
**پاسخ: فقط به‌صورت محدود و مشروط؛ نه به‌عنوان جایگزین نگاشت.**
- **ممکن است:** شمارش `Companies` و مقایسه با `maxCompanies` لایسنس جاری داخل `ApplyAsync` و زیر همان قفل و تراکنش Serializable. `maxCompanies` در payload v1 و v2 هر دو هست. بدون Schema.
- **صحت فقط در شرایط زیر:**
  1. نصب Dedicated/Offline با یک Tenant (دیتابیس = نصب). در Shared، معنای «سقف نصب» نسبت به Tenant تعریف نشده (D-093 Proposed)؛ باید fail-closed یا خارج از محدوده بماند.
  2. تعریف مالک: آیا Companyهای غیرفعال و Companyهای قدیمی می‌شمارند؟ نصب‌هایی که الان بیشتر از سقف Company دارند چه می‌شوند (D-059 بند ۶: ایجاد جدید مسدود، داده حفظ)؟
  3. سیاست `Missing`/`Observe` (D-044) تصمیم‌نشده و `EnforcementMode` وجود ندارد.
  4. خواندن لایسنس جاری نیازمند اتصال runtime (`ICustomerLicenseRuntimeEvaluator`) است که هنوز به هیچ مسیر وصل نیست.
- **ناکافی است برای:** اتصال Company تازه به entitlement خودش، جلوگیری از ارث‌بری دسترسی، یا یکتایی نگاشت. پس باید هیچ module/feature/seat به Company تازه نسبت ندهد (طبق Issue).
- **ریسک:** ایجاد حس امنیت کاذب (سقف شمارشی در حالی که entitlement جدا نیست) و نیاز به بازنگری با ورود Tenant.

## مقایسهٔ گزینه‌ها (همه `Proposed`؛ بدون تصمیم)

| معیار | الف) ستون یکتای `Companies.CompanyReference` (nullable) | ب) افزودن ستون به `ProvisioningRequestClaim` | ج) جدول `CompanySetupRecord` کامل | د) فازبندی: قرارداد Vendor ← enforcement شمارشی بدون Schema ← نگاشت پس از تأیید |
|---|---|---|---|---|
| **هزینه** | کم (یک ستون، یک Index، تست) | کم | بالا (Entity، Configuration، FK، Index، Migration، تست) | متوسط، پخش‌شده در چند PR |
| **پیچیدگی** | کم؛ اتمیک با درج Company؛ سابقه: `CustomerBoundaryId` (D-038) | کم | متوسط؛ دو محل برای Company↔Installation (با Claim هم‌پوشان) | بالا از نظر هماهنگی؛ هر PR ساده |
| **ریسک** | شناسهٔ لایسنس وارد جدول حسابداری می‌شود؛ چرخه‌عمر و ممیزی مستقل ندارد؛ Index فیلترشده با `sp_rename` تداخل دارد (یافتهٔ D1b در PR-D) | فقط شرکت‌های Provisioning؛ برای «Company اضافه» بدون Provisioning کافی نیست | بیشترین انعطاف؛ ریسک افزایش سطح و تکرار مسئولیت؛ ستون `InstallationId` تا حدی زائد | شمارش بدون نگاشت حس امنیت کاذب می‌دهد؛ نیاز به بازنگری با Tenant |
| **وابستگی** | Schema مشتری (G5، مجوز جدا)، قرارداد v5، تولید مرجع در Vendor (F2)، D-089 | همان و Claim | همان به‌علاوهٔ طبقه‌بندی (نصب‌محور یا Tenant-owned) | مرحلهٔ ۱: Vendor و قرارداد؛ مرحلهٔ ۲: اتصال runtime (#384/#391)، تصمیم شمارش و D-044؛ مرحلهٔ ۳: یکی از الف/ج |
| **پوشش Issue #392** | بخشی (بدون lifecycle/audit timestamp؛ Issue «معادل مورد تأیید» را مجاز می‌کند) | ناقص | کامل | کامل در پایان |
| **اثر بر Tenant (D-087/D-089)** | ستون روی Company؛ با افزودن `TenantId` در آینده بی‌تعارض | نیاز به بازنگری | باید تعیین شود Tenant ستون داشته باشد یا نه | مرحلهٔ ۳ پس از PR-4/PR-6 ایمن‌تر |

**ترجیح فنی اولیهٔ این بررسی (غیرتصمیمی):** د برای ترتیب‌دهی؛ در مرحلهٔ ۳، الف اگر مالک به lifecycle/ممیزی جدا نیاز ندارد، وگرنه ج. ب به‌تنهایی کافی نیست.

## موارد بدون شواهد کافی
- قالب دقیق `IdPattern` و اینکه آیا برای مرجع تصادفی/غیرقابل‌حدس طول و منبع تصادف کافی تعریف شده است (فقط وجود الگو دیده شد، محتوای regex بازنگری نشد).
- وجود یا نبود Unique روی `ProvisioningRequestClaim.CompanyId` (فقط FK و Indexهای فهرست‌شده دیده شد).
- رفتار Restore و Clone برای Claim و نگاشت: فقط استنباط است (F6)؛ سناریوی آزمایش‌شده‌ای وجود ندارد.
- رویهٔ «مدیر مجاز مشتری» برای ساخت Company (مدیر نصب D-088 یا مدیر شرکت؟) در Issue و ADRها تعریف نشده است (D-064 تفکیک را می‌گوید، ولی تصمیم برای این قابلیت نیست).
- اینکه Vendor نگاشت `CompanyReference` را در کدام مدل و با کدام Issue نگه دارد (مالک ندارد؛ F2).
- محتوای کامل D-053 و `LICENSING_WORKFLOWS.md` بخش‌های ۱۱–۱۲ بازخوانی نشد.
- رفتار واقعی Production روی نصب با Companyهای بیش از سقف: آزمایش نشد.

## تصمیم‌های نیازمند تأیید مالک
1. مالک تولید و نگهداری `CompanyReference` در Vendor و Issue مربوط (#393، #228 یا جدا).
2. نسخهٔ جدید قرارداد Provisioning (v5 یا معادل) و رابطهٔ آن با Payload v2.
3. انتخاب بین الف، ب، ج، د (یا ترکیب) و طبقه‌بندی (نصب‌محور یا Tenant-owned).
4. معنای شمارش `maxCompanies` (غیرفعال‌ها و Companyهای قدیمی) و رفتار نصب‌های دارای شرکت بیش از سقف.
5. سیاست `Missing`/`Observe`/`Enforce` (D-044) و محدودهٔ آن به Dedicated/Offline.
6. تعریف «مدیر مجاز مشتری» برای ساخت Company.
7. سیاست Restore/Clone برای نگاشت (وابسته به OD-06، #364).
8. ترتیب #392 نسبت به PR-4/PR-6 و ادامهٔ نویسندهٔ Tenant-agnostic تا PR-6.
9. اصلاح ناهماهنگی‌های مستند (Threat Model سطر ۲۰۶/۲۰۷، OD-14، عبارت #396) در PR مستند جدا.

## خلاصه تغییرات
- فقط افزودن همین فایل handoff.

## فایل‌های مهم تغییرکرده
- `docs/handoffs/ISSUE-392-company-reference-binding-evidence.md` — جدید.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. هیچ فایل تصمیم‌گیری، WORK_LOG یا INDEX تغییر نکرد (طبق محدودیت مأموریت).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build` / `dotnet test` | اجرا نشد | تغییر فقط مستندات است؛ کدی تغییر نکرد |
| `python3 scripts/check_static_assets.py` | اجرا نشد | بی‌ارتباط با تغییر مستندات |
| `git diff --check` | موفق | بدون خروجی |
| خواندن کد و Issueها (`gh api`، `grep`) | انجام شد | نتایج در بخش یافته‌ها؛ هیچ تست یا اجرای runtime انجام نشد |

## امنیت، داده و سازگاری
- بدون اثر بر امنیت، داده یا Migration. هیچ دیتابیس مشتری لمس نشد.

## ریسک‌ها و کارهای باقی‌مانده
- بدون ثبت تصمیم، #392 همچنان مسدود است؛ باید مالک Vendor و قرارداد ابتدا تعیین شود (F2، تصمیم‌های ۱ و ۲).

## شناسه‌های Git
- Branch: `docs/issue-392-company-reference-evidence`
- مبنا: `main@990e815`
- Pull Request: در گزارش نهایی ثبت می‌شود.
