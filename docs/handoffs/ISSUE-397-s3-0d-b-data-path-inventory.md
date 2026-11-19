# S3-0d-b — Inventory کامل مسیرهای داده و جداسازی Tenant/Company

- **Issue:** #397 (S3-0)
- **تاریخ:** 2026-11-15
- **مبنا:** `origin/main@9eb281b` (پس از Merge شدن #665، #666، #668، #669)
- **ماهیت:** مستندات؛ فقط خواندن Source. هیچ Entity، API، Authorization، UI، Schema، Migration، Backfill یا داده تغییر نکرد. Build/Test اجرا نشد (تغییر مستنداتی).
- **ادامهٔ:** `ISSUE-397-s3-0-tenant-company-audit.md` بخش ۱۳ (دور اول). این سند آن را بازنویسی نمی‌کند و دور دوم را کامل می‌کند.
- **مدل هدف:** D-087؛ تصمیم‌های D-088 و D-089 پذیرفته‌شده‌اند. `TenantId` فعلی در ستون‌ها = `Company.Id` (میراثی).

## ۱. روش بررسی
جست‌وجوی مستقیم Source (`grep`) و خواندن فایل‌ها؛ هیچ SQL روی دیتابیس واقعی اجرا نشد. الگوهای جست‌وجو:
- `ExecuteUpdate|ExecuteDelete|ExecuteSqlRaw|FromSqlRaw|FromSqlInterpolated|SqlQuery` (کل مخزن بدون bin/obj/Migrations/tests)
- `BackgroundService|IHostedService`
- `text/csv|ToCsv|CsvCell|FileContentResult|File(|TypedResults.File`
- `MapGroup|MapGet|MapPost|MapPut|MapDelete|RequireAuthorization` در `Server/Api/**` و `Server/Api/Trade/**`
- `FindAccessibleCompanyAsync|AuthorizeCompanyScopeAsync|HasPermissionAsync` (شمارش به‌تفکیک فایل)
- `TenantId`/`CompanyId` در `Domain/**`، `HasQueryFilter` در Infrastructure/Modules/Domain، `DbSet<` در `DatabaseContext`
- نام تست‌ها در `tests/Template.Tests/**` با واژه‌های Other/Cross/Foreign/Wrong/Tenant/Isolat (بر پایهٔ نام تست؛ بدنهٔ هر تست خط‌به‌خط بازبینی نشد)

فایل‌های اصلی خوانده‌شده: `Server/Api/*.cs` (۱۹ فایل)، `Server/Api/Trade/*.cs`، `Server/Infrastructure/Tenancy/*`، `Server/Infrastructure/Trade/TradeAuthorizationAdapter.cs`، `Server/Infrastructure/CorporateGroups/CorporateGroupService.cs`، `Modules/Trade/Trade.Infrastructure/**`، `Domain/**`، فهرست `Server/Pages/**`.

## ۲. یافته‌های سراسری (با شواهد)
1. **بدون Global Query Filter:** `HasQueryFilter` در Core، Trade و Domain یافت نشد؛ جداسازی فقط با predicate صریح در هر Query و Store است.
2. **Entityهای دارای `TenantId`** (`Domain/**`): `CompanyMembershipRequest` (`DestinationTenantId`)، `CompanyRole`، `FiscalPeriod`، `UserCompanyMembership`، `SecurityAuditEvent` (nullable) و پنج Entity منابع انسانی (`CompanyEmployee`، `CompanyEmployeeAssignment`، `CompanyEmployeePhoto`، `CompanyOrganizationalUnit`، `CompanyPosition`). `Company` فاقد `TenantId` است.
3. **`CompanyId` واقعی** فقط در `ProvisioningRequestClaim`، گروه‌های شرکتی/تلفیق (`CorporateGroup*`، `OwnershipRelationship`، `Consolidation*`) و Entityهای Trade (از طریق `TenantId`!) دیده می‌شود. `User` فقط `DefaultCompanyId` دارد (ترجیح کاربر، نه مالکیت).
4. **Trade:** `TradeEntity.TenantId` (`Modules/Trade/Trade.Domain/Common/TradeEntity.cs`) تنها شناسهٔ مالکیت است و Adapter مقدار Company جاری را در آن می‌گذارد. Storeها (`PartyStore`، `ProductStore`) در همهٔ مسیرهای List/Find/Update و فرزندان (`Roles`) `TenantId` را فیلتر می‌کنند. FK یا مرجع سطح دیتابیس به Core وجود ندارد (طبق مرز ماژول).
5. **Trade Migration:** Snapshot پنج جدول دارد؛ Migration اولیه (`20261113072823_InitialTradeMasterData`) فقط `Parties`، `Products`، `PartyRoleAssignments` را می‌سازد. `PartyBankAccounts` و `ProductIdentifiers` عمداً در مدل مانده‌اند و در Migration نیستند (مستند در `TradeMasterDataMigrationSqlServerTests`).

## ۳. ماتریس مالکیت Entity
| Entity | مالکیت فعلی | مالکیت هدف | `TenantId` فعلی | `CompanyId` | کنترل دسترسی | خواندن/نوشتن (فایل) | ریسک Cross-Tenant | تست موجود (نام/فایل) | شکاف |
|---|---|---|---|---|---|---|---|---|---|
| `Company` | Legacy (بدون Tenant) | Company زیر Tenant | ندارد | `Id` | `CompanyAccessService` (عضویت فعال) | `ApiEndpoints.GetCompaniesAsync`، `/admin/companies` (مدیر نصب) | رابطهٔ Company→Tenant وجود ندارد | `CompanyAccessTests` (Overview از شرکت دیگر ⇒ 404) | تست دو Tenant مستقل ندارد (Tenant نیست) |
| `FiscalPeriod` | Legacy Tenant | Company | = `Company.Id` | ندارد | `WorkContextService` + `CompanyAccessService` | `WorkContextService` | وابسته به معادلهٔ `Tenant=Company` | `WorkContextTests.APeriodOfAnotherCompany_IsRefused_AsNotFound` | تست SQL Server برای دورهٔ شرکت دیگر یافت نشد |
| `CompanyRole`، `UserCompanyMembership` | Legacy Tenant | Company | = `Company.Id` | ندارد | `CompanyAccessService` + مجوز `roles.*` | `CompanyRoleAdministrationEndpoints`، `CompanyAccessService` | ریسک تفسیر نادرست در گذار؛ FK مرکب `(RoleId, TenantId)` مانع نقش خارجی | `CompanyRoleAdministrationEndpointTests.UpdateRole_RejectsRoleIdOwnedByAnotherCompany`، `AssignMembership_RejectsRoleFromAnotherCompany`، `TransferMembers_*`، `CompanyRoleMembersSqlServerTests` | — |
| `CompanyMembershipRequest` | Legacy Tenant (`DestinationTenantId` ← `DestinationCompanyId`) | درخواست بین‌شرکتی | = Company مقصد | ندارد | `AuthorizeDestinationApproverAsync` (مجوز مقصد + استثنای D-071) | `CompanyMembershipRequestEndpoints` | معنای ستون با نامش نمی‌خواند؛ مدیر نصب می‌تواند صف هر شرکت را ببیند و تأیید کند (D-071، ثبت‌شده در PR #668) | `CompanyMembershipRequestEndpointTests.ApproveMembership_DoesNotUsePermissionsGrantedInAnotherCompany`، `BootstrapMembershipApprovalTests`، `*SqlServerTests` | تطبیق D-071 با بند ۴ D-088 تأییدنشده |
| HR (`CompanyEmployee`، `Assignment`، `Photo`، `OrganizationalUnit`، `Position`) | Legacy Tenant | Company | = `Company.Id` | ندارد | مجوز HR هر درخواست + `{companyId}` مسیر | `HrOrganizationalStructureEndpoints`، `HrEmployeePhotoEndpoints` | عکس BLOB در SQL Server؛ FKهای مرکب تعلق فرزند را نگه می‌دارند | `HrEmployeePhotoEndpointTests.Photo_OfACompanyIsInvisibleAndImmutableFromAnotherCompany`، `HrOrganizationalStructureEndpointTests.Assignment_SelfManagementAndCrossCompanyManager_AreRefused`، `HrWorkflowsEndpointTests.UnitSubtree_UnknownOrForeignUnit_Is404WithoutDisclosure`، `HrOrganizationalStructureSqlServerTests` | — |
| `SecurityAuditEvent` | Legacy/نصب | system/tenant/company (طبقه‌بندی باید تعریف شود) | nullable؛ = Company یا null | ندارد | فقط مدیر نصب (`AuthorizeInstallationAdministratorAsync`) | `SecurityAuditAdministrationEndpoints`، `InstallationAdminAudit` | فیلتر Company از `TenantId` میراثی؛ رویداد سراسری ممکن است بدون Tenant باشد | `SecurityAuditAdministrationEndpointTests`، `SecurityAuditAdministrationSqlServerTests`، `InstallationAdmin*Tests` | معنای system/tenant/company تعریف نشده |
| `User`، `UserSession`، `LoginLog`، `Role` (legacy)، `Page`، `MenuItem`، `PageCategory`، `ApplicationHandler`، `Permission*` | Global (هویت و فراداده نصب) | Global | — | `User.DefaultCompanyId` فقط ترجیح | نقش Admin (بازبینی از DB، PR-E1a) و Session | `Server/Pages/Admin/**`، `UserAdministrationEndpoints`، `AdminUserSessionEndpoints` | مدیر نصب سراسری است؛ مجاز طبق D-088 | `AdminRoleRevalidationHttpTests`، `InstallationAdminAudit*Tests` | — |
| `ProvisioningRequestClaim` | سطح نصب/درخواست | سطح نصب + Tenant از نگاشت D-089 | ندارد | `CompanyId` | حساب Infrastructure + امضا | `CustomerProvisioningApplyService` | `TenantId` از `Company.Id` ساخته می‌شود؛ نگاشت `CustomerReference→Tenant` هنوز نیست | `CustomerProvisioningClaimsSqlServerTests`، `CustomerProvisioningTransactionalSqlServerTests` | رفتار D-089 (رد ناشناخته، شناسهٔ نصب) هنوز تست ندارد (پیاده‌سازی نشده) |
| `CorporateGroup*`، `OwnershipRelationship`، `Consolidation*` | Company-owned با `CustomerBoundaryId` | گروه/مالکیت ≠ Tenant؛ نیازمند Tenant مشترک و مجوز | — | `OwnerCompanyId`/`CompanyId` | `CorporateGroupService` + `CompanyAccessService` | `CorporateGroupDirectoryEndpoints`، `CorporateGroupService` | `CustomerBoundaryId` جایگزین Tenant نیست | `CorporateGroupServiceTests.CorporateGroupAndOwnershipWrites_RejectDifferentCustomerBoundaries`، `CorporateGroupDirectoryEndpointTests`، `CorporateGroupSqlServerTests`، `OwnershipScopeArchitectureTests.NoSourceFile_UsesCustomerBoundaryIdAsTheTenantBoundary` | تأیید Tenant مشترک طرفین پیاده نیست (Tenant نیست) |
| Trade `Party`، `PartyRoleAssignment`، `Product` | Legacy Tenant (= Company) | Company | = Company جاری | ندارد | `TradeAccessFilter` (fail-closed) + `TradeAuthorizationAdapter` + Entitlement (`DenyAll` در تولید) | `PartyStore`، `ProductStore`، `Server/Api/Trade/*` | درج مستقیم SQL بدون FK به Core ممکن است | `TradePartyHttpTests.OtherTenantPartyIsInvisibleAndEveryAccessAnswersTheSameEmpty404`، `TradeProductHttpTests.OtherTenantProductIsInvisible…`، `TradeAccessHttpTests`، `TradeAuthorizationAdapterTests` | «Tenant دیگر» در این تست‌ها یک Company دیگر است؛ Tenant واقعی نیست |
| Trade `PartyBankAccount`، `ProductIdentifier` | در مدل، نه در Migration | Company | = Company جاری | ندارد | مانند بالا | مدل `TradeDbContext` | جدولی در دیتابیس ایجاد نمی‌شود | `TradeMasterDataMigrationSqlServerTests` | — |
| مرجع Trade (کشور/واحد) | Global ایستا (دیکشنری در کد) | Global | — | — | مجوز مشاهدهٔ Trade | `TradeReferenceEndpoints` | داده سفارشی نیست | `TradePartyHttpTests.CountriesReference*`، `TradeProductHttpTests.UnitsReference*` | Entity جداگانهٔ Vessel/Currency/Port در Source Trade یافت نشد (فقط مقدار enum `PartyRole.VesselAgent` و کد ارز ISO در Consolidation) |
| `InstallationIdentity`، `InstalledLicense`، `CustomerOfflineRevocationState` | Global (نصب) | Global | — | — | حساب Infrastructure | سرویس‌های `Server/Infrastructure/Licensing/**` | — | `CustomerLicenseImportServiceTests`، `CustomerOfflineRevocationSqlServerTests` | — |

## ۴. فهرست Endpointهای حساس (مسیر → کنترل واقعی)
| مسیر | کنترل (از Source) | نکته |
|---|---|---|
| `/companies/{companyId}/users` (GET/POST) | `FindAccessibleCompanyAsync` + `HasPermissionAsync` | غیرعضو ⇒ 404 |
| `/companies/{companyId}/org/{employees,units,positions,assignments}` + `/photo` | عضویت فعال + مجوز HR هر درخواست | عکس: `HrEmployeePhotoEndpoints` |
| `/admin/companies/{companyId}/roles*`، `effective-permissions`، `/membership-requests*` | `AuthorizeCompanyScopeAsync`/`AuthorizeDestinationApproverAsync` | مدیر نصب: استثنای D-071 با ثبت دسترسی (PR #668) |
| `/me/membership-requests*`، `/me/permissions`، `/me/sessions*` | هویت کاربر | محدود به خود کاربر |
| `/trade/*` | `TradeAccessFilter` (بدون Metadata ⇒ 403)، Antiforgery، WorkContext سمت سرور | Entitlement `DenyAll` ⇒ `module_unavailable` |
| `/context` | `WorkContextService` (اعتبارسنجی دوباره از DB) | کوکی فقط انتخاب را نگه می‌دارد |
| `/corporate-groups/{groupId}/directory` | `CorporateGroupService` + مجوز | `CustomerBoundaryId` محدودکننده، نه Tenant |
| `/admin/users*`، `/admin/security/*`، `/admin/companies`، `/admin/access/catalog*` | نقش Admin بازبینی‌شده از DB | سراسری طبق D-088؛ ثبت دسترسی (PR #668) |
| `/provisioning/apply`، `/licensing/*`، `/operator/customer-boundaries/*` | حساب `AccountKind=Infrastructure` | سطح نصب؛ بدون Tenant |

## ۵. Export و Report
- **تنها خروجی فایل داده:** `POST /admin/security/audit-events/export` (CSV، فقط مدیر نصب، ممیزی‌شده؛ `SecurityAuditAdministrationEndpoints`). تست: `SecurityAuditAdministration*Tests`.
- `HrEmployeePhotoEndpoints` فایل (تصویر) را از BLOB سرو می‌کند؛ تست Cross-Company دارد.
- `Server/Program.cs` و `DatabaseConnectionSettingsStore` در جست‌وجوی `File(` آمدند؛ مربوط به نصب/تنظیمات‌اند، نه داده Tenant.
- گزارش مالی/تجاری/PDF/Excel در Source وجود ندارد (جست‌وجوی بالا). ادعای «همه گزارش‌ها» محدود به همین جست‌وجو است.

## ۶. Raw SQL و عملیات Set-based
| محل | عملیات | دامنه |
|---|---|---|
| `Server/Infrastructure/Security/SessionValidator.cs`، `UserSessionService.cs`، `UserPasswordService.cs` | `ExecuteUpdateAsync` روی `UserSessions` (لغو نشست) | هویت/نصب؛ predicate بر `SessionId`/`UserId` |
| `CustomerInstallationIdentityEnrollmentService`، `CustomerLicenseImportService`، `CustomerProvisioningApplyService` | `ExecuteSqlRawAsync` برای `sp_getapplock` | قفل؛ پارامتری؛ بدون خواندن/نوشتن داده شرکت |
| `Infrastructure/Persistence/TpmAnchoredOfflineRevocationStateStore.cs` | `FromSqlRaw` | وضعیت ضد‌برگشت لایسنس؛ نه داده شرکت |
| `ExecuteDelete` / `ExecuteSqlInterpolated` / `SqlQuery` | یافت نشد | — |
نتیجه: هیچ Raw SQL یا عملیات Set-based روی داده Company/Tenant/Trade در Source فعلی یافت نشد. (برای `DatabaseSetup` و Migration Host خارج از دامنه داده Tenant.)

## ۷. فایل‌ها و Jobها
- **فایل HR:** BLOB در `CompanyEmployeePhotos` (SQL Server)؛ با تصمیم «باینری اسناد Trade خارج SQL» یکی نیست. Storage سند Trade پیاده نشده است.
- **Background:** فقط `VendorAuditCheckpointBackgroundService` و دو Startup Service در `Vendor/Vendor.Host` (Host مستقل Vendor؛ داده Tenant مشتری Server نیست؛ مستقل‌بودن دیتابیس آن بررسی نشد). در Server هیچ `BackgroundService`/`IHostedService` برای داده مشتری یافت نشد.
- `Admin/FileManager`: مسیرها به `wwwroot` محدود شد (PR #664)؛ داده Tenant نیست.

## ۸. صفحات Razor
- `Server/Pages/Admin/**` (Users، Roles، MenuItems، Page*، ApplicationHandlers، FileManager): مدیریت سراسری نصب با `[Authorize(Roles = Admin)]` + بازبینی نقش؛ Users ثبت‌شده (PR #668). Roles/MenuItems/Page*/ApplicationHandlers فراداده سراسری‌اند؛ ممیزی تغییر Roles در Backlog.
- `Account/**`، `Google/**`: هویت و پروفایل خود کاربر.
- `Page`، `Index`، `About`، `Contact`: محتوای سراسری عمومی.
- `DatabaseSetup`: پیش از نصب؛ خارج از دامنه.
- `Index.cshtml(.cs)`: فهرست شرکت‌های کاربر را از `CompanyAccessService.GetAccessibleCompaniesAsync` (عضویت فعال، سمت سرور) می‌خواند. `Admin/Users/Update|Delete` شناسهٔ شرکت‌های کاربر هدف را فقط برای ثبت ممیزی می‌خوانند (`InstallationAdminAudit.GetCompanyIdsAsync`). جست‌وجوی `Companies|CompanyId|FiscalPeriod|Trade|Employee|Party` در `Server/Pages/**` فقط همین سه فایل را نشان داد؛ داده HR/Trade در Razor نیست (محتوای هر `.cshtml` خط‌به‌خط بازبینی نشد).

## ۹. تست‌های امنیتی/Isolation
- **جداسازی دو Company (بر پایهٔ نام تست):** `CompanyAccessTests`، `WorkContextTests`، `CompanyRoleAdministrationEndpointTests`، `CompanyRoleDelegationCeilingEndpointTests`، `CompanyUserDirectoryEndpointTests`، `CompanyUserProvisioningEndpointTests`، `HrEmployeePhotoEndpointTests`، `HrOrganizationalStructureEndpointTests`، `HrWorkflowsEndpointTests`، `CompanyMembershipRequestEndpointTests`، `MePermissionsEndpointTests`، `RolePermissionPreviewEndpointTests`، `CorporateGroup*Tests`، `TradeAuthorizationAdapterTests`، `TradeProductServiceTests`.
- **روی SQL Server واقعی:** `CompanyRoleMembersSqlServerTests`، `CompanyUserDirectorySqlServerTests`، `CompanyUserProvisioningSqlServerTests`، `HrOrganizationalStructureSqlServerTests`، `MePermissionsSqlServerTests`، `CorporateGroupSqlServerTests`، `CompanyMembershipRequest*SqlServerTests`، `CustomerProvisioningClaimsSqlServerTests`.
- **Trade HTTP:** `TradePartyHttpTests.OtherTenantPartyIsInvisible…`، `TradeProductHttpTests.OtherTenantProductIsInvisible…`، `DuplicateActiveCode…FreeInOtherTenants`، `TradeAccessHttpTests` (WorkContext دست‌کاری‌شده، عضویت لغوشده، Gate).
- **دو Tenant مستقل:** **وجود ندارد** (Entity Tenant وجود ندارد). تست‌های «Other Tenant» در Trade و `CompanyAccessTests.Overview_OfAnotherTenantsCompany…` در واقع دو Company را می‌سنجند.
- **شناسهٔ جعلی:** `UpdateRole_RejectsRoleIdOwnedByAnotherCompany`، `AssignMembership_RejectsRoleFromAnotherCompany`، `ApproveMembership_RejectsInactiveOrForeignCompanyRole`، `WorkContextTests.ATamperedForeignOrGarbageValue…`، `CompanyAccessTests.Overview_IgnoresATenantClaimForgedIntoThePrincipal`، `RolePermissionPreviewEndpointTests.Preview_RejectsUnknownCodesForeignRoles…`.
- **فقط Source/Architecture:** `OwnershipScopeArchitectureTests` (دو Scope مجزا، ممنوعیت `CustomerBoundaryId` به‌جای Tenant)، `ArchitectureDependencyGuardTests`، `DatabaseModelTests` (کلید/FK).
- **Export بدون تست منفی Cross-Tenant:** تنها Export (ممیزی امنیتی) سراسری است و تست Cross-Tenant مفهومی ندارد.
- **اجرا (نسخهٔ اولیه #670):** هیچ تستی اجرا نشد؛ فهرست بالا بر پایهٔ نام‌ها بود. وضعیت اجرای واقعی در بخش «تکمیل شواهد» زیر آمده است.

## ۱۰. وضعیت: تأییدشده، ناقص، اثبات‌نشده
**تأییدشده (با Source):** بند ۲؛ ماتریس بند ۳ برای Entityهای فهرست‌شده؛ نبود Raw SQL/Set-based روی داده Tenant؛ نبود Job در Server؛ نبود Global Query Filter؛ فرق Snapshot و Migration Trade.
**ناقص/اثبات‌نشده:**
1. بدنهٔ هر تست خط‌به‌خط بازبینی نشد؛ جداسازی فقط از نام/محتوای مرتبط استنباط شده و هیچ تستی اجرا نشد.
2. `.cshtml` و `ClientApp` برای نشت داده بازبینی نشد.
3. دیتابیس واقعی (مقادیر `TenantId`) بررسی نشد.
4. «تأییدکنندهٔ» تطبیق D-071 با D-088 بند ۴ مالک است.
5. Vessel/Currency/Port و Entityهای V3.2 هنوز در کد نیستند؛ ماتریس فقط موجودها را می‌پوشاند.
6. گزارش‌های آینده (مالی/تجاری) هنوز وجود ندارند.

**جمع‌بندی وضعیت پیشنهادی (نیازمند تأیید مالک):** S3-0d-b برای «inventory ایستای Source» کامل است؛ اما به‌دلیل موارد ۱ تا ۳ **Verified** نیست.

## ۱۱. گام بعدی
- تصمیم مالک: پذیرش وضعیت S3-0d-b؛ تطبیق D-071/D-088.
- PR-B (بسته OD-11/12/13/14/17) و PR-C (هم‌ترازی اسناد).
- بعد از تصمیم‌ها: طراحی S3 (Tenant، نگاشت، Schema) با تأیید جداگانه؛ تست Integration دو Tenant مستقل و تست منفی Trade با درج مستقیم.

## ۱۲. محدودیت‌های این PR
بدون تغییر کد، تست، Schema، Migration، Backfill، Authorization یا UI. `DECISIONS.md`، `ROADMAP.md`، `PROJECT_STATUS.md`، `WORK_LOG.md` طبق دستور دست نخورد (در PR-C).

## ۱۳. تکمیل شواهد (S3-0d-b، پیگیری پس از #670)
مبنا: `origin/main@47cca6e`. بدنهٔ تست‌ها خوانده شد، نه فقط نام‌ها.

### ۱۳.۱ شاهد برای ادعاهای «Verified»
| ادعا | شاهد مشخص | وضعیت |
|---|---|---|
| Trade Party/Product: موجودیت Company دیگر نامرئی و بدون تغییر | `TradePartyHttpTests.OtherTenantPartyIsInvisible…` و معادل Product: GET/PUT/لیست/فیلتر با شناسهٔ بیرونی؛ پاسخ 404 خالی؛ ردیف بیرونی در DB دست‌نخورده؛ `tenantId` در Body نادیده | بدنه بازبینی شد؛ **روی SQL Server اجرا نشد** (`Assert.SkipUnless`) |
| `TenantId` از Body کلاینت پذیرفته نمی‌شود | همان تست‌ها + `tradeClient.test.ts:83` (Client فیلد را نمی‌فرستد) | بدنه بازبینی شد؛ Vitest اجرا نشد (`node_modules` در این محیط نیست) |
| HR: Employee/Unit/Position/Assignment بیرونی | **قبلاً فقط** `UnitSubtree`، Photo، Manager بیرونی. بقیهٔ Handlerهای دارای شناسه تست نداشتند ← **شکاف واقعی** | با تست جدید بسته شد (۱۳.۲) |
| Membership/Role/Provisioning: نقش و عضویت Company دیگر | تست‌های `…RejectsRoleFromAnotherCompany`، `…ForeignCompanyRole`، `Directory_…HidesForeignCompanies` — بدنه‌ها Assertion روی کد وضعیت و عدم جابه‌جایی عضو دارند | بدنه بازبینی شد؛ InMemory اجرا شد |
| CompanyAccess/WorkContext: عضویت لغوشده، Cookie دستکاری‌شده، دورهٔ مالی Company دیگر | `CompanyAccessTests`، `WorkContextTests`، `Integration/WorkContextHttpTests` | InMemory اجرا شد؛ HTTP/SQL Server اجرا نشد |
| Audit: خواندن/تغییر مدیر نصب ثبت می‌شود | `InstallationAdminAuditTests`، `InstallationAdminAuditHttpTests` | InMemory اجرا شد |

### ۱۳.۲ شکاف واقعی و تست افزوده‌شده
`ForeignCompanyIdentifiers_AreRefusedByEveryHrHandler_AndTheForeignRowsStayUntouched` (در `HrOrganizationalStructureEndpointTests`): عضوِ دارای همهٔ مجوزهای HR شرکت خودش، ۱۳ Handler را با شناسهٔ Company دیگر (Employee، User-link، Unit، Position، Assignment) و ۳ ارجاع Body (Unit/Position بیرونی در شروع/تغییر تخصیص) صدا می‌زند؛ همه ۴۰۴ (یا ۴۰۴/۴۰۹/۴۰۰ برای ارجاع Body) می‌شوند و ردیف‌های بیرونی بدون تغییرند. رفتار فعلی Production صحیح است (تست از اول سبز بود).
**اثبات توان شکست:** حذف `current.TenantId == companyId` از `GetUnitAsync` ← تست شکست خورد (`GetUnit … got status=200` با دادهٔ واحد بیرونی). سپس برگردانده شد.

### ۱۳.۳ بازبینی Razor و ClientApp
- `Server/Pages`: فقط `Admin/Users/Update|Delete.cshtml.cs` شناسه Company را می‌خوانند (برای ثبت ممیزی مدیر نصب، `InstallationAdminAudit.GetCompanyIdsAsync`)، و `Index` با `GetAccessibleCompaniesAsync`؛ هیچ Query بدون محدودیت عضویت برای کاربر عادی نیست.
- `ClientApp`: انتخاب Company فقط `companyId` را به `/context` می‌دهد و Server دوباره از DB اعتبارسنجی می‌کند؛ هیچ `localStorage/sessionStorage` برای Company/Tenant استفاده نمی‌شود (جست‌وجوی Source)؛ `tenantId` در Client Trade ارسال نمی‌شود (تست Client).
- Bulk/Export: بدون تغییر نسبت به بند ۵ و ۶ (تنها Export = CSV ممیزی سراسری مدیر نصب؛ `ExecuteUpdate` فقط `UserSessions`).

### ۱۳.۴ طبقه‌بندی تست‌های ناموجود
| دسته | مورد |
|---|---|
| قابل‌آزمون با مدل Company فعلی | (بسته‌شده) HR شناسهٔ بیرونی. باقی: Trade Children (PartyBankAccount/PartyRoleAssignment/ProductIdentifier) فقط از طریق Party/Product قابل دسترسی‌اند و Endpoint مستقل ندارند ← نیاز به تست جدا نیست |
| نیازمند Tenant واقعی | هر ادعای «دو Tenant مستقل»، فیلتر Tenant مستقل از Company، Tenant با چند Company |
| نیازمند SQL Server واقعی | اجرای تست‌های Trade/HR/WorkContext HTTP روی SQL Server در CI؛ در این PR اجرا نشد |
| نیازمند تصمیم معماری | Global Query Filter، DB-level Append-only، فیلتر Tenant در Job/Export آینده |

### ۱۳.۵ وضعیت نهایی بندهای «اثبات‌نشده» ۱ تا ۳
۱. بدنهٔ تست‌های مرتبط بازبینی شد و یک شکاف واقعی (HR) بسته شد. ۲. `.cshtml` و `ClientApp` بازبینی شد (بند ۱۳.۳)؛ Vitest اجرا نشد. ۳. مقادیر `TenantId` در دیتابیس واقعی همچنان **NotVerified** (دیتابیس واقعی در دسترس نیست).
**نتیجه:** S3-0d-b برای مسیرهای قابل‌اثبات با مدل Company فعلی **Verified (InMemory)**؛ ادعای ایزولاسیون دو Tenant واقعی **NotVerified**، و بخش SQL Server **NotRun**. تأیید وضعیت نهایی با مالک است.

---

## ۱۴. تکمیل مأموریت 2026-11-18 — مسیرهای ایجاد داده، کاربر/عضویت، انتخاب پیش‌فرض، Session و FK

- **مبنا:** `main@b8cadbfd` (پس از Mergeهای #704، #705 و #709).
- **ماهیت:** فقط مستندات؛ خواندن Source با grep و بازخوانی فایل‌ها. هیچ کد، تست، Schema، Migration یا تنظیمی تغییر نکرد و هیچ تستی اجرا نشد.
- **این بخش، بندهای ۵.۱ تا ۵.۵ مأموریت جدید را به بخش‌های ۱ تا ۱۳ می‌افزاید** و آن‌ها را بازنویسی نمی‌کند؛ موارد تکراری فقط ارجاع می‌گیرند.

### ۱۴.۰ وضعیت فاز صفر (پیشگیری از تداخل — با شواهد واقعی)
| مورد | وضعیت واقعی | اثر بر این مأموریت |
|---|---|---|
| PR #653 (دور اول S3-0d) | merged در `2026-11-14T14:13:30Z` | کار تکراری انجام نشد؛ این سند همان ادامهٔ مستند است |
| Branch مأموریت `docs/s3-0d-b-data-path-inventory` | commit `3f4ed81a` (۱۱۴ خط) عیناً از طریق PR #670 در main است؛ branch بدون PR باز رها شده | این مأموریت روی ادامهٔ همین branch انجام شد: merge غیرمخرب `origin/main` به داخل branch (بدون reset/force) و سپس commit جدید |
| `docs/handoffs/ISSUE-397-s3-0d-b-data-path-inventory.md` | در main موجود (بخش ۱..۱۲ از #670، بخش ۱۳ از #674) | فقط بخش ۱۴ افزوده شد؛ سابقه دست نخورد |
| «رفع HTTP 429» | دو جریان متمایز: (الف) خطای واقعی HTTP 429 در E2E = PR #654 (`8f103aa0`، سطح rate limit مخصوص سرور E2E) و `ba32a690` — **merged و کامل**؛ (ب) Issue #429 (کنتراست سایدبار/پانورامای برج میلاد) = branch `fix/issue-429-sidebar-contrast-milad-tower` با ۷ commit **merge‌نشده** و بدون PR باز | هیچ‌کدام ناتمامِ این مأموریت نیستند؛ stream (ب) متعلق به جریان دیگری است و دست نخورد |
| Branch `test/tenant-s3-0d-b-isolation-verification` | merge commit تاریخی `3f449883` از دو جریان #673/#674؛ محتوای تست/کد آن (HrOrganizationalStructureEndpointTests، LegacyTenantIdEntityContractTests، handoffها) با main **بیت‌به‌بیت یکسان** است (diff مستقیم خالی)؛ فقط WORK_LOG به‌دلیل قدیمی‌بودن عقب است | بدون PR باز؛ دست نخورد و ادعای کار ناتمام برای آن ثبت نشد |
| PRهای باز مخزن | صفر | بدون تداخل موازی |

### ۱۴.۱ DatabaseSetup و مسیرهای ایجاد داده (بند ۵.۱)

**نکتهٔ سراسری:** «راه‌اندازی زیرساخت» هیچ دادهٔ Tenant/Company نمی‌سازد؛ تنها مسیرِ ایجاد Company در کل Server، Provisioning است. ماتریس بند ۳ قبلی با این بخش مکمل می‌شود.

**مسیر ۱ — راه‌اندازی زیرساخت (pre-tenant):**
- ورود: `Server/Pages/DatabaseSetup.cshtml.cs` با توکن یک‌بارمصرف از `SetupTokenFilePath`؛ توکن و credentialها هرگز لاگ نمی‌شوند (Program.cs:~340).
- سرویس: `DatabaseConnectionSetupService.ConfigureAsync` (Singleton با SemaphoreSlim — یک setup هم‌زمان). مراحل: اعتبارسنجی (allowlist نام دیتابیس برای نام‌های فایل مشتق `.mdf/_log.ldf`، blocklist دیتابیس سیستمی روی نام NFKC-normalized و تأیید server-side با `DB_ID`) → تست اتصال → `CREATE DATABASE` پارامتری با `QUOTENAME` و بدون مسیر فایل (خطای 1801 «از قبل ساخته شد» = بدون شکست) → طبقه‌بندی وضعیت دیتابیس (`Empty/ForeignOrLegacy/NewerThanApplication/PartiallyMigrated/UpToDate` از `__EFMigrationsHistory` + شمارش جدول) → Migration فقط روی Empty/PartiallyMigrated (ادامهٔ Partial فقط با رضایت صریح) → ذخیرهٔ connection string محافظت‌شده.
- **بدون Seed:** هیچ Users/Companies/FiscalPeriods/Memberships/Roles ساخته نمی‌شود. `InfrastructureMasterBootstrap` فقط credential مدیر زیرساختی را validate و hash می‌کند و دستورات user-secrets/env تولید می‌کند (D-044؛ «No Users-table seed» در doc خود فایل).
- **تراکنش/شکست:** تا قبل از تست اتصال، لغو از سمت مرورگر کار را نیمه‌کاره می‌کند؛ پس از آن کار با `CancellationToken.None` ادامه می‌یابد (doc پارامتر) — ایجاد DB و Migration نیمه‌رها نمی‌شوند. خطاها با TrackingId و کد از `DatabaseSetupErrorCatalog` گزارش می‌شوند؛ هیچ DROP/DELETE وجود ندارد.
- **Idempotency:** گیت یک‌بارمصرف — توکن باید هنوز باز باشد و وضعیت فایل تنظیمات همان state startup باشد، وگرنه `AlreadyConfigured`؛ فایل معتبرِ هم‌زمان هرگز بازنویسی نمی‌شود.
- **Migration Strategy (Program.cs):** `--migrate` با `DatabaseMigrationHost` (Core همیشه اول؛ Trade فقط با `--include-trade`) — برای shared و dedicated یکی، چون Migration «متعلق به دیتابیس» است؛ Migration خودکار روی startup فقط Development + فلگ صریح + فقط وقتی اتصال از تنظیمات محلی نیامده؛ در بقیهٔ حالت‌ها فقط هشدار pending؛ **schema legacy `EnsureCreated` هرگز کورکورانه ارتقا نمی‌یابد**.

**مسیر ۲ — ایجاد Company (تنها مسیر: Provisioning):**
- `Companies.Add` در کل Server فقط در `CustomerProvisioningApplyService.ApplyAsync:339` است؛ هیچ endpoint ادمین شرکت نمی‌سازد (`/companies` فقط GET فهرستِ قابل‌دسترسی است).
- در یک تراکنش `Serializable` با قفل `sp_getapplock`: راستی‌آزمایی بستهٔ امضاشده (v5) → idempotency replay (ACK ذخیره‌شده با nonce/payload-hash یکسان ⇒ همان ACK بازگردانده می‌شود؛ `AlreadyApplied`) یا `provisioning_request_id_conflict` → ایجاد هم‌زمان: `Company` + `FiscalPeriod` + `CompanyRole` (با Permissionهای زیرسیستم‌های payload) + کاربر مدیر اولیه (رمز تصادفی غیرقابل‌استفاده، `MustChangePassword=true`، `DefaultCompanyId=null`) + `UserCompanyMembership` فعال (TenantId=Company.Id، نقش = نقش جدید) + `ProvisioningRequestClaim` (hashهای nonce/payload/token، انقضای enrollment، CompanyId/FiscalPeriodId/AdminUserId) + `CompanySetupRecord` (سمت مشتری؛ schema در مخزن، Migration واقعی اجرا نشده) + رویداد ممیزی → یک `SaveChanges` + `Commit`؛ شکست ⇒ `Rollback` + `ChangeTracker.Clear` و بررسی درخواست هم‌زمان.
- مالکیت: Company/FiscalPeriod/Role/Membership = Legacy-Tenant (= Company.Id) طبق ماتریس بند ۳؛ User = Global؛ Claim = سطح نصب.
- Dedicated/Cloud: کد واحد است؛ تفاوت فقط deployment است (رشتهٔ اتصال و `InstallationId` از پیکربندی استقرار؛ `--migrate` برای هر دو). Online/Offline در این مسیر معنا ندارد؛ Offline فقط در licensing (بخش‌های قبلی سند).

**مسیر ۳ — کاربر و عضویت:** بند ۱۴.۲.

### ۱۴.۲ جریان ایجاد کاربر و عضویت (بند ۵.۲)

**Frontend (`ClientApp/src/components/admin/users/userCreateFlow.ts`):**
- جریان دومرحله‌ای طبق D-072: گام ۱ ایجاد حساب (`createAdminUser` → `POST /admin/users`)؛ گام ۲ رکورد پرسنلی HR **بدون link** (ایجاد link به عضویت فعال نیاز دارد که حساب تازه ندارد). شکست گام ۱ به فرم پرتاب می‌شود (هیچ چیزی ساخته نشده)؛ پس از وجود حساب، تابع دیگر throw نمی‌کند — شکست گام ۲ با flash گزارش می‌شود تا حساب پنهان نشود. 409 شمارهٔ پرسنلی تکراری فقط برای «پیشنهاد دست‌نخورده» تا ۲ بار با شمارهٔ جدید تکرار می‌شود؛ عکس پس از ایجاد رکورد جدا آپلود می‌شود.
- سازگاری FE/BE: اعتبارسنجی سمت کلاینت (`userFormValidation`) با `UserCreationValidation` سرور هم‌سیاست است؛ `companyId` رکورد پرسنلی از WorkContext سمت سرور می‌آید و endpointهای HR همان را با عضویت فعال دوباره می‌سنجند (بند ۱۳).

**Backend — دو دروازهٔ ایجاد کاربر با قرارداد واحد:**
- `UserCreationValidation` مشترک برای `POST /admin/users` (مدیر نصب) و `POST /companies/{companyId}/users` (D-078) — «endpoint شرکت نباید bypass ضعیف‌تر همان سیاست باشد»؛ email اختیاری، username با regex/طول، سیاست رمز + تأیید، موبایل با فرمت ثابت.
- **D-078 (`CompanyUserProvisioningEndpoints`):** شرکت از route؛ نویسنده یا مدیر نصب است (با شرکت فعال) یا باید عضویت فعال + `users.create` در **همان شرکت** داشته باشد ⇒ «مدیر شرکت A هرگز نمی‌تواند عضویت فعال در شرکت B بسازد؛ تنها مسیر ورود به شرکت دیگر، گردش‌کار درخواست با تأیید مقصد است» (doc خود فایل). عضویت همان‌شرکت فوراً فعال با `RoleId=null` (roleless طبق D-078؛ هیچ نقش/مجوز/دوره‌ای تا `roles.assign` زیر سقف D-071). پاسخ 201 یکنواخت (ایجاد یا استفادهٔ مجدد) ⇒ non-enumerable؛ هدر no-store؛ rate limit حساس + anti-forgery.
- **جلوگیری از عضویت تکراری:** عضویت فعال موجود ⇒ 409؛ عضویت غیرفعال/بدون نقش ⇒ 409 با ارجاع به گردش‌کار درخواست/تأیید؛ هم‌زمانی با unique index دیتابیسی `(UserId, TenantId)` → SqlException 2601/2627 ⇒ 409. تکرار username/email/موبایل با unique indexهای User + پیش‌چک ⇒ 409.
- **بین‌شرکتی (درخواست/تأیید):** `RequestMembershipAsync` — درخواست‌کننده فعال؛ شرکت مقصد فقط وقتی مجاز که فعال و در **همان `CustomerBoundaryId`** یکی از شرکت‌های قابل‌دسترس درخواست‌کننده باشد (fail-closed، مستقل از مجوز)؛ عضویت فعال مقصد ⇒ 409؛ درخواست Pending موجود ⇒ 409 + unique index فیلتردار `[Status]='Pending'` به‌عنوان آخرین نگهبان هم‌زمانی.
- **`ApproveAsync`:** مجوز مقصد `AuthorizeDestinationApproverAsync` (مدیر نصب از DB با استثنای bootstrap D-071/بدون سقف D-064، یا دارندهٔ مجوز تأیید در شرکت مقصد؛ غیرمجاز ⇒ 404 یکنواخت) **دو بار** اجرا می‌شود — بار دوم داخل تراکنش `ReadCommitted` تا سقف واگذاری با همان ردیف‌های تازه نوشته شود؛ درخواست باید `DestinationTenantId == companyId` و Pending باشد؛ تأیید درخواست خودش ⇒ 403؛ نقش مقصد باید `TenantId == companyId` و فعال و غیرسیستمی باشد؛ سقف D-071: فقط Permissionهایی که تأییدکننده الان در مقصد دارد و هرگز مجوز تأیید واگذار نمی‌شود (جلوگیری از تکثیر تأییدکننده)؛ `TrackedDelegationCeiling` + نگهبان‌های RowVersion ⇒ لغو هم‌زمان fail-closed؛ عضویت: ایجاد فعال با نقش یا فعال‌سازی/تعویض نقش عضویت غیرفعال؛ عضویت فعال موجود ⇒ 409؛ ثبت `ApprovedRoleId` و ممیزی.
- **تخصیص TenantId/CompanyId:** در همهٔ این مسیرها `TenantId` از context سمت سرور (route تأییدشده با عضویت) می‌آید؛ هیچ ورودی کلاینتِ TenantId پذیرفته نمی‌شود (تست‌های بند ۹/۱۳).
- **تغییر تصمیم‌های مصوب تأیید عضویت:** هیچ تغییری داده نشد.

### ۱۴.۳ `SetDefaultCompanyAsync` (بند ۵.۳)
- **تک پیاده‌سازی:** `Server/Api/WorkContextEndpoints.cs:83` (route `POST /context/default-company` در خط ۲۸)؛ call site دیگری برای این نام وجود ندارد.
- اعتبارسنجی: anti-forgery؛ فقط کاربر شرکت‌محور؛ حساب باید فعال باشد (Unauthorized در غیر این صورت)؛ شرکت فقط با `FindAccessibleCompanyAsync` (عضویت فعال) پذیرفته می‌شود — **شرکت نامرتبط و ناموجود هر دو 404** (بدون قابلیت کشف شناسه‌ها)؛ ذخیره در `Users.DefaultCompanyId` (ترجیح پروفایل کاربر، ماندگار بین نشست‌ها — بخشی از Session نیست).
- **دورهٔ مالی اینجا بررسی نمی‌شود** — عمداً: این فقط ترجیح است؛ ورود واقعی به محیط کار از `POST /context` (`SelectAsync`) است که دوره را با `TenantId == access.CompanyId` (شناسهٔ تأییدشده، نه ورودی کلاینت) و سیاست `FiscalPeriodSelectionPolicy` می‌سنجد؛ دورهٔ غیرقابل‌انتخاب ⇒ 409، دورهٔ شرکت دیگر/ناموجود ⇒ 404 یکنواخت.
- چندشرکتی: ترجیح پیشنهادی است؛ در `GET /context` اگر شرکت ترجیحی دیگر در گزینه‌های قابل‌دسترس نباشد، **پنهان و بی‌اثر** می‌شود («A stale preference never grants access») — پس لغو عضویت ترجیح را بی‌اثر می‌کند بدون نیاز به پاک‌سازی.
- عبور از مرز Tenant: ممکن نیست — همان دروازهٔ عضویت فعالِ همهٔ مسیرها؛ امروز (Legacy) عضویت یعنی Company، و در مدل هدف D-087 نیز عضویت به Company است، پس همین چک برای Tenant آینده کافی می‌ماند.
- سازگاری با Session/پروفایل: ترجیح در DB است و در `GET /context` بازگردانده می‌شود؛ کوکی WorkContext فقط انتخاب همین نشست را نگه می‌دارد (D-031) و در هر خواندن دوباره از DB اعتبارسنجی می‌شود؛ هیچ ترجیحی در کوکی/ذخیرهٔ کلاینت ذخیره نمی‌شود (بند ۱۳.۳).
- تست‌های مرتبط: `WorkContextTests`، `Integration/WorkContextHttpTests`، `CompanyAccessTests` (کوکی دست‌کاری‌شده، دورهٔ شرکت دیگر).

### ۱۴.۴ Session و احراز هویت (بند ۵.۴)
- **`SessionValidator` روی هر درخواست:** حساب موجود و فعال؛ credential stamp = SHA-256 از hash ذخیره‌شده با مقایسهٔ زمان-ثابت (تغییر رمز/قالب hash همهٔ sessionهای قبلی را باطل می‌کند)؛ guard «enrollment provisioning ناتمام» (`MustChangePassword`) در همان یک query؛ ردیف session سمت سرور (D-069) باید متعلق به همین کاربر، غیرلغوشده و داخل پنجرهٔ idle ۳۰ دقیقه‌ای (هم‌سان با sliding expiration کوکی) باشد؛ نوشتن `LastSeenAtUtc` حداکثر هر ۵ دقیقه با `ExecuteUpdateAsync` مشروط به `RevokedAtUtc == null` (لغو هم‌زمان، concurrency exception نمی‌سازد؛ مسیر tracked فقط برای InMemory تست). حساب زیرساختی با پیکربندی سنجیده می‌شود. کوکیِ بدون stamp/SessionId (نسخهٔ قدیم) یک‌بار رد و کاربر به ورود مجدد می‌رود.
- **نقش (R-16):** claim نقش کوکی در هر درخواست با نقش **فعلی DB** جایگزین می‌شود؛ نقش لغوشده/تغییرکرده از کوکی قدیم اثری ندارد (session می‌ماند، فقط claim عوض می‌شود) — `AdminRoleRevalidationHttpTests`.
- **مسیرهای لغو session:** خودکاربر (`UserSessionService.RevokeAsync` با reason code)، مدیر نصب (`AdminUserSessionEndpoints`: فهرست/لغو یکی/لغو همه، ممیزی `AdminUserSessionRevoked`)، تغییر رمز (`UserPasswordService` همهٔ sessionهای فعال کاربر را set-based لغو می‌کند) و credential stamp. **لغو عضویت/تغییر وضعیت عضویت، session را لغو نمی‌کند** — عمداً: به‌جایش `CompanyAccessService` عضویت‌ها را در هر فراخوانی از DB می‌خواند و `GetCurrentAsync` انتخاب ذخیره‌شده را در هر خواندن دوباره resolve و در صورت بی‌اعتباری کوکی را حذف می‌کند ⇒ لغو عضویت از «درخواست بعدی» محیط کار را می‌بندد، بدون حذف session (کاربر ممکن است عضو شرکت دیگر باشد).
- **Context در Session:** هیچ TenantId/CompanyId/FiscalPeriodId قابل‌اعتمادی در session/claim نیست؛ انتخاب فقط در `WorkContextCookie` per-sign-in است (D-031) و ورودی کلاینت هرگز مستقیماً tenant predicate نمی‌شود (`ResolveAsync` خط ~۲۶۰: `item.TenantId == access.CompanyId` با شناسهٔ تأییدشده). تست‌های claim جعلی در بند ۹.
- چندشرکتی: گزینه‌ها فقط از `GetAccessibleCompaniesAsync` (عضویت فعال + شرکت فعال + نقش فعال + `Role.TenantId == membership.TenantId`)؛ شرکت ناشناخته و شرکت tenant دیگر پاسخ یکسان می‌دهند.

### ۱۴.۵ Foreign Key و Referential Integrity (بند ۵.۵)
**FKهای واقعی دیتابیسی (همه `Restrict`) در Core** — از `Infrastructure/Persistence/Configurations/*`:
| رابطه | نوع FK | تضمین مرز |
|---|---|---|
| `UserCompanyMemberships.TenantId → Companies.Id` | ساده | عضویت به شرکت واقعی |
| `UserCompanyMemberships.(RoleId, TenantId) → CompanyRoles.(Id, TenantId)` (HasPrincipalKey) | مرکب | **نقش شرکت دیگر در سطح DB ناممکن** |
| `UserCompanyMemberships.(Id, TenantId)` | AlternateKey | هدف مرجع مرکب امن برای D-072 |
| `FiscalPeriods.TenantId → Companies.Id` + AK `(Id,TenantId)` + unique `(TenantId,StartDate,EndDate)` | ساده | دورهٔ هر شرکت از شرکت دیگر جدا |
| `CompanyRoles.TenantId → Companies.Id` + unique `(TenantId,Code)` + AK | ساده | نقش‌های هر شرکت مستقل |
| `CompanyMembershipRequests.DestinationTenantId → Companies` + مرکب `(ApprovedRoleId, DestinationTenantId) → CompanyRoles.(Id,TenantId)` | ساده+مرکب | تأیید با نقش شرکت مقصد دیگر ناممکن؛ unique `(UserId,DestinationTenantId)` فیلتردار `[Status]='Pending'` |
| HR: `CompanyEmployees/Units/Positions/Assignments/Photos.TenantId → Companies` + FKهای مرکب فرزند→والد با `(Id,TenantId)` | ساده+مرکب | تعلق فرزند به همان Tenant/Company در DB قفل است |
| `UserSessions.UserId → Users` | ساده | session یتیم ناممکن |
| `Users.DefaultCompanyId → Companies` با **SetNull** | ساده | حذف شرکت، ترجیح را null می‌کند |
| `ProvisioningRequestClaims.ParentCompanyId → Companies` | ساده | — |
| `CorporateGroupMembers → CorporateGroups + Companies`، `OwnershipRelationships.Parent/Subsidiary → Companies` + uniqueهای `(…) + ValidFrom` | ساده | روابط گروه/مالکیت بین شرکت‌ها **در DB ممکن‌اند**؛ جداسازی در اپ با تساوی `CustomerBoundaryId` و پاسخ fail-closed «unknown» (D-033/D-037/D-038؛ بند ۱۴.۳ / `GetCompanyRelationshipTypesAsync`) |
| uniqueهای هویتی: `Username`، `EmailAddress` (فیلتردار)، `Companies.NationalId` (فیلتردار)؛ `ProvisioningRequestClaims.RequestId/NonceHash/EnrollmentTokenHash` | — | تکرار/کاشتنی ناممکن در DB |

**شکاف‌های مستند (موجود، بدون تغییر در این PR):**
1. `UserCompanyMemberships.UserId` **FK به Users ندارد** (قبلاً در DATABASE.md ثبت شده؛ جلوگیری از عضویت یتیم فقط در اپ/تست؛ اصلاح، تصمیم جدا می‌خواهد).
2. `ProvisioningRequestClaims`: `(FiscalPeriodId, CompanyId)` یک FK مرکب به `FiscalPeriod (Id, TenantId)` با `Restrict` دارد (`ProvisioningRequestClaimConfiguration`، Migration `20261029161402_AddCustomerProvisioningClaims`)؛ `CompanyId` FK مستقل به `Companies` ندارد و فقط از همین FK مرکب پوشش می‌گیرد. `AdminUserId` FK ندارد و فقط ایندکس `(AdminUserId, MustChangePassword)` دارد (صحت در اپ، داخل همان تراکنش Serializable که همه را یک‌جا می‌سازد؛ عمر ردیف کوتاه است).
3. `SecurityAuditEvents` عمداً FK ندارد (مستقل‌بودن audit؛ فقط index).
4. **Trade:** `TradeEntity` AK `(TenantId, Id)` دارد ولی هیچ FK به Core ندارد (مرز ماژول، طبق بند ۲ قبلی)؛ `TenantId` فقط Guid است — درج مستقیم SQL می‌تواند ردیف با TenantId دلخواه بسازد؛ کنترل در اپ با `TradeAccessFilter` fail-closed است و تهدید درج مستقیم در Threat/بند ۱۱ قبلی باز است. FKهای داخلی Trade (Party→PartyBankAccount و…) با AK مرکب داخل همان DbContext است.
- **دو Tenant متفاوت:** با معنای Legacy (`TenantId == Company.Id`)، تمام FKهای مرکب بالا «از شرکت دیگر» را در سطح DB ناممکن می‌کنند؛ تنها روابط عمداً بین‌شرکتی (گروه/مالکیت) و روابط مستقل از شرکت (کاربر/نقش سراسری/Trade بدون FK) از این قاعده خارج‌اند — اولی با `CustomerBoundaryId` در اپ محدود می‌شود و دومی طبق D-088 مجاز است. در گذار به Tenant واقعی (S3-0f)، همین FKهای مرکب الگوی امن را آماده دارند (`TenantId` بخشی از کلید است).
- تست‌های مدل: `DatabaseModelTests` (تغطیهٔ همهٔ جدول‌ها، PK Guid برای همه، `TheFiscalPeriodOfARunCompany_MustBelongToTheSameCompany`، singleton/RowVersion) و `LegacyTenantIdEntityContractTests` (فهرست قطعی ستون‌های Legacy: ۱۰ ستون Core + ۵ ستون Trade؛ با mutation).
- **طبقه‌بندی مالکیت additions به ماتریس بند ۳:** `Users.DefaultCompanyId` = Cross-company relationship (ترجیح، FK SetNull)؛ `ProvisioningRequestClaims` = Installation-owned با ارجاعات؛ `SecurityAuditEvents` = Global/audit با `TenantId` nullable میراثی؛ `CompanyEmployeePhotos` = Company-owned (BLOB در SQL).

### ۱۴.۶ وضعیت نهایی به‌روز S3-0d-b
- **Verified (static/InMemory):** همهٔ بندهای ۱ تا ۱۴ در حد خواندن Source؛ چرخهٔ کامل «ایجاد دیتابیس → Provisioning → کاربر/عضویت → انتخاب context → اعتبارسنجی هر درخواست» با مسیرهای دقیق و کنترل‌های هر نقطه مستند شد.
- **NotVerified/NotRun (بدون تغییر):** دو Tenant واقعی (Entity وجود ندارد)، اجرای تست‌ها روی SQL Server واقعی، مقادیر `TenantId` در دیتابیس واقعی، اجرای Vitest.
- **هیچ تصمیم جدیدی گرفته نشد**؛ شکاف‌های FK (بند ۱۴.۵) فقط ثبت شدند. ورودی‌های طراحی S3-0e/f/g: بند ۱۴ نشان می‌دهد تنها نقطهٔ ایجاد Company مسیر Provisioning است و الگوی FK مرکب `(…, TenantId)` از قبل برقرار است — این دو، ریسک طرح گذار را کاهش می‌دهند.
