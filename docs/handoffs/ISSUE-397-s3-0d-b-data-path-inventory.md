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
- **اجرا:** در این PR هیچ تستی اجرا نشد؛ فهرست بالا بر پایهٔ نام‌ها و فایل‌هاست و موفقیت آن‌ها در این کار ادعا نمی‌شود.

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
