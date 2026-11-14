# ممیزی S3-0e — مالکیت داده و چرخه عمر Tenant/Company

- **Issue:** #397 — https://github.com/hamidreza1394/Accounting/issues/397
- **تاریخ:** 2026-11-14
- **نوع کار:** فقط ممیزی و مستندسازی (read-only)؛ هیچ کد، Migration، Schema، داده، Commit، Push یا PR ایجاد نشده است.
- **مبنای ممیزی:** `main@d81bb29dc686d582a57904b2fc5b05ec3b7f45d1` (پس از Merge PR #652) به‌علاوهٔ بخش ۱۳ گزارش S3-0d که فقط روی شاخهٔ `docs/s3-0d-data-path-inventory` (`d1e5623`) است و هنوز در `main` نیست.
- **وضعیت:** تکمیل‌شده با محدودیت‌های بخش ۲.۳ و ۱۴.
- **مرجع معماری:** D-087 (`Accepted`؛ پیاده‌سازی `NotImplemented`).

## 1. Executive Summary

1. **Tenant هنوز وجود ندارد.** در کد، `Domain/` و `Infrastructure/` هیچ Entity یا جدول Tenant ندارند و `Domain.Company` ستون `TenantId` ندارد. تمام ۸ Entity دارای `TenantId` غیرnullable (`FiscalPeriod`، `CompanyRole`، `UserCompanyMembership` و پنج Entity منابع انسانی)، `CompanyMembershipRequest.DestinationTenantId` و `TradeEntity.TenantId` در عمل `Company.Id` را نگه می‌دارند. (Confirmed)
2. **ایجاد Company فقط از یک مسیر تولیدی انجام می‌شود:** `CustomerProvisioningApplyService.ApplyAsync` (`Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs`). همان مسیر `TenantId = company.Id` را روی FiscalPeriod، CompanyRole و UserCompanyMembership می‌نویسد. هیچ مسیر موازی یا متناقضی برای ایجاد Company پیدا نشد؛ ایجاد Tenant در هیچ مسیری وجود ندارد. (Confirmed)
3. **ویرایش، غیرفعال‌سازی و حذف Company در کد پیاده‌سازی نشده است.** هیچ endpoint یا سرویسی که `Company.Name`، `NationalId` یا `IsActive` را تغییر دهد یا Company را حذف کند پیدا نشد (جست‌وجوی چندالگویی؛ Confirmed با محدودیت روش جست‌وجو). تنها نوشتن روی Company بعد از ایجاد، تخصیص `CustomerBoundaryId` توسط endpoint اپراتوری است.
4. **فاقد جداسازی Shared/Dedicated در کد:** مفهومی به نام Shared/Dedicated Database در کد اجرایی نیست؛ برنامه یک `DatabaseContext` (و یک `TradeDbContext`) با یک Connection String دارد (`Server/Program.cs`). انتخاب دیتابیس صرفاً پیکربندی نصب است؛ بنابراین شرط D-087 («همان مدل منطقی») از نظر معماری سازگار است، ولی هیچ‌چیز آن اثبات یا تست نشده است.
5. **ریسک اصلی جداسازی (Confirmed):** «مدیر نصب» (`User.Role.Name == Constants.Role.Admin`، نقش سراسری legacy) فهرست همهٔ کاربران، همهٔ شرکت‌ها و همهٔ رویدادهای امنیتی را بدون فیلتر Tenant می‌خواند. در نصب تک‌مشتری بخشی از طراحی است؛ در Shared Database چندمشتری، مرز Tenant را می‌شکند.
6. **CustomerBoundaryId در عمل نقش مرز مشتری را بازی می‌کند** (کشف مقصد درخواست عضویت، گروه‌ها) و مقدار آن هنگام ایجاد Company از یک تنظیم سطح نصب (`Provisioning:CustomerBoundaryId`) می‌آید. این با مفهوم D-087 بند ۸ سازگار نیست اگر به‌جای Tenant استفاده شود؛ باید تصمیم صریح گرفته شود (بخش ۱۰).
7. **منبع معتبر Company→Tenant در کد وجود ندارد.** نگاشت تنها از ورودی معتبر provisioning (که امروز Tenant ندارد) یا تصمیم مالک محصول ممکن است. هیچ نگاشت خودکار از `CustomerBoundaryId`، گروه یا مالکیت مجاز نیست.
8. **پیشنهاد گذار حداقلی** (بخش ۱۱): افزودن جدول `Tenants` و ستون `Companies.TenantId`، نگهداری `TenantId` موجود روی Entityهای فرزند با «معنای جدید = Tenant» فقط پس از یک نگاشت صریح، و افزودن `CompanyId` جدا به آن‌ها در گام‌های بعد؛ هیچ Backfill پیش از تصمیم‌های بخش ۱۰ ممکن نیست. این فقط طرح اولیه است.

## 2. Scope and Method

### 2.1 منابع خوانده‌شده
`AGENTS.md`، `CLAUDE.md`، `docs/INDEX.md`، `docs/DECISIONS.md` (D-087 به‌طور کامل)، `docs/PROJECT_STATUS.md` (بخش S3-0)، `docs/DATABASE.md`، `docs/SECURITY.md` (بندهای مرتبط) و گزارش `ISSUE-397-s3-0-tenant-company-audit.md` (نسخهٔ `main` و نسخهٔ شاخهٔ S3-0d شامل §12 شواهد SQL Server و §13 inventory مسیرهای داده). یافته‌های S3-0a تا S3-0d دوباره از ابتدا ممیزی نشدند؛ فقط با کد تأیید یا تکمیل شدند.

### 2.2 روش
جست‌وجوی ایستا و خواندن مستقیم کد روی clone سطحی (depth=1) از `main`. هیچ فرمان build/test/dotnet و هیچ اتصال به دیتابیس اجرا نشد.

### 2.3 تفاوت‌ها و محدودیت‌های مهم دستور کار با وضعیت مخزن
- **فایل `docs/handoffs/ISSUE-397-s3-0d-data-path-inventory.md` که در دستور کار آمده وجود ندارد.** نتایج S3-0d به‌صورت §13 داخل `ISSUE-397-s3-0-tenant-company-audit.md` روی شاخهٔ `docs/s3-0d-data-path-inventory` ثبت شده و در `main` نیست. این گزارش همان بخش را مبنا گرفت.
- **تعریف S3-0e متفاوت است:** `docs/PROJECT_STATUS.md` و §12.6 گزارش S3-0 عنوان S3-0e را «منبع معتبر نگاشت» می‌دانند؛ دستور کار این ممیزی آن را «ممیزی مالکیت و چرخه عمر» تعریف کرده است. پیش از ثبت وضعیت در PROJECT_STATUS باید نام‌گذاری یکدست شود.
- **کامپیوتر کاربر به bridge وصل نبود.** `git status` و تغییرات commit‌نشدهٔ درخت کاری خود شما بررسی نشد؛ وضعیت Git فقط مربوط به clone تمیز این session است (بخش ۱۴).
- ممیزی روی `main` است؛ شاخه‌های فعال (مثلاً `feat/issue-397-trade-module-boundary`) خوانده نشدند.

## 3. Entity Ownership Matrix

راهنما: **Confidence** = Confirmed (خواندن مستقیم کد/کانفیگ) / Partial / Unknown. «TenantId (فعلی)» یعنی معنای واقعی نه نام. دستهٔ هدف از D-087 و سایر ADRهای پذیرفته‌شده؛ تصمیم‌گیری نهایی نیست.

| Entity | Current Ownership | Target Ownership | TenantId (فعلی) | CompanyId (فعلی) | CustomerBoundaryId | Evidence | Confidence | Required Action |
|---|---|---|---|---|---|---|---|---|
| Company | ریشهٔ داده و عملاً مرز Tenant | Company-owned زیر Tenant | ندارد | `Id` خودش | ستون nullable، ایندکس غیریکتا | `Domain/Company.cs`؛ `Infrastructure/Persistence/Configurations/CompanyConfiguration.cs` | Confirmed | تغییر: `Company→Tenant` FK؛ تصمیم دربارهٔ `NationalId` یکتا |
| Tenant | وجود ندارد | Tenant-owned ریشه | — | — | — | جست‌وجوی `class Tenant` در کل ریپو بدون نتیجه | Confirmed | تغییر (Entity و جدول جدید در PR مستقل) |
| FiscalPeriod | Company-owned (از طریق `TenantId`=Company.Id) | Company-owned: `TenantId`+`CompanyId` | `= Company.Id` (FK به `Companies.Id`) | ندارد | — | `Domain/FiscalPeriod.cs`؛ `FiscalPeriodConfiguration.cs` | Confirmed | تغییر |
| CompanyRole | Company-owned (نقش مختص شرکت) | Company-owned (یا Tenant-owned؛ تصمیم باز OD-4) | `= Company.Id` | ندارد | — | `Domain/CompanyRole.cs`؛ `CompanyRoleConfiguration.cs` | Confirmed | تصمیم معماری + تغییر |
| CompanyRolePermission | وابسته به نقش | تابع CompanyRole | از طریق `RoleId` | ندارد | — | `DatabaseContext.CompanyRolePermissions` | Partial (کانفیگ FK مرکب خوانده نشد) | بدون تغییر مستقل |
| PermissionDefinition | Global/System (کاتالوگ مجوز) | Global/System | ندارد | ندارد | — | `DatabaseContext.PermissionDefinitions`؛ استفاده در `CompanyAccessService.GetCompaniesWithPermissionAsync` | Confirmed | بدون تغییر |
| UserCompanyMembership | رابطهٔ User(Realm)↔Company | Company-owned با Tenant | `= Company.Id` | ندارد | — | `Domain/UserCompanyMembership.cs` (`TenantId`، `Company`)؛ `UserCompanyMembershipConfiguration.cs` | Confirmed | تغییر |
| CompanyMembershipRequest | درخواست کاربر برای شرکت مقصد | Company-owned (مقصد) + Tenant مقصد | `DestinationTenantId = Company.Id` | ندارد | کشف مقصد با مرز مشترک | `Server/Api/CompanyMembershipRequestEndpoints.cs` سطرهای 88–123 | Confirmed | تغییر؛ تصمیم OD-3 |
| User | Identity Realm/نصب | Identity Realm | ندارد | `DefaultCompanyId` (ترجیح، بدون اعطای دسترسی) | — | `Domain/User.cs`؛ `UserConfiguration.cs` | Confirmed | بدون تغییر (تصمیم OD-2 درباره یکتایی) |
| UserSession | Identity Realm | Identity Realm | ندارد | ندارد | — | `Domain/UserSession.cs`؛ گزارش S3-0c-b §12.5 | Confirmed | بدون تغییر |
| LoginLog | Identity Realm | Identity Realm | ندارد | ندارد | — | `DatabaseContext.LoginLogs` | Partial (فیلدهای کامل خوانده نشد) | بدون تغییر / تأیید در S3-0f |
| Role / Permission / Page / PageCategory / MenuItem / ApplicationHandler (قالب legacy) | Global/System | Global/System | ندارند | ندارند | — | `DatabaseContext` DbSets | Partial (کاربردشان در مدل مجوز فعلی بررسی نشد؛ فقط `Role.Admin` برای مدیر نصب تأیید شد) | تصمیم: آیا نقش `Admin` سراسری باقی می‌ماند (OD-1) |
| SecurityAuditEvent | ترکیب system/company | باید نوع رویداد system/tenant/company داشته باشد | nullable؛ در بیشتر نویسنده‌ها `= companyId`؛ در برخی null | ندارد | — | `Domain/SecurityAuditEvent.cs`؛ نویسنده‌ها: `CompanyUserProvisioningEndpoints`، `HrOrganizationalStructureEndpoints`، `CompanyMembershipRequestEndpoints`، `CustomerBoundaryEndpoints` (بدون TenantId) | Confirmed | تصمیم OD-5 + تغییر |
| ProvisioningRequestClaim | رکورد idempotency سطح نصب | Installation/Tenant-level (تصمیم) | ندارد | `CompanyId`، `FiscalPeriodId`، `ParentCompanyId?` | — | `Domain/ProvisioningRequestClaim.cs`؛ `CustomerProvisioningApplyService` | Confirmed | تصمیم OD-6 |
| InstallationIdentity / InstalledLicense / CustomerOfflineRevocationState | Installation (Global برای این نصب) | Installation | ندارند | ندارند | — | `DatabaseContext` DbSets؛ `TpmAnchoredOfflineRevocationStateStore` | Partial (محتوای ستون‌ها خوانده نشد) | بدون تغییر؛ ارتباط با Tenant در طراحی لایسنس (خارج دامنه) |
| CorporateGroup | Company-owned (OwnerCompanyId) | Tenant-aware؛ گروه داخل مرز مجاز | ندارد | `OwnerCompanyId` | کنترل مرز مشترک | `Domain/CorporateGroups/CorporateGroup.cs` | Confirmed | تغییر؛ تصمیم OD-3 |
| CorporateGroupMember | Company عضو | همان | ندارد | `CompanyId` | کنترل مرز مشترک | `CorporateGroupMember.cs` | Confirmed | تغییر |
| OwnershipRelationship | بین دو Company | همان + same-Tenant | ندارد | `ParentCompanyId`، `SubsidiaryCompanyId` | کنترل مرز مشترک | `OwnershipRelationship.cs` | Confirmed | تغییر |
| ConsolidationProfile / ConsolidationRun | گروه‌محور | Tenant-aware | ندارد (فیلدها خوانده نشد) | — | — | `DatabaseContext` | Unknown | بازبینی در S3-0f |
| ConsolidationRunCompany | Company در اجرای تلفیق | `TenantId`+`CompanyId` | ندارد؛ FK مرکب به `FiscalPeriods(Id, TenantId)` | `CompanyId` | — | `Domain/CorporateGroups/Consolidation.cs`؛ `ConsolidationRunCompanyConfiguration.cs`؛ §12.4 | Confirmed | تغییر (وابسته به برابری Tenant=Company) |
| HR: CompanyOrganizationalUnit، CompanyPosition، CompanyEmployee، CompanyEmployeeAssignment، CompanyEmployeePhoto | Company-owned | `TenantId`+`CompanyId` | `= Company.Id` | ندارد | — | `Domain/Hr/*.cs`؛ `Infrastructure/Persistence/Configurations/CompanyEmployee*.cs`، `CompanyOrganizationalUnitConfiguration.cs`، `CompanyPositionConfiguration.cs` | Confirmed | تغییر |
| Trade: Party، PartyRoleAssignment، PartyBankAccount، Product، ProductIdentifier | Company-owned | `TenantId`+`CompanyId` | `= Company.Id` (`TradeEntity.TenantId`، KDoc: «The existing Accounting Company.Id») | ندارد | — | `Modules/Trade/Trade.Domain/Common/TradeEntity.cs`؛ `PartyStore`، `ProductStore`؛ `TradeAuthorizationAdapter` | Confirmed | تغییر |
| داده مرجع Trade (کشور، واحد) | Global/System، ثابت در کد | Global/System | — | — | — | `Server/Api/Trade/TradeReferenceEndpoints.cs`؛ `TradeReferenceData` | Confirmed (فقط کشور و واحد؛ سایر موارد V3.2 هنوز Entity نیستند) | بدون تغییر |
| Vendor: VendorCustomer، VendorInstallation، VendorContract، VendorLicense | سامانهٔ فروشنده (دیتابیس جدا؛ D-020) | خارج از Tenant/Company مشتری | ندارد | ندارد | — | `Vendor/Vendor.Persistence/Domain/VendorBusinessEntities.cs` (جست‌وجوی TenantId/CompanyId در Vendor بدون نتیجه) | Confirmed | بدون تغییر |
| فایل‌ها: عکس HR | Company-owned (BLOB در SQL) | Company-owned | `= Company.Id` | — | — | `CompanyEmployeePhoto`؛ `Server/Api/HrEmployeePhotoEndpoints.cs` | Confirmed | تغییر همراه HR |
| فایل‌ها: اسناد Trade | پیاده‌سازی نشده | Company-owned، باینری خارج SQL | — | — | — | S3-0d §13.3 | Partial (از گزارش، نه بازبینی مجدد) | NotImplemented |

نکته: Entityهای مرجع Trade V3.2 (Vessel، Currency، Port، …) هنوز در کد وجود ندارند؛ ماتریس آن‌ها پس از Entity شدنشان بسته می‌شود.

## 4. Tenant Lifecycle Findings

| مورد | یافته | Confidence |
|---|---|---|
| ایجاد Tenant | هیچ مسیر کدی ندارد؛ Entity/جدول Tenant وجود ندارد | Confirmed |
| ویرایش/حذف Tenant | وجود ندارد | Confirmed |
| «کدام سرویس مسئول ایجاد Tenant خواهد بود؟» | **تصمیم معماری لازم است (OD-6).** مسیر منطقی فعلی برای ورود Company، `CustomerProvisioningApplyService` است چون تنها نقطهٔ ایجاد Company است؛ ولی payload نسخهٔ فعلی (`CompanyReference`/Tenant) را ندارد (Issueهای #392/#228 بازند)؛ پس انتخاب بین «Provisioning بسته‌ٔ Vendor Tenant را بسازد» و «اپراتور نصب Tenant را جدا بسازد» با کد قابل اثبات نیست | Confirmed (نبود منبع) / تصمیم باز |
| Tenant در DatabaseSetup / نصب اولیه | `Server/Pages/DatabaseSetup.cshtml.cs` و `Server/Infrastructure/DatabaseSetup/*` هیچ ارجاعی به Company یا Tenant ندارند؛ فقط Connection String و حساب Master زیرساختی (در Configuration، نه جدول Users) را مدیریت می‌کنند | Confirmed |
| Shared vs Dedicated | در کد تمایزی نیست؛ همان `DatabaseContext` و `TradeDbContext` با یک Connection String؛ انتخاب در سطح استقرار | Confirmed |

## 5. Company Lifecycle Findings

### 5.1 ایجاد
- **تنها مسیر تولیدی:** `CustomerProvisioningApplyService` (حول سطر 193 `new Domain.Company { Name = payload.CompanyName, CustomerBoundaryId = _settings.CustomerBoundaryId, IsActive = true }` و سطر 287 `_database.Companies.Add(company)`). در همان تراکنش Serializable با قفل `sp_getapplock`، این‌ها ساخته می‌شوند: `Company`، `FiscalPeriod` (`TenantId = company.Id`)، `CompanyRole` «company-admin» (`TenantId = company.Id`)، `User` مدیر اولیه (رمز تصادفی غیرقابل‌استفاده؛ تغییر اجباری)، `UserCompanyMembership` (`TenantId = company.Id`)، `ProvisioningRequestClaim` و رویداد `ProvisioningApplied`. (Confirmed)
- جست‌وجوی `new Company`/`Companies.Add` در `Server`، `Infrastructure`، `Application`، `Domain`، `Modules`، `Vendor` فقط همین مسیر را در کد تولیدی نشان داد؛ بقیه موارد فقط در `tests/` هستند. هیچ seed (`HasData`) برای Company یا Tenant نیست. (Confirmed)
- **Company بدون Tenant معتبر:** چون Tenant وجود ندارد، هر Company موجود «بدون Tenant» است؛ این همان وضع legacy است و نه یک مسیر دور زدن. در مدل هدف، مسیر ایجاد باید Tenant معتبر را اجباری کند؛ امروز چنین قیدی نمی‌تواند وجود داشته باشد.
- **`CustomerBoundaryId` هنگام ایجاد از تنظیم سطح نصب می‌آید** (`CustomerProvisioningSettings.CustomerBoundaryId`، یک مقدار برای کل نصب)؛ بنابراین همهٔ Companyهای ایجادشده در یک نصب یک مرز مشترک می‌گیرند. در Shared Database با چند مشتری، این مقدار به‌تنهایی مرز مشتری را نمی‌سازد. endpoint `/operator/customer-boundaries/assign` برای Companyی که از قبل `CustomerBoundaryId` غیرخالی دارد، تخصیص مرز متفاوت را رد می‌کند (Conflict؛ بخش ۵.۳ و `Server/Api/CustomerBoundaryEndpoints.cs` سطرهای ≈243–252)؛ بنابراین جداسازی مرز پس از provisioning با این مسیر ممکن نیست و به رویهٔ جداگانهٔ بازبینی‌شده و حسابرسی‌شده نیاز دارد. هیچ OD موجود این رویه را پوشش نمی‌دهد (OD-1 دربارهٔ نقش `Admin` است) و تصمیم آن با مالک پروژه است. (Confirmed)

### 5.2 اتصال به Tenant
وجود ندارد (بخش ۴). هیچ FK Company→Tenant نیست.

### 5.3 ویرایش، غیرفعال‌سازی، حذف
- در جست‌وجوی الگو، هیچ endpoint، سرویس یا ExecuteUpdate/Remove برای تغییر `Name`، `NationalId`، `IsActive` یا حذف `Company` پیدا نشد (`Companies.Remove`، `ExecuteUpdate`، `ExecuteDelete`، انتساب `company.IsActive =` و مشابه). این جمله به همین فیلدها و الگوها محدود است و دربارهٔ `CustomerBoundaryId` صدق نمی‌کند (مورد بعد). اثبات کامل بودن همهٔ شکل‌های نوشتن (SQL خام، فیلدهای دیگر) **Unverified** است؛ جست‌وجوی تکمیلی `company.<Property> =` در کد تولیدی فقط انتساب `CustomerBoundaryId` را نشان داد. `IsActive` فقط **خوانده** می‌شود (مثلاً در `CompanyMembershipRequestEndpoints` و `CompanyAccessService`). (Confirmed با محدودیت روش: جست‌وجوی الگو، نه اجرای تست)
- تنها نوشتن پس از ایجاد: `CustomerBoundaryEndpoints.AssignWithinTransactionAsync` که `CustomerBoundaryId` را فقط برای حساب `Infrastructure` و با Anti-forgery می‌نویسد و تخصیص مجدد به مرز دیگر را رد می‌کند (سطر ≈245: Conflict). (Confirmed)
- نتیجه: چرخهٔ عمر «ویرایش/غیرفعال/حذف Company» در کد `NotImplemented` است و باید در طراحی گذار فرض شود.

### 5.4 مسیرهای موازی یا متناقض
مسیر موازی ایجاد Company پیدا نشد. تناقض کدی: همان رکورد Company را سه مفهوم می‌خواند (شرکت حقوقی، Tenant، مرز مشتری از طریق `CustomerBoundaryId`).

## 6. Data Access and Isolation Findings

جدول زیر مسیرهای بررسی‌شده در این ممیزی است (علاوه بر §13 گزارش S3-0d که تکرار نشد).

| مسیر | فیلتر Tenant | فیلتر Company | مجوز سمت سرور | می‌تواند داده Company/Tenant دیگر را برگرداند؟ | وابسته به Tenant=Company؟ | Evidence |
|---|---|---|---|---|---|---|
| `CompanyAccessService.FindAccessibleCompanyAsync/HasPermissionAsync/GetCompaniesWithPermissionAsync` | — (مفهومی ندارد) | بله، از `membership.TenantId` | بله، عضویت فعال + شرکت فعال + نقش فعال + مجوز از DB در هر درخواست | خیر در مدل فعلی (fail-closed) | **بله** | `Server/Infrastructure/Tenancy/CompanyAccessService.cs` |
| `CompanyUserDirectoryEndpoints.GetUsersAsync` | — | `membership.TenantId == companyId` | بله، `FindAccessibleCompanyAsync` + مجوز `users.list` | خیر | بله | `Server/Api/CompanyUserDirectoryEndpoints.cs` |
| HR endpoints (`HrOrganizationalStructureEndpoints`، `HrEmployeePhotoEndpoints`) | — | `TenantId == companyId` در همه queryها؛ نصب در URL با `AuthorizeHrScopeAsync` | بله | خیر | بله | `Server/Api/Hr*.cs` |
| `WorkContextService` | — | `period.TenantId == CompanyId` | بله (اعتبارسنجی مجدد در DB؛ کوکی فقط انتخاب) | خیر | بله | `Server/Infrastructure/Tenancy/WorkContextService.cs` |
| Trade endpoints (`/trade/*`) | `TenantId` (حامل `CompanyId`) | از طریق همان | بله؛ `TradeAccessFilter` روی کل گروه + `TradeGateMarker`؛ مجوز از `TradeAuthorizationAdapter` | خیر؛ `PartyStore`/`ProductStore` همهٔ queryها `TenantId` دارند | **بله** | `Server/Api/Trade/*`؛ `TradeAuthorizationAdapter`؛ `PartyStore` |
| `GET /companies` (`ApiEndpoints.GetCompaniesAsync`) | — | فقط شرکت‌های عضو کاربر | بله | خیر | بله | `Server/Api/ApiEndpoints.cs` سطر 79 |
| **`UserAdministrationEndpoints.GetUsersAsync/GetUserAsync`** | **ندارد** | **ندارد** | فقط «مدیر نصب» (`IsInstallationAdministratorAsync`: `User.Role.Name == Constants.Role.Admin`) | **بله، همهٔ کاربران و عضویت‌های همهٔ شرکت‌ها** (طراحی‌شده برای نصب تک‌مشتری) | — | `Server/Api/UserAdministrationEndpoints.cs` سطرهای 36–160، 532–546 |
| **`SecurityAuditAdministrationEndpoints` (list، export، `/admin/companies`)** | **ندارد** | فیلتر اختیاری `TenantId == companyId` | همان «مدیر نصب» | **بله، رویدادهای و فهرست همهٔ شرکت‌ها (CSV تا ۱۰٬۰۰۰ ردیف)** | بله (فیلتر روی `TenantId`) | `Server/Api/SecurityAuditAdministrationEndpoints.cs` سطرهای 58–134، 236–270، 278–330 |
| `AdminUserSessionEndpoints` | — | — | (بررسی نشد: فقط نقشه مسیرها) | Unknown | — | `Server/Api/AdminUserSessionEndpoints.cs` |
| `CompanyMembershipRequestEndpoints` (ایجاد و کشف) | مرز `CustomerBoundaryId` به‌عنوان قید کشف | `DestinationTenantId` | بله (کاربر باید عضو شرکتی با همان مرز باشد) | فقط شرکت‌هایی با همان `CustomerBoundaryId`؛ کشف با نام دقیق | بله + **استفادهٔ کارکردی از CustomerBoundaryId** | سطرهای 88–123، 245–270 |
| `CustomerBoundaryEndpoints` (اپراتور) | — | — | فقط `AccountKind=Infrastructure` + Anti-forgery + rate limit؛ 404 برای سایرین | خیر؛ فقط شناسه‌ها | — | `Server/Api/CustomerBoundaryEndpoints.cs` |
| Provisioning (Apply) | — | ایجاد Company | بسته امضاشده Vendor + تنظیمات نصب؛ مسیر مستقل از کاربر | — | بله | `CustomerProvisioningApplyService` |
| `Raw SQL` | — | — | — | مسیر عملیاتی بدون scope یافت نشد (`ExecuteSqlRawAsync` فقط `sp_getapplock`؛ `FromSqlRaw` فقط `TpmAnchoredOfflineRevocationStateStore`؛ `SqlCommand` فقط در `DatabaseConnectionSetupService`) | — | جست‌وجوی مستقیم |
| Background Jobs | — | — | — | در Core/Trade هیچ `BackgroundService` یافت نشد؛ فقط در Host مستقل Vendor | — | جست‌وجوی `BackgroundService|IHostedService` |
| Export / گزارش | — | — | فقط export رویداد امنیتی (بالا) و عکس HR (`TypedResults.File`، با Company scope) | هم‌چنین `CompanyRoleAdministrationEndpoints` فقط مجوزهای `.export` را فهرست می‌کند و فایل نمی‌دهد | — | `Server/Api/SecurityAuditAdministrationEndpoints.cs` سطر 423؛ `HrEmployeePhotoEndpoints` سطر 76 |

### 6.1 اعتماد به Client
در endpointهای بررسی‌شده، `companyId` از مسیر URL فقط یک «درخواست» است و با عضویت DB اعتبارسنجی می‌شود؛ `TenantId` از کلاینت پذیرفته نمی‌شود (در سازگاری با D-013). استثنا: `companyId` به‌عنوان فیلتر اختیاری گزارش امنیتی برای مدیر نصب (مجاز بر اساس نقش). برای `TradeAccessContext` مقدار از `WorkContext` اعتبارسنجی‌شدهٔ سمت سرور می‌آید. (Confirmed)

### 6.2 تست‌های موجود
فایل‌های `tests/Template.Tests/*` شامل موارد «شرکت دیگر/foreignCompany» برای HR، نقش‌ها، عضویت، گروه و Trade هستند (`HrOrganizationalStructureEndpointTests`، `CompanyMembershipRequestEndpointTests`، `TradeAuthorizationAdapterTests` و …). **هیچ‌کدام در این ممیزی اجرا نشد** و هیچ‌کدام مدل Tenant مستقل را پوشش نمی‌دهند؛ نتیجه‌ای «Pass» ادعا نمی‌شود.

## 7. Identity and Membership Findings

1. **User در سطح Identity Realm/نصب است، نه Tenant.** `User` فاقد `TenantId`/`CompanyId` است؛ فقط `DefaultCompanyId` دارد که «هرگز دسترسی نمی‌دهد» و FK با `SetNull` است. (Confirmed: `Domain/User.cs`؛ `UserConfiguration.cs`)
2. **یکتایی سراسری Username و Email:** `UserConfiguration` برای `EmailAddress` و `Username` ایندکس یکتا دارد؛ Snapshot برای `Username` فیلتر `[Username] IS NOT NULL` را نشان می‌دهد (و خروجی زنده در §12.5 با آن موافق است؛ در `UserConfiguration.cs` همین فیلتر به‌صورت کامنت است، ظاهراً Snapshot/Migration آن را اعمال کرده است؛ تفاوت کد کانفیگ و Snapshot نیازمند بررسی در S3-0f است). تکرار Username/Email بین Tenantها در Shared DB پشتیبانی نمی‌شود. (Confirmed)
3. **UserSession** فقط `UserId` دارد؛ محدودهٔ نصب. Company/Tenant در هر درخواست از DB اعتبارسنجی می‌شود (`CompanyAccessService`، `WorkContextService`). (Confirmed)
4. **UserCompanyMembership:** رابطهٔ `UserId`(scalar)↔`TenantId(=Company.Id)`↔`RoleId?` با FK مرکب نقش `(RoleId, TenantId)`. نقش اختیاری است (D-078؛ بدون نقش = بدون مجوز). یکتایی `(UserId, TenantId)` طبق S3-0d. (Confirmed)
5. **CompanyRole/Permission:** نقش‌ها **Company-scoped**‌اند (`CompanyRole.TenantId = Company.Id`)، و `PermissionDefinition` کاتالوگ سراسری است. نقش سیستمی «company-admin» در هر Company جداگانه ساخته می‌شود. (Confirmed)
6. **«مدیر نصب» = نقش سراسری legacy `Role.Admin`** روی `User.RoleId`؛ مستقل از Membership. در مدل هدف، باید مشخص شود این نقش «اپراتور پلتفرم»، «ادمین Tenant» یا هر دو است (OD-1).
7. **CustomerBoundaryId به‌جای TenantId؟** ثبت دقیق:
   - تعریف و مستند D-038: مرز محدودکنندهٔ روابط گروهی/مالکیت.
   - **کاربرد کارکردی فعلی:** (الف) محدودیت کشف شرکت مقصد در درخواست عضویت؛ (ب) محدودکردن گروه‌ها/مالکیت‌ها در `CorporateGroupService`؛ (ج) مقدار پیش‌فرض نصب هنگام ایجاد Company.
   - هیچ کدی `CustomerBoundaryId` را در ستونی به نام `TenantId` کپی نکرده یا آن را به‌جای `TenantId` در FK به کار نبرده است (جست‌وجوی `CustomerBoundaryId` در کل کد). پس **جایگزینی مستقیم رخ نداده**؛ اما «کشف عضویت» عملاً از آن به‌عنوان مرز مشتری استفاده می‌کند. (Confirmed)
8. رد حساب مدیر اولیه در provisioning با کد `provisioning_initial_admin_conflict` بر اساس بررسی سراسری Username/Email (`CustomerProvisioningApplyService` پیش از ساخت Company). این رفتار سطح نصب است؛ برای چند Tenant در یک DB باید با OD-2 هم‌راستا شود. (Confirmed)

## 8. Legacy Assumptions

فهرست فرض‌های Company=Tenant که هنوز در کد هستند (Confirmed مگر خلاف آن گفته شود):

| # | فرض | محل |
|---|---|---|
| L1 | `membership.TenantId` همان `companyId` است | `CompanyAccessService` (همهٔ متدها) |
| L2 | `FiscalPeriod.TenantId == CompanyId` | `WorkContextService`، `TradeAuthorizationAdapter.HasPermissionForAllCompanyPeriodsAsync` |
| L3 | `DestinationTenantId` = شناسهٔ Company مقصد | `CompanyMembershipRequestEndpoints` سطرهای 113–123، 267 |
| L4 | `TradeAccessContext.TenantId` = CompanyId؛ `TradePeriodScope.TenantId` = CompanyId | `TradeAuthorizationAdapter`، `Trade.Application/Authorization/*` |
| L5 | `TradeEntity.TenantId` = `Company.Id` (KDoc صریح) | `Modules/Trade/Trade.Domain/Common/TradeEntity.cs` |
| L6 | `SecurityAuditEvent.TenantId` = Company (فیلتر `companyId` گزارش روی `TenantId`) | `SecurityAuditAdministrationEndpoints` سطرهای 127، 320 |
| L7 | ایجاد Company با `TenantId = company.Id` در همه Entityهای فرزند | `CustomerProvisioningApplyService`، `CompanyUserProvisioningEndpoints` (سطرهای 122، 194، 267) |
| L8 | FK مرکب `ConsolidationRunCompany(FiscalPeriodId, CompanyId)` → `FiscalPeriods(Id, TenantId)` | `ConsolidationRunCompanyConfiguration`؛ §12.4 |
| L9 | یکتایی کد Role/Employee/Unit/Position/Party/Product با `TenantId` (که عملاً Company است) | `Infrastructure/Persistence/Configurations/*`؛ `PartyStore` |
| L10 | `Company.NationalId` یکتای سراسری (بدون Tenant) | `CompanyConfiguration` |
| L11 | «Installation administrator» دسترسی سراسری روی همهٔ کاربران/شرکت‌ها/رویدادها دارد | `UserAdministrationEndpoints`، `SecurityAuditAdministrationEndpoints` |
| L12 | `DelegationCeilingService` نقش/عضویت را با `TenantId == companyId` می‌سنجد | `Server/Infrastructure/Tenancy/DelegationCeilingService.cs` سطرهای 113–116، 181 |
| L13 | تنظیم تک‌مقداری `CustomerBoundaryId` برای کل نصب | `CustomerProvisioningSettings`، `CustomerProvisioningParentClaimResolver` |

## 9. Confirmed Risks

شدت وابسته به مدل استقرار است؛ «نصب تک‌مشتری/Dedicated» = پایین، «Shared چندمشتری» = بالا. هیچ تست مخربی اجرا نشد.

| ID | ریسک | شدت (Shared) | شدت (Dedicated) | Evidence | Confidence |
|---|---|---|---|---|---|
| R-1 | مدیر نصب (`Role.Admin` سراسری) به کاربران، شرکت‌ها، عضویت‌ها و رویدادهای امنیتی (با Export CSV) همهٔ Tenantها دسترسی دارد | بالا | پایین (طراحی‌شده) | `UserAdministrationEndpoints` 36–160، 532–546؛ `SecurityAuditAdministrationEndpoints` 58–134، 236–330 | Confirmed |
| R-2 | معنای دوگانهٔ `TenantId` پس از گذار: اگر فقط ستون/شناسه‌ها تغییر کنند، همهٔ کد L1–L12 بدون خطای کامپایل معنای نادرست می‌گیرد (Tenant با Company اشتباه می‌شود) | بالا | بالا | بخش ۸ | Confirmed |
| R-3 | `CustomerBoundaryId` کارکرد مرز مشتری دارد ولی منبعش تنظیم سطح نصب است؛ در Shared DB دو مشتری ممکن است بدون اقدام اپراتور مرز یکسان بگیرند و یکدیگر را در «کشف عضویت» ببینند | متوسط تا بالا (وابسته به راه‌اندازی اپراتور) | پایین | `CustomerProvisioningApplyService` سطر 197؛ `CompanyMembershipRequestEndpoints` 88–96 | Confirmed (کد)؛ Partial (اثر عملیاتی) |
| R-4 | یکتایی سراسری `Company.NationalId` و `Username/Email` در Shared DB: دو Tenant نمی‌توانند یک شرکت یا کاربر هم‌نام ثبت کنند و وجود رکورد از طریق خطا قابل حدس است | متوسط | پایین | `CompanyConfiguration`؛ `UserConfiguration` | Confirmed |
| R-5 | `SecurityAuditEvent` نوع رویداد (system/tenant/company) را ندارد؛ `TenantId` گاهی Company و گاهی null است؛ فیلتر گزارش نتیجهٔ ناقص می‌دهد | متوسط | پایین | بخش ۳ و ۶ | Confirmed |
| R-6 | نبود مسیر ویرایش/غیرفعال/حذف Company: حذف یا غیرفعال‌سازی فقط با دستکاری مستقیم دیتابیس ممکن است؛ در Tenant چندشرکتی مدیریت چرخهٔ عمر ممکن نیست | متوسط | متوسط | بخش ۵.۳ | Confirmed (جست‌وجو) |
| R-7 | بدون Global Query Filter و بدون Tenant، جداسازی کاملاً به محتاط‌بودن هر query وابسته است؛ برای ماژول‌های آتی Trade (V3.2) این الگو باید تکرار شود | متوسط | پایین | `PartyStore`، HR endpoints (الگوی دستی `TenantId == companyId`) | Confirmed (الگو)؛ ریسک آینده |
| R-8 | شکاف Migration host: `--migrate` فقط Core را اجرا می‌کند؛ جداول `trade` در `Farano-Local` وجود ندارند | متوسط | متوسط | S3-0c/d؛ §12.2؛ `Server/Program.cs` سطرهای 347–367 | Confirmed (ارجاع به گزارش قبلی؛ تکرار ممیزی نشد) |

یافتهٔ منفی (مفید): endpointهای company-scoped بررسی‌شده همگی عضویت را از DB می‌سنجند؛ مسیر بدون scope روی داده تجاری Company در جست‌وجوهای این ممیزی یافت نشد (محدود به الگوهای جست‌وجوشده).

## 10. Open Architectural Decisions

بدون تصمیم‌گیری در این ممیزی؛ هر مورد پیش از S3-0f/طراحی Migration باید تعیین شود.

| ID | سؤال | چرا مهم است | اولویت |
|---|---|---|---|
| OD-1 | نقش `Admin` سراسری legacy در مدل هدف چه معنایی دارد: اپراتور پلتفرم (cross-tenant)، ادمین Tenant یا هر دو؟ دسترسی به لیست کاربران/شرکت‌ها/Audit باید چگونه Tenant-scoped شود؟ | R-1؛ تعیین طراحی authorization | **پیش از Migration** |
| OD-2 | دامنهٔ یکتایی `Username/Email` در Shared DB: سراسری (Realm) یا per-Tenant؟ (D-087 آن را تغییر نداده) | R-4؛ تعیین مدل Identity | **پیش از Migration** |
| OD-3 | آیا `CustomerBoundaryId` باید در «کشف عضویت» و گروه‌ها همچنان قید کارکردی بماند، یا کشف باید به Tenant منتقل شود؟ آیا بین Tenantهای مختلف کشف مجاز است؟ | R-3؛ D-087 بند ۸ و D-038 | **پیش از Migration** |
| OD-4 | `CompanyRole`: Company-scoped (فعلی) یا Tenant-scoped با تخصیص به Company؟ | شکل کلید مرکب و Unique `Code` | پیش از Migration |
| OD-5 | `SecurityAuditEvent`: ستون `Scope`/`CompanyId` و نگاشت رویدادهای legacy؟ | R-5 | پیش از Migration (فقط شکل؛ Backfill قابل موکول) |
| OD-6 | منبع معتبر Company→Tenant و سرویس مسئول ایجاد Tenant: بستهٔ Provisioning Vendor، اپراتور نصب یا هر دو؟ نسخهٔ payload (v4/#392/#228) | بدون آن Backfill غیرممکن | **پیش از Migration** |
| OD-7 | `Company.NationalId`: یکتای سراسری، per-Tenant، یا حذف قید؟ | R-4 | پیش از Migration |
| OD-8 | `ProvisioningRequestClaim`: ثبت در سطح نصب یا Tenant؟ | idempotency | قابل موکول به طراحی PR |
| OD-9 | گروه C و گروه‌های شرکتی: صراحتاً فقط same-Tenant؟ | D-087 بند ۶ | پیش از طراحی Trade S3 |
| OD-10 | نام و استراتژی کدی: نگه‌داشتن نام `TenantId` با معنای جدید یا نام موقت برای جلوگیری از R-2 | R-2 | پیش از Migration |

## 11. Recommended Minimal Transition Strategy

> فقط طراحی اولیهٔ فنی؛ برنامهٔ مهاجرت تأییدشده نیست. هیچ مورد زیر اجرا نشده و هر مورد به PR مستقل و تأیید جدا نیاز دارد (D-087 «نحوه مهاجرت»).

### 11.1 اصول
1. **افزایشی و بدون بازنویسی ستون‌های موجود:** `TenantId` فعلی روی Entityهای فرزند (که امروز Company.Id است) **نام و داده‌اش را نگه می‌دارد**؛ یعنی «TenantId legacy = Company.Id» تا وقتی نگاشت ثبت شود.
2. **Tenant به‌عنوان مرز جدید فقط روی Company افزوده شود:** جدول `Tenants` + ستون `Companies.TenantId` (nullable در گام ۱، NOT NULL پس از Backfill). این تنها تغییر ضروری Schema برای تعریف «هر Company دقیقاً یک Tenant» است.
3. **تفکیک بدون ستون تکراری در بیشتر جداول:** برای داده company-owned، `CompanyId` جدید فقط وقتی لازم است که `TenantId` فعلی باید معنای Tenant بگیرد. روش حداقلی: به‌جای افزودن `CompanyId` همزمان به همهٔ جداول، دو مرحله:
   - **مرحلهٔ A (بدون تغییر معنا):** افزودن `Tenants` و `Companies.TenantId`؛ همهٔ ستون‌های `TenantId` فرزند همچنان `Company.Id`. اعتبارسنجی پیوند Company→Tenant در `CompanyAccessService` و `WorkContext` اضافه می‌شود (resolve از DB؛ نه از کلاینت). هیچ جدول فرزند تغییر نمی‌کند.
   - **مرحلهٔ B (تفکیک معنا، جدول‌به‌جدول):** برای هر Entity بر اساس اولویت ریسک (Trade → HR → Membership/Role → گروه FiscalPeriod)، افزودن `CompanyId` و تغییر معنای `TenantId` به Tenant واقعی همراه با Backfill از `Company.TenantId` و FK/Alternate Key مرکب، در PR مستقل و با تست SQL Server واقعی. گروه FiscalPeriod (`FiscalPeriod`، `ProvisioningRequestClaim`، `ConsolidationRunCompany`) باید در یک PR هم‌زمان تغییر کند، زیرا FK مرکب `ConsolidationRunCompany(FiscalPeriodId, CompanyId)` → `FiscalPeriods(Id, TenantId)` را حفظ می‌کند.
4. **CustomerBoundaryId دست‌نخورده:** هیچ کپی/تبدیل/اشتقاق به Tenant انجام نشود. ستون و ایندکس و endpoint اپراتوری بدون تغییر.
5. **بدون جدول یا Entity تکراری:** از Entityهای موجود (Company، Membership، Role) استفاده می‌شود؛ جدول Tenant تنها Entity جدید است.
6. **Shared و Dedicated:** همان مدل؛ در Dedicated یک ردیف Tenant و یک یا چند Company؛ در Shared چند Tenant. هیچ تغییری در Connection String لازم نیست (D-087 «قید سادگی»).
7. **امنیت در طول گذار:** تا اتمام مرحلهٔ B، مرز Tenant در لایهٔ سرویس (نه با Global Query Filter) اعمال شود: `CompanyAccessService` هر Company را با `Company.TenantId` مقایسه می‌کند و هر `companyId` ورودی ابتدا Tenant جاری را تأیید می‌کند. Global Query Filter فقط پس از طراحی context ورود/jobها/عملیات سیستمی (طبق D-087) افزوده شود.
8. **اولویت اقدام ایمنی بدون Migration:** قبل از ورود هر مشتری دوم به یک Shared DB، OD-1 باید پاسخ داده شود (R-1)؛ این تغییر کد است نه Schema و می‌تواند مستقل از Tenant انجام شود.

### 11.2 Backfill
- فقط از **منبع معتبر OD-6**: ورودی Provisioning/تصمیم مالک؛ برای نصب‌های موجود (`Farano-Local` و مشتریان) فهرست رسمی Company→Tenant توسط مالک تأیید شود. ممنوع: استنتاج از نام، `CustomerBoundaryId`، گروه یا مالکیت.
- ساده‌ترین حالت: نصب تک‌شرکتی → یک Tenant به ازای هر Company موجود؛ نصب چندشرکتی → گروه‌بندی طبق تأیید مالک.

### 11.3 تناسب با ~۱۰۰ کاربر هم‌زمان
مدل افزایشی تعداد Join/ایندکس را تقریباً ثابت نگه می‌دارد (افزودن `TenantId` به `Companies` و کلیدهای مرکب موجود). Resolve Tenant از Company می‌تواند همراه همان queryهای عضویت موجود در `CompanyAccessService` انجام شود (یک join روی جدول کوچک `Companies`) و از اضافه‌شدن round-trip جدید جلوگیری می‌کند. هیچ‌گونه Sharding/Microservice لازم نیست (طبق D-087). اندازه‌گیری عملکرد انجام نشده است.

### 11.4 تست‌های لازم (باید در PRهای اجرایی ساخته شود)
دو Tenant در یک DB بدون نشت؛ Company با Tenant ناسازگار رد می‌شود؛ شماره تجاری یکسان در دو Company مجاز و در یک Company مردود (D-087 معیار پذیرش)؛ ادمین Tenant A هیچ داده Tenant B را نمی‌بیند؛ مسیرهای `UserAdministration`/`SecurityAudit` Tenant-scoped.

## 12. Deferred Findings

قابل موکول به زمان پیاده‌سازی (نه مانع S3-0f):
- ماتریس نهایی Entityهای مرجع Trade V3.2 (Vessel، Port، …) پس از Entity شدن.
- بازبینی `ConsolidationProfile/Run`، `LoginLog`، `InstallationIdentity`، `InstalledLicense` (فیلدها خوانده نشد).
- `AdminUserSessionEndpoints` (فقط نقشه مسیر دیده شد؛ بررسی scope عمیق نشد).
- بازبینی خط‌به‌خط کل Exportهای آینده Trade.
- اندازه‌گیری عملکرد (۱۰۰ کاربر) و بار join.
- تأیید تفاوت `UserConfiguration.cs` با Snapshot برای فیلتر Username (§7 مورد ۲).
- سیاست داده مرجع Global/سفارشی (گروه A V3.2) پس از پیاده‌سازی.
- شواهد زندهٔ داده (مقدار واقعی TenantId/CompanyId؛ فقط metadata خوانده شده است).

## 13. Evidence Index

| کد | فایل / عضو | استفاده |
|---|---|---|
| E1 | `Domain/Company.cs` | Company بدون TenantId؛ `CustomerBoundaryId?` |
| E2 | `Infrastructure/Persistence/Configurations/CompanyConfiguration.cs` | ایندکس `NationalId` یکتا (filtered)؛ ایندکس `CustomerBoundaryId` |
| E3 | `Infrastructure/Persistence/DatabaseContext.cs` | فهرست DbSetها |
| E4 | `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs` (`ApplyAsync`، `ReissueEnrollmentTokenAsync`) | تنها ایجاد Company؛ `TenantId = company.Id` |
| E5 | `Server/Infrastructure/Provisioning/CustomerProvisioningSettings.cs` | `CustomerBoundaryId` سطح نصب |
| E6 | `Server/Api/CustomerBoundaryEndpoints.cs` (`AssignAsync`، `AssignWithinTransactionAsync`، `DiagnoseAsync`) | نوشتن `CustomerBoundaryId`؛ فقط Infrastructure |
| E7 | `Server/Infrastructure/Tenancy/CompanyAccessService.cs` | منبع دسترسی؛ L1 |
| E8 | `Server/Infrastructure/Tenancy/WorkContextService.cs` | L2 |
| E9 | `Server/Infrastructure/Tenancy/DelegationCeilingService.cs` | L12 |
| E10 | `Domain/UserCompanyMembership.cs`، `Domain/CompanyRole.cs`، `Domain/FiscalPeriod.cs`، `Domain/Hr/*.cs`، `Domain/SecurityAuditEvent.cs`، `Domain/ProvisioningRequestClaim.cs`، `Domain/CorporateGroups/*.cs` | فیلدهای مالکیت |
| E11 | `Domain/User.cs`، `Infrastructure/Persistence/Configurations/UserConfiguration.cs` | Identity Realm؛ یکتایی؛ `DefaultCompanyId` |
| E12 | `Server/Api/UserAdministrationEndpoints.cs` (`GetUsersAsync`، `GetUserAsync`، `IsInstallationAdministratorAsync`) | R-1 |
| E13 | `Server/Api/SecurityAuditAdministrationEndpoints.cs` (`GetAuditEventsAsync`، `GetAdminCompaniesAsync`، `ExportAuditEventsAsync`) | R-1، R-5 |
| E14 | `Server/Api/CompanyMembershipRequestEndpoints.cs` | استفاده از CustomerBoundaryId؛ L3 |
| E15 | `Server/Api/CompanyUserDirectoryEndpoints.cs`، `Server/Api/CompanyUserProvisioningEndpoints.cs` | Company-scoped؛ L7 |
| E16 | `Server/Api/HrOrganizationalStructureEndpoints.cs`، `Server/Api/HrEmployeePhotoEndpoints.cs` | Company scope HR |
| E17 | `Modules/Trade/Trade.Domain/Common/TradeEntity.cs`، `Modules/Trade/Trade.Infrastructure/Parties/PartyStore.cs`، `Server/Infrastructure/Trade/TradeAuthorizationAdapter.cs`، `Server/Api/Trade/*` | L4–L5 |
| E18 | `Server/Program.cs` سطرهای 118–191، 347–380 | یک Connection String؛ `--migrate` |
| E19 | `Server/Pages/DatabaseSetup.cshtml.cs`، `Server/Infrastructure/DatabaseSetup/*` | نبود Company/Tenant در نصب اولیه |
| E20 | `Vendor/Vendor.Persistence/Domain/VendorBusinessEntities.cs` | Vendor خارج از مدل Tenant مشتری |
| E21 | `docs/DECISIONS.md` D-087؛ `docs/handoffs/ISSUE-397-s3-0-tenant-company-audit.md` §12 و §13 (شاخهٔ S3-0d) | مبنا |

شماره‌سطرها در بخش‌های بالا از clone سطحی `main@d81bb29` گرفته شده‌اند؛ برای ارجاع نهایی باید با SHA ثابت دوباره تأیید شوند.

## 14. Audit Completion Assessment

| معیار پایان کار | وضعیت |
|---|---|
| مالکیت موجودیت‌های اصلی به‌اندازهٔ کافی روشن است؟ | **بله، برای Core، HR، Membership/Role، Audit، Provisioning، Trade فعلی و Vendor** (Confirmed). ۳ Entity (ConsolidationProfile/Run، LoginLog) و مجموعهٔ Installation/License فقط Partial/Unknown هستند و مانع طراحی نیستند. |
| مسیر ایجاد Tenant و Company مشخص است؟ | **Company: بله** (یک مسیر: `CustomerProvisioningApplyService`)؛ **Tenant: مسیری وجود ندارد** و سرویس مسئول باید با OD-6 تعیین شود. |
| وابستگی‌های مهم به مدل Legacy شناسایی شده‌اند؟ | **بله** (L1–L13 و R-1 تا R-8). |
| تصمیم‌های ضروری پیش از Migration | OD-1، OD-2، OD-3، OD-4، OD-6، OD-7، OD-10 (و OD-5 برای شکل ستون). |
| قابل موکول به زمان پیاده‌سازی | OD-8، OD-9 (پیش از Trade S3)، موارد بخش ۱۲. |

**نتیجه:** ممیزی S3-0 را می‌توان **مشروط** بست: ورود به S3-0f (طراحی حداقلی گذار) ممکن است، به شرط آنکه OD-1، OD-2، OD-3، OD-6 به‌عنوان ورودی طراحی ثبت شوند. Backfill تا پاسخ OD-6 ممکن نیست.

### محدودیت‌های ممیزی
- فقط خواندن ایستای کد؛ هیچ build/test/dotnet و هیچ اتصال دیتابیس اجرا نشد. **هیچ تستی اجرا نشده و هیچ Pass اعلام نمی‌شود.**
- clone سطحی از `main`؛ بدون تاریخچه. شاخه‌های دیگر و PRهای باز بررسی نشد.
- جست‌وجوهای «عدم وجود» (مثلاً نبود endpoint ویرایش Company) به الگوهای جست‌وجوشده محدودند.
- کامپیوتر کاربر در دسترس نبود؛ تغییرات commit‌نشدهٔ احتمالی کاربر بررسی نشد.
- دیتابیس واقعی فقط از طریق خروجی S3-0c-b (metadata؛ `Farano-Local`) مبنا بود؛ دیتابیس‌های دیگر بررسی نشدند و timestamp آن (2026-11-18) هنوز تأیید نشده است.

### وضعیت Git
- Clone محلی session: شاخه `main`، `HEAD=d81bb29`، قبل از نوشتن این فایل working tree تمیز بود.
- تنها تغییر: افزودن همین فایل گزارش (untracked). Commit، Push، PR و Merge انجام نشد.
