# طراحی S3-0f — مهاجرت حداقلی و کم‌ریسک به معماری Tenant/Company (D-087)

- **Issue:** #397
- **تاریخ:** 2026-11-14
- **نوع کار:** فقط طراحی و مستندسازی. هیچ کد، Migration، Schema، داده، Commit، Push، PR یا Merge انجام نشده است.
- **وضعیت این سند:** `Proposed` — هیچ تصمیم این سند Accepted نیست و هیچ‌کدام بدون تأیید مالک محصول وارد `DECISIONS.md` نمی‌شود.
- **مرجع معماری:** D-087 (`Accepted`؛ پیاده‌سازی `NotImplemented`).

## 0. وضعیت Git و دسترسی (ثبت‌شده پیش از شروع)

| مورد | مقدار |
|---|---|
| آخرین SHA شاخه `main` در origin | `8f103aa088f63df074171901d1970a33596c8cea` (PR #654؛ فقط تغییرات e2e/rate-limit نسبت به `d81bb29`) |
| HEAD clone محلی این session | `d81bb29dc686d582a57904b2fc5b05ec3b7f45d1` روی `main`؛ تفاوت با `origin/main` فقط ۵ فایل مربوط به e2e است و روی این طراحی اثر ندارد |
| گزارش S3-0e | **در `main` وجود ندارد.** فقط یک فایل untracked روی همین clone است (`docs/handoffs/ISSUE-397-s3-0e-tenant-company-ownership-audit.md`)؛ در هیچ شاخهٔ origin هم پیدا نشد. این طراحی بر همان نسخهٔ محلی تکیه دارد و با گزارشی که در `main` ادغام شده اشتباه گرفته نشود. |
| گزارش S3-0d | فایل مستقل `ISSUE-397-s3-0d-data-path-inventory.md` وجود ندارد؛ محتوا §13 فایل `ISSUE-397-s3-0-tenant-company-audit.md` روی شاخهٔ `docs/s3-0d-data-path-inventory` است و در `main` نیست. |
| شواهد SQL Server | §12 همان فایل (`Farano-Local`؛ فقط metadata) |
| تغییرات commit‌نشدهٔ کاربر | کامپیوتر کاربر به bridge وصل نیست؛ درخت کاری شما بررسی نشد. بیرون از clone این session چیزی حذف، Reset یا Stash نشده است. |
| ناهماهنگی نام‌گذاری | `PROJECT_STATUS.md` مرحلهٔ S3-0f را «قرارداد Migration host» و S3-0g را «ERD و طرح گذار» می‌نامد؛ این دستور کار S3-0f را «طراحی مهاجرت» تعریف کرده است. پیش از ثبت در PROJECT_STATUS نام‌ها یکدست شوند. |

> **یادداشت اصلاحی (S3-0g، 2026-11-14):** اعتبارسنجی فنی در `docs/handoffs/ISSUE-397-s3-0g-tenant-company-technical-validation.md` چند فرض این سند را رد یا اصلاح کرد. متن اصلی این سند عمداً بدون تغییر مانده است؛ در صورت تعارض، S3-0g مقدم است. موارد اصلی: (۱) فاز A با «`Companies.TenantId` nullable + کلید جایگزین» در EF قابل ساخت نیست؛ کلید جایگزین و NOT NULL باید پس از Backfill بیایند (X1/S-1). (۲) Rename بدون Pin نام قیدها، Drop/Add تولید می‌کند؛ Pin اجباری است (X2/S-2). (۳) Trade هیچ FK به `Companies` ندارد؛ ادعای §2 دربارهٔ Trade درست نیست (X3/S-4). (۴) PR-2 (Guard) پیش‌نیاز اجباری PRهای تغییر نام است، نه موازی (S-3). (۵) Migration NOT NULL باید دستی باشد (S-6). همهٔ موارد `Proposed` هستند.

## 1. Executive Summary

1. **طرح قابل اجراست و با D-087 سازگار است**، به شرط تأیید تصمیم‌های بخش ۳ و رفع دو عدم‌قطعیت فنی بخش ۱۴ (رفتار EF/ابزار گارد برای `RenameColumn`؛ شکاف Migration host برای Trade). هیچ تستی در این مرحله اجرا نشده است.
2. **ایدهٔ مرکزی («Rename-then-Add»):** ستون legacy به نام `TenantId` که در عمل `Company.Id` است، *به‌جای کپی یا بازنویسی*، در سطح فیزیکی به `CompanyId` تغییر نام می‌دهد (`sp_rename`، فقط metadata). همهٔ FKها، کلیدهای جایگزین و ایندکس‌های یکتای موجود (`(TenantId, Code)` و …) خودبه‌خود به قیدهای **Company-scoped** تبدیل می‌شوند که دقیقاً چیزی است که D-087 بند ۷ می‌خواهد. سپس یک ستون `TenantId` واقعی افزوده می‌شود.
3. **جلوگیری از اشتباه TenantId با CompanyId:** تغییر نام property در C# باعث خطای کامپایل در هر نقطه‌ای می‌شود که هنوز «TenantId» را به‌جای شرکت به کار برده؛ یعنی تغییر معنا هرگز بی‌صدا رخ نمی‌دهد (ریسک R-2 گزارش S3-0e).
4. **منبع Company→Tenant (OD-6) — پیشنهاد:** گزینهٔ ترکیبی ساده: Tenant فقط توسط اپراتور (حساب `Infrastructure`) ثبت می‌شود؛ Provisioning هرگز Tenant نمی‌سازد و فقط با `CustomerReference` امضاشدهٔ موجود در بسته آن را پیدا می‌کند (بدون تغییر قرارداد بسته، بدون وابستگی به #392/#228 و بدون سامانهٔ دوم).
5. **Backfill:** از طریق یک فرمان اپراتوری با manifest تأییدشده توسط مالک، با dry-run، idempotent و audit؛ **نه** داخل Migration خودکار (چون `--migrate` روی همهٔ نصب‌ها اجرا می‌شود و نباید نگاشت مبهم را حدس بزند).
6. **امنیت:** Global Query Filter پیشنهاد نمی‌شود. پیشنهاد: فیلتر صریح یکپارچه با یک extension مشترک و interfaceهای نشانگر (`ICompanyOwned`/`ITenantOwned`)، Tenant از عضویت و `Companies.TenantId` استخراج شود (بدون round-trip اضافه) و تست‌های دو-Tenant.
7. **تغییرات ضروری Schema:** جدول `Tenants`؛ ستون `Companies.TenantId`؛ تغییر نام ستون در حدود ۱۵ جدول؛ افزودن ستون `TenantId` واقعی به جدول‌های company-owned (مرحله‌ای)؛ دو ستون در `SecurityAuditEvents`. بدون حذف جدول، بدون جدول تکراری.
8. **آمادگی ورود به پیاده‌سازی:** **خیر برای Schema/Backfill**؛ بله برای PRهای فقط‌مستندات و PRهای مستقل از تصمیم (بخش ۱۳ و ۱۵). اولین PR: PR-0 (مستندات)؛ اولین PR فنی: PR-1 (قرارداد Migration host برای Trade).

## 2. Architecture Baseline

خلاصهٔ شواهد (مبنا: S3-0e و بازبینی مستقیم کد در این مرحله؛ جزئیات و مسیر فایل‌ها در S3-0e §13):

- Tenant وجود ندارد؛ `Companies` ستون `TenantId` ندارد. ۸ Entity Core + `CompanyMembershipRequest.DestinationTenantId` + Trade، ستون «TenantId» دارند که `Company.Id` را نگه می‌دارد، و همگی FK به `Companies.Id` دارند (§12.4 S3-0 روی `Farano-Local`: فعال و Trusted).
- **قیدهای موجود که ارزش حفظ دارند** (از `Infrastructure/Persistence/Configurations/*`):
  - `CompanyRole`: یکتا `(TenantId, Code)`؛ AK `(Id, TenantId)`.
  - `UserCompanyMembership`: یکتا `(UserId, TenantId)`؛ FK مرکب `(RoleId, TenantId)→CompanyRoles(Id, TenantId)`.
  - `FiscalPeriod`: یکتا `(TenantId, StartDate, EndDate)`؛ AK `(Id, TenantId)`؛ هدف FK از `ProvisioningRequestClaims(FiscalPeriodId, CompanyId)` و `ConsolidationRunCompanies(FiscalPeriodId, CompanyId)`.
  - HR: کلیدهای جایگزین `(Id, TenantId)` و FKهای مرکب بین Employee/Assignment/Unit/Position/Photo/Membership.
  - Trade: AK `(TenantId, Id)` و ایندکس‌های یکتا فیلتردار `(TenantId, Code)` برای Party/Product، `(TenantId, Gtin|Irc)` برای ProductIdentifier.
- **قیدی که درست است و بدون تغییر می‌ماند:** `CorporateGroupMember.CompanyId`، `OwnershipRelationship.{Parent,Subsidiary}CompanyId`، `CorporateGroup.OwnerCompanyId`، `ConsolidationRunCompany.CompanyId` از ابتدا CompanyId واقعی‌اند.
- **ایجاد Company:** فقط `CustomerProvisioningApplyService`. بسته Provisioning فقط `CustomerReference` (≤128)، `InstallationId`، `CompanyType`، `ParentCompanyReference` دارد؛ `CustomerProvisioningPackageVerifier` الزام می‌کند `payload.CustomerReference` و `payload.InstallationId` با تنظیمات نصب (`CustomerProvisioningSettings`: یک مقدار) برابر باشند. **نتیجه:** امروز هر نمونهٔ برنامه دقیقاً یک مشتری (CustomerReference) را provision می‌کند.
- **مدیر نصب:** `Role.Admin` (نقش سراسری legacy روی `User.RoleId`). Provisioning کاربر اول شرکت را با `RoleId = null` می‌سازد و فقط `CompanyRole` «company-admin» می‌دهد؛ **هیچ API مشتری‌محور که `User.RoleId` را مقدار دهد پیدا نشد** (در `UserAdministrationEndpoints`، `RoleId` فقط در محافظ «آخرین ادمین» خوانده می‌شود؛ Confirmed با جست‌وجو؛ Partial چون seed/ابزار DB بررسی نشد). پس `Role.Admin` عملاً نقش اپراتور/توسعه‌دهنده است، نه نقش مشتری.
- **ابزار Migration:** گاردهای CI `generate_migrations.py --check`، `verify_migration_model.py`، `ef_model.py` فقط AddColumn، AddForeignKey، CreateIndex/DropIndex، AlterColumn و CreateTable را خودکار می‌پذیرند؛ **Drop، Rename و تغییر رابطه** «دستی/ردشده»اند و `Sql(...)` فقط با ورودی بازبینی‌شده در `HAND_WRITTEN_SQL` مجاز است. این قید مستقیماً روی طراحی اثر می‌گذارد (بخش ۴.۳ و ۱۴).
- **Migration host:** `--migrate` فقط `DatabaseContext` (Core) را اجرا می‌کند؛ Trade (`__TradeMigrationsHistory`) اجرا نمی‌شود و در `Farano-Local` جداول `trade` نیستند.
- قید سادگی D-087: Modular Monolith، یک DatabaseContext به‌ازای نمونه، بدون Microservice/Sharding/database-per-company.

## 3. Open Decisions

بدون تأیید مالک Accepted نمی‌شوند. شمارهٔ OD-1 تا OD-10 مطابق S3-0e؛ OD-11 تا OD-14 در این طراحی جدید ثبت شدند.

| ID | موضوع | پیشنهاد این طرح (Proposed) | مانع کدام فاز |
|---|---|---|---|
| OD-1 | نقش مدیر نصب | تفکیک مفهومی: `Role.Admin` = Platform Administrator؛ مدیر مشتری از مجوزهای `CompanyRole` موجود | E2 |
| OD-2 | یکتایی Username/Email | حفظ Global در Identity Realm | هیچ فاز |
| OD-3 | CustomerBoundaryId و کشف عضویت | استقلال کامل؛ کشف فقط درون Tenant و (تا تصمیم دیگر) درون همان مرز | E2 |
| OD-4 | `CompanyRole` Company-scoped یا Tenant-scoped | Company-scoped (وضع فعلی؛ مطابق متن `DATABASE.md`) | C1 |
| OD-5 | `SecurityAuditEvent` نیاز به ستون Scope؟ | خیر؛ دامنه از ترکیب `TenantId`/`CompanyId` (هر دو null = system) | C-Audit |
| OD-6 | منبع Company→Tenant | ترکیب ساده: Tenant توسط اپراتور؛ Provisioning فقط resolve می‌کند | D، E4 |
| OD-6a | آیا `CustomerReference` (مشتری Vendor) معادل یک Tenant است؟ | فرض ۱:۱ برای نصب‌های فعلی؛ **باید مالک صریحاً تأیید کند** | D، E4 |
| OD-7 | `Company.NationalId` یکتای سراسری | بدون تغییر در این طرح؛ تصمیم جدا پیش از ورود مشتری دوم به DB مشترک | Shared |
| OD-8 | ProvisioningRequestClaim سطح نصب یا Tenant | سطح نصب؛ Tenant از Company مشتق شود | — |
| OD-9 | گروه C و گروه شرکتی | فقط same-Tenant | Trade S3 |
| OD-10 | نام‌گذاری در دوره گذار | تغییر نام ستون/property legacy به `CompanyId` (بخش ۴.۳) | C1 |
| **OD-11** | آیا متن D-087 بند ۳ («company-owned علاوه بر TenantId به CompanyId متصل») ستون **فیزیکی** `TenantId` روی همهٔ جدول‌های company-owned Core legacy را الزام می‌کند، یا مشتق‌بودن از `Companies.TenantId` برای جدول‌های Workflow کافی است؟ | ستون فیزیکی برای Trade/HR/Membership/Role/FiscalPeriod/Audit؛ مشتق برای Workflow/گروه (جدول ۷) | C2 |
| **OD-12** | تحمل Downtime: تغییر نام ستون با نسخه‌های قدیمی برنامه ناسازگار است | پنجرهٔ نگهداری کوتاه به‌ازای هر PR تغییر نام (اتمی و فوری) | C1 |
| **OD-13** | Backfill اولیه نصب‌های موجود: manifest دستی مالک یا استخراج از `ProvisioningRequestClaims`؟ | manifest مالک؛ Claim فقط شاهد تطبیقی | D |
| **OD-14** | Shared DB: یک نمونهٔ برنامه برای چند Tenant، یا نمونهٔ جدا به‌ازای Tenant؟ (تنظیمات provisioning فعلی تک‌مشتری است) | تعیین نشده؛ این طرح هر دو را بدون تغییر مدل منطقی پشتیبانی می‌کند اما provisioning چندمشتری فقط پس از این تصمیم | Shared |

## 4. Options and Recommendations

### 4.1 OD-1 — نقش مدیر نصب

| گزینه | مزیت | عیب | اثر دیتابیس | اثر امنیتی |
|---|---|---|---|---|
| A) `Role.Admin` = Platform Administrator؛ مدیر مشتری = مجوزهای `CompanyRole` موجود (**پیشنهاد**) | بدون نقش/مجوز جدید؛ با Provisioning فعلی هم‌خوان (مشتری `RoleId=null`) | `Role.Admin` سراسری می‌ماند؛ باید فقط به اپراتور داده شود | بدون تغییر | R-1 فقط وقتی باقی است که `Role.Admin` به مشتری داده شود؛ نیاز به Guard و سند |
| B) Tenant Administrator جدید | دقیق‌ترین نمایش | سیستم نقش موازی؛ مجوزها و UI جدید؛ ریسک پیچیدگی که دستور کار منع کرده | جدول/ستون نقش جدید | وابسته به صحت پیاده‌سازی |
| C) تفکیک کامل Platform و Tenant | حداکثر جداسازی | هزینهٔ B + مهاجرت نقش‌ها | بالا | بالا |

