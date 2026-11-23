# تحویل وظیفه — ISSUE-UNNUMBERED-trade-tenant-company-owner-decisions-2026-11-23

- **Issue:** UNNUMBERED (مرتبط با #397 / S3-0؛ شمارهٔ جعلی ساخته نشد)
- **Agent/مسئول:** Claude (Lead، مسئول زیرسیستم Trade)
- **تاریخ:** 2026-11-23
- **وضعیت:** انجام‌شده (فقط مستندات؛ CI در انتظار)

## ۱. هدف، محدوده و وضعیت مخزن

ثبت رسمی چهار تصمیم مالک (Q-1..Q-4)، هماهنگ‌سازی مستندات متعارض، تحلیل اثر بر Core و Trade و نقشهٔ اجرایی. هیچ کد، Entity، EF Configuration، Migration، Schema، API، UI یا دیتابیسی تغییر نکرد و `DenyAllTradeModuleEntitlement` و تمام Gateهای Trade دست‌نخورده‌اند.

- مبنا: `origin/main@950baa3` («build(publish): enforce customer artifact allow-list for G2 (#771)»). Working Tree پیش از شروع تمیز بود.
- شاخه: `docs/trade-tenant-company-owner-decisions-2026-11-23` (از `main` ساخته شد).
- **به‌روزرسانی:** PR **#780** (D-118 و بازنگری D-091) پس از نگارش این handoff در `main` ادغام شد (`490224e`). D-118 شمارهٔ خود را دارد و D-119..D-121 با آن تداخل ندارد؛ تداخل متنی `DECISIONS.md`/`INDEX.md`/`WORK_LOG.md` هنگام هماهنگ‌سازی این PR با `main` رفع شد و `DATABASE.md` پس از آن هم‌تراز شد.

## ۲. ثبت تصمیم‌ها

| تصمیم مالک | شناسه | محل ثبت | وضعیت قبلی ← جدید | تاریخ تأیید |
|---|---|---|---|---|
| Q-1 مالکیت Party و Product | **D-119** (جدید) | `DECISIONS.md` انتها | — ← `Accepted` / `NotImplemented` | 2026-11-23 |
| Q-2 تفکیک فیزیکی `TenantId`/`CompanyId` | **D-120** (جدید) | `DECISIONS.md` انتها | — ← `Accepted` / `NotImplemented` | 2026-11-23 |
| Q-3 ترتیب Core ← Trade | **D-121** (جدید) | `DECISIONS.md` انتها | — ← `Accepted` / `NotImplemented` | 2026-11-23 |
| Q-4 قرارداد D-090 | **D-090** (موجود، در محل) | `DECISIONS.md` (بخش «تصمیم مالک (2026-11-23)» و «حدود تصمیم») | `Proposed` ← `Accepted` / `NotImplemented` | 2026-11-23 |

- متن Proposed اولیهٔ D-090 حذف نشد؛ تنها سربرگ وضعیت تغییر کرد و بخش تصمیم مالک افزوده شد.
- **D-091:** بخش «گام P: `HasColumnName`» و «Rename اختیاری» توسط D-120 با ارجاع صریح جایگزین (superseded) شد؛ D-091 تا تصویب جداگانه `Proposed` است و متن آن دست نخورد (بازنگری آن در #780 جریان دارد و با D-120 سازگار است).
- تصمیم‌های باز (بخش ۸) Accepted نشدند: ترتیب دقیق Migrationها و Sequenceهای Core؛ Backfill و رکوردهای بدون نگاشت؛ سیاست حذف/غیرفعال‌سازی؛ جزئیات Unique Index؛ Permission/Authorization/Entitlement؛ `TradeAccessContext`/`TradePeriodScope`؛ D-091..D-096 و G0/G3/G5/G6 و N-5/N-5b.

مستندات هماهنگ‌شده (همه با «به‌روزرسانی 2026-11-23» و بدون پاک‌کردن تاریخچه): `TRADE_TENANT_COMPANY_AUDIT.md`، `TRADE_DECISION_RECONCILIATION.md`، `TRADE_S3_READINESS.md`، `PROJECT_STATUS.md`، `ROADMAP.md` (بند S3-0)، `TRADE_MODULE.md` (به‌علاوهٔ ثبت محدودیت درج مستقیم SQL که معیار پذیرش D-090 بود)، `EXCEL_IMPORT_DESIGN.md` (یک سلول)، `INDEX.md`، `WORK_LOG.md`.
**هماهنگ‌شده پس از Merge #780:** جملهٔ «D-090 … Proposed» در `DATABASE.md` اصلاح شد. handoffهای تاریخی بازنویسی نمی‌شوند.

## ۳. وضعیت واقعی معماری از سورس (`main@950baa3`)

### Core
| Entity/Table | مالکیت | ستون‌ها و معنا | وضعیت |
|---|---|---|---|
| `Tenant` | — | **وجود ندارد.** `class Tenant`/`DbSet<Tenant>` در سورس نیست؛ نامی از `Tenants` در ۲۱ Migration و Snapshot Core نیست | شکاف اصلی |
| `Company` (`Domain/Company.cs`) | Company | `Name`، `NationalId`، `CustomerBoundaryId` (nullable؛ Tenant نیست)، `IsActive`؛ **ستون Tenant ندارد** | `Company.Id` تنها مالک legacy |
| `FiscalPeriod`، `UserCompanyMembership`، `CompanyRole`، `CompanyEmployee*`، `CompanyOrganizationalUnit`، `CompanyPosition` | Company | `TenantId` که در واقع `Company.Id` است؛ `CompanyMembershipRequest.DestinationTenantId` = Company مقصد | قفل‌شده با `LegacyTenantIdEntityContractTests` (۱۰ ستون) |
| `SecurityAuditEvent` | ترکیبی | `TenantId` nullable، گاهی Company | باز (OD-5) |
| `CorporateGroup*`، `OwnershipRelationship` | Company | از ابتدا `CompanyId`/`OwnerCompanyId` | عضویت گروه دسترسی نمی‌دهد |

### Trade
| Entity / جدول | مالکیت (D-119) | ستون فعلی | معنای واقعی `TenantId` | `CompanyId` | AK / FK / Unique | Query / Insert isolation | نقص یا ریسک |
|---|---|---|---|---|---|---|---|
| `Party` / `trade.Parties` | Company | `TenantId` | `Company.Id` | ندارد | AK `(TenantId, Id)`؛ Unique `(TenantId, Code)` فعال | محمول `TenantId` در `PartyStore`؛ Insert با `tenantId` Context؛ بدون کنترل زوج | معنا و نام اشتباه؛ Unique باید Company-scoped بماند |
| `PartyRoleAssignment` / `PartyRoleAssignments` | Company (تابع Party) | `TenantId` | `Company.Id` | ندارد | FK `(TenantId, PartyId)→Parties(TenantId, Id)`؛ Unique `(TenantId, PartyId, Role)` | Include با محمول | FK باید دو شناسه شود |
| `PartyBankAccount` / `PartyBankAccounts` | Company | `TenantId` | `Company.Id` | ندارد | در مدل: Unique `(TenantId, Iban)` و `(TenantId, PartyId, AccountNumber)`؛ `ExcludeFromMigrations` | جدول وجود ندارد؛ مسیر نوشتن ندارد | باید مستقیماً با شکل نهایی ساخته شود |
| `Product` / `trade.Products` | Company | `TenantId` | `Company.Id` | ندارد | AK `(TenantId, Id)`؛ Unique `(TenantId, Code)` | محمول در `ProductStore` | مثل Party |
| `ProductIdentifier` / `ProductIdentifiers` | Company | `TenantId` | `Company.Id` | ندارد | FK `(TenantId, ProductId)` و `(TenantId, SupplierPartyId)→Parties`؛ Unique GTIN/IRC؛ `ExcludeFromMigrations` | جدول و مسیر نوشتن وجود ندارد | تأمین‌کننده باید هم‌Company باشد (D-119 بند ۵) |

- Global Query Filter وجود ندارد؛ `IgnoreQueryFilters`/Raw SQL/`ExecuteUpdate` در Trade یافت نشد. Background Service و Import/Export در Trade وجود ندارد.
- `DbContext`: `TradeDbContext` (Schema `trade`، History جدا)، همان Connection String Core (`Program.cs`: `AddTradePersistence(connectionString)`)؛ Migration فقط با `--include-trade`.
- تنها Migration Trade: `20261113072823_InitialTradeMasterData` (سه جدول؛ `Down` فقط حذف جدول‌ها).
- DTOها هیچ فیلد Tenant/Company ندارند؛ فیلد ناشناخته نادیده گرفته می‌شود.
- OriginFiscalPeriodId/FiscalPeriodId: این پنج جدول ندارند؛ مشمول بودن آن‌ها تصمیم نشده است (D-117 بند ۴؛ پیشنهاد: غیرمشمول چون داده پایه‌اند).

### نگاشت Company→Tenant (۶.۱)
- هیچ Tenant و هیچ ستون/رابطهٔ Company→Tenant در سورس نیست؛ پس برای **همهٔ** Companyهای موجود نگاشت وجود ندارد و ساختنش از داده ممکن نیست: `CustomerBoundaryId`، عضویت گروه، نام یا دامنه Tenant نیستند (D-089، AGENTS §5.3). هر رکورد Trade/Core بدون نگاشت معتبر باید Migration را **متوقف** کند؛ حدس‌زدن Tenant ممنوع است.
- وضعیت داده واقعی مشتریان (Companyهای متعلق به Tenantهای مختلف یا تک‌Tenant) از سورس معلوم نیست؛ **Unknown** و نیازمند Manifest مصوب (D-092، `Proposed`).

## ۴. مسیر Context (احراز‌شده از سورس)

| مرحله | کجا / چه کسی | شاهد |
|---|---|---|
| کاربر احراز‌شده | Cookie Authentication؛ Claim `UserId` و `SessionId` | `WorkContextCookie.SessionIdClaimType` |
| Tenant Resolution | **امروز وجود ندارد** (Tenant = Company). هدف: Host canonical طبق D-103، سپس `Users.TenantId` طبق D-118 (پیشنهادی در #780) | D-087، D-103 |
| عضویت و مجوز Company | `CompanyAccessService`: عضویت فعال، Company فعال، نقش؛ هر درخواست از DB | `WorkContextService.ResolveAsync` ← `FindAccessibleCompanyAsync` |
| Context معتبر | `WorkContextService.GetCurrentAsync` کوکی `.Faranou.WorkContext` را می‌خواند، دوباره Company و دوره را از DB resolve می‌کند؛ اگر Company یا دوره دیگر معتبر نباشد کوکی را حذف می‌کند، و کوکی دست‌کاری‌شده، منقضی یا متعلق به کاربر/نشست دیگر `null` می‌دهد و نادیده گرفته می‌شود (لزوماً حذف نمی‌شود) | `WorkContextService` |
| انتقال Host ← Trade | `TradeAccessFilter` ← `TradeAccessGate` ← `TradeAuthorizationAdapter` که `TradeAccessContext` را از `WorkContext` سمت سرور می‌سازد و با کلید خصوصی ثبت می‌کند؛ `TradeAccessFilter` پس از تصمیم `Allowed` آن را با `AccessContextItemKey` در `HttpContext.Items` می‌گذارد و Endpoint فقط از همان می‌خواند | `TradeAuthorizationAdapter` (ساخت `TradeAccessContext`)، `TradeProductEndpoints`/`TradePartyEndpoints` |
| ترتیب Gate | Context ← دوره ← entitlement ← مجوز؛ fail-closed (امروز `DenyAll`) | `TradeAccessGate` |

**جلوگیری از جعل:** کوکی با `ITimeLimitedDataProtector` (Purpose `Faranou.WorkContext.v1`، عمر حداکثر ۱۲ ساعت) و payload شامل `UserId`، `SessionId`، `CompanyId`، `FiscalPeriodId` است؛ `Read` تطبیق با کاربر و نشست جاری را می‌سنجد؛ HttpOnly، SameSite=Lax، Secure خارج از Development. Claim جعلی نادیده گرفته می‌شود و شناسهٔ Client هرگز مجوز نیست. آزمون‌ها: `ForgedContextIsDenied`، `CookieForForeignCompanyOrPeriodResolvesNull`، `Overview_IgnoresATenantClaimForgedIntoThePrincipal`، `AUserCanNotAccessACompany_InWhichSheHasNoActiveMembership` (از اجرای قبلی همین جلسه روی همان SHA؛ در این مأموریت دوباره اجرا نشد).
**جلوگیری از Context باقی‌مانده:** کوکی به `UserId`+`SessionId` گره خورده (نشست دیگر ⇒ نادیده)، عمر محدود دارد، و هر درخواست عضویت زنده را دوباره می‌سنجد؛ حذف عضویت یا غیرفعال‌شدن Company فوراً Context را `null` می‌کند و کوکی پاک می‌شود. تغییر Company فقط با `SelectAsync` (نیازمند عضویت) و ثبت Audit.

**شکاف‌های این مسیر نسبت به D-090:** (۱) کوکی و `TradeAccessContext` فقط یک شناسه (Company) دارند؛ (۲) Tenant هرگز از کوکی/Claim/ورودی خوانده نشود و باید در همان Resolve از رابطهٔ DB Company→Tenant بیاید و با Company در یک مرحله ساخته شود تا زوج ناسازگار وجود نداشته باشد؛ (۳) Tenant مشتق از Company باید با Tenant حاصل از Host/کاربر برابر باشد، وگرنه fail-closed و Audit (پیشنهاد معیار پذیرش C4؛ مرز Tenant در مدل Shared)؛ (۴) Trade زوج را دوباره کنترل کند (هر Query/Insert دو شناسه) و AK/FK مرکب داخل Trade وجود داشته باشد؛ تغییر `TradeAccessContext` خود تصمیم باز N-5a است.

## ۵. نقشهٔ PRها (سازگار با Q-3؛ هیچ‌کدام ساخته نشد)

ترتیب Merge: P1 (موازی) — C1 → C2 → C3 → C4 → T1 → T2. وضعیت هر Schema PR تا مجوز جداگانهٔ Schema `NeedsApproval` است.

| PR | هدف / Scope | وابستگی | Migration | تست لازم | معیار پذیرش | ریسک Rollback |
|---|---|---|---|---|---|---|
| **P1** (اختیاری، بدون Schema) | تست معماری: هر Query روی `TradeEntity` محمول scope دارد؛ Trade به Core وابسته نیست؛ آزمون‌های جعل Context بیشتر | هیچ | ندارد | Unit/Architecture | سبز؛ بدون تغییر رفتار | ناچیز |
| **C1** (Core) | Entity/جدول `Tenant` و `Companies.TenantId` nullable + FK | تأیید Schema (G0) | دارد (Up/Down) | EF Model، SQL Server Up/Down | Down آزموده؛ بدون Backfill | کم (ستون nullable) |
| **C2** (Core) | ثبت Tenant اپراتوری (D-089) و Manifest نگاشت؛ گزارش dry-run فقط‌خواندنی؛ توقف در رکورد بدون نگاشت | C1، تصمیم D-092 | ندارد یا Seed کنترل‌شده | Idempotent، رکورد ناقص ⇒ توقف | گزارش بدون حدس | کم |
| **C3** (Core) | Backfill با Manifest، NOT NULL، AK `(TenantId, Id)` روی `Companies` برای FK مرکب | C2، G5 (Backup) | دارد | SQL Server: زوج ناسازگار رد شود | هیچ Company بدون Tenant | متوسط (Down: حذف Tenant؛ بازسازی از Manifest) |
| **C4** (Core/Host) | `WorkContext` علاوه بر Company، Tenant را از رابطهٔ DB resolve می‌کند (نه از کوکی) | C3 | ندارد | Forged Context، Cross-Tenant | زوج فقط از DB؛ **Tenant مشتق از Company باید با Tenant حاصل از Host/کاربر (D-103، و D-118 در صورت تصویب) برابر باشد؛ عدم تطابق ⇒ fail-closed و Audit** (کاربر عضو Company در Tenant B نباید از Host/دامنهٔ Tenant A وارد شود) | کم |
| **T1** (Trade، اتمیک) | Rename `TenantId→CompanyId` + `TenantId` واقعی در سه جدول؛ AK `(TenantId, CompanyId, Id)`؛ FK مرکب فرزندان؛ Unique Company-scoped؛ Entity/Config/Store/Service/تست؛ `TradeAccessContext` دو‌شناسه‌ای (N-5a) | C4؛ تأیید N-5a؛ O-1..O-3 | دارد (Up/Down) | گزارش ۷ | Up/Down روی SQL Server آزموده؛ همهٔ تست‌های Isolation سبز؛ بدون `HasColumnName` | **بالا**: Down مقدار TenantId را از دست می‌دهد؛ Backup الزامی؛ پس از Cutover Roll-forward |
| **T2** (Trade) | ساخت `PartyBankAccounts` و `ProductIdentifiers` با شکل نهایی (حذف `ExcludeFromMigrations`) | T1 | دارد | FK مرکب سه‌ستونی، Supplier هم‌Company | رد تأمین‌کنندهٔ Company دیگر | کم (جدول جدید) |

چرا T1 یک PR اتمیک است: Migration جدا از کد باعث ناسازگاری Snapshot/Model می‌شود و دو Migration پشت‌سرهم با Q-3 بند ۵ (پرهیز از Migration غیرضروری و وضعیت میانی نامعتبر) سازگار نیست. T1 بزرگ‌ترین ریسک است و باید با Backup (G5) و آزمون Up/Down روی SQL Server آزمایشی بسته شود.

## ۶. شکاف‌های معماری

| # | شدت | شواهد | پیامد | راهکار | وابستگی به مالک |
|---|---|---|---|---|---|
| G-1 | **High (مانع)** | Tenant در Core وجود ندارد؛ `Company` ستون Tenant ندارد؛ هیچ Migration نام `Tenants` ندارد | D-090 قابل پیاده‌سازی نیست؛ نگاشت قابل ساخت نیست | C1..C4 | Schema/G0 |
| G-2 | **High** | `TradeEntity` تنها `TenantId` دارد و `TradeAuthorizationAdapter` مقدار Company را در آن می‌گذارد؛ `LegacyTenantIdEntityContractTests` همین را قفل می‌کند | مدل D-087/D-119/D-120 پیاده نیست | T1 | تأیید Schema |
| G-3 | **High** | `TradeAccessContext(UserId, TenantId, FiscalPeriodId, AllowsWrites)` فقط یک شناسه | قرارداد D-090 زوجی ممکن نیست | N-5a در T1 | **تصمیم باز N-5** |
| G-4 | **High** | `Trade` به Core وابسته نیست، پس Backfill از Core در Migration Trade مسیر مستقیم ندارد | سازوکار Backfill نامشخص | O-1 | تصمیم مالک |
| G-5 | Medium | AKها `(TenantId, Id)` و FKها دو‌ستونه‌اند (`PartyConfiguration`، `ProductConfiguration`، `TradeEntityConfiguration`) | اتصال Company-A به Party Company-B در همان Tenant بعد از جداسازی ممکن می‌شود | AK/FK سه‌ستونی در T1 | — |
| G-6 | Medium | FK تأمین‌کننده `(TenantId, SupplierPartyId)` در `ProductIdentifierConfiguration` | بعد از جداسازی، تأمین‌کنندهٔ Company دیگر مجاز می‌شود (D-119 بند ۵) | FK `(TenantId, CompanyId, SupplierPartyId)` در T2 | — |
| G-7 | Medium | Unique `(TenantId, …)` برای Code/IBAN/GTIN/IRC | دامنهٔ یکتایی باید Company شود | Unique با `CompanyId` | O-2 (جزئیات) |
| G-8 | Medium | Global Query Filter نیست؛ کنترل فقط محمول در Store (`PartyStore`، `ProductStore`) | Store جدید ممکن است محمول را فراموش کند | P1 تست معماری | — |
| G-9 | Low | کامنت/تست‌ها «tenant-level master data (D-063 بند ۳)» (`TradePartyEndpoints`، `TradeProductEndpoints`، `ClosedPeriodPartyWritesAreAllowedBecausePartyIsTenantLevelMasterData`) | ناسازگاری واژگانی با D-119؛ رفتار نوشتن در دورهٔ بسته دست‌نخورده | اصلاح در T1؛ رفتار تا تصمیم مالک ثابت | غیرمسدودکننده |
| G-10 | Low | `ExcludeFromMigrations` برای دو جدول؛ Snapshot آن‌ها را دارد | ساخت بعدی با شکل legacy اشتباه است | T2 | — |
| G-11 | Info | D-091 متن اولیه (HasColumnName) با Q-2 در تعارض بود | رفع‌شده در سطح ستون: D-091 اصلاح‌شدهٔ #780 با D-120 هم‌جهت است؛ نام فیزیکی قیدها همچنان تابع D-095 | D-120 | — |

## ۷. تست و اثبات

نوع و وضعیت (هیچ تستی در این مأموریت اجرا نشد؛ تغییر فقط مستندات است):

| دسته | موجود | کمبود / لازم |
|---|---|---|
| Unit / Domain | `TradeMasterDataDomainTests` | سازنده‌های دو‌شناسه‌ای (T1) |
| EF Core Model | `TradeMasterDataModelTests`، `LegacyTenantIdEntityContractTests` | قفل جدید: `CompanyId`+`TenantId` در هر Entity؛ ممنوعیت `HasColumnName("TenantId")` |
| SQL Server Integration | `TradeProductStoreSqlServerTests`، `TradePartyHttpTests` (دو Company)، `TradeMasterDataMigrationSqlServerTests` (فقط با `FARANOU_TEST_SQLSERVER`) | AK/FK سه‌ستونی و رد زوج ناسازگار روی SQL Server |
| Migration Up/Down | `TradeMasterDataMigrationSqlServerTests` برای Migration اولیه | Up/Down Rename+Backfill؛ توقف در رکورد بدون نگاشت؛ اجرای تکراری؛ همزمانی |
| Cross-Company | `TradeMasterDataCompanyIsolationServiceTests`، `OtherTenantPartyIsInvisible…` | هر دو Company در یک Tenant |
| Cross-Tenant | **ندارد** (Tenant وجود ندارد) | (Tenant-A, Company-A)، (Tenant-A, Company-B)، (Tenant-B, Company-C)؛ Company از Tenant دیگر رد شود؛ کاربر عضو Company در Tenant B با Host/Tenant A ⇒ fail-closed |
| Forged Context | `TradeAuthorizationAdapterTests`، `TradeAccessGateTests` | جعل TenantId در کوکی/Claim/Header |
| Insert/Update Relationship | — | فرزند به Parent Company دیگر؛ Supplier Company دیگر؛ Update با Id بیگانه |

**نقل‌شده؛ در این PR اجرا نشد** — نتیجهٔ اجرای جلسهٔ قبل (همان SHA کد) و شاهد این PR نیست: ۴۷۲ تست فیلترشدهٔ Trade/Legacy/Ownership؛ ۴۴۱ موفق، ۰ ناموفق، ۳۱ skip (SQL Server پیکربندی نبود). تست‌های SQL Server **اجرا نشدند** و موفق گزارش نمی‌شوند.

> **به‌روزرسانی (ادامهٔ مأموریت، 2026-11-23):** تحلیل کامل O-1..O-4 در `ISSUE-UNNUMBERED-trade-tenant-company-o1-o4-analysis-2026-11-23.md` است و پیشنهاد O-1 را عوض می‌کند: گزینهٔ «د» (فرض خالی‌بودن) کنار گذاشته شد چون شواهدی برای خالی‌بودن نصب‌ها نیست (Unknown)، و گزینهٔ پیشنهادی اکنون «expand → Backfill اپراتوری → contract» بدون Staging است. O-3 دیگر تصمیم مسدودکننده نیست و جزء T1 است. این بخش به‌عنوان سابقه نگه داشته شد.

## ۸. موارد نیازمند تصمیم بعدی مالک (فقط مانع‌ها)

| # | مسئله | گزینه‌ها | مزایا / معایب | پیشنهاد |
|---|---|---|---|---|
| **O-1** | چگونه نگاشت معتبر Company→Tenant به Backfill Trade برسد بدون وابستگی Trade به Core؟ | الف) Host (`DatabaseMigrationHost`) پیش از Migration، Manifest تأییدشده را در جدول staging متعلق به schema `trade` می‌نویسد و Migration (اسکریپت ایستا، بدون پارامتر زمان اجرا) آن را مصرف و حذف می‌کند؛ پیش‌شرط: جدول staging کامل و سازگار با Hash/یکپارچگی (D-092)، وگرنه توقف؛ ب) ستون `TenantId` در Migration اول nullable، Backfill توسط Host، سپس Migration دوم NOT NULL؛ ج) خواندن مستقیم جدول‌های Core در Migration Trade؛ د) اگر مالک تأیید کند هیچ نصب مشتری داده‌ای در `trade.*` ندارد (Trade با `DenyAllTradeModuleEntitlement` fail-closed و Migration آن opt-in با `--include-trade` است)، T1 پیش‌شرط «جدول‌ها خالی‌اند، وگرنه توقف» می‌گیرد و Backfill کلاً حذف می‌شود | الف: یک Migration بدون وابستگی Trade←Core ولی جدول staging و مرحلهٔ Host لازم. ب: ساده ولی دو Migration (هزینهٔ بیشتر؛ همین الگو در C1/C3 Core هم هست و Q-3 بند ۵ «تا حد امکان» است). ج: با D-090 ناسازگار. د: ساده‌ترین و کم‌ریسک‌ترین؛ نیازمند تأیید مالک دربارهٔ خالی‌بودن `trade.*` در همهٔ نصب‌ها (راستی‌آزمایی نشده) | **د در صورت تأیید خالی‌بودن، وگرنه الف** |
| **O-2** | جزئیات یکتایی هر فیلد (Code، IBAN، شماره حساب، GTIN، IRC) در سطح Company | همه Company-scoped؛ یا برخی (IBAN/GTIN/IRC) Tenant-scoped | Company-scoped با D-119 هم‌خوان؛ Tenant-scoped یکتایی بین شرکت‌ها را تحمیل می‌کند و مرز Company را سست می‌کند | همه Company-scoped |
| **O-3** | آیا تغییر `TradeAccessContext` به زوج (N-5a) در T1 تأیید می‌شود؟ | داخل T1؛ یا PR جدا قبل از T1 | PR جدا Context دو‌شناسه‌ای را قبل از Schema می‌گذارد ولی با DB تک‌ستونی نمی‌خواند (وضعیت میانی نامعتبر) | داخل T1 |
| **O-4** | سیاست رکوردهای بدون نگاشت و Companyهای غیرفعال | توقف کامل؛ یا نگاشت دستی در Manifest | توقف ایمن‌تر؛ دستی کند ولی کنترل‌شده | توقف + Manifest دستی |

غیرمسدودکننده: مشمول بودن Party/Product در `OriginFiscalPeriodId` (D-117 بند ۴؛ پیشنهاد غیرمشمول)، و رفتار نوشتن Party/Product در دورهٔ مالی بسته پس از Company-owned شدن (بدون تغییر نگه داشته شد).

## ۹. بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `git diff --check` | اجرا شد (نتیجه در گزارش نهایی PR) | فقط مستندات |
| تأیید نبود تغییر کد (`git diff --name-only` فقط `docs/`) | اجرا شد | هیچ `.cs`/Migration/Schema |
| `dotnet build/test` | **اجرا نشد** | تغییر فقط مستندات است؛ CI Pending |
| `python3 scripts/check_static_assets.py` | اجرا نشد | تغییر در دارایی وب نیست |

## ۱۰. امنیت، داده و سازگاری
- هیچ اثر کد/زمان اجرا بر امنیت، احراز هویت، مجوز، Entitlement یا داده/Migration ندارد؛ D-090 به‌عنوان قرارداد امنیتی آینده `Accepted` شد (پیاده‌سازی `NotImplemented`).
- Gate، `DenyAllTradeModuleEntitlement` و Fail-Closed دست‌نخورده‌اند.

## ۱۱. ریسک‌ها و کارهای باقی‌مانده
- بزرگ‌ترین ریسک: T1 و Backfill (G-1، G-4)؛ پیش‌شرط‌ها G0، G5.
- پس از Merge #780 (انجام‌شده): جملهٔ D-090 در `DATABASE.md` اصلاح شد. باقی‌مانده: افزودن ارجاع «هم‌راستا با D-120» به D-095 در PR جدا (D-091 اصلاح‌شدهٔ #780 خودش با D-120 هم‌جهت است).
- شمارهٔ D-118 متعلق به #780 (ادغام‌شده) است؛ شکاف شماره‌ای باقی نمانده.
- دستیابی به پنج مورد O-1..O-4 پیش از T1.
- Dependabot PRهای #774..#779 هنوز به‌دلیل نبود `MSSQL_SA_PASSWORD` در Dependabot secrets ناسبزند (نامرتبط با این کار).

## ۱۲. شناسه‌های Git
- Branch: `docs/trade-tenant-company-owner-decisions-2026-11-23`
- Commit: (پس از commit ثبت می‌شود)
- Pull Request: Draft (پس از ایجاد ثبت می‌شود)
