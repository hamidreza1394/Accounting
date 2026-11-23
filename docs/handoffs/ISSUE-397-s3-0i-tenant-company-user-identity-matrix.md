# S3-0i — ماتریس تکمیلی هویت و مالکیت Tenant/Company (2026-11-23)

- **مخزن/مبنای کد:** `hamidreza1394/Accounting`، شاخه `main`، Commit `62b0c3c6385e299785d2e56c94d3f8d7bdfff10e`.
- **نوع کار:** ممیزی ایستا و مستندسازی؛ بدون تغییر کد، Schema، Migration، Backfill، Provisioning، دیتابیس یا Runtime.
- **وضعیت:** طراحی تکمیلی؛ D-118 برای هویت کاربران مشتری Accepted است؛ D-091 جهت Rename فیزیکی را منعکس می‌کند اما ترتیب اجرایی/گیت‌ها همچنان Proposed است.
- **رابطه با اسناد قبلی:** این الحاقیه مفروضات هویتی قدیمی S3-0h را در موارد مشخص جایگزین می‌کند؛ متن تاریخی S3-0h و S3-0e حذف نشده است. هیچ گیت G0/G3/G5/G6 بسته نشده است.
- **محدودیت ممیزی:** Entityها، EF Configurationهای اصلی، مسیرهای Login/Provisioning/Membership و اسناد مرجع خوانده شدند. این سند ادعای ممیزی کامل همه queryها، endpointها، migrationها، همه زیرسیستم‌ها یا اجرای SQL Server ندارد؛ آن inventory باید در مرحله اجرایی/گیت مربوطه تکمیل شود.

## 1. قواعد معماری تأییدشده

1. Tenant مرز مشتری/فضای کاری است؛ Company شرکت حقوقی/عملیاتی داخل Tenant است (D-087/D-103).
2. هر Company دقیقاً به یک Tenant تعلق دارد؛ زوج Tenant/Company ناسازگار نباید پذیرفته شود.
3. هر رکورد Company-owned در مدل هدف TenantId و CompanyId مستقل دارد. اگر جدول به چند Company ارجاع می‌دهد، فیلدهای دامنه‌ای موجود مانند ParentCompanyId، SubsidiaryCompanyId یا OwnerCompanyId حفظ می‌شوند؛ CompanyId تکراری و بی‌معنا اضافه نمی‌شود.
4. هر رکورد Tenant-owned فقط TenantId دارد؛ جداول Global، Installation-scoped، Vendor-scoped و Identity infrastructure طبق مالکیت واقعی خودشان مکانیکی Tenant/Company نمی‌گیرند.
5. هویت کاربران مشتری Tenant-scoped است (D-118): User دارای TenantId است، Username در محدوده Tenant یکتا است و User CompanyId ندارد.
6. یک User می‌تواند در چند Company همان Tenant عضویت مستقل داشته باشد؛ هر عضویت مقصد جداگانه لازم است و عضویت بین‌شرکتی جدید طبق D-065 نیازمند تأیید مقصد است.
7. دو Tenant مستقل می‌توانند Username یکسان داشته باشند؛ این حساب‌ها مستقل‌اند. هیچ ارتباط هویتی یا مجوزی از Username، Email، Mobile، CustomerBoundary، دامنه، مالکیت یا گروه استنتاج نمی‌شود.
8. Email، CellPhoneNumber و verification keyها فعلاً Unique Index سراسری فعلی را حفظ می‌کنند؛ تغییر این دامنه نیازمند تصمیم مستقل است.
9. Tenant در Login فقط از Host canonical/verified طبق D-103 یا تنظیم معتبر Dedicated resolve می‌شود؛ شناسه Tenant کلاینت مرجع نیست.
10. D-090 قرارداد Host→Trade با pair معتبر را حفظ می‌کند؛ Trade به Core FK مستقیم نمی‌گیرد و implementation آن در حوزه Claude باقی می‌ماند.

## 2. ماتریس Entity/جدول