**گزینهٔ ساده‌تر برای C:** همان A، با فیلتر اجباری `tenantId` روی `UserAdministration` و `SecurityAuditAdministration` وقتی نصب بیش از یک Tenant دارد (بخش ۹.۳). **پیشنهاد:** A.

### 4.2 OD-2 — یکتایی Username/Email

| گزینه | Login | عضویت چندشرکتی | بازیابی رمز | Session | دیتابیس |
|---|---|---|---|---|---|
| Global در Realm (**پیشنهاد**) | بدون ابهام | یک کاربر می‌تواند عضو شرکت‌های چند Tenant باشد (دسترسی فقط با عضویت) | یک مسیر | `UserSession` بدون تغییر | بدون تغییر |
| Tenant-scoped | نیاز به انتخاب Tenant پیش از ورود یا شناسهٔ ورود جدید | کاربر باید برای هر Tenant هویت جدا داشته باشد | مبهم (کدام Tenant؟) | `UserSession` نیازمند TenantId | تغییر ایندکس‌ها + احتمالاً ستون |
| Realm مستقل مشتری | ساده برای Dedicated | تکرار کاربران | جدا | جدا | معادل Dedicated؛ برای Shared نیازمند جدول Realm |

پیشنهاد: Global تا نیاز مستند تجاری؛ **هیچ فاز این طرح به OD-2 وابسته نیست.**

