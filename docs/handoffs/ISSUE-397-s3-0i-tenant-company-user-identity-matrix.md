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
| UserSessions | وابسته به User (Tenant از رابطه User) | Id + UserId | FK موجود به UserId کافی است؛ TenantId/CompanyId تکراری اضافه نشود؛ query مدیریتی از طریق User فیلتر شود |
| LoginLogs | وابسته به User | Id + UserId | Tenant از User resolve شود؛ TenantId/CompanyId تکراری لازم نیست مگر نیاز partition/audit اثبات شود |
| UserCompanyMemberships | Company-owned relationship | UserId + TenantId + CompanyId + RoleId? | ستون TenantId فعلی در واقع CompanyId است؛ Rename فیزیکی و افزودن TenantId واقعی؛ Unique per User+Company؛ FK مرکب به User/Company/Role |
| CompanyRoles | Company-owned | Id + TenantId + CompanyId | TenantId فعلی در واقع CompanyId است؛ Rename و افزودن TenantId واقعی؛ Code یکتا در محدوده Company |
| FiscalPeriods | Company-owned | Id + TenantId + CompanyId | TenantId فعلی در واقع CompanyId است؛ Rename و افزودن TenantId؛ بازطراحی AK/Unique بر مبنای Company |
| CompanyMembershipRequests | درخواست عضویت به مقصد Company | Id + UserId + DestinationTenantId + DestinationCompanyId | DestinationTenantId فعلی در واقع CompanyId مقصد است؛ Rename به DestinationCompanyId و افزودن TenantId واقعی مقصد؛ User و Company مقصد باید به همان Tenant تعلق داشته باشند |
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
- `UserSessions`: FK موجود `UserId → Users.Id` کافی است؛ UserId سراسری و یکتا است و Tenant از User resolve می‌شود؛ SessionId تصادفی/PK مستقل باقی می‌ماند؛ TenantId/CompanyId تکراری ذخیره نمی‌شود.
- `UserCompanyMemberships`: Unique `(UserId, TenantId, CompanyId)`؛ FK به User با `(UserId,TenantId)`؛ FK به Company با `(CompanyId,TenantId)`؛ Role با `(RoleId,TenantId,CompanyId)` یا کلید معادل معتبر.
- `Companies`: AK `(Id,TenantId)` و FK `TenantId → Tenants.Id`؛ قیود مرکب برای Company-owned children.
- `CompanyRoles`: Unique `(TenantId,CompanyId,Code)`؛ نقش با Company scope.
- `FiscalPeriods`: Unique بازه/شناسه در محدوده Company، نه کل Tenant.
- `Users.DefaultCompanyId`: ترجیح است؛ FK مرکب `(DefaultCompanyId,TenantId)` یا کنترل معادل؛ انتخاب نهایی باید membership را نیز بررسی کند.
- `LoginLogs`: query/report از طریق User و Tenant فیلتر شود؛ از ذخیره Scope تکراری بدون نیاز اجتناب شود.
- `CompanyMembershipRequests`: زوج `(UserId,DestinationTenantId)` باید به User همان Tenant و زوج `(DestinationCompanyId,DestinationTenantId)` به Company مقصد همان Tenant اشاره کند؛ درخواست Cross-Tenant رد شود.
- `SecurityAuditEvents`: Indexها بر اساس nullable scope و retention policy طراحی شوند؛ هر دو Scope null فقط برای eventهای system-level مجاز است.
- همه unique indexها و FKهای فعلی که TenantId legacy را CompanyId فرض کرده‌اند باید در D-091 inventory و بازطراحی شوند؛ نام‌های قدیمی قیدها قبل از Migration pin شوند (D-095).

## 4. جریان Login و انتخاب Company

1. Host canonical و verified یا Dedicated Installation config، Tenant را سمت سرور resolve می‌کند (D-103).
2. Authentication با `TenantId + normalized Username` جستجو می‌کند؛ TenantId از درخواست کاربر پذیرفته نمی‌شود.
3. پس از اعتبارسنجی رمز و وضعیت حساب، `UserSession` فقط با `UserId` ایجاد می‌شود؛ `TenantId` در این گام صرفاً زمینهٔ اعتبارسنجی است و در Session ذخیره نمی‌شود — تعلق Tenant از رابطهٔ `UserId → Users.TenantId` Resolve می‌شود (سازگار با ردیف جدول بالای سند و بندهای 29/59).
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


## تحویل وظیفه طبق قالب مخزن

- **Issue:** #397 — https://github.com/hamidreza1394/Accounting/issues/397
- **Agent/مسئول:** ChatGPT؛ ممیزی و مستندسازی Read-only
- **تاریخ:** 2026-11-23
- **وضعیت:** ناقص از نظر ممیزی اجرایی کامل؛ بسته مستنداتی برای Review آماده است.