| Entity / جدول | مالکیت هدف | شناسه‌های هدف | وضعیت/اقدام |
|---|---|---|---|
| Tenants | ریشه مشتری | Id | Entity/جدول هدف؛ ایجاد و provisioning طبق D-089/D-110، هنوز NotImplemented |
| Companies | Tenant-owned entity | Id + TenantId | افزودن TenantId واقعی و FK به Tenants؛ CompanyId همان Id است |
| Users | Tenant-owned identity | Id + TenantId | افزودن TenantId اجباری؛ حذف Company scope؛ Username unique per Tenant |
| UserSessions | متعلق به User/Tenant | Id + UserId + TenantId | TenantId اجباری و FK مرکب به User؛ بدون CompanyId، چون انتخاب شرکت بعد از ورود است |
| LoginLogs | وابسته به User | Id + UserId | Tenant از User resolve شود؛ TenantId/CompanyId تکراری لازم نیست مگر نیاز partition/audit اثبات شود |
| UserCompanyMemberships | Company-owned relationship | UserId + TenantId + CompanyId + RoleId? | ستون TenantId فعلی در واقع CompanyId است؛ Rename فیزیکی و افزودن TenantId واقعی؛ Unique per User+Company؛ FK مرکب به User/Company/Role |
| CompanyRoles | Company-owned | Id + TenantId + CompanyId | TenantId فعلی در واقع CompanyId است؛ Rename و افزودن TenantId واقعی؛ Code یکتا در محدوده Company |
| FiscalPeriods | Company-owned | Id + TenantId + CompanyId | TenantId فعلی در واقع CompanyId است؛ Rename و افزودن TenantId؛ بازطراحی AK/Unique بر مبنای Company |
| CompanyMembershipRequests | درخواست عضویت به مقصد Company | Id + TenantId + DestinationTenantId + DestinationCompanyId | DestinationTenantId فعلی در واقع CompanyId مقصد است؛ Rename به DestinationCompanyId و افزودن TenantId واقعی مقصد؛ API/DTO نیز اصلاح شود |
| SecurityAuditEvents | Mixed: System/Tenant/Company | Id + TenantId? + CompanyId? + UserId? | System: هر دو null؛ Tenant event: فقط TenantId؛ Company event: هر دو؛ معنای ستون legacy باید جداگانه Backfill شود |
| CompanyOrganizationalUnit | Company-owned | Id + TenantId + CompanyId | TenantId فعلی legacy CompanyId؛ Rename + TenantId واقعی |
| CompanyPosition | Company-owned | Id + TenantId + CompanyId | TenantId فعلی legacy CompanyId؛ Rename + TenantId واقعی |
| CompanyEmployee | Company-owned | Id + TenantId + CompanyId | TenantId فعلی legacy CompanyId؛ Rename + TenantId واقعی |
| CompanyEmployeeAssignment | Company-owned | Id + TenantId + CompanyId | TenantId فعلی legacy CompanyId؛ Rename + TenantId واقعی |
| CompanyEmployeePhoto | Company-owned | Id + TenantId + CompanyId | TenantId فعلی legacy CompanyId؛ Rename + TenantId واقعی |
| CorporateGroups | Company-owned by owner company | Id + TenantId + OwnerCompanyId | OwnerCompanyId حفظ شود؛ TenantId باید با OwnerCompany تطبیق کند؛ CompanyId تکراری اضافه نشود |
| CorporateGroupMembers | رابطه گروه و Company | Id + TenantId + CorporateGroupId + CompanyId | CompanyId عضو حفظ شود؛ Group و Member باید متعلق به همان Tenant باشند |
| OwnershipRelationships | رابطه دو Company | Id + TenantId + ParentCompanyId + SubsidiaryCompanyId | هر دو Company باید در همان Tenant باشند؛ فیلد CompanyId عمومی تکراری اضافه نشود |
| ConsolidationProfile / ConsolidationRun | نیازمند تعیین owner دقیق | فعلاً بدون فرض فیلد جدید | ممیزی فعلی برای تعیین Company owner و دامنه هر رکورد کافی نیست؛ پیش از DDL بررسی مستقل لازم است |
| ConsolidationRunCompanies | مشارکت Company در Run | ConsolidationRunId + TenantId + CompanyId + FiscalPeriodId | Company، Run و FiscalPeriod باید به همان Tenant/Company تعلق داشته باشند؛ FK مرکب فعلی بر اساس TenantId legacy بازبینی شود |
| ProvisioningRequestClaims | Installation-scoped | InstallationId + CompanyId? + FiscalPeriodId? + AdminUserId? | TenantId مکانیکی اضافه نشود؛ رابطه AdminUser با Tenant/Company در سرویس Provisioning باید صریحاً اعتبارسنجی شود |
| InstallationIdentities | Installation-scoped | InstallationId | بدون TenantId/CompanyId مکانیکی |
| InstalledLicenses | Installation-scoped | InstallationId / License identity | بدون TenantId/CompanyId مکانیکی مگر قرارداد License صریحاً آن را لازم کند |
| CustomerOfflineRevocationStates | Installation-scoped | InstallationId | بدون TenantId/CompanyId مکانیکی |
| CompanySetupRecords | Installation-scoped mapping به Company | InstallationId + CompanyId | CompanyId فعلی حفظ شود؛ TenantId فقط با نیاز اثبات‌شده و FK/Registry معتبر اضافه شود |
| PermissionDefinitions | Global catalog | Id | بدون TenantId/CompanyId |
| Roleهای سراسری قدیمی | Legacy/system role catalog | Id | بدون TenantId/CompanyId مکانیکی؛ مجوز عملیاتی شرکت همچنان CompanyRole است |
| Trade entities مانند Party/Product و جداول وابسته | Company-owned در Trade | TenantId + CompanyId | کد و migration فقط در حوزه Claude؛ بدون FK مستقیم به Core طبق D-090؛ Host pair را validate و Store هر دو Scope را اعمال کند |
| Trade reference data | Global/static | شناسه مرجع | بدون TenantId/CompanyId |