### 4.3 OD-3 — CustomerBoundaryId

`CustomerBoundaryId` ستون و ایندکس و endpoint اپراتوری فعلی را **بدون تغییر** نگه می‌دارد و هرگز منبع Backfill یا کلید Tenant نیست. پیشنهاد برای کشف عضویت (`CompanyMembershipRequestEndpoints`): در فاز E2، شرط فعلی «مرز مشترک» با شرط **«Company مقصد در Tenant یکی از شرکت‌های درخواست‌کننده باشد»** *افزوده* شود (AND، نه جایگزین)؛ یعنی کشف فقط درون Tenant و درون مرز؛ کشف بین Tenantها ممنوع تا تصمیم صریح امنیتی. نیاز به کشف بین شرکت‌های یک Tenant حفظ می‌شود.

### 4.4 OD-6 — منبع Company→Tenant

| گزینه | امنیت | Idempotency | Dedicated | Shared | هزینه / عیب |
|---|---|---|---|---|---|
| 1) اپراتور Tenant می‌سازد؛ Provisioning فقط Company را به آن وصل می‌کند | فقط `AccountKind=Infrastructure` + Anti-forgery (الگوی `CustomerBoundaryEndpoints`) | ثبت Tenant با کلید یکتا | یک Tenant | چند Tenant | اتصال Company به Tenant در Provisioning نیازمند فیلد جدید در بسته یا تنظیمات |
| 2) بسته Provisioning امضاشده Tenant را می‌سازد | امضای Vendor | RequestId موجود | خوب | خوب | **نیازمند تغییر قرارداد بسته (v4)، Vendor و Issueهای #392/#228 که باز است**؛ مسیر دوم ساخت داده |
| 3) ترکیب ساده (**پیشنهاد**) | Tenant فقط اپراتوری؛ Provisioning فقط **resolve** با `payload.CustomerReference` که امروز امضاشده و برابر با تنظیمات نصب است | Tenant با `CustomerReference` یکتا؛ Provisioning رد/تکرار مثل `StoreRejectedAcknowledgementAsync`؛ Claim بدون تغییر | یک Tenant ثبت‌شده هنگام نصب | چند Tenant ثبت‌شده؛ **ولی** provisioning چندمشتری (تنظیمات تک‌مقداری) نیازمند OD-14 | بدون تغییر قرارداد بسته؛ بدون سامانهٔ دوم؛ فرض OD-6a |

قواعد گزینهٔ ۳: Provisioning اگر Tenant با آن `CustomerReference` ثبت نشده باشد، با کد خطای جدید (مثلاً `provisioning_tenant_unregistered`) **رد** می‌کند و هیچ Tenant/Company ساخته نمی‌شود؛ `ParentCompanyReference` (subsidiary) باید به Company همان Tenant resolve شود (قید جدید در `CustomerProvisioningParentClaimResolver`). **هیچ Tenant یا Company از `CustomerBoundaryId`، نام شرکت، گروه یا مالکیت حدس زده نمی‌شود.**

### 4.5 گزینه‌های راهبرد تغییر ستون (مهم‌ترین تصمیم فنی طرح)

| گزینه | شرح | مزیت | عیب | Schema |
|---|---|---|---|---|
| **R) Rename-then-Add (پیشنهاد)** | `TenantId`→`CompanyId` (متادیتا) سپس افزودن `TenantId` واقعی | بدون جابه‌جایی داده؛ قیدهای موجود بی‌تغییر و درست؛ خطای کامپایل برای هر مصرف قدیمی؛ دوره گذار کوتاه | نسخهٔ قدیمی برنامه با Schema جدید ناسازگار (OD-12)؛ ابزار گارد باید Rename را بپذیرد (spike) | RenameColumn + AddColumn |
| E) Expand/Contract با ستون کپی | افزودن `CompanyId`، نوشتن دوگانه، سپس حذف `TenantId` قدیمی و افزودن مجدد | سازگار با نسخهٔ قدیمی در دوره گذار | هزینهٔ بالا: دو ستون، dual-write، بازنویسی همهٔ FK/AK/ایندکس‌های مرکب (Drop/Add)، دوبار Migration | AddColumn+Backfill+Drop |
| K) نگه‌داشتن «TenantId=Company» برای همیشه + ستون جدید `OwnerTenantId` | کمترین Migration | بدهی معنایی دائمی؛ **ریسک R-2 را ثابت می‌کند**؛ مخالف D-087 | کم | **رد می‌شود** |

