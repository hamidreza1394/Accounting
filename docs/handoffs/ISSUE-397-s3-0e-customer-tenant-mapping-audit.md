# ممیزی S3-0e — منبع معتبر نگاشت Customer→Tenant و Company→Tenant

- **Issue:** [#397](https://github.com/hamidreza1394/Accounting/issues/397) — S3-0e «تعیین نگاشت داده‌های موجود به Tenant»
- **تاریخ:** 2026-11-17
- **مبنای ممیزی:** `main@755150929b211ac3b952a3c3a7f0856624b11f0d` (پس از Merge PR #699). شماره خط‌ها به همین SHA ثابت ارجاع دارند.
- **نوع کار:** فقط ممیزی و مستندسازی (read-only). هیچ کد، Entity، Schema، Migration، Backfill، API، Authorization یا داده‌ای تغییر نکرده و Build/Test اجرا نشده است (تغییر فقط مستنداتی است).
- **مکمل:** این گزارش، `ISSUE-397-s3-0e-tenant-company-ownership-audit.md` (ممیزی مالکیت 2026-11-14) را بازنویسی یا تکرار نمی‌کند؛ آن ممیزی مالکیت/چرخهٔ عمر بود و این گزارش روی «منبع معتبر نگاشت مشتری→Tenant» تمرکز دارد و شواهد آن را روی main جدید به‌روزرسانی می‌کند.
- **تصمیم‌های مبنای الزامی:** D-087 (`Accepted`)، D-088 (`Accepted`)، D-089 (`Accepted`؛ پیاده‌سازی `NotImplemented`)، D-037/D-038 (`Accepted`) و **D-103** (تصمیم تأییدشدهٔ مالک؛ متن ADR در PR باز [#700](https://github.com/hamidreza1394/Accounting/pull/700) است و هنوز در `main` ادغام نشده — طبق دستور مأموریت، D-103 در این گزارش به‌عنوان تصمیم تأییدشده رعایت شده و دوباره به‌عنوان سؤال باز مطرح نمی‌شود).
- **برچسب‌گذاری یافته‌ها:** `Confirmed` (اثبات با کد/دادهٔ واقعی) / `Partial` (بخشی اثبات‌شده) / `Unknown` (شواهد کافی نیست) / `Proposed` (پیشنهاد فنی، نه تصمیم تأییدشده).

## 1. خلاصه مدیریتی و نتیجهٔ اصلی

1. **نگاشت Company→Tenant از منبع معتبر قابل استخراج است — اما به‌صورت شرطی و پس از یک گام تصمیم مالک.** زنجیرهٔ امضاشدهٔ Vendor→Customer برای مشتری‌های Provisioning‌شده از نظر فنی کامل است: `CustomerReference` (≡ `VendorCustomer.CustomerNumber`) هویت مشتری را حمل می‌کند و `CompanyReference` (از `LicensePayload` v2، ذخیره در `CompanySetupRecords`) هر شرکت را به‌صورت یکتا و امضاشده به Company محلی متصل می‌کند (`Confirmed`). آنچه هنوز وجود ندارد، خودِ موجودیت Tenant و جدول نگاشت `CustomerReference → TenantId` است (D-089 `NotImplemented`؛ `Confirmed`). برای شرکت‌های Provisioning‌شده، مشتق Tenant از همین زنجیره «حدس» نیست؛ برای شرکت‌های قدیمیِ فاقد شواهد Provisioning، تنها مرجع مجاز Manifest تأییدشدهٔ مالک است (D-092 `Proposed`؛ طبق D-103 فقط Backfill یک‌باره) — `Proposed`.
2. **تمام ستون‌های `TenantId` موجود در Core/HR/Trade عملاً `Company.Id` هستند** و این قرارداد legacy هم در Snapshot مدل EF (FK `TenantId` → `Companies`) و هم در رفتار سرویس‌ها اثبات است (`Confirmed`).
3. **یکی کردن Company با Tenant در Shared DB چندمشتری از نظر امنیتی نامعتبر است و D-087/D-103 دقیقاً همین را رد کرده‌اند**؛ مدل هدف، Tenant = مرز CustomerAccount/فضای کاری و Company = شرکت عملیاتی داخل آن است.
4. **یک ریسک پایداری تازه در سمت Vendor کشف شد:** `CustomerNumber` (منبع `CustomerReference`) پس از ایجاد مشتری قابل ویرایش است (`VendorBusinessEndpoints.cs` Update، خط ۲۸۰..۳۱۳)؛ در حالی که `CompanyReference` در amendmentها تغییرناپذیر است. سیاست تغییر CustomerNumber پس از Provisioning باید پیش از S3 تصمیم‌گیری شود (`Confirmed` کد؛ اثر عملیاتی `NeedsApproval` مالک).
5. **دیتابیس واقعی در این ممیزی در دسترس نبود** (بخش ۹)؛ وضعیت داده فقط از شواهد تاریخی Farano-Local (2026-11-14، خروجی read-only اپراتور) استفاده شده و آن شواهد «تاریخی» می‌مانند و دوباره اعتبارسنجی نشده‌اند.
6. **نتیجه:** S3-0e قابل تکمیل است — به‌عنوان «تعیین منبع و طرح نگاشت» نه «اجرای نگاشت». اجرا (ساخت Tenant، Backfill) طبق گیت‌های موجود باید در S3-0g/S3-0h با تأییدهای جداگانه انجام شود.

## 2. مدل واقعی Customer در Vendor

| مورد | یافته | برچسب | شواهد (main@75515092) |
|---|---|---|---|
| موجودیت مشتری | `VendorCustomer`: `Id` (Guid)، `CustomerNumber`، `LegalName`، `DisplayName`، `Status`، RowVersion؛ رابطهٔ 1:n به `VendorInstallation` و `VendorContract` و `VendorLicense` | `Confirmed` | `Vendor/Vendor.Persistence/Domain/VendorBusinessEntities.cs:4-17` |
| یکتایی CustomerNumber | ایندکس یکتا روی `CustomerNumber` (MaxLength 32) | `Confirmed` | `Vendor/Vendor.Persistence/VendorDbContext.cs:71,78` |
| محل ساخته‌شدن CustomerReference در بستهٔ provisioning | هنگام صدور، `payload.CustomerReference` باید دقیقاً برابر `customer.CustomerNumber` باشد (Ordinal)؛ مغایرت → رد صدور | `Confirmed` | `Vendor/Vendor.Host/Signing/VendorProvisioningIssuanceService.cs:79` |
| ویرایش‌پذیری CustomerNumber پس از ایجاد | **قابل ویرایش است** — endpoint Update مشتری `item.CustomerNumber = number` را می‌نویسد (با بررسی یکتایی)؛ هیچ gaurd «پس از Provisioning تغییر نکند» وجود ندارد | `Confirmed` | `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs:280-313` (به‌ویژه ۲۸۷ و ۲۹۳) |
| هویت شرکت در Vendor | `CompanyReference` مقداری operator-entered و opaque است (الگوی `^[A-Za-z0-9._-]{8,MaxIdChars}$`)؛ در `LicensePayload` v2 به‌ازای هر شرکت در `companyEntitlements` (≤ `maxCompanies`) می‌آید؛ یکتا داخل payload؛ Vendor هیچ کدی آن را «نمی‌سازد» و در amendment فقط با برابری با مرجعِ امضاشدهٔ payload پذیرفته می‌شود | `Confirmed` | `Licensing.Contracts/StructuralValidation.cs:7,20-21,159-161`؛ `Licensing.Contracts/CompanyLicenseEntitlement.cs`؛ `Vendor/Vendor.Host/Signing/VendorLicensePayloadBuilder.cs`؛ `Vendor/Vendor.Host/Authorization/VendorLicenseCompanyDraft.cs:9,60-79,134`؛ `Vendor/Vendor.Host/Signing/VendorLicenseAmendmentPayloadBuilder.cs` |
| ماندگاری CompanyReference در Vendor | هیچ جدول مستقلی برای CompanyReference وجود ندارد؛ فقط داخل JSON بستهٔ امضاشدهٔ `SignedLicenseEnvelopes` (و دُraft گذرا در حین صدور) | `Confirmed` | `Vendor/Vendor.Persistence/VendorDbContext.cs:13-21` (DbSetها)؛ `VendorLicenseCompanyDraft.cs:195-201` |
| انتقال رابطه در provisioning و ACK | بستهٔ v5: `customerReference` + `companyReference` (اختیاری) + `installationId` + `requestId` + `nonce` + `payloadSha256`؛ ACK امضاشده: `customerReference`، `installationId`، `result`، `errorCode` (بدون companyReference — ACK در سطح درخواست است)؛ Vendor چهار کد رد شرکت‌محور (`provisioning_license_not_installed`، `provisioning_not_entitled_for_company`، `provisioning_company_reference_conflict`، `provisioning_company_capacity_exceeded`) + دو کد قدیمی را در allow-list می‌پذیرد (PR #699 ادغام‌شده) | `Confirmed` | `Licensing.Contracts/CustomerProvisioningPackage.cs:15,31,56,100`؛ `Licensing.Contracts/CustomerProvisioningAcknowledgement.cs:12-21`؛ `Vendor/Vendor.Host/Authorization/ProvisioningAcknowledgementStateRules.cs` |
| ثبت گروه شرکتی در Vendor | وجود ندارد — Vendor مفهوم CorporateGroup/مالکیت ندارد؛ گروه فقط در Accounting با `CustomerBoundaryId` تعریف می‌شود | `Confirmed` | جست‌وجوی `Group|Ownership` در `Vendor/**` بدون نتیجه؛ `VendorBusinessEntities.cs` |
| اثبات «چند Company متعلق به یک مشتری» | بله — از مسیر امضاشده: چند `CompanyEntitlement` با `CompanyReference`های متمایز در payload v2 یک مشتری، پس از Apply به چند `CompanySetupRecords` یکتا تبدیل می‌شود؛ نیازی به استنتاج از نام/دامنه/مرز نیست | `Confirmed` | بند 3 و `Server/Infrastructure/Provisioning/CustomerCompanyEntitlementResolver.cs:53` |
| اعتبارسنجی رابطه در سمت Accounting | بسته با `CustomerReference` ناهمسان با پیکربندی نصب fail-closed رد می‌شود؛ `CompanyReference` باید در entitlement لایسنس نصب‌شدهٔ جاری باشد؛ تکرار مرجع → `provisioning_company_reference_conflict` | `Confirmed` | `Server/Infrastructure/Provisioning/CustomerProvisioningPackageVerifier.cs:27,53`؛ `CustomerProvisioningApplyService.cs:218-231` |
| پایداری شناسه پس از تغییر نام/دامنه مشتری | `CompanyReference`: تغییرناپذیر در amendment (PR #680) → پایدار. `CustomerNumber`/`CustomerReference`: **قابل تغییر در Vendor**؛ سیاست تغییر پس از Provisioning ثبت نشده؛ `CompanySetupRecords` مقدار مرجع را verbatim نگه می‌دارد و پس از تغییر CustomerNumber، صدور بستهٔ جدید با مرجع قدیمی همچنان ممکن است ولی انسجام عملیاتی به سیاست Vendor وابسته است | `Partial` | بند بالا + `docs/ROADMAP.md` ردیف #680؛ تصمیم سیاست با مالک (`NeedsApproval`) |

## 3. مدل واقعی Company در Accounting

| مورد | یافته | برچسب | شواهد (main@75515092) |
|---|---|---|---|
| موجودیت Company | بدون `TenantId`؛ دارای `Name`، `NationalId` (یکتای سراسری filtered)، `IsActive`، `CustomerBoundaryId` (Guid? nullable، بدون FK) | `Confirmed` | `Domain/Company.cs`؛ `Infrastructure/Persistence/Configurations/CompanyConfiguration.cs` |
| FK ساختار legacy | در Snapshot مدل، ستون‌های نام‌دار `TenantId` جدول‌های Core/HR همگی FK به `Companies` هستند (مثال صریح: `UserSessions.Company` با `HasForeignKey("TenantId")`)؛ ۶۳ ارجاع `TenantId` در Snapshot Core | `Confirmed` | `Infrastructure/Persistence/Migrations/DatabaseContextModelSnapshot.cs:1910-1935,2000-2040` و مشابه |
| `CompanySetupRecords` (جدید #684) | نگاشت 1:1 امضاشدهٔ Vendor `CompanyReference` → Company؛ ایندکس یکتا روی `CompanyId` و روی `CompanyReference` (Collation `Latin1_General_BIN2`)؛ FK با `Restrict` | `Confirmed` | `Domain/CompanySetupRecord.cs:25-37`؛ `Infrastructure/Persistence/Configurations/CompanySetupRecordConfiguration.cs:33-39`؛ Migration `20261116070805_AddCompanySetupRecords` |
| `ProvisioningRequestClaim` | شناسه‌های idempotency + `CustomerReference`، `InstallationId`، `CompanyId`، `FiscalPeriodId`، `AdminUserId`، `CompanyType`، `ParentCompanyId?` | `Confirmed` | `Domain/ProvisioningRequestClaim.cs:7-33` |
| مسیر ایجاد Company | **تنها مسیر تولیدی:** `CustomerProvisioningApplyService.ApplyAsync` — `new Domain.Company { CustomerBoundaryId = _settings.CustomerBoundaryId, IsActive = true }` و `_database.Companies.Add(company)`؛ در v5 همراه `CompanySetupRecord` در یک `SaveChanges` اتمیک داخل تراکنش `Serializable` + `sp_getapplock` | `Confirmed` | `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs:201-206,243-250,339-346` |
| مسیرهای دیگر ایجاد Company / تغییر مالکیت | هیچ مسیر موازی در `Server/**`، `Infrastructure/**`، `Modules/**` یافت نشد؛ ویرایش/غیرفعال‌سازی/حذف Company در کد پیاده نشده؛ تنها نوشتنِ پس از ایجاد، تخصیص `CustomerBoundaryId` از endpoint اپراتوری است | `Confirmed` (با محدودیت روش جست‌وجو) | جست‌وجوی `Companies.Add|new Domain.Company` در کد تولیدی؛ و ممیزی قبلی §5.3 (`ISSUE-397-s3-0e-tenant-company-ownership-audit.md`) |
| ترتیب enforcement در Apply v5 | Verifier → Claim/Replay → بررسی‌های v1-v4 (والد، مدیر اولیه) → Entitlement → Duplicate → Capacity → ایجاد Company + Record؛ خطاها با کدهای ثابت و ACK رد قابل بازپخش | `Confirmed` | `CustomerProvisioningApplyService.cs:181-250`؛ `docs/handoffs/ISSUE-392-customer-implementation-runbook.md` §۱ و §۳ |

## 4. جدول شواهد مسیرهای Provisioning و نگاشت (خلاصهٔ اجرایی)

| گام | جزء | فایل/عضو | نقش در نگاشت |
|---|---|---|---|
| ۱ | صدور بستهٔ v5 با `companyReference` | `VendorProvisioningIssuanceService.cs:79` + Gate دوکلیدی (بسته؛ `CustomerConsumerReady=false`) | `customerReference ≡ CustomerNumber`؛ صدور عملیاتی خاموش است |
| ۲ | اعتبارسنجی بسته در مشتری | `CustomerProvisioningPackageVerifier.cs:27,53` | `CustomerReference` باید با `Provisioning:CustomerReference` پیکربندی نصب برابر باشد (fail-closed) |
| ۳ | قفل و idempotency | `CustomerProvisioningApplyService.cs` (`sp_getapplock`، Claim یکتا) | Replay بایت‌به‌بایت همان ACK؛ بدون اثر جانبی |
| ۴ | entitlement شرکت | `CustomerCompanyEntitlementResolver.cs:53,67` | مرجع ∈ `companyEntitlements` لایسنس نصب‌شده؛ `MaxCompanies` = تعداد کل شرکت‌های نصب (block-new) |
| ۵ | یکتایی مرجع | `CustomerProvisioningApplyService.cs:228-231` + ایندکس یکتای BIN2 | تکرار `CompanyReference` → `provisioning_company_reference_conflict` |
| ۶ | ایجاد Company + Record اتمیک | `CustomerProvisioningApplyService.cs:201-206,243-250,339-346` | تنها محل تولید رابطهٔ امضاشدهٔ CompanyReference→Company |
| ۷ | ACK و وضعیت Vendor | `ProvisioningAcknowledgementStateRules.cs` (پس از #699) | چهار کد رد شرکت‌محور ACK را به `Failed` می‌برند |

## 5. جدول رابطه‌های موجود: CustomerReference، CompanyId، CustomerBoundaryId، TenantId

| رابطه | منبع فعلی در کد | یکتایی/قید | برچسب |
|---|---|---|---|
| `CustomerReference` (Vendor) ↔ `VendorCustomer` | برابری اجباری با `CustomerNumber` هنگام صدور بسته؛ ایندکس یکتای CustomerNumber | یکتا در Vendor DB؛ ولی مقدار پس از ایجاد قابل تغییر است | `Confirmed` (قید) + `Unknown` (سیاست تغییر) |
| `CustomerReference` ↔ نصب مشتری | مقایسه با `Provisioning:CustomerReference` در Verifier و در ParentClaimResolver (`claim.CustomerReference == settings.CustomerReference`) | یک مقدار به‌ازای هر نصب (پیکربندی استقرار) | `Confirmed` |
| `CustomerReference` ↔ Company (محلی) | مستقیم وجود ندارد؛ فقط ثبت تاریخی در `ProvisioningRequestClaims.CustomerReference` و برابری در Replay (خط ۱۴۵) | — | `Confirmed` (نبود) |
| `CompanyReference` ↔ `Company` | `CompanySetupRecords` (1:1، unique BIN2، FK Restrict) | یکتا؛ تنها برای بسته‌های v5 شرکت‌محور؛ بسته‌های v1-v4 بدون Record می‌سازند | `Confirmed` |
| `CustomerBoundaryId` ↔ `Company` | ستون nullable بدون FK؛ مقدار از پیکربندی سطح نصب در هنگام Apply؛ تخصیص دستی فقط endpoint اپراتوری (1..500، 409 برای مرز متفاوت)؛ محدودکنندهٔ گروه/مالکیت/کشف عضویت | غیرتهی تغییر نمی‌کند؛ «null = Provision‌نشده = Fail-closed» | `Confirmed`؛ و D-087 §8/D-103: **منبع Tenant نیست** |
| `TenantId` ↔ `Company.Id` (legacy) | FK ساختاری در Snapshot + تفسیر سرویس‌ها (بند ۷) | در همهٔ نویسنده‌های فعلی `= company.Id` | `Confirmed` (میراثی) |
| `Tenant` (موجودیت مستقل) | وجود ندارد — هیچ Entity/جدول Tenant در Core/Trade نیست | — | `Confirmed` (نبود) |
| `CustomerReference` → `TenantId` (هدف D-089) | هیچ جدول نگاشتی وجود ندارد؛ D-089: یک‌به‌یک، یکتا، فقط مدیر نصب | — | `Confirmed` (NotImplemented) |

## 6. تفاوت دادهٔ موجود با مدل هدف D-087 و D-103

| بعد | وضعیت موجود (`Confirmed`) | مدل هدف D-087/D-103 |
|---|---|---|
| Tenant | موجودیت ندارد؛ `TenantId` = `Company.Id` | Tenant = مرز CustomerAccount/فضای کاری؛ شناسهٔ مستقل؛ یک Tenant چند Company |
| Company | Company خودش مرز Tenant عملی است | شرکت عملیاتی داخل Tenant؛ استقلال دفاتر/اسناد/مجوزها حفظ می‌شود |
| چند Company در یک مشتری | از مسیر Provisioning قابل تشخیص است (چند CompanySetupRecord برای مشتری واحد) | باید داخل Tenant مشترک نگه داشته شود؛ اشتراک Tenant مانع استقلال حسابداری Company نیست |
| مشتری مستقل | در Shared DB امروز هیچ مرزبندی بین مشتری‌ها روی Companyها نیست (یک `CustomerBoundaryId` برای کل نصب از پیکربندی) | هر مشتری مستقل = Tenant مستقل؛ اتصال در Cloud فقط با Provisioning معتبر Vendor + Registry محلی |
| دامنه/نام تجاری | استفادهٔ کارکردی از `CustomerBoundaryId` برای کشف عضویت (`CompanyMembershipRequestEndpoints.cs:88-100`) | دامنه فقط lookup key است و مجوز نیست؛ اشتراک دامنه/برند/مدیر دلیل ادغام Tenant نیست |
| CorporateGroup/مالکیت | داخل یک `CustomerBoundaryId` محدود می‌شود (`CorporateGroupService.cs:146-147,429-430`) | نباید از مرز CustomerAccount عبور کند؛ گروه‌های یک مشتری می‌توانند Tenant مشترک داشته باشند |
| Backfill | — | Manifest D-092 فقط Backfill یک‌باره؛ شناسایی Tenant در زمان ورود از Registry محلی انجام می‌شود، نه Manifest |

## 7. مواردی که نگاشت‌شان با شواهد قطعی قابل تعیین است (`Confirmed`)

1. **شرکت‌های Provisioning‌شدهٔ v5 شرکت‌محور:** نگاشت `CompanyReference → Company` با `CompanySetupRecords` یکتا ثبت شده و `CustomerReference` بسته در همان Claim ثبت است؛ لذا Company→مشتری (Vendor) برای این شرکت‌ها از زنجیرهٔ امضاشده اثبات‌پذیر است.
2. **مشتری نصب در Dedicated/تک‌مشتری:** `CustomerReference` نصب یک مقدار پیکربندی است و کل نصب به یک مشتری Vendor تعلق دارد؛ نگاشت «تمام Companyهای نصب → یک Tenant» از نظر منطقی یک‌به‌یک است (تصمیم شکل جدول با S3؛ اصل از D-089 §۶: نصب اختصاصی دقیقاً یک Tenant در Bootstrap).
3. **شرکت‌های legacy بدون Record:** در نصب تک‌مشتری، همهٔ Companyهای موجود به مشتری همان نصب تعلق دارند — نگاشت Tenant آن‌ها بدون حدس قابل تعیین است (Tenant همان Tenant یکتای نصب).
4. **نبود Tenant و نبود هر مسیر استنتاج مجاز:** از `CustomerBoundaryId`، گروه، نام یا مالکیت نباید Tenant ساخته شود (D-087 §۹، D-089 §۷؛ `OwnershipScopeArchitectureTests.NoSourceFile_UsesCustomerBoundaryIdAsTheTenantBoundary` این قاعده را به‌صورت معماری پوشش می‌دهد).

## 8. موارد مبهم یا فاقد منبع معتبر

| مورد | وضعیت | برچسب |
|---|---|---|
| گروه‌بندی Companyهای یک مشتریِ چندشرکتی در یک Tenant مشترک | D-103 مجازش کرده اما لیست رسمی «کدام Companyها در یک Tenant» برای هر مشتری در کد/داده وجود ندارد؛ برای مشتری‌هایی که همهٔ شرکت‌هایشان با Provisioning v5 آمده‌اند، از `CompanySetupRecords + CustomerReference` قابل مشتق است؛ برای Companyهای قدیمی نصب‌های Shared نیست | `Partial` |
| سیاست تغییر `CustomerNumber` پس از Provisioning | کد اجازهٔ تغییر می‌دهد؛ هیچ سند/ADR سیاستی وجود ندارد؛ اثر بر بسته‌های آینده و انسجام `CompanySetupRecords`/Claimهای قدیمی باید تعیین شود | `Unknown` → تصمیم مالک |
| وضعیت واقعی داده‌ها (تعداد Companyها، تکراری بودن `CustomerBoundaryId`، وضعیت ستون‌های `TenantId`) | دیتابیس واقعی در این ممیزی در دسترس نبود؛ گزارش Farano-Local فقط شاهد تاریخی 2026-11-14 است و timestamp آن (2026-11-18) از قبل تأییدنشده مانده | `Unknown` |
| اعمال Migrationهای #684 روی دیتابیس‌های واقعی | Migration `20261116070805_AddCompanySetupRecords` در مخزن است؛ اجرای آن روی هیچ دیتابیس مشتری اثبات نشده؛ خروجی Farano-Local (۱۶ Migration تا `AddUserSessions`) قبل از آن است | `Unknown` |
| اتصال Companyهایlegacy به مشتری‌های متعدد در نصب Shared واقعی | هیچ نصب Shared چندمشتری واقعی در شواهد نیست؛ R-3 ممیزی قبلی (`CustomerBoundaryId` یکتای نصب برای همهٔ Companyها) همچنان برقرار است | `Unknown` |
| ماندگاری `CompanyReference` در دو نسخهٔ Clone/Restore (T6/T32) | رفتار fail-closed ثبت شده؛ سناریوی آزمایش‌شدهٔ اختصاصی برای جدول نگاشت وجود ندارد (Runbook §۵) | `Partial` |

## 9. بررسی داده‌های موجود و محدودیت دقیق آن

- **دسترسی دیتابیس:** در این اجرا هیچ اتصال مجاز read-only به هیچ دیتابیس مشتری/تولید در دسترس نبود و هیچ داده‌ای خوانده یا تغییر داده نشد. طبق دستور مأموریت، از Source Code نتیجهٔ «قطعی داده‌ای» گرفته نشد و همهٔ گزاره‌های داده‌محور در بخش‌های بالا برچسب `Unknown` دارند.
- **استفاده از گزارش Farano-Local فقط به‌عنوان شاهد تاریخی:** خروجی read-only اپراتور برای `Farano-Local` (ثبت‌شده 2026-11-14 در `PROJECT_STATUS.md` بخش «دریافت شواهد زندهٔ SQL Server برای S3-0» و `ISSUE-397-s3-0-tenant-company-audit.md` §۱۲) نشان داده بود: SQL Server 2022 Developer؛ فقط Schema `dbo`؛ بدون جدول‌های `trade`؛ ۱۶ Migration Core تا `20261105094700_AddUserSessions`؛ **Companies فاقد TenantId** و FKهای چند جدول TenantId → Companies.Id؛ UserSessions بدون TenantId؛ timestamp خروجی 2026-11-18 (جلوتر از تاریخ ثبت؛ نیازمند تأیید ساعت). این شواهد دوباره اعتبارسنجی نشد و به‌عنوان وضعیت همان لحظهٔ همان دیتابیس ثبت می‌ماند؛ **نتیجهٔ این ممیزی به آن وابسته نیست** — یافته‌های ساختاری (نبود Tenant، legacy FK) مستقلاً از Source/Snapshot اثبات شده‌اند.

## 10. پیشنهاد نهایی منبع معتبر نگاشت Company→Tenant (`Proposed` — نیازمند تأیید مالک در S3-0g/S3-0h)

اصول: هیچ جدول/سرویس/شناسهٔ تکراری ساخته نشود؛ از موجودیت‌های امضاشدهٔ موجود استفاده شود؛ D-089 و D-103 بدون تغییر بمانند.

| نصب/حالت | منبع معتبر پیشنهادی نگاشت |
|---|---|
| **Cloud / Shared — Provisioning آینده** | زنجیرهٔ امضاشدهٔ Vendor: `CustomerReference` (≡`CustomerNumber` در صدور) → رکورد Tenant در Registry محلی که مدیر نصب می‌سازد (D-089 §۲؛ 1:1، یکتا)؛ سپس `CompanyReference` (از `LicensePayload` v2، ثبت در `CompanySetupRecords`) → Company داخل همان Tenant. Resolve در زمان ورود/اجرای عادی فقط از Registry محلی، نه تماس زندهٔ Vendor و نه Manifest (D-103). |
| **Cloud / Shared — شرکت‌های Provisioning‌شدهٔ موجود (v5)** | مشتق یک‌بارمصرف هدایت‌شده از `CompanySetupRecords ⟕ ProvisioningRequestClaims.CustomerReference` (هر دو در همان دیتابیس و حاصل بستهٔ امضاشده‌اند) به‌عنوان ورودی Manifest مالک؛ عدم پوشش یا تعارض → توقف کامل (منطق D-092). |
| **Cloud / Shared — شرکت‌های legacy فاقد شواهد** | فقط Manifest تأییدشدهٔ مالک (D-092، `Proposed`)؛ هیچ استنتاج از نام/دامنه/گروه/`CustomerBoundaryId` ممنوع. |
| **Dedicated / Offline** | Tenant اولیه در Bootstrap نصب ساخته می‌شود (D-089 §۶؛ عامل `Bootstrap` با ثبت حسابرسی) و **تک Tenant** نصب است؛ Provisioning معتبر Company جدید را فقط داخل همان Tenant می‌سازد؛ `TenantId` از پیکربندی معتبر Installation به دست می‌آید و با DB تطبیق داده می‌شود (D-103). |
| **گروه‌های شرکتی چندشرکتی یک مشتری** | Tenant مشترک فقط با لیست رسمی مالک در Manifest یا زنجیرهٔ Provisioning؛ عضویت گروه و مالکیت (`CorporateGroupService`) همچنان هیچ دسترسی داده‌ای نمی‌دهد و نباید از مرز CustomerAccount عبور کند (D-037/D-103). |

**نکتهٔ کلیدی انتساب (`Proposed`):** در مرحلهٔ گذار، ستون `Companies.TenantId` جدید (برنامهٔ PR-4 طرح S3-0h) باید از همین منبع پر شود و ستون‌های `TenantId` legacy فرزند در فاز A بدون تغییر معنا باقی بمانند (طرح دو مرحله‌ای A/B ممیزی مالکیت 2026-11-14 §۱۱؛ با D-090/D-091/D-095 `Proposed` هم‌راستاست). این گزارش هیچ Migration را طراحی یا اجرا نمی‌کند.

## 11. اثر تصمیم بر Cloud و Dedicated

- **Cloud/Shared:** نگاشت باید در سطح داده (Registry + FK مرکب) اعمال شود؛ دیتابیس مشترک به تنهایی هیچ جداسازی‌ای نمی‌دهد و «مدیر نصب» سراسری فعلی (`UserAdministrationEndpoints`/`SecurityAuditAdministrationEndpoints`) بدون فیلتر Tenant می‌خواند — پیش از ورود مشتری دوم، OD-1 (D-088) و فیلتر Tenant باید بسته شوند (ریسک R-1 ممیزی مالکیت؛ `Confirmed`). اتصال مشتری به Tenant فقط از Provisioning معتبر + Registry محلی؛ تماس زندهٔ Vendor در هر درخواست ممنوع (D-103).
- **Dedicated:** یک Tenant در Bootstrap؛ هزینهٔ نگاشت تقریباً صفر است؛ اما همان مدل منطقی و authorization برقرار می‌ماند و دیتابیس اختصاصی مجوز حذف TenantId یا دور زدن کنترل سمت سرور نیست (D-087 §۴). Trade/Migration host و سیاست `--include-trade` تغییری در این تصمیم نمی‌خواهد (D-094 `Proposed`).
- **قید یکتایی هویت:** Username/Email در سطح Identity Realm یکتای سراسری‌اند؛ Shared DB چند Tenant با تکرار هویت پشتیبانی نمی‌شود تا OD-2 تعیین شود (D-087 تغییرش نداده؛ `Confirmed`).

## 12. اثر بر شرکت‌های گروهی و شرکت‌های مستقل

- **شرکت‌های مستقل مشتری‌های متفاوت:** باید Tenantهای جدا بگیرند؛ اشتراک دامنه، برند، مدیر یا سهام‌دار دلیل ادغام نیست (D-103). از آنجا که `CustomerBoundaryId` فعلی از پیکربندی سطح نصب می‌آید (`CustomerProvisioningApplyService.cs:204`)، در یک Shared DB دو مشتریِ Provisioning‌شده امروز مرز یکسان می‌گیرند — پس از گذار، مرز درستی باید از Tenant/Registry بیاید و `CustomerBoundaryId` فقط محدودکنندهٔ روابط گروهی بماند (ریسک R-3؛ `Confirmed` کد، اثر عملیاتی با تصمیم مالک).
- **گروه شرکتی یک مشتری:** می‌تواند Tenant مشترک داشته باشد (D-103)؛ `CompanySetupRecords` برای هر شرکت مستقل است و استقلال حسابداری/دفاتر/مجوزها شرکت‌محور باقی می‌ماند؛ گزارش عملیاتی چندشرکتی همچنان تابع مجوز all-or-nothing است (D-063 §۴) و گزارش تلفیقی حسابداری جداگانه است (D-034).
- **مرز گروه/مالکیت:** `CorporateGroupService` و `OwnershipRelationship` نباید از مرز مشتری عبور کنند؛ شکل صحیح آینده، قید same-Tenant (D-087 §۶ / OD-9) است — شکل دقیق قید در S3-0g.

## 13. ریسک‌های امنیتی و جداسازی اطلاعات

| ID | ریسک | شدت Shared / Dedicated | شواهد | برچسب |
|---|---|---|---|---|
| M-1 | معنای دوگانهٔ `TenantId` پس از گذار: تغییر نام/داده بدون تغییر معنای کد، همهٔ L1-L13 ممیزی مالکیت را بدون خطای کامپایل غلط می‌کند | بالا / بالا | `CompanyAccessService.cs:108,212,245`؛ `WorkContextService.cs:65,262`؛ `TradeEntity.cs` (KDoc «The existing Accounting Company.Id»)؛ `CompanyMembershipRequestEndpoints.cs:113,123,267` | `Confirmed` |
| M-2 | تغییرپذیری `CustomerNumber` در Vendor → شکنندگی نگاشت CustomerReference پس از Provisioning | متوسط / متوسط | `VendorBusinessEndpoints.cs:287,293` | `Confirmed` (کد)؛ سیاست با مالک |
| M-3 | مدیر نصب سراسری بدون فیلتر Tenant در Shared DB | بالا / پایین (طراحی‌شده) | `UserAdministrationEndpoints`، `SecurityAuditAdministrationEndpoints` (ممیزی مالکیت R-1؛ در main فعلی بدون تغییر) | `Confirmed` |
| M-4 | یکتایی سراسری Username/Email و `NationalId` در Shared DB چند Tenant | متوسط / پایین | `UserConfiguration.cs`؛ `CompanyConfiguration.cs` (R-4) | `Confirmed` |
| M-5 | Restore/Clone: برگشتن ردیف‌های Claim/Record به عقب؛ همان بسته دوباره قابل Apply | متوسط / متوسط | Runbook #392 §۵؛ OD-06/#364 باز | `Partial` |
| M-6 | درج مستقیم SQL که مرز Tenant را دور می‌زند (همهٔ قیدها برنامه‌محورند) | متوسط / پایین | D-038 ریسک باقی‌مانده؛ D-090 محدودیت ثبت‌شده | `Confirmed` (طرح) |
| M-7 | اشتراک ناخواستهٔ Tenant دو مشتری در Backfill (خطای اپراتور/Manifest ناقص) | بالا / — | D-092: توقف کامل برای Manifest ناقص/اضافه/ناسازگار | `Confirmed` (کنترل در طرح) |

## 14. موارد نیازمند تأیید مالک پیش از Backfill

1. سیاست تغییر `CustomerNumber` پس از Provisioning (قفل‌کردن، ثبت ممیزی، یا اجازه با بازصدور) — M-2.
2. تأیید فهرست رسمی Company→Tenant برای هر نصب موجود (Manifest D-092) — از جمله `Farano-Local` (پیش‌نهاد فنی: تک‌مشتری → یک Tenant)؛ طبق D-103 Manifest فقط برای Backfill یک‌بارمصرف است.
3. پاسخ OD-2 (دامنهٔ یکتایی Username/Email در Shared DB) و OD-7 (`NationalId`) پیش از ورود مشتری دوم به Shared DB.
4. تأیید شکل جدول Registry/Tenants و ستون `Companies.TenantId` (PR-4 طرح S3-0h؛ D-089: «شکل جدول Tenants و نگاشت در S3 با تأیید جداگانهٔ Schema») و اینکه آیا Provisioning آینده شناسهٔ Tenant یا نسخهٔ نگاشت را در دادهٔ امضاشده حمل کند (شفاف‌سازی باز D-089).
5. تصمیم نهایی OD-13/D-092 (Manifest، `Proposed`) و OD-14/D-093 (Shared Runtime) و OD-11/D-090 و OD-12/D-091 و OD-15/D-095 و OD-16/D-096 و OD-17/D-094 — همگی `Proposed`.
6. گیت اجرا: Backup (G5)، اجرای Migration روی دیتابیس مشتری با مجوز جداگانه، و `dry-run` پیش‌فرض ابزار Backfill.

## 15. تست‌ها و شواهدی که پیش از Migration باید تهیه شوند

1. **مطابقت نگاشت با شواهد:** تست read-only که برای هر `CompanySetupRecords` رکورد، Claim متناظر با همان `CustomerReference/InstallationId` را بیابد و هر ناهم‌خوانی را گزارش کند (ورودی dry-run Manifest).
2. **دو Tenant مستقل در یک Shared DB:** تست جداسازی واقعی دو Tenant (امروز هیچ تستی دو Tenant مستقل ندارد — فقط دو Company؛ `Confirmed` در S3-0d-b §۱۳.۴) برای CompanyAccess، WorkContext، HR، Trade و مدیر نصب.
3. **نگاشت یکتا:** تلاش برای نگاشت دو Tenant به یک `CustomerReference` و دو Company به یک `CompanyReference` در جدول جدید → رد؛ و رد Company با Tenant ناسازگار (معیار پذیرش D-089/D-087).
4. **رفتار Provisioning پس از گذار:** بستهٔ v5 معتبر فقط داخل Tenant نگاشت‌شده شرکت می‌سازد؛ `CustomerReference` ناشناخته → رد صریح بدون ساخت Tenant/Company؛ شناسهٔ نصب ناهمسان → رد (معیار D-089).
5. **اکوسیستم legacy:** تست‌های `CompanyRoleMembersSqlServerTests`، `CorporateGroupSqlServerTests`، `DatabaseMigrationHostSqlServerTests` و تست‌های FK مرکب پس از تغییر معنا سبز بمانند؛ `TenantMigrationSqlServerValidationTests` (G3) برای Rename/NOT NULL در صورت تأیید D-091.
6. **شواهد دادهٔ واقعی:** اجرای مجدد خروجی read-only روی دیتابیس‌های واقعی (شامل بررسی اعمال/ناعمال بودن Migration `20261116070805` و مقدار ستون‌های `TenantId`) و رفع ابهام timestamp — پیش‌نیاز ورودی Manifest.
7. **Restore/Clone:** سناریوی آزمایش‌شدهٔ برگشت Claim/Record و رفتار fail-closed بستهٔ قبلاً اعمال‌شده (M-5).

## روش و محدودیت‌های این ممیزی

- فقط خواندن ایستای Source روی `main@75515092` + API/GitHub؛ هیچ Build/Test/dotnet و هیچ اتصال دیتابیس اجرا نشد؛ هیچ تستی اجرا یا Pass نشد.
- یافته‌های «نبود» (نبود Tenant، نبود مسیر دیگر ایجاد Company، نبود Group در Vendor) به الگوهای جست‌وجو محدودند؛ ممیزی مالکیت 2026-11-14 همین یافته‌ها را نیز مستقلاً ثبت کرده بود.
- متن D-103 از شاخهٔ PR #700 (`docs/handoffs/ISSUE-397-d103-tenant-customer-domain.md`، فقط‌خواندنی) استخراج شد؛ هیچ فایل متعلق به آن PR تغییر نکرد.
- `docs/WORK_LOG.md`، `docs/INDEX.md`، `docs/PROJECT_STATUS.md`، `docs/ROADMAP.md`، `docs/SECURITY.md`، `docs/DECISIONS.md` و `docs/ACCESS_CONTROL_ARCHITECTURE.md` عمداً تغییر نکردند، چون PR باز #700 روی همان فایل‌ها کار می‌کند و مأموریت، تغییر فایل‌های مشترک را ممنوع کرده است. هم‌ترازی آن‌ها پس از Merge #700 در یک PR مستنداتی جداگانه انجام شود.

## وضعیت Git

- **Branch:** `docs/issue-397-s3-0e-customer-tenant-mapping-audit` از `main@755150929b211ac3b952a3c3a7f0856624b11f0d`.
- **تغییر:** فقط افزودن همین فایل (`docs/handoffs/ISSUE-397-s3-0e-customer-tenant-mapping-audit.md`).
- **PR:** آدرس در گزارش نهایی مأموریت ثبت می‌شود؛ PR ادغام نمی‌شود (ادغام با مالک است).