## 3. قواعد کلیدی و Indexهای هدف

- `Users`: PK فعلی Id حفظ می‌شود؛ AK مرکب `(Id, TenantId)` برای FKهای امن قابل استفاده است؛ Username با canonicalization یکسان در Login و Unique Index، در محدوده Tenant یکتا می‌شود.
- `UserSessions`: TenantId اجباری و FK مرکب `(UserId, TenantId) → Users(Id, TenantId)`؛ SessionId تصادفی/PK مستقل باقی می‌ماند؛ CompanyId روی Session ذخیره نمی‌شود.
- `UserCompanyMemberships`: Unique `(UserId, TenantId, CompanyId)`؛ FK به User با `(UserId,TenantId)`؛ FK به Company با `(CompanyId,TenantId)`؛ Role با `(RoleId,TenantId,CompanyId)` یا کلید معادل معتبر.
- `Companies`: AK `(Id,TenantId)` و FK `TenantId → Tenants.Id`؛ قیود مرکب برای Company-owned children.
- `CompanyRoles`: Unique `(TenantId,CompanyId,Code)`؛ نقش با Company scope.
- `FiscalPeriods`: Unique بازه/شناسه در محدوده Company، نه کل Tenant.
- `Users.DefaultCompanyId`: ترجیح است؛ FK مرکب `(DefaultCompanyId,TenantId)` یا کنترل معادل؛ انتخاب نهایی باید membership را نیز بررسی کند.
- `LoginLogs`: query/report از طریق User و Tenant فیلتر شود؛ از ذخیره Scope تکراری بدون نیاز اجتناب شود.
- `CompanyMembershipRequests`: مقصد به زوج معتبر `(DestinationTenantId,DestinationCompanyId)` اشاره کند؛ درخواست Cross-Tenant رد شود.
- `SecurityAuditEvents`: Indexها بر اساس nullable scope و retention policy طراحی شوند؛ هر دو Scope null فقط برای eventهای system-level مجاز است.
- همه unique indexها و FKهای فعلی که TenantId legacy را CompanyId فرض کرده‌اند باید در D-091 inventory و بازطراحی شوند؛ نام‌های قدیمی قیدها قبل از Migration pin شوند (D-095).