**گزینهٔ ساده‌تر برای R اگر گارد/EF مانع شد:** E فقط برای جدول‌های کوچک‌تر و Trade (چون Trade امروز دادهٔ زنده ندارد، بخش ۶)، یا اجرای Rename با Migration دستی بازبینی‌شده و افزودن پشتیبانی Rename به گارد.

#### جزئیات فنی Rename (فرض‌ها؛ **اجرا و تأیید نشده**)
- در SQL Server، `sp_rename` ستون، FK/AK/ایندکس‌ها را همراه می‌کند. اما EF نام پیش‌فرض قیدها را از نام ستون می‌سازد (`FK_CompanyRoles_Companies_TenantId`، `IX_CompanyRoles_TenantId_Code`)؛ با تغییر نام ستون، EF ممکن است `RenameIndex` و برای FK/AK **Drop+Add** تولید کند. راه‌حل: نام قیدهای legacy را با `HasConstraintName`/`HasDatabaseName` ثابت کردن تا فقط `RenameColumn` تولید شود. باید spike (PR-2) تأیید کند که (الف) EF 10 فقط `RenameColumn` می‌دهد، (ب) `ef_model.py`/`migration_model.py` این پیکربندی را می‌فهمند و گاردها قابل‌توسعه‌اند، (ج) `Down` معکوس درست است.
- Rename فوری و بدون اسکن داده است؛ برای اندازه‌های این پروژه (۱۰۰ کاربر) هزینه‌ای ندارد.

## 5. Target Data Model

```
Tenants (جدید)                          Companies (موجود)
  Id  PK                                  Id PK
  Name  nvarchar(200)                     Name, NationalId, CustomerBoundaryId?  (بدون تغییر)
  IsActive, CreatedAtUtc, RowVersion      TenantId  FK→Tenants.Id  (nullable→NOT NULL)
  [CustomerReference nvarchar(128) NULL   AK (Id, TenantId)   ← هدف FK مرکب جدول‌های company-owned
   unique filtered; فقط پس از OD-6]       IX (TenantId)

جدول company-owned (نمونه: CompanyRoles)
  CompanyId  (تغییر نام‌یافتهٔ TenantId legacy)  FK→Companies.Id (موجود)
  TenantId   (جدید؛ nullable→NOT NULL)
  FK مرکب (CompanyId, TenantId) → Companies(Id, TenantId)   ← ناسازگاری Company/Tenant ناممکن
  قیدهای موجود روی (CompanyId, …) بدون تغییر ← یکتایی Company-scoped (D-087 بند ۷)
```

نکات:
- جدول Tenant فقط ستون‌های لازم را دارد؛ **ستون `CustomerBoundaryId` در Tenant وجود ندارد**؛ ارتباطی با آن ندارد.
- FK قدیمی `CompanyId→Companies.Id` حفظ می‌شود (حذف/بازسازی FK لازم نیست)؛ FK مرکب در کنار آن اضافه می‌شود (افزونگی ناچیز؛ جایگزینی آن‌ها فقط در یک پاک‌سازی آتی).
- Entity جدید Tenant-owned در دامنهٔ فعلی وجود ندارد؛ هیچ Entity تکراری یا «TenantSettings» حدسی ساخته نمی‌شود.
- Entityهای آیندهٔ S3 (ImportCase، Proforma، …) از ابتدا با `TenantId`+`CompanyId` و FK مرکب ساخته می‌شوند؛ پس **مهاجرت Trade (PR-9) باید پیش از اولین Migration جدول S3 تمام شود** تا الگوی دوگانه ایجاد نشود.

## 6. Migration Phases

### مرحلهٔ A — ایجاد مرز Tenant (Schema؛ PR-4)
- `Tenants` + `Companies.TenantId` **nullable** + AK `(Id, TenantId)` + ایندکس `TenantId`. کد: Entity `Tenant`، رابطهٔ `Company.Tenant`. بدون تغییر رفتار.
- رفتار Companyهای قدیمی: `TenantId = NULL` = «تخصیص‌نیافته (legacy)». در این حالت `CompanyAccessService` مثل امروز کار می‌کند و `TenantId` را `null` برمی‌گرداند؛ هیچ مسیر Shared مبتنی بر Tenant نباید فعال شود تا Backfill کامل شود.
- روش ایجاد Tenant: PR-5 (اپراتور)؛ روش تخصیص: PR-5 و PR-6 (بخش ۸).
- ستون `CustomerReference` در `Tenants` فقط با PR-5 و پس از تأیید OD-6 افزوده می‌شود.

### مرحلهٔ B — سازگاری Legacy (کد بدون Schema؛ PR-3)
- interfaceهای نشانگر `ICompanyOwned` (`CompanyId`) و `ITenantOwned` (`TenantId`)؛ extension واحد `WhereCompany(companyId)` / `WhereTenant(tenantId)`.
- **قانون تشخیص دو مفهوم در دوره گذار:** (۱) در هر Entity هنوز-تغییرنام‌نیافته، property موجود `TenantId` با KDoc اجباری «legacy: همان Company.Id» علامت می‌خورد و یک تست معماری فهرست قطعی این Entityها را نگه می‌دارد (فقط اجازهٔ کاهش؛ افزودن مورد جدید شکست می‌دهد). (۲) Entity جدید حق ندارد `TenantId` به معنای Company داشته باشد. (۳) پس از هر PR تغییر نام، Entity از فهرست حذف می‌شود.
- نام‌گذاری موقت دیگری لازم نیست؛ لایهٔ سازگاری = خود تغییر نام property.

### مرحلهٔ C — تفکیک جدول‌به‌جدول (Schema + کد؛ PRهای ۷ تا ۱۰ و ۱۱)
هر گروه در یک PR، با ترتیب ریسک/وابستگی:
- **C1:** تغییر نام `TenantId→CompanyId` (و `DestinationTenantId→DestinationCompanyId`) + کد مکانیکی. بدون داده‌حرکت.
- **C2:** افزودن ستون `TenantId` واقعی (nullable) + FK مرکب `(CompanyId, TenantId)`؛ نوشتن `TenantId` در مسیرهای نوشتن (از `Companies.TenantId`).
- **C3 (پس از Backfill):** `NOT NULL` با پیش‌شرط.

گروه‌ها بر اساس بستار FK (باید هم‌زمان تغییر کنند): **G1** = CompanyRole، UserCompanyMembership، CompanyMembershipRequest، پنج Entity HR (HR به Membership و Role ارجاع مرکب دارد)؛ **G2** = FiscalPeriod + ProvisioningRequestClaim + ConsolidationRunCompany (FK مرکب به `FiscalPeriods(Id, TenantId)`)؛ **G3** = Trade (DbContext و history جدا)؛ **G4** = SecurityAuditEvent (بدون FK).

### مرحلهٔ D — Backfill (بخش ۸)
### مرحلهٔ E — امنیت (بخش ۹)، موازی با C پس از PR-4.