### هدف و معیارهای پذیرش
- [x] ثبت تصمیم هویت Tenant-scoped کاربران مشتری در D-118.
- [x] تفکیک User در سطح Tenant از Membership در سطح Company.
- [x] ثبت تکرار Username بین Tenantها بدون پیوند هویتی.
- [x] تعیین تکلیف عدم افزودن TenantId تکراری به UserSession و LoginLog.
- [x] بازنگری جهت D-091 برای Rename فیزیکی legacy TenantId به CompanyId و افزودن TenantId واقعی.
- [x] ثبت ماتریس Entity/مالکیت و کلیدهای هدف.
- [ ] ممیزی کامل همه Queryها، Endpointها، Writers، Migrationها و آزمون‌های SQL Server در Issue اجرایی بعدی.
- [ ] بستن گیت‌های G0/G3/G5/G6؛ این PR هیچ‌کدام را نمی‌بندد.

### خلاصه تغییرات
- D-118 به‌عنوان تصمیم Accepted مالک ثبت شد؛ پیاده‌سازی NotImplemented باقی است.
- D-091 به جهت فیزیکی Rename اصلاح شد؛ جزئیات ترتیب و مجوز عملیاتی Proposed باقی است.
- اسناد Access Control، User Access، Database و Index با تصمیم جدید هماهنگ شدند.
- S3-0h به این الحاقیه ارجاع می‌دهد؛ تاریخچه قدیمی حذف یا بازنویسی نشده است.

### فایل‌های مهم تغییرکرده
- `docs/DECISIONS.md` — D-091 بازنگری و D-118 افزوده شد.
- `docs/ACCESS_CONTROL_ARCHITECTURE.md` — قرارداد هویت و عضویت.
- `docs/USER_ACCESS_MANAGEMENT.md` — دامنه هویت کاربران.
- `docs/DATABASE.md` — مدل هدف User/Membership/Session.
- `docs/INDEX.md` — پیوند handoff و فهرست D-118.
- `docs/handoffs/ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md` — ارجاع به تصمیمات جدیدتر.
- `docs/handoffs/ISSUE-397-s3-0i-tenant-company-user-identity-matrix.md` — ماتریس تکمیلی و این گزارش.
- `docs/WORK_LOG.md` — رکورد append-only این وظیفه.

### تصمیم‌های فنی
- User مشتری Tenant-owned است؛ UserCompanyMembership Company-owned.
- UserSession و LoginLog از طریق UserId به Tenant تعلق دارند؛ ستون تکراری TenantId/CompanyId برای آن‌ها افزوده نمی‌شود.
- Email/Mobile و verification keyها فعلاً global باقی می‌مانند؛ تغییر آن‌ها باز است.
- CompanyMembershipRequest با User و Company مقصد از طریق Tenant مشترک کنترل می‌شود.
- Trade implementation در این PR خارج از دامنه و همچنان تحت مسئولیت Claude است.

### بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بازبینی فایل‌ها و کد GitHub | انجام شد | Read-only روی main@62b0c3c6385e299785d2e56c94d3f8d7bdfff10e |
| dotnet build | اجرا نشده | فقط مستندات تغییر کرده‌اند؛ محیط clone محلی در دسترس نبود |
| dotnet test | اجرا نشده | هیچ تستی ادعا نمی‌شود |
| SQL Server Up/Down/Backfill | اجرا نشده | هیچ Migration یا Schema تغییر نکرد |
| git diff --check | اجرا نشده | تغییر از طریق GitHub API ثبت شد؛ CI/Review لازم است |
| GitHub Actions PR checks | در انتظار/در حال اجرا | فقط نتیجه روی head نهایی معتبر است |

### امنیت، داده و سازگاری
- هیچ C#/TypeScript، Entity، EF Configuration، API، Migration، Schema، داده، Provisioning یا Runtime تغییر نکرد.
- هیچ Tenant، User یا Company واقعی ساخته/تغییر داده نشد.
- هیچ گیت امنیتی یا S3-0 باز نشد.

### ریسک‌ها و کارهای باقی‌مانده
- احراز هویت فعلی Username را بدون Tenant جستجو می‌کند؛ تا پیاده‌سازی D-118، قابلیت تکرار Username در Shared DB عملیاتی نیست.
- Email/Mobile uniqueness همچنان global است.
- Provisioning مدیر اولیه، Password Recovery، lockout، صفحات Admin legacy و CorporateGroup/Consolidation نیازمند ممیزی اجرایی دقیق‌اند.
- D-091 و Backfill manifest هنوز مجوز اجرای Migration نیستند.
- CI و Review نهایی PR #780 باید بررسی شوند؛ Merge انجام نشده است.

### شناسه‌های Git
- **Branch:** `docs/tenant-company-user-identity-2026-11-23`
- **Commit:** تاریخچه Commit در PR #780؛ head نهایی PR مرجع است.
- **Pull Request:** https://github.com/hamidreza1394/Accounting/pull/780