## 4. جریان Login و انتخاب Company

1. Host canonical و verified یا Dedicated Installation config، Tenant را سمت سرور resolve می‌کند (D-103).
2. Authentication با `TenantId + normalized Username` جستجو می‌کند؛ TenantId از درخواست کاربر پذیرفته نمی‌شود.
3. پس از اعتبارسنجی رمز و وضعیت حساب، UserSession با UserId و TenantId ایجاد می‌شود.
4. فهرست Companyها فقط از Membershipهای Approved و فعال همان Tenant ساخته می‌شود.
5. انتخاب Company ورودی درخواست است؛ سرور عضویت، وضعیت Company، Role، Permission و FiscalPeriod را دوباره اعتبارسنجی می‌کند.
6. اگر دو Tenant هر دو `hamid` داشته باشند، دامنه/Installation context مشخص می‌کند کدام حساب lookup شود؛ نام کاربری به‌تنهایی هرگز Tenant را تعیین نمی‌کند.

## 5. نکات باز و موارد نیازمند بررسی مستقل

1. دامنه یکتایی Email و CellPhoneNumber فعلاً Global باقی می‌ماند؛ مالک می‌تواند در تصمیم جداگانه درباره Tenant-scoped شدن آن‌ها تصمیم بگیرد.
2. Canonicalization دقیق Username (case-folding، فاصله، Unicode و collation SQL Server) باید در قرارداد Login و Unique Index یکسان باشد؛ نباید فقط به `ToLower()` وابسته باشد.
3. رابطه دقیق ProvisioningRequestClaim با Tenant جدید، به‌ویژه AdminUserId، باید با D-110 و bootstrap مستقل مدیر اولیه هماهنگ شود؛ بدون افزودن ستون صرفاً برای راحتی.
4. نوع رویدادهای SecurityAuditEvent و Backfill آن‌ها باید از writers واقعی استخراج شود؛ system events نباید به Tenant ساختگی نسبت داده شوند.
5. CorporateGroup/Ownership/Consolidation باید same-Tenant را در همه queryها و تراکنش‌ها کنترل کنند؛ صرفاً FK به Companyهای مستقل کافی نیست.
6. مدیر نصب/Role قدیمی `Admin`، صفحات Razor قدیمی و APIهای سراسری باید با D-064/D-029 تطبیق داده شوند؛ وجود Master config account به معنی مجاز بودن دسترسی cross-tenant برای هر User با Role=Admin نیست.
7. Email/Mobile verification key uniqueness و password recovery endpoints باید در ممیزی اجرایی بررسی شوند؛ این ADR دامنه یکتایی آن‌ها را تغییر نداده است.

## 6. وضعیت گیت‌ها و گام بعدی

- D-118 Accepted فقط قرارداد هویت مشتری را تعیین می‌کند؛ پیاده‌سازی آن NotImplemented است.
- D-091 هنوز Proposed است؛ جهت Rename فیزیکی مطابق نظر مالک است اما ترتیب اجرایی، Down/Roll-forward، SQL Server proof و مجوزهای migration باید جداگانه بسته شوند.
- D-092 Manifest Backfill، D-093 Shared Runtime، D-094 Trade migration policy، D-095 constraint naming و D-096 runbook طبق وضعیت ADR خود باقی می‌مانند.
- G0 نیازمند تأیید طرح جامع؛ G3 نیازمند آزمون SQL Server واقعی؛ G5 نیازمند Backup و مجوز دیتابیس مشتری؛ G6 نیازمند آزمون جداسازی Shared Runtime است.
- هیچ Migration، Backfill، schema change یا اجرای دیتابیس در این کار انجام نشده است.