## 7. Entity-by-Entity Migration Matrix

دسته‌ها: **G** Global/System، **T** Tenant-owned، **C** Company-owned. «ستون فیزیکی TenantId» با OD-11.

| Entity | دسته هدف | TenantId واقعی لازم؟ | CompanyId جدا؟ | اقدام Schema | قیدها/ایندکس | گروه | ریسک |
|---|---|---|---|---|---|---|---|
| Tenant (جدید) | T (ریشه) | — | — | CreateTable | PK؛ (CustomerReference) یکتا بعداً | A | پایین |
| Company | C زیر Tenant | FK `TenantId` | `Id` خودش | AddColumn `TenantId` + AK `(Id,TenantId)` + IX | `NationalId` بدون تغییر (OD-7) | A | پایین |
| CompanyRole | C (OD-4) | بله (فیزیکی) | Rename | RenameColumn؛ AddColumn `TenantId`؛ FK مرکب | یکتا `(CompanyId, Code)` و AK `(Id, CompanyId)` همان قیدهای موجود | G1 | متوسط |
| CompanyRolePermission | C (تابع نقش) | خیر (مشتق از Role) | خیر | هیچ | — | — | — |
| UserCompanyMembership | C | بله | Rename | همان | یکتا `(UserId, CompanyId)`؛ FK مرکب نقش `(RoleId, CompanyId)` | G1 | **بالا** (مسیر هر درخواست) |
| CompanyMembershipRequest | C (مقصد) | مشتق (OD-11) | `DestinationCompanyId` Rename | RenameColumn | یکتای فیلتردار `(UserId, DestinationCompanyId)` | G1 | متوسط |
| CompanyEmployee / Assignment / Photo / OrgUnit / Position | C | بله | Rename | RenameColumn؛ AddColumn؛ FK مرکب | همهٔ AK/FK مرکب موجود بی‌تغییر | G1 | متوسط (۵ جدول، FK متقابل زیاد) |
| FiscalPeriod | C | بله | Rename | RenameColumn؛ AddColumn؛ FK مرکب | یکتا `(CompanyId, Start, End)`؛ هدف FK مرکب دو جدول دیگر | G2 | **بالا** (WorkContext) |
| ProvisioningRequestClaim | G سطح نصب (OD-8) | مشتق | `CompanyId` (موجود) | هیچ در Schema | FK مرکب `(FiscalPeriodId, CompanyId)` با تغییر نام ستون هدف خودکار همگام | G2 | متوسط |
| ConsolidationRunCompany | C | مشتق | `CompanyId` (موجود) | هیچ | همان | G2 | متوسط |
| CorporateGroup / Member / OwnershipRelationship | C (مالک/طرفین) | مشتق + قاعده same-Tenant | موجود | هیچ Schema در این طرح | رد same-Tenant در کد (OD-9) | — | متوسط |
| ConsolidationProfile / Run | تابع CorporateGroup | مشتق | — | هیچ | — | — | پایین |
| Trade: Party، PartyRoleAssignment، PartyBankAccount، Product، ProductIdentifier | C | بله | Rename | `trade` schema: RenameColumn؛ AddColumn `TenantId`؛ FK مرکب به `Companies(Id,TenantId)` **یا** (OD-11) فقط منطقی در سرویس چون Trade به Core FK ندارد (مرز ماژول) | یکتای فیلتردار `(CompanyId, Code)` و `(CompanyId, Gtin|Irc)`؛ AK `(CompanyId, Id)` | G3 | **بالا** (قرارداد Trade و S3) |
| SecurityAuditEvent | ترکیبی (OD-5) | nullable | Rename `TenantId→CompanyId` | RenameColumn؛ AddColumn `TenantId` nullable؛ IX `(TenantId, OccurredAtUtc)` | هیچ FK (امروز هم نیست) | G4 | پایین |
| User / UserSession / LoginLog | G (Realm) | خیر | خیر | هیچ | یکتایی سراسری (OD-2) | — | — |
| PermissionDefinition، Role/Permission/Page/... legacy | G | خیر | خیر | هیچ | — | — | — |
| InstallationIdentity / InstalledLicense / CustomerOfflineRevocationState | G (نصب) | خیر | خیر | هیچ | — | — | — |
| داده مرجع Trade (کشور، واحد؛ ثابت در کد) | G | خیر | خیر | هیچ | — | — | — |
| Vendor.* | خارج | — | — | هیچ | — | — | — |

نکتهٔ Trade و مرز ماژول: `Trade.*` به Core Domain/DbContext Reference نمی‌دهد؛ FK بین schemaها طبق `TRADE_MODULE.md` ساخته نمی‌شود. بنابراین سازگاری `(CompanyId, TenantId)` در Trade با (الف) FK به `dbo.Companies` (نقض مرز و نیازمند تصمیم) یا (ب) کنترل منطقی در Host پیش از use case (الگوی فعلی `TradePeriodScope`) + تست SQL تأمین می‌شود. پیشنهاد: (ب) تا تصمیم؛ این در OD-11 گنجانده شده است.

## 8. Backfill Strategy

اصول: منبع هر نگاشت صریح و معتبر؛ بدون حدس؛ توقف کنترل‌شده؛ idempotent؛ قابل حسابرسی.

### 8.1 D1 — ثبت Tenant و تخصیص Company (فرمان اپراتوری، نه Migration)
- ابزار: فرمان اپراتوری کوچک (الگوی `--settings`/endpoint عملیاتی `Infrastructure`) — **نه سامانهٔ دوم**؛ همان کد سرویس PR-5 برای ثبت Tenant جدید و تخصیص.
- ورودی: manifest تأییدشدهٔ مالک: `{ tenants: [{ name, customerReference? }], companies: [{ companyId, tenant }] }`.
- شاهد تطبیقی (غیرمرجع): برای Companyهایی که `ProvisioningRequestClaims` دارند، `claim.CustomerReference` با `tenant.customerReference` مقایسه می‌شود؛ ناسازگاری = توقف. **`CustomerBoundaryId`، نام شرکت، گروه و مالکیت هرگز خوانده نمی‌شوند.**
- اعتبارسنجی پیش از نوشتن: هر Company موجود دقیقاً یک‌بار در manifest؛ هیچ Company ناشناخته؛ هیچ Tenant بدون Company (هشدار)؛ هیچ Company که از قبل Tenant متفاوت دارد (بازتخصیص ممنوع؛ رویهٔ جدا و حسابرسی‌شده، مثل قاعدهٔ `CustomerBoundaryEndpoints`).
- اجرا: تراکنش Serializable + قفل `sp_getapplock` (الگوی provisioning)، رویداد `SecurityAuditEvent` با SHA256 manifest و شمار (اندازهٔ محدود)، حالت `--dry-run` پیش‌فرض.
- Idempotency: اجرای مجدد با همان manifest هیچ تغییری نمی‌دهد (گزارش «بدون تغییر»).

