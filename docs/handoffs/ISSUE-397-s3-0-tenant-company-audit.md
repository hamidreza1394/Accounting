# تحویل ممیزی S3-0 — Tenant/Company و طراحی V3.2

- **Issue:** #397 — https://github.com/hamidreza1394/Accounting/issues/397
- **مسئول:** ممیزی ایستای مخزن از طریق GitHub
- **تاریخ:** 2026-11-14
- **وضعیت:** ناقص / بخشی مسدود؛ ممیزی منبع کد و بازبینی مالکیت V3.2 انجام شد، ممیزی زنده SQL Server و تعیین نگاشت داده باز است.
- **مبنای ممیزی:** main@b4d38c0999ff1db7c2f8cc9bb78da1c55b27a6ad (پس از Merge PR #650)

## 1. هدف و معیارهای پذیرش

S3-0 باید پیش از هر تغییر Schema، مدل واقعی، Migrationها، مسیرهای دسترسی و طراحی V3.2 را با D-087 تطبیق دهد. این گزارش فقط شواهد موجود در شاخه main را بررسی می‌کند؛ به SQL Server نصب‌شده یا دیتابیس مشتری اتصال برقرار نشده است.

## 2. خلاصه اجرایی

1. D-087 پذیرفته شده، اما مدل کد و Schema فعلی هنوز Company=Tenant است؛ Entity مستقل Tenant وجود ندارد.
2. در مدل فعلی، فیلدهای TenantId در چند Entity عملاً Company.Id را نگه می‌دارند. Company خودش TenantId ندارد.
3. Trade نیز فقط TenantId دارد و CompanyId مستقل ندارد؛ Party و Product در عمل با CompanyId فیلتر می‌شوند، اما نام فیلد و قرارداد persistence هنوز legacy است.
4. در V3.2، گروه B و بسیاری از گزارش‌ها هنوز TenantId را معادل شرکت مالک تعریف می‌کنند. این تعریف برای مدل هدف D-087 معتبر نیست.
5. قواعد گروه C در V3.2 دسترسی دو شرکت را با OR روی شناسه شرکت مدل می‌کنند، اما تعلق هر دو شرکت به یک Tenant و مجوز صریح رابطه بین‌شرکتی را به‌صورت کافی مشخص نمی‌کنند.
6. در Server/Program.cs، فرمان --migrate فقط Migrationهای Core را اجرا می‌کند. TradeDbContext ثبت شده، اما در مسیر اپراتوری Migration مربوط به Trade اجرا نمی‌شود.
7. تعداد Migrationهای منبع کد: ۲۰ Migration برای Core و یک Migration برای Trade. این تعداد، تاریخچه واقعی اعمال‌شده روی دیتابیس نصب‌شده را اثبات نمی‌کند.
8. نگاشت معتبر Company→Tenant برای داده‌های موجود از کد فعلی قابل استخراج نیست. CustomerBoundaryId، CorporateGroup و OwnershipRelationship طبق D-087 جایگزین Tenant نیستند.
9. هیچ Entity، API، Migration، Schema، داده یا تنظیم runtime در این ممیزی تغییر نکرده است.

## 3. شواهد مدل فعلی Core

### 3.1 هویت Tenant و Company

- Domain.Company فاقد TenantId است؛ در مدل فعلی رابطه مستقل Company→Tenant وجود ندارد.
- درخت کد Entity مستقلی به نام Tenant نشان نمی‌دهد.
- Company دارای CustomerBoundaryId اختیاری است؛ این فیلد مرز مستقل D-038 است و منبع معتبر Tenant نیست.
- CompanyConfiguration روی NationalId ایندکس یکتا دارد؛ این قید جدا از مرز Tenant/Company است و در طرح گذار باید اثر آن بر شرکت‌های یک مشتری بازبینی شود.

### 3.2 Entityهای دارای TenantId در Core

هشت Entity دارای TenantId غیرnullable در کد فعلی مشاهده شد:
- FiscalPeriod
- CompanyRole
- UserCompanyMembership
- CompanyEmployee
- CompanyEmployeeAssignment
- CompanyEmployeePhoto
- CompanyOrganizationalUnit
- CompanyPosition

SecurityAuditEvent نیز TenantId nullable دارد.

CompanyMembershipRequest فیلدی با نام DestinationTenantId دارد، اما API مقدار آن را از DestinationCompanyId درخواست می‌گیرد؛ بنابراین در قرارداد جاری، این شناسه عملاً مقصد Company است، نه Tenant مستقل.

### 3.3 FKها و یکتایی فعلی

- FiscalPeriod.TenantId به Company.Id متصل است؛ یکتایی دوره با (TenantId, StartDate, EndDate) و ایندکس وضعیت نیز با TenantId تعریف شده است.
- CompanyRole.TenantId به Company.Id متصل است؛ Code در محدوده TenantId یکتا و کلید جایگزین (Id, TenantId) تعریف شده است.
- UserCompanyMembership.TenantId به Company.Id متصل است؛ یکتایی (UserId, TenantId) و FK نقش (RoleId, TenantId) به CompanyRole وجود دارد.
- CompanyEmployee، CompanyOrganizationalUnit و CompanyPosition نیز TenantId را به Company.Id متصل می‌کنند و ایندکس‌های کد/شماره پرسنلی را با TenantId می‌سازند.
- CompanyEmployeeAssignment و CompanyEmployeePhoto با FKهای مرکب شامل TenantId، تعلق فرزند به والد شرکت‌محور را در مدل فعلی محدود می‌کنند.
- ConsolidationRunCompany دارای CompanyId است، اما FK دوره مالی آن از (FiscalPeriodId, CompanyId) به (FiscalPeriod.Id, FiscalPeriod.TenantId) متصل می‌شود؛ این تطبیق اکنون فقط به دلیل Company=Tenant کار می‌کند.

این قیود در وضعیت فعلی جلوی اتصال برخی رکوردهای دو Company متفاوت را می‌گیرند، اما مرز مستقل Tenant را پیاده نمی‌کنند. در مدل هدف، FKها و alternate keyها باید TenantId و CompanyId را جداگانه حفظ کنند.

## 4. شواهد مسیرهای دسترسی و داده

- CompanyAccessService عضویت فعال را از دیتابیس می‌خواند، اما membership.TenantId را به‌عنوان CompanyId استفاده می‌کند.
- WorkContextService دوره‌ها را با period.TenantId == CompanyId فهرست و اعتبارسنجی می‌کند.
- CompanyMembershipRequestEndpoints مقدار DestinationTenantId را از DestinationCompanyId می‌سازد و با TenantId فعلی مقایسه می‌کند.
- CompanyUserProvisioningEndpoints برای عضویت شرکت، TenantId = companyId می‌نویسد.
- CustomerProvisioningApplyService نیز برای Company ایجادشده TenantId = company.Id ثبت می‌کند.
- مسیرهای HR مانند خواندن عکس کارمند، CompanyId را با TenantId رکورد مقایسه می‌کنند.
- TradeAuthorizationAdapter، CompanyId زمینه جاری را به TradeAccessContext.TenantId تبدیل می‌کند؛ PartyStore و ProductStore نیز فقط TenantId را فیلتر می‌کنند.
- CorporateGroupService عضویت و مجوز شرکت‌ها را بررسی می‌کند و مرز CustomerBoundaryId مشترک را کنترل می‌کند؛ CustomerBoundaryId به‌تنهایی اثبات نمی‌کند دو Company به یک Tenant تعلق دارند.
- User.Username و User.EmailAddress در Identity Realm فعلی ایندکس یکتای سراسری دارند. D-087 این قرارداد هویت را تغییر نداده است؛ یکتایی tenant-scoped نباید بدون تصمیم جداگانه طراحی هویت اعمال شود.
- UserSession فاقد TenantId است؛ نشست به User مرکزی تعلق دارد و دسترسی Company در هر درخواست باید جداگانه اعتبارسنجی شود.
- SecurityAuditEvent فقط TenantId nullable دارد و CompanyId مستقل ندارد؛ نوع رخدادهای tenant-level، company-level و system-level باید در ماتریس مالکیت مشخص شود.

## 5. شواهد Trade و Migration

### 5.1 مدل Trade

- TradeEntity فقط TenantId دارد و CompanyId مستقل ندارد.
- Party و Product در Domain به‌عنوان داده company-scoped توصیف شده‌اند، اما در persistence با TenantId فیلتر می‌شوند.
- ایندکس یکتای کد Party و Product روی (TenantId, Code) است.
- FKهای فرزند مانند PartyRoleAssignment و ProductIdentifier از کلید مرکب (TenantId, ParentId) استفاده می‌کنند.
- برای مدل هدف D-087، داده company-owned باید TenantId و CompanyId داشته باشد؛ یکتایی تجاری در سطح Company است و کلیدها باید مرز Tenant را نیز حفظ کنند.

### 5.2 Snapshot و Migration

- Core Model Snapshot شامل ۳۳ نگاشت جدول است.
- پوشه Core شامل ۲۰ فایل Migration اصلی (غیر Designer) است.
- Trade Model Snapshot شامل پنج نگاشت جدول در schema trade است: Parties، PartyBankAccounts، PartyRoleAssignments، Products و ProductIdentifiers.
- Trade فقط یک Migration اصلی دارد: InitialTradeMasterData؛ این Migration سه جدول Parties، PartyRoleAssignments و Products را ایجاد می‌کند.
- PartyBankAccounts و ProductIdentifiers در مدل EF هستند، اما با ExcludeFromMigrations از Migration فعلی کنار گذاشته شده‌اند.
- Trade از history table مستقل dbo.__TradeMigrationsHistory استفاده می‌کند. Core از history table پیش‌فرض EF Core استفاده می‌کند (در تنظیم پیش‌فرض SQL Server معمولاً dbo.__EFMigrationsHistory).
- این اعداد از فایل‌های منبع و Snapshot به دست آمده‌اند؛ تعداد جدول‌ها یا Migrationهای واقعاً اعمال‌شده روی دیتابیس نصب‌شده هنوز مشخص نیست.

### 5.3 شکاف Migration Host

Server/Program.cs، AddTradePersistence را ثبت می‌کند، اما IDatabaseSchemaMigrator فقط DatabaseContext را مدیریت می‌کند. فرمان dotnet run --project Server -- --migrate، DatabaseSchemaMigrator هسته را فراخوانی می‌کند و TradeDbContext.Database.MigrateAsync را اجرا نمی‌کند.

FaranouApplicationFactory در تست‌های Integration پس از EnsureDeletedAsync، Migrationهای Core و سپس Trade را روی دیتابیس تست اجرا می‌کند. این مسیر فقط harness تست است و اثبات نمی‌کند میزبان نصب مشتری Migrationهای Trade را اجرا می‌کند.

## 6. بازبینی مالکیت در V3.2 پس از D-087

وضعیت سند TRADE_DESIGN_V3_2.md همچنان Proposed است. D-086 قواعد کسب‌وکاری مشخصی را Accepted کرده، اما کل مدل V3.2 به‌طور یکپارچه Accepted نشده است.

### گروه A — داده مرجع واقعاً مشترک

V3.2 این موارد را بدون TenantId و سراسری/فقط‌خواندنی می‌داند:
Vessel، VesselAlias، Currency، Country، Port، PermitType، DocumentType، CostType، FxChannel و CustomsOffice.

این طبقه‌بندی فقط در صورتی معتبر است که این داده‌ها واقعاً global و غیرقابل سفارشی‌سازی توسط Tenant/Company باشند. اگر داده مرجع قابل تعریف یا سفارشی‌سازی توسط مشتری باشد، باید مالکیت Tenant/Company جداگانه تعیین شود.

### گروه B — داده‌های تک‌شرکتی

V3.2 این موارد را با TenantId برابر شرکت مالک معرفی می‌کند:
Party، Product، VesselCompanyNote، Proforma، OrderRegistration، ImportCase، Voyage، BillOfLading، CustomsDeclaration، Permit، ForexCommitment، ForexVoucher، Facility، Guarantee، Cheque و CostItem.

در مدل هدف، این داده‌ها company-owned هستند و باید TenantId و CompanyId داشته باشند. تمام رویدادها، Revisionها، Amendmentها، پیوندهای تک‌شرکتی، فایل‌ها و ImportBatch/ImportRow/ImportValidationError نیز باید scope والد شرکت‌محور خود را حفظ کنند.

### گروه C — پیوندهای چندشرکتی

- VoyageAllocationShare در V3.2 یک‌طرفه است و TenantId آن برابر AllocatedCompanyId تعریف شده است. در مدل هدف باید TenantId مشترک معتبر و CompanyId شرکت تخصیص‌گیرنده صریح باشد؛ شرکت مالک Voyage نیز باید در همان Tenant و طبق مجوز مجاز باشد.
- BillOfLadingOrderAllocation، CustomsDeclarationBillAllocation و ForexVoucherBillCoverage دوطرفه‌اند و دو Company طرف رابطه را نگه می‌دارند.
- تا زمان تصویب قابلیت صریح cross-tenant، این روابط باید فقط بین Companyهای متعلق به یک Tenant معتبر باشند. برای هر ردیف باید TenantId مشترک و هر دو CompanyId طرفین قابل اعتبارسنجی باشند.
- شرط OR روی OwnerCompanyId/CounterpartyCompanyId به‌تنهایی کافی نیست؛ دسترسی به ردیف پیوند باید عضویت Tenant، رابطه گروهی مجاز و permission صریح را بررسی کند. دسترسی به ردیف پیوند نباید دسترسی به بدنه سایر اسناد شرکت مقابل ایجاد کند.
- رابطه CorporateGroup یا OwnershipRelationship به‌تنهایی مجوز عملیاتی ایجاد نمی‌کند (D-087).

### گزارش‌ها و سایر بخش‌ها

- R01 تا R10 در جدول گزارش‌های V3.2 عمدتاً TenantId را برابر OwnerCompanyId/شرکت ثبت‌کننده تعریف می‌کنند؛ این scope باید به TenantId + CompanyId تبدیل شود.
- R11 و پیوندهای گروه C به هر دو شرکت و قاعده دسترسی دوطرفه محدود نیاز دارند.
- TradeDocument و لینک‌های سند باید Tenant/Company والد معتبر را حفظ کنند؛ خود فایل باینری طبق طراحی خارج SQL Server است.
- ImportBatch، ImportRow و ImportValidationError باید در scope همان Company/Tenant اجرا شوند.
- TradeDocument، DocumentVersion، فایل‌های Trade، ImportCase و عملیات S3 هنوز در کد جاری پیاده‌سازی نشده‌اند.
- در درخت فعلی پروژه، Background Job اختصاصی Trade مشاهده نشد. وجود VendorAuditCheckpointBackgroundService مربوط به سامانه Vendor است و جایگزین jobهای آینده Trade نیست.
- عکس پرسنلی HR در حال حاضر به‌صورت byte[] در جدول CompanyEmployeePhotos ذخیره می‌شود؛ این استثنا نباید با تصمیم ذخیره فایل‌های تجاری Trade در خارج SQL Server خلط شود.

## 7. ماتریس Current → Target اولیه

| حوزه | وضعیت فعلی | قرارداد هدف D-087 | اقدام لازم |
|---|---|---|---|
| Tenant | Entity/جدول مستقل ندارد | Tenant مستقل | تعیین منبع نگاشت و ساخت مدل در PR جدا |
| Company | بدون TenantId | Company.TenantId اجباری | FK و اعتبارسنجی تعلق |
| Membership/Role | TenantId عملاً CompanyId | TenantId + CompanyId | بازطراحی FK/alternate key و unique index |
| FiscalPeriod | TenantId عملاً CompanyId | TenantId + CompanyId | FK مرکب و کنترل WorkContext |
| HR | TenantId عملاً CompanyId | TenantId + CompanyId | اصلاح Entity، query، photo و FKهای فرزند |
| Audit | TenantId nullable و بدون CompanyId | دامنه رخداد باید tenant/company/system باشد | طبقه‌بندی و قرارداد audit |
| Corporate Groups | Company IDs و CustomerBoundaryId | Tenant معتبر + Company طرفین + permission صریح | اعتبارسنجی same-tenant و محدودیت cross-tenant |
| Trade Party/Product | TenantId تنها؛ عملاً CompanyId | TenantId + CompanyId | اصلاح base entity، store، index و FK |
| Trade group C | شناسه‌های Company طرفین، بدون قرارداد Tenant مستقل | TenantId معتبر + CompanyIdهای طرفین | same-tenant validation و permission صریح |
| Trade files | پیاده‌سازی نشده | metadata scoped؛ باینری خارج SQL | طراحی storage reference و authorization |
| Identity/Session | هویت مرکزی؛ نشست بدون TenantId | Identity Realm مستقل؛ authorization Company/Tenant در هر درخواست | حفظ global uniqueness تا تصمیم جداگانه |

این ماتریس اولیه است؛ ERD و طرح Migration نهایی تا دریافت Schema واقعی و تعیین نگاشت Company→Tenant تأیید نمی‌شود.

## 8. موارد مسدود و نیازمند تصمیم/شاهد

1. **نگاشت Company→Tenant:** در کد فعلی منبع معتبر وجود ندارد. CustomerBoundaryId، CorporateGroup و OwnershipRelationship قابل استفاده به‌عنوان Tenant نیستند. CustomerProvisioningApplyService نیز فقط TenantId=Company.Id می‌نویسد. CompanyReference در provisioning schema v4 موجود نیست و Issueهای #392/#228 بازند.
2. **داده‌های موجود:** برای هر Company باید Tenant معتبر تعیین شود؛ هیچ backfill یا ادغام Tenant از روی نام، CustomerBoundaryId یا عضویت گروهی مجاز نیست.
3. **روابط گروه C:** پیش‌فرض امن تا تصمیم جداگانه، محدودکردن دو Company طرف رابطه به یک Tenant و الزام مجوز صریح است.
4. **Global reference data:** مالکیت و سیاست سفارشی‌سازی گروه A باید پیش از Migration تأیید شود.
5. **Audit scope:** معنای TenantId فعلی در SecurityAuditEvent و نیاز به CompanyId باید بر اساس انواع رخداد تعیین شود.
6. **Migration deployment:** مسیر اعمال Core و Trade در Shared/Dedicated هنوز یکپارچه و Verified نیست.
7. **SQL Server live evidence:** تاریخچه واقعی __EFMigrationsHistory و __TradeMigrationsHistory و Schema نصب‌شده در این ممیزی خوانده نشده است. فایل read-only زیر برای اجرای مالک/اپراتور تهیه شده است.
8. **تصمیم‌های باز Trade:** P-4/P-5 از D-080 و D-083 همچنان Proposed/NeedsApproval هستند. OD-007 فقط مدل Forex را مسدود می‌کند؛ OD-008 تخصیص‌های گروه C را تحت تأثیر می‌گذارد؛ OD-017 دقت نهایی مبالغ/تناژ را باز می‌گذارد. این موارد نباید به‌صورت ضمنی Approved شوند.

## 9. فایل ممیزی SQL Server

فایل scripts/audits/s3-0c-sqlserver-readonly-audit.sql فقط metadata دیتابیس را از sys catalog می‌خواند و در صورت وجود، ردیف‌های Migration History دو Context را نمایش می‌دهد. هیچ جدول، داده، Migration یا تنظیمی را تغییر نمی‌دهد. آن را در SSMS روی دیتابیس مورد بررسی اجرا کنید؛ خروجی‌ها باید شامل نام DB، جدول‌ها/ستون‌ها، indexها، FKها، check constraintها و Migration History باشند. Connection String یا رمز را ارسال نکنید.

## 10. آزمون و اعتبارسنجی

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| GitHub source tree و فایل‌های main | بررسی شد | مبنا SHA b4d38c09 |
| Core Model Snapshot | بررسی شد | ۳۳ نگاشت جدول |
| Core Migration files | بررسی شد | ۲۰ Migration اصلی |
| Trade Model Snapshot | بررسی شد | پنج نگاشت جدول |
| Trade Migration files | بررسی شد | یک Migration؛ سه جدول ایجاد می‌کند |
| SQL Server live catalog/history | اجرا نشد | اتصال مستقیم در دسترس نبود |
| dotnet build / dotnet test | اجرا نشد | در این ممیزی محیط اجرای محلی/SQL Server استفاده نشد |
| git diff --check | اجرا نشد | تغییرات از طریق GitHub Contents API ثبت می‌شوند؛ CI PR باید بررسی شود |

## 11. محدودیت و گام بعدی

- هیچ Migration، backfill، DDL، API، Entity یا تغییر runtime انجام نشد.
- گام بعدی: اجرای اسکریپت read-only روی SQL Server منتخب و ارائه خروجی metadata؛ سپس تکمیل ماتریس Schema واقعی و Migration History.
- پس از آن، مالک محصول باید منبع معتبر Company→Tenant را تعیین کند. تا آن زمان هیچ نگاشت خودکار و هیچ طرح backfill قابل تأیید نیست.
- بعد از دریافت دو شاهد فوق، ERD حداقلی و طرح گذار افزایشی/قابل بازگشت تهیه و برای تأیید جداگانه ارائه می‌شود.

## شناسه‌های Git

- **Branch:** docs/s3-0-tenant-company-audit
- **Base:** b4d38c0999ff1db7c2f8cc9bb78da1c55b27a6ad
- **Commit:** در PR ثبت می‌شود.
- **Pull Request:** پس از ثبت تغییرات ایجاد می‌شود؛ Merge انجام نمی‌شود.


## 12. الحاقیهٔ شواهد زنده SQL Server — دریافت خروجی اپراتور

**وضعیت:** دریافت شد؛ ممیزی metadata و Migration History دیتابیس منتخب تکمیل شد. این الحاقیه بخش‌های قبلی گزارش را که دریافت SQL Server را «باز» یا «اجرانشده» اعلام می‌کردند، برای همین دیتابیس به‌روز می‌کند.

### 12.1 مشخصات محیط و ملاحظهٔ زمان

| مشخصه | مقدار گزارش‌شده |
|---|---|
| Database | `Farano-Local` |
| SQL Server | `DESKTOP-75DF1LQ` |
| ProductVersion | `16.0.1000.6` |
| Edition | `Developer Edition (64-bit)` |
| CapturedAt | `2026-11-18 11:03:14.1939760 +01:00` |

**ملاحظهٔ ممیزی:** مقدار CapturedAt در خروجی، 2026-11-18 است؛ این تاریخ از تاریخ جاری ثبت این الحاقیه (2026-11-14) جلوتر است. مقدار عیناً از خروجی حفظ شده و ساعت سیستم/SQL Server مستقلاً تأیید نشده است. این اختلاف بر مشاهدهٔ metadata تأثیری ندارد، اما timestamp باید پیش از استفاده به‌عنوان زمان قطعی ممیزی تأیید شود.

### 12.2 Schema و جدول‌های واقعی

- تمام جدول‌های فهرست‌شده در خروجی در Schema برابر `dbo` هستند.
- هیچ جدول در Schema برابر `trade` در این دیتابیس گزارش نشده است.
- جدول `dbo.__TradeMigrationsHistory` نیز وجود ندارد؛ نتیجهٔ بخش Trade history برابر `NOT FOUND` است.
- این نتیجه فقط وضعیت دیتابیس `Farano-Local` را اثبات می‌کند؛ دربارهٔ دیتابیس‌های دیگر یا اجرای Trade Migration در محیط دیگری نتیجه‌گیری نمی‌شود.
- نبود جدول‌های Trade در این دیتابیس با وجود پنج نگاشت در Trade Model Snapshot و یک فایل Migration در Source، تفاوت وضعیت Source و دیتابیس نصب‌شده را نشان می‌دهد. از این خروجی به‌تنهایی نمی‌توان علت ایجادنشدن جداول را تعیین کرد.

### 12.3 Migration History واقعی

در `dbo.__EFMigrationsHistory` تعداد ۱۶ Migration از Core ثبت شده است. آخرین Migration ثبت‌شده:

`20261105094700_AddUserSessions` — ProductVersion `10.0.14`

Migrationهای قدیمی‌تر با ProductVersion `10.0.13` و موارد جدیدتر با `10.0.14` ثبت شده‌اند. تعداد ۱۶ مورد اعمال‌شده در این دیتابیس با ۲۰ فایل Migration اصلی موجود در Source یکی نیست؛ اولی تاریخچهٔ دیتابیس منتخب و دومی شمار فایل‌های Source است. هیچ Migration در این بررسی اجرا نشده است.

### 12.4 روابط و قیود تأییدشده

- `Companies` در دیتابیس زنده نیز ستون `TenantId` ندارد.
- FKهای `FiscalPeriods.TenantId`، `CompanyRoles.TenantId`، `UserCompanyMemberships.TenantId`، `CompanyEmployees.TenantId`، `CompanyOrganizationalUnits.TenantId` و `CompanyPositions.TenantId` به `Companies.Id` متصل‌اند.
- `CompanyMembershipRequests.DestinationTenantId` نیز به `Companies.Id` متصل است؛ نام ستون با معنای عملیاتی فعلی آن (شناسهٔ شرکت مقصد) یکسان نیست.
- FK مرکب `ConsolidationRunCompanies(FiscalPeriodId, CompanyId)` به `FiscalPeriods(Id, TenantId)` متصل است؛ بنابراین تطبیق دوره و شرکت در دیتابیس فعلی همچنان بر قرارداد legacy تکیه دارد.
- FKهای مرکب HR و Role/Membership نیز TenantId را در کلیدهای ارجاعی حفظ می‌کنند.
- در خروجی FK و Check Constraint، تمام موارد گزارش‌شده فعال و Trusted هستند: `IsDisabled=0` و `IsNotTrusted=0`.
- `Companies.NationalId` دارای unique filtered index سراسری روی مقدارهای غیر NULL است؛ دامنهٔ این قید باید در طراحی هدف جداگانه بررسی شود و در این مرحله تغییر نمی‌کند.

### 12.5 Identity، Session و Audit

- `Users.EmailAddress` دارای unique index بدون فیلتر است.
- `Users.Username` دارای unique filtered index برای مقدارهای غیر NULL است.
- هر دو قید در دیتابیس فعلی tenant-scoped نیستند؛ D-087 به‌تنهایی مجوز تغییر دامنهٔ یکتایی Identity Realm را نمی‌دهد.
- `UserSessions` فقط به `Users.Id` ارجاع می‌دهد و TenantId/CompanyId ندارد؛ اعتبارسنجی مجوز شرکت باید در مسیر درخواست باقی بماند.
- `SecurityAuditEvents.TenantId` nullable است و ستون `CompanyId` ندارد. طبقه‌بندی رخدادهای system/tenant/company و نگاشت legacy آن باید در طرح هدف مشخص شود.
- اسکریپت فقط metadata و Migration History را خوانده است؛ هیچ دادهٔ تجاری، تعداد رکوردها یا مقدار واقعی TenantId را بررسی نکرده است.

### 12.6 نتیجه و موارد باز

1. **S3-0c-b برای دیتابیس Farano-Local تکمیل شد**؛ هیچ DDL/DML یا Migration اجرا نشده است.
2. مدل زنده همچنان Company=Tenant legacy است و هیچ Tenant مستقل در Schema این دیتابیس وجود ندارد.
3. Trade Schema و Trade Migration History در این دیتابیس وجود ندارند؛ مسیر اعمال Migrationهای Trade در نصب مشتری همچنان یک شکاف عملیاتی باز است.
4. نگاشت معتبر Company→Tenant از این خروجی قابل استخراج نیست؛ هیچ backfill یا استنتاج از CustomerBoundaryId/CorporateGroup/OwnershipRelationship مجاز نیست.
5. S3-0d-b (inventory کامل مسیرهای داده)، S3-0e (منبع معتبر نگاشت)، S3-0f (قرارداد Migration host) و S3-0g (ERD و طرح گذار) همچنان بازند.
6. پیش از طراحی Migration باید روشن شود این دیتابیس محلی نمایندهٔ چه سناریوی نصب/داده‌ای است و وضعیت Trade tables در محیط هدف چگونه باید ایجاد شود.

**محدودهٔ بررسی:** فقط خواندن خروجی SELECT-only اپراتور؛ بدون اتصال مستقیم، اجرای SQL از سمت ممیز، تغییر Schema، تغییر داده، ساخت Entity/API یا اجرای Migration.