### 8.2 D2 — Backfill مشتق ستون‌های `TenantId` جدول‌های فرزند
- قطعی و بدون حدس: `UPDATE child SET TenantId = c.TenantId FROM child JOIN Companies c ON c.Id = child.CompanyId WHERE child.TenantId IS NULL`؛ شرط: `c.TenantId IS NOT NULL`.
- محل اجرا: داخل همان فرمان اپراتوری (batch کوچک، قابل از‌سرگیری) یا Migration با ورودی بازبینی‌شده در `HAND_WRITTEN_SQL`. پیشنهاد: فرمان اپراتوری؛ Migration C3 فقط پیش‌شرط می‌سنجد.
- `SecurityAuditEvent`: ردیف‌هایی که `CompanyId` آن‌ها به Company موجود نمی‌رسد، `TenantId=NULL` می‌مانند و گزارش می‌شوند (ستون nullable می‌ماند).

### 8.3 D3 — اعتبارسنجی قبل و بعد
قبل: `COUNT(*)` هر جدول، فهرست Companyها، فهرست FK یتیم. بعد: همان شمارها برابر؛ `Companies WHERE TenantId IS NULL = 0`؛ برای هر جدول `COUNT(*) WHERE TenantId IS NULL = 0` و `COUNT(*)` ردیف‌هایی که `child.TenantId <> Companies.TenantId = 0`. خروجی به‌صورت گزارش ذخیره و ضمیمهٔ handoff PR اجرا می‌شود.

### 8.4 D4 — الزام NOT NULL
Migration C3 با پیش‌شرط دستی بازبینی‌شده: اگر ردیف NULL وجود دارد `THROW` با پیام روشن و Migration شکست می‌خورد (توقف کنترل‌شده، بدون تغییر ناقص چون تراکنش).

### 8.5 تکرار، رول‌بک، Recovery
- **قبل از D1:** Backup کامل (الزام). همهٔ Schemaهای D1/D2 افزایشی‌اند؛ رول‌بک = بازگرداندن کد و نادیده‌گرفتن ستون‌های nullable (بی‌ضرر)؛ تا قبل از C3: `UPDATE Companies SET TenantId = NULL` (فقط با رویهٔ اپراتوری مستند و رویداد audit).
- نیازمند Backup قبل از C1: تغییر نام ستون (بخش ۱۱).

## 9. Authorization and Security

### 9.1 جریان هدف
`CompanyAccessService` همان query عضویت فعلی را (که `Company` را برای `Name` join می‌کند) با خواندن `Company.TenantId` گسترش می‌دهد؛ یعنی `CompanyAccess` فیلد `TenantId` می‌گیرد **بدون round-trip اضافه**. `WorkContext` و `TradeAccessContext` هر دو `(TenantId, CompanyId)` را حمل می‌کنند و فقط از دادهٔ اعتبارسنجی‌شدهٔ سرور ساخته می‌شوند؛ هیچ `TenantId` از کلاینت/Claim پذیرفته نمی‌شود (D-013/D-087 بند ۵).

### 9.2 انتقال تدریجی هر مؤلفه (فقط پس از PR-4؛ هر مورد PR مستقل)
| مؤلفه | تغییر |
|---|---|
| `CompanyAccessService` | افزودن `TenantId` به `CompanyAccess`؛ شناسهٔ `membership.CompanyId` پس از C1؛ در حالت `Company.TenantId = NULL` رفتار فعلی |
| `WorkContextService` | `FiscalPeriod.CompanyId == CompanyId`؛ ثبت Tenant در context؛ کوکی همچنان فقط انتخاب |
| `DelegationCeilingService` | تغییر نام مکانیکی `TenantId→CompanyId` (سطرهای membership/role)؛ منطق بدون تغییر |
| `TradeAuthorizationAdapter` | `TradeAccessContext(userId, tenantId, companyId, fiscalPeriodId, allowsWrites)`؛ `HasPermissionAsync` فقط با `companyId`؛ `TradePeriodScope` با `CompanyId` |
| HR APIs | فیلتر `CompanyId` (موجود، تغییر نام) و مقایسهٔ `Tenant` همان Company در `AuthorizeHrScopeAsync` |
| `UserAdministrationEndpoints` / `SecurityAuditAdministrationEndpoints` | OD-1؛ فیلتر `tenantId` اجباری وقتی نصب بیش از یک Tenant دارد؛ فیلتر audit از `CompanyId` + `TenantId` |
| `CompanyMembershipRequestEndpoints` | OD-3: شرط Tenant افزوده به شرط مرز |
| Provisioning | OD-6: resolve Tenant؛ `Company.TenantId` هنگام ایجاد |

### 9.3 Global Query Filter در برابر فیلتر صریح
| ملاک | Global Query Filter | فیلتر صریح + extension (`WhereCompany`) |
|---|---|---|
| هزینهٔ پیاده‌سازی | بالا: context ورود، provisioning، jobها، endpointهای اپراتوری و مسیر بدون Company جاری باید `IgnoreQueryFilters` یا context سیستمی بگیرند | پایین: کد موجود همین الگو را دارد؛ فقط یکسان‌سازی |
| ریسک خطا | فراموشی `IgnoreQueryFilters` یا context نادرست → نشت یا قفل شدن (خطای خاموش) | فراموشی فیلتر → نشت؛ با تست دو-Tenant و بازبینی grep‌پذیر کنترل می‌شود |
| پیچیدگی تست | همهٔ مسیرها باید با/بدون فیلتر تست شوند | تست مسیر به مسیر (موجود) |
| تناسب با Trade و S3 | مناسب پس از وجود jobها و گزارش‌ها | کافی برای وضعیت فعلی (بدون job) |
| D-087 | «فقط پس از طراحی context ورود/jobها/مسیرهای بدون Company و با تست منفی» | سازگار |

**پیشنهاد:** فیلتر صریح یکپارچه + تست؛ بازبینی Global Query Filter فقط هنگام ورود اولین Background Job/Export Tenant-level. این گزینه ساده‌تر و ارزان‌تر است و امنیت را به تست‌های ۱–۱۰ بخش ۱۲ گره می‌زند.

### 9.4 کنترل‌های پایدار
Anti-forgery و Cookie بدون تغییر؛ پیام خطای یکسان (404) برای Company ناموجود/غیرمجاز؛ `CustomerBoundaryEndpoints`/endpoint ثبت Tenant فقط `AccountKind=Infrastructure` و Rate-limit؛ هیچ Secret/Connection String در Manifest، Log یا گزارش.

## 10. Shared/Dedicated Compatibility

- **یک مدل منطقی، یک کد:** تفاوت فقط در تعداد ردیف `Tenants` و Connection String. Dedicated: یک Tenant (ثبت هنگام نصب از `Provisioning:CustomerReference`)؛ Shared: چند Tenant.
- **Tenant در هر دو حالت حذف نمی‌شود** و کنترل‌های سمت سرور برداشته نمی‌شوند (D-087 بند ۴).
- **Provisioning چندمشتری:** تنظیمات و `CustomerProvisioningPackageVerifier` امروز فقط یک `CustomerReference` را می‌پذیرند؛ بنابراین «یک نمونه برنامه، چند Tenant» برای *provisioning* پیاده‌نشده است (OD-14). گزینه‌های بعدی: (الف) نمونهٔ جدا به‌ازای Tenant روی یک DB مشترک (پین‌کردن Tenant در تنظیمات)؛ (ب) گسترش تنظیمات به فهرست مشتری‌ها. تصمیم و PR جدا لازم است. بقیهٔ مسیرها (دسترسی، HR، Trade) برای Shared آماده می‌شوند.
- Backup/Restore و انتقال بین مدل‌ها: Tenant با `Id` و `CustomerReference` ثابت قابل انتقال است؛ نیازی به تغییر شناسه‌ها نیست.
- Migration host: Core و Trade باید در هر دو مدل با همان مسیر اعمال شوند (PR-1).

## 11. Rollback and Recovery

| مرحله | بازگشت | شرط/توضیح |
|---|---|---|
| A (Tenants، Companies.TenantId) | `Down` migration (DropColumn/DropTable) | قبل از D1: بی‌خطر. بعد از D1: داده‌های تخصیص از دست می‌رود → Backup قبل از D1 |
| B (کد) | revert PR | بدون اثر داده |
| C1 (RenameColumn) | `Down` = Rename معکوس + deploy نسخهٔ قدیمی کد | **ناسازگار با نسخهٔ قدیمی برنامه تا Rollback کامل؛ ترتیب: توقف برنامه → Migration → نسخهٔ جدید کد** (OD-12)؛ Backup الزامی |
| C2 (AddColumn `TenantId` + FK مرکب) | `Down` (حذف FK و ستون) | ستون nullable بی‌ضرر؛ FK با NULL اعمال نمی‌شود |
| D1/D2 (Backfill) | `UPDATE … SET TenantId = NULL` با رویهٔ مستند؛ تا قبل از C3 | ثبت audit؛ نیازمند Backup |
| C3 (NOT NULL) | `Down` به nullable | پیش‌شرط مانع اعمال ناقص |
| Provisioning (OD-6) | revert کد | بدون ردیف Tenant، provisioning با خطای واضح رد می‌شود (fail closed) |

اصل: هیچ Migration منتشرشده بازنویسی نمی‌شود؛ هر Migration `Down` و تست رفت‌وبرگشت روی SQL Server واقعی دارد (طبق `AGENTS.md` و `docs/database-migrations.md`).

## 12. Test and Acceptance Plan

نوع تست: **U** = Unit، **I** = Integration (HTTP/TestHost)، **S** = SQL Server واقعی (`scripts/dev/test-sqlserver.sh up`؛ فقط وقتی «موفق» گزارش شود که واقعاً اجرا شده).

| # | معیار | تست | نوع |
|---|---|---|---|
| 1 | دو Tenant در DB مشترک داده یکدیگر را نمی‌بینند | هر endpoint company-scoped (HR، Trade، Membership، Role، Fiscal، Directory) با دو Tenant و کاربر هر Tenant؛ مسیرهای OD-1 | S + I |
| 2 | هر Company دقیقاً یک Tenant | NOT NULL + FK؛ درج Company بدون Tenant رد می‌شود | S |
| 3 | Tenant چند Company | دو Company در یک Tenant؛ دسترسی تفکیکی | S + I |
| 4 | Dedicated همان مدل | اجرای مجموعهٔ ۱–۳ با یک Tenant و Connection String جدا؛ آزمون جداسازی اتصال | S |
| 5 | عدم اشتباه Tenant/Company | تست معماری (فهرست Entityهای legacy)؛ کامپایل پس از Rename؛ درج ناسازگار `(CompanyId, TenantId)` رد می‌شود | U + S |
| 6 | CustomerBoundaryId مستقل | هیچ کدی آن را به TenantId کپی یا از آن Tenant نمی‌سازد (تست grep/معماری)؛ تخصیص مرز تأثیری روی Tenant ندارد | U + I |
| 7 | مجوز سمت سرور | درخواست با `companyId` غیرمجاز/`tenantId` جعلی در Body یا Claim رد می‌شود؛ 404 یکسان | I |
| 8 | یکتایی Company-scoped | دو Company در یک Tenant با کد/شماره یکسان مجاز؛ تکرار در یک Company رد (موجود روی `(CompanyId, Code)`) | S |
| 9 | Backfill تکرارپذیر/قابل حسابرسی | dry-run بدون نوشتن؛ اجرای دوم no-op؛ بازتخصیص رد؛ manifest ناقص توقف؛ شمارش قبل/بعد؛ رویداد audit | S + I |
| 10 | عدم شکستن رفتار فعلی | کل مجموعهٔ فعلی (`HrOrganizationalStructureEndpointTests`، `CompanyAccessTests`، `WorkContextTests`، `Trade*` و SQL Server tests) پس از هر PR؛ مقایسهٔ API contract | I + S |

تست‌های Migration: `python3 scripts/generate_migrations.py --check`، `scripts/verify_migration_model.py`، `dotnet ef migrations has-pending-model-changes`، `scripts/tests/test_migration_tooling.py`، `test_migration_guard_mutations.py` (با Mutation test برای قاعدهٔ Rename جدید)، و تست رفت‌وبرگشت Up/Down روی SQL Server. **هیچ‌یک در این مرحله اجرا نشده است.**

## 13. PR Breakdown

هر PR مستقل، قابل Build/Test، با Migration کوچک و قابل بازگشت؛ تغییرات امنیتی و Schema جدا. ستون «وابسته به تأیید» تصمیم‌هایی است که باید پیش از **ساخت** PR Accepted شده باشند.

| PR | محتوا | نوع | وابسته به تأیید | پیش‌نیاز |
|---|---|---|---|---|
| **PR-0** | ثبت S3-0e/S3-0f و (در صورت تأیید) ADR `Proposed`، به‌روزرسانی `DATABASE.md`/`ROADMAP.md` | مستندات | هیچ | — |
| **PR-1** | قرارداد Migration host: `--migrate` و بررسی pending برای Trade (`__TradeMigrationsHistory`) + تست SQL Server | کد (بدون Schema جدید) | هیچ | — |
| **PR-2** | Spike و توسعهٔ ابزار گارد برای `RenameColumn` و نام ثابت قیدها + Mutation test | اسکریپت/تست | هیچ | — |
| **PR-3** | interfaceهای `ICompanyOwned/ITenantOwned`، extension فیلتر، تست معماری فهرست legacy | کد | هیچ | — |
| **PR-4** | Schema A: `Tenants` + `Companies.TenantId` (nullable) + AK + IX + Entity | Schema | تأیید شکل Tenant (ساده) | — |
| **PR-5** | ثبت Tenant توسط اپراتور + فرمان Backfill D1 (dry-run/audit) + (اختیاری) ستون `CustomerReference` | کد + Schema کوچک | OD-6، OD-6a، OD-13 | PR-4 |
| **PR-6** | Provisioning: resolve Tenant، `Company.TenantId`، رد fail-closed | کد | OD-6، OD-6a | PR-5 |
| **PR-7** | G1: RenameColumn + کد مکانیکی (Role، Membership، MembershipRequest، HR) | Schema (rename) + کد | OD-4، OD-10، OD-12 | PR-2، PR-3، PR-4 |
| **PR-8** | G2: FiscalPeriod/Claim/ConsolidationRunCompany (rename) | Schema + کد | OD-10، OD-12 | PR-7 |
| **PR-9** | G3: Trade (rename + `TenantId`) | Schema + کد | OD-10، OD-11، OD-12 | PR-1، PR-2، PR-4 |
| **PR-10** | G4: SecurityAuditEvent (rename + `TenantId`) + فیلتر گزارش | Schema + کد | OD-5 | PR-4 |
| **PR-11a..d** | C2: ستون `TenantId` واقعی + FK مرکب برای G1..G3 | Schema | OD-11 | PR-7..10 |
| **PR-12** | D2: Backfill مشتق + گزارش | کد/فرمان | OD-13 | PR-5، PR-11 |
| **PR-13** | C3: NOT NULL با پیش‌شرط | Schema | پایان Backfill | PR-12 |
| **PR-E1** | `CompanyAccess`/`WorkContext` با `TenantId` | کد امنیتی | — | PR-4 |
| **PR-E2** | Admin endpoints (OD-1) + کشف عضویت (OD-3) | کد امنیتی | OD-1، OD-3 | PR-E1، PR-5 |
| **PR-E3** | `TradeAccessContext(Tenant, Company)` | کد امنیتی | — | PR-9، PR-E1 |

**اولین PR:** PR-0 (فقط مستندات). **اولین PR فنی:** PR-1 (مستقل از همهٔ تصمیم‌ها و مانع تغییر Schemaی Trade). PR-2، PR-3 و PR-4 می‌توانند موازی با PR-1 باشند.

## 14. Risks and Deferred Items

| ریسک | شدت | کاهش |
|---|---|---|
| **K-1:** رفتار EF/ابزار گارد برای Rename اثبات‌نشده؛ ممکن است Drop/Add FK تولید کند | متوسط | Spike PR-2؛ نام ثابت قیدها؛ گزینهٔ E به‌عنوان جایگزین |
| **K-2:** ناسازگاری نسخهٔ قدیمی برنامه با Schema پس از Rename | متوسط | پنجرهٔ نگهداری؛ ترتیب توقف→Migration→deploy؛ Backup |
| **K-3:** شکاف Migration host برای Trade (جداول `trade` در `Farano-Local` نیستند) | **بالا** برای PR-9 | PR-1 پیش از هر Schema Trade |
| **K-4:** فرض OD-6a (`CustomerReference` ≡ Tenant) نادرست باشد | بالا | تأیید صریح مالک؛ manifest مالک مرجع نهایی است |
| **K-5:** Backfill روی نصب‌های ناشناخته با Company بدون Claim | متوسط | manifest دستی؛ توقف در ناقص‌بودن |
| **K-6:** Shared چندمشتری با provisioning تک‌مقداری | متوسط | OD-14؛ PR جدا |
| **K-7:** `Role.Admin` سراسری هنوز در Shared (R-1) | بالا برای Shared | OD-1؛ E2؛ قاعدهٔ «به مشتری داده نشود» |
| **K-8:** `Company.NationalId` یکتای سراسری در Shared | متوسط | OD-7 پیش از مشتری دوم |
| **K-9:** صحت FK مرکب در Trade بدون FK بین schemaها | متوسط | کنترل منطقی + تست SQL (OD-11) |
| **K-10:** ۱۰۰ کاربر هم‌زمان: اضافه‌شدن ایندکس/ستون‌ها | پایین | join روی `Companies` در query موجود؛ اندازه‌گیری انجام نشده |

**به تعویق:** یکتایی `NationalId` (OD-7)؛ Global Query Filter؛ جدول حذف FK قدیمی `CompanyId→Companies.Id` پس از FK مرکب؛ مدل چندمشتری provisioning (OD-14)؛ ماتریس Entityهای مرجع Trade V3.2؛ بازبینی `AdminUserSessionEndpoints` و `LoginLog`؛ اندازه‌گیری عملکرد؛ تأیید تفاوت `UserConfiguration.cs` و Snapshot برای فیلتر Username.

## 15. Approval Checklist (مالک محصول)

- [ ] OD-6: تأیید گزینهٔ ترکیبی (اپراتور Tenant می‌سازد؛ Provisioning فقط resolve می‌کند)
- [ ] OD-6a: تأیید اینکه `CustomerReference` برای نصب‌های موجود معادل یک Tenant است (یا اعلام جایگزین)
- [ ] OD-13: روش Backfill اولیه (manifest مالک)
- [ ] OD-1: Platform Administrator = `Role.Admin` و مدیر مشتری از مجوزهای `CompanyRole`
- [ ] OD-3: کشف عضویت = شرط Tenant AND شرط مرز؛ بدون کشف بین Tenantها
- [ ] OD-4: `CompanyRole` Company-scoped
- [ ] OD-5: بدون ستون Scope برای Audit
- [ ] OD-10: تغییر نام property/ستون legacy به `CompanyId`
- [ ] OD-11: ستون فیزیکی `TenantId` برای کدام جدول‌ها و رفتار Trade (FK یا کنترل منطقی)
- [ ] OD-12: پذیرش پنجرهٔ نگهداری کوتاه به‌ازای PR تغییر نام
- [ ] OD-14: مدل Shared (یک نمونه برای چند Tenant یا نمونه به‌ازای Tenant)
- [ ] تأیید ترتیب PRها و اینکه PR-0 و PR-1 شروع شوند

### پیشنهاد ثبت در اسناد (فقط Proposed؛ پس از تأیید)
- **`DECISIONS.md`:** ADR جدید (شمارهٔ بعدی از انتهای فایل) با وضعیت `Proposed` برای راهبرد Rename-then-Add و OD-6؛ ADRهای جدا برای OD-1، OD-3، OD-11، OD-14. هیچ‌کدام `Accepted` نشود تا تأیید صریح.
- **`DATABASE.md`:** بخش «طرح گذار Tenant/Company (Proposed)» با مدل بخش ۵ و جدول وضعیت فاز؛ تا قبل از تأیید، جملهٔ «هیچ ستون/کلید Tenant جدید ساخته نشود» حفظ شود.
- **`ROADMAP.md`:** زیر «S3» ردیف‌های S3-0e/S3-0f با واژهٔ `Proposed` و فهرست PR-0..PR-E3؛ همچنین هم‌ترازی نام S3-0f/S3-0g با `PROJECT_STATUS.md`.
- **`PROJECT_STATUS.md`:** فقط پس از ادغام گزارش‌های S3-0e/S3-0f؛ وضعیت `NotImplemented` برای مدل هدف.

## 16. محدودیت‌های این طراحی

- هیچ build، test، Migration، Spike یا اتصال دیتابیس اجرا نشد؛ هیچ ادعای «Pass» وجود ندارد. رفتار `sp_rename`/EF/گارد فرض مستندشده است، نه اثبات‌شده.
- شواهد از clone سطحی `main@d81bb29` و تطبیق با `origin/main@8f103aa` (فقط e2e تفاوت دارد)؛ شاخه‌های دیگر و PRهای باز بررسی نشدند.
- شواهد دیتابیس فقط metadata `Farano-Local` است؛ دیتابیس مشتریان واقعی و مقادیر TenantId/CompanyId بررسی نشد.
- گزارش S3-0e هنوز در `main` نیست؛ این طرح بر نسخهٔ محلی آن تکیه دارد.
- کامپیوتر کاربر در دسترس نبود؛ تغییرات commit‌نشدهٔ شما بررسی نشد.
