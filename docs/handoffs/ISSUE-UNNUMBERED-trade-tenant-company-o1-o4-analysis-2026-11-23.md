# Handoff — تحلیل O-1..O-4 و نقشهٔ اصلاح‌شدهٔ Core/Trade (فقط مستندات)

- **Issue:** UNNUMBERED (مرتبط با #397 S3-0، #784، #780)
- **تاریخ:** 2026-11-23
- **نویسنده:** Claude (Lead فنی Trade)
- **ماهیت:** بررسی، طراحی و مستندسازی؛ هیچ کد، Migration، Schema، DB، API، UI، Entity یا Gate تغییر نکرد.
- **وضعیت همهٔ پیشنهادهای این سند:** `Proposed` — هیچ‌کدام تصمیم مالک نیست و هیچ ADR تغییر نکرد. D-090 و D-119..D-121 همان‌طور که در PR #784 ثبت شده‌اند باقی می‌مانند.

## ۱. وضعیت واقعی مخزن و PRها (از GitHub، 2026-11-23)

| مورد | وضعیت واقعی | شاهد |
|---|---|---|
| `main` | `b458a09` (Vendor: export signed license envelopes)؛ پس از `950baa3` سه فایل `docs/` و کد Vendor تغییر کرد؛ به Trade/Tenant ربطی ندارد | `git log origin/main` |
| #784 | باز، **Draft**، قبل از این به‌روزرسانی `dirty` نسبت به main (فقط `WORK_LOG.md`)؛ اکنون main در شاخه ادغام شد و تداخل حل شد (هر دو ورودی حفظ شد) | `gh api pulls/784`، `git merge` |
| #780 | باز، **غیر Draft**، `mergeable_state=dirty` (پایه `62b0c3c`، عقب‌تر از main)؛ تمام Jobهای CI روی head آن `success` است | `gh api pulls/780`، check-runs |
| تداخل #780 با #784 | `DECISIONS.md`، `INDEX.md`، `WORK_LOG.md` (متنی؛ هر دو سمت افزایشی‌اند) | `git merge-tree` |
| CI #784 | فقط CodeQL/Analyze اجرا شده و `success`؛ Job های `build-and-test` و `publish` برای PR فقط‌مستندات اجرا نشدند | check-runs |
| PRهای #774..#779 | دست‌نخورده | — |

### سازگاری #780 با #784
- **D-118** (هویت کاربر Tenant-scoped) شمارهٔ خود را حفظ می‌کند؛ D-119..D-121 با آن تداخل شماره ندارند.
- **D-091 در #780** (Rename فیزیکی، افزودن `TenantId` واقعی، `HasColumnName` راه‌حل نهایی نیست، `Down` پس از ورود داده جدید اطلاعات مالکیت را از بین می‌برد) **با D-120 هم‌جهت است** و تناقض ندارد؛ هر دو Rename ستون و افزودن `TenantId` واقعی را می‌گویند و ترتیب Migrationها را باز می‌گذارند.
- **D-095** در هیچ‌یک از دو PR تغییر نکرده. متن D-095 از ابتدا فرض می‌کند «ستون legacy به `CompanyId` تغییر نام می‌دهد» و فقط دربارهٔ **نام فیزیکی قیدها** (FK/AK/Index) تصمیم می‌خواهد؛ پس با D-120 در سطح ستون ناسازگار نیست. این خواندن، بررسی قبلی را اصلاح می‌کند که D-095 را در سطح ستون ناسازگار می‌دانست.
- D-118 بند ۸ (Tenant از Host canonical، D-103) قاعدهٔ «Tenant مشتق از Company باید با Tenant حاصل از Host/کاربر برابر باشد» را پشتیبانی می‌کند و آن را نقض نمی‌کند.
- **ترتیب پیشنهادی Merge:** ابتدا #780 (CI سبز، تصمیم D-118 مصوب مالک، فقط نیاز به به‌روزرسانی با main)، سپس هم‌ترازی #784 با آن. تصمیم Merge با مالک/Maintainer است؛ هیچ‌کدام در این مأموریت ادغام نشد و #780 تغییر نکرد.
- **کار بعد از Merge #780:** در `DATABASE.md` جملهٔ مربوط به D-090 با وضعیت `Accepted` هم‌تراز شود؛ سپس در D-091 و D-095 ارجاع «هم‌راستا با D-120» افزوده شود.

## ۲. O-1 — شواهد و نتیجهٔ فنی

### ۲.۱ شواهد دربارهٔ داده‌ی Trade (پاسخ به پنج پرسش مأموریت)

| پرسش | یافته | وضعیت |
|---|---|---|
| ۱. داده واقعی نصب‌های مشتری در دسترس است؟ | هیچ دسترسی به دیتابیس یا نصب مشتری در این جلسه نیست و مخزن هم فهرست نصب‌ها ندارد | **Unknown** |
| ۲. جداول Trade در نصبی داده دارند؟ | قابل اثبات نیست | **Unknown** |
| ۳. Migration اولیهٔ Trade اجرا شده؟ | کد: Migration `20261113072823_InitialTradeMasterData` از 2026-11-13 در `main` است و `Server.dll --migrate --include-trade` از 2026-11-14 (PR #665) وجود دارد؛ **فقط اپراتور** با این فلگ آن را اجرا می‌کند. `DatabaseSchemaMigrator` (مسیر Development/راه‌اندازی اولیه) فقط Core را Migrate می‌کند و Trade را نه. CI هم فقط `--migrate` (بدون Trade) می‌زند. اینکه اپراتوری آن را روی دیتابیس واقعی زده باشد، از سورس معلوم نیست | **Unknown** |
| ۴. شواهد قابل اتکا برای خالی‌بودن؟ | وجود ندارد. شواهد غیرمستقیم: (الف) در `Server/Program.cs` فقط `DenyAllTradeModuleEntitlement` ثبت است، پس Endpoint عملیاتی در تولید داده نمی‌نویسد؛ (ب) هیچ مسیر خودکار Seed یا Import در Trade نیست. اما SQL مستقیم، نسخه‌های قدیمی‌تر، محیط‌های Development/Staging و تست‌های دستی با Entitlement جعلی این استدلال را نمی‌شکنند ولی اثبات هم نمی‌کنند | **Unknown** |
| ۵. History/Deployment/Backup قابل استفاده؟ | `__TradeMigrationsHistory` و Backup روی دیتابیس مشتری‌اند و در دسترس نیستند. در مخزن Runbook یا لاگ استقرار مشتری نیست. جملهٔ «هنوز بهره‌برداری عملیاتی مشتری آغاز نشده» فقط در متن پیشنهادی D-091 در PR #780 (Merge نشده) آمده و مدرک اجرایی نیست | **Unknown** |

**نتیجه:** دربارهٔ خالی‌بودن جدول‌های `trade.*` در هیچ نصبی ادعایی نمی‌شود. گزینهٔ «د» (فرض خالی‌بودن) که در بازبینی قبلی پیشنهاد شده بود، **به‌عنوان مبنای طراحی کنار گذاشته می‌شود**؛ فقط به‌عنوان یک پیش‌شرط **بررسی‌شونده در زمان اجرا** (نه فرض) باقی می‌ماند.

### ۲.۲ ریشهٔ مسئله (از سورس)
- Trade هیچ ارجاع مستقیم (Project/FK/SQL) به Core ندارد (D-090).
- `DatabaseMigrationHost.RunAsync` همین حالا هر دو Context را در یک اجرا ترتیب می‌دهد: قفل `sp_getapplock` ← Core ← `trade.CheckPreconditionAsync` ← Trade. Host در لایهٔ Server است و مجاز به شناخت هر دو است.
- Migration در EF یک اسکریپت ایستا است و پارامتر زمان اجرا نمی‌گیرد؛ پس «Manifest به Migration داده شود» بدون جدول واسط عملاً شدنی نیست.

### ۲.۳ گزینه‌ها

| گزینه | شرح | نقاط ضعف | نتیجه |
|---|---|---|---|
| ج) خواندن `dbo.Companies` داخل Migration Trade | یک Migration با `UPDATE … FROM dbo.Companies` | وابستگی پنهان Schema از Trade به Core؛ ناسازگار با Q-4/D-090 | **رد** |
| الف) جدول Staging در schema `trade` | Host نگاشت را در `trade.<staging>` می‌نویسد، Migration مصرف و حذف می‌کند | جدول و Migration اضافی برای ساخت Staging (یا DDL خارج از History)؛ **منبع حقیقت دوم** برای نگاشت (D-092 فقط Manifest/نتیجهٔ تأییدشده را مرجع می‌داند)؛ نیاز به Hash/Integrity جدا | رد به‌عنوان پیش‌فرض |
| د) فرض خالی‌بودن | بدون Backfill | بدون شاهد؛ در صورت غلط‌بودن، Migration شکست می‌خورد یا (بدتر) داده بدون Tenant می‌ماند | رد به‌عنوان فرض |
| **ب′) Expand → Backfill اپراتوری → Contract** | Migration اول: Rename + افزودن `TenantId` nullable. فرمان اپراتوری Backfill در Host. Migration دوم: NOT NULL + AK/FK/Unique، با Guard توقف | دو Migration به‌جای یک | **پیشنهاد** |

### ۲.۴ پیشنهاد O-1: «ب′» (Host-orchestrated، بدون Staging، بدون اتکا به خالی‌بودن)
یک گزینهٔ مشخص (`Proposed`):

1. **منبع نگاشت = ستون `Companies.TenantId` در Core پس از C3** (که خودش از Manifest تأییدشدهٔ D-092 پر و تأیید شده است). نگاشت دوباره در Trade کپی یا نگهداری نمی‌شود؛ پس منبع حقیقت دوم و جدول Staging لازم نیست.
2. **Migration اول Trade (expand):** Rename `TenantId→CompanyId`، افزودن `TenantId` واقعی nullable؛ `Down` آن معکوس است.
3. **فرمان اپراتوری Backfill Trade** در Host (Server)، هم‌الگوی D-092: `--dry-run` پیش‌فرض، تراکنش Serializable، `sp_getapplock` همان منبع قفل، فقط SQL پارامتری:
   - برای هر ردیف `trade.*` (شامل `IsDeleted=1`)، `TenantId` را از `Companies.TenantId` ردیف `Id = CompanyId` می‌گذارد.
   - هیچ‌چیز حدس زده نمی‌شود؛ ردیفی که Company متناظر ندارد یا `Companies.TenantId` آن NULL است «بدون نگاشت» است.
   - اگر حتی یک ردیف بدون نگاشت بود: **هیچ تغییری اعمال نمی‌شود (all-or-nothing)**، گزارش (`CompanyId`، نام جدول، تعداد؛ بدون داده تجاری) چاپ می‌شود و کد خروج غیرصفر برمی‌گردد.
   - اجرای مجدد بدون ردیف NULL بی‌اثر است (`WHERE TenantId IS NULL`) → Idempotent.
4. **Migration دوم Trade (contract):** ابتدا Guard (`IF EXISTS (… TenantId IS NULL) THROW`)؛ سپس NOT NULL، AK `(TenantId, CompanyId, Id)`، FK مرکب فرزندان، Unique Index. اگر Guard بخورد، Migration شکست می‌خورد و Host کد خروج ۲ برمی‌گرداند؛ اپراتور Backfill را اجرا و `--migrate --include-trade` را دوباره می‌زند (EF از همان Migration ادامه می‌دهد).
5. **حالت جدول خالی خودکار پوشش داده می‌شود:** در جدول خالی Guard می‌گذرد و Backfill کاری ندارد؛ پس همان مسیر، بدون فرض خالی‌بودن و بدون شاخه جدا، کار می‌کند.

دلایل فنی: بدون جدول/سرویس/Workflow اضافی؛ Trade از Core مستقل می‌ماند (فقط Host هر دو را می‌شناسد، مثل امروز)؛ همان الگوی «A1 nullable → D1 Backfill (عملیات) → A2 NOT NULL» که برای Core در S3-0h آمده؛ رد شدن در Guard به‌جای ادامهٔ ناقص (Fail-Closed)؛ با SQL Server/EF فعلی سازگار است.

مصالحه: دو Migration در یک PR (T1). این با Q-3 بند ۵ («Migration غیرضروری») سازگار است چون Migration دوم دقیقاً Guard و قیود را حمل می‌کند و راهی برای یک Migration بدون وابستگی به Core یا فرض خالی‌بودن پیدا نشد. وضعیت میانی بین دو Migration فقط درون یک اجرای اپراتوری با نمونه‌های متوقف‌شده (D-096) دیده می‌شود و Trade در تولید `DenyAll` است.

### ۲.۵ تحلیل گزینهٔ Staging (در صورت علاقهٔ مالک)
اگر مالک بخواهد Staging با وجود هزینه‌اش انتخاب شود، حداقل شرایط آن:
- **محل:** جدول در schema `trade` که فقط توسط یک Migration EF ساخته و در Migration بعدی حذف شود (نه DDL آزاد خارج از History).
- **ساختار حداقل:** `(CompanyId uniqueidentifier PK, TenantId uniqueidentifier NOT NULL, SourceHash char(64))`.
- **ثبت:** فقط Host می‌نویسد، از `Companies.TenantId` تأییدشده؛ `PK` تکرار را رد می‌کند؛ نگاشت متناقض (یک Company با دو Tenant) غیرممکن است.
- **تأیید Manifest:** Hash کل نگاشت با Hash Manifest مقایسه و در گزارش چاپ شود؛ ناهمخوانی ⇒ توقف.
- **Migration:** به Staging فقط از طریق SQL داخل `trade` دسترسی دارد (بدون وابستگی به Core).
- **Transaction/Rollback و Idempotency:** Backfill داخل Migration، با تراکنش خود EF؛ شکست ⇒ Rollback؛ اجرای مجدد پس از پر کردن Staging.
- **هزینهٔ اضافه نسبت به ب′:** حداقل یک Migration ساخت Staging، یک مرحلهٔ Host برای پر کردن، منبع حقیقت دوم و آزمون‌های همگام‌بودن. به همین دلیل پیشنهاد نمی‌شود.

### ۲.۶ ریسک Rollback گزینهٔ پیشنهادی (صادقانه)
- `Down` Migration دوم قیود را برمی‌گرداند و NOT NULL را برمی‌دارد؛ `Down` Migration اول `TenantId` واقعی را حذف و نام ستون را برمی‌گرداند. **`Down` به‌تنهایی ایمن تلقی نمی‌شود**: ردیف‌هایی که پس از Cutover نوشته شده‌اند، `TenantId` خود را از دست می‌دهند (قابل بازسازی از `Companies.TenantId`، مشروط بر اینکه Core جلوتر Rollback نشده باشد) و کد باید نسخهٔ قدیمی شود.
- ترتیب Rollback باید عکس ترتیب اجرا باشد (Trade قبل از Core).
- Backup قبل از اجرا (G5) و اثبات Up/Down روی SQL Server (G3) پیش‌شرط قطعی‌اند.

## ۳. O-2 — Unique Index

### ۳.۱ استخراج از سورس (`Modules/Trade/Trade.Infrastructure/Persistence/Configurations`)

| فیلد | جدول | Unique/Index فعلی (نام) | دامنهٔ فعلی (ستون کلید) | وضعیت فیزیکی |
|---|---|---|---|---|
| `Code` | `Parties` | `UX_Parties_Tenant_Code_Active` روی `(TenantId, Code)`، `[IsDeleted]=0` | شرکت (مقدار legacy = `Company.Id`) | فیزیکی |
| `(PartyId, Role)` | `PartyRoleAssignments` | `UX_PartyRoleAssignments_Tenant_Party_Role_Active` | شرکت | فیزیکی |
| `Code` | `Products` | `UX_Products_Tenant_Code_Active` | شرکت | فیزیکی |
| `Iban` | `PartyBankAccounts` | `UX_PartyBankAccounts_Tenant_Iban_Active` `(TenantId, Iban)`، `[IsDeleted]=0 AND [Iban] IS NOT NULL` | شرکت، **بین همهٔ Partyهای آن شرکت** | `ExcludeFromMigrations` |
| `AccountNumber` | `PartyBankAccounts` | `UX_PartyBankAccounts_Tenant_Party_Account_Active` `(TenantId, PartyId, AccountNumber)` | شرکت + همان Party | `ExcludeFromMigrations` |
| `Gtin` | `ProductIdentifiers` | `UX_ProductIdentifiers_Tenant_GTIN_Active` `(TenantId, Gtin)`، `NOT NULL` | شرکت | `ExcludeFromMigrations` |
| `Irc` | `ProductIdentifiers` | `UX_ProductIdentifiers_Tenant_IRC_Active` `(TenantId, Irc)`، `NOT NULL` | شرکت | `ExcludeFromMigrations` |
| `SupplierPartyId` | `ProductIdentifiers` | Unique ندارد؛ FK مرکب `(TenantId, SupplierPartyId)→Parties` | شرکت | `ExcludeFromMigrations` |

**نکتهٔ مهم:** چون ستون legacy `TenantId` امروز `Company.Id` است، **همهٔ این یکتایی‌ها همین حالا عملاً Company-scoped هستند**. پس «Company-scoped» برای اکثر فیلدها به‌معنی تغییر رفتار نیست، فقط اصلاح نام/معنا است؛ این تغییر برای داده‌های موجود **اثر داده‌ای ندارد** (مقادیر یکتا همان می‌مانند).

### ۳.۲ پیشنهاد (`Proposed`) و دلیل تجاری هر فیلد

| فیلد | دامنهٔ پیشنهادی | تکرار در دو Company یک Tenant | دلیل |
|---|---|---|---|
| `Party.Code`، `Product.Code` | Company | مجاز | کدگذاری داخلی هر شرکت است و Q-1 داده را مستقل می‌داند |
| `PartyRoleAssignment (Party, Role)` | Company (عملاً درون Party) | مجاز | وابسته به Party همان شرکت |
| `IBAN` | Company | **مجاز** | یک تأمین‌کنندهٔ مشترک را هر شرکت به‌صورت Party جدا ثبت می‌کند (Q-1)؛ Tenant-scoped بودن شرکت دوم را از ثبت همان IBAN باز می‌دارد و **وجود IBAN در شرکت دیگر را فاش می‌کند** (نشت اطلاعات بین Company) |
| `AccountNumber` | Company + Party | مجاز | شماره حساب فقط درون همان Party معنی دارد |
| `GTIN` | Company | مجاز | یک کالا را چند شرکت یک گروه می‌فروشند؛ شناسهٔ کالا جهانی است، رکورد کاتالوگ نه (Q-1: بدون Shared Catalog) |
| `IRC` | Company | مجاز | همان استدلال GTIN؛ به‌علاوه IRC شناسهٔ منبع ثبت است و بین دو شرکت مستقل می‌تواند مشترک باشد |
| `SupplierPartyId` | بدون Unique؛ FK مرکب شامل `TenantId` و `CompanyId` | — | باید به Party **همان Company** اشاره کند (Q-1 بند ۵) |

**فرمول پیاده‌سازی (کمترین پیچیدگی):** کلید هر Unique Index فقط `CompanyId` را (نام جدید ستون پس از Rename) شامل شود؛ افزودن `TenantId` به Index زائد است، چون هر Company دقیقاً به یک Tenant تعلق دارد. بنابراین Unique Indexها با Rename ستون بدون تغییر معنایی به‌روز می‌شوند و Migration فقط `RenameIndex` (در صورت تغییر نام) می‌خواهد که Guard فعلی آن را پشتیبانی می‌کند. AK و FK چون نام قید دارند، موضوع D-095 هستند.

**یافتهٔ کد که باید در T1 رعایت شود:** `PartyStore`/`ProductStore` خطای Unique را با **نام Index** (`UX_Parties_Tenant_Code_Active` و …) تشخیص می‌دهند. هر تغییر نام Index باید هم‌زمان با Store و تست‌ها انجام شود، وگرنه خطای یکتایی به `500` تبدیل می‌شود.

مالک باید فقط تأیید کند: «همهٔ فیلدها Company-scoped، IBAN/GTIN/IRC هم» (پیشنهاد). اگر برای فیلدی (مثلاً IRC) نیاز تجاری Tenant-level دارد باید صریحاً اعلام شود؛ در این صورت آن مورد به Shared Catalog/Tenant-level data نزدیک می‌شود که Q-1 آن را رد کرده است.

## ۴. O-3 — قرارداد Context (`Proposed`؛ ناشی از Q-4، نه تصمیم مستقل)

### ۴.۱ بررسی سورس
- `TradeAccessContext(UserId, TenantId, FiscalPeriodId, AllowsWrites)` و `TradePeriodScope(TenantId, FiscalPeriodId)` هر دو تک‌شناسه‌اند؛ `TenantId` در آن‌ها امروز مقدار `Company.Id` است.
- مصرف‌کنندگان: `TradeAccessGate` (۲ ارجاع)، `PartyService` (۶)، `ProductService` (۶)، `PartyStore` (۸)، `ProductStore` (۴)، Configurationها، `TradeAuthorizationAdapter` و دو Endpoint (`TradePartyEndpoints`، `TradeProductEndpoints`)؛ ۶ فایل تست مستقیماً `TradeAccessContext/TradePeriodScope` را می‌سازند.
- Endpointها Context را فقط از `HttpContext.Items[AccessContextItemKey]` می‌خوانند؛ DTOها فیلد Tenant/Company ندارند.

### ۴.۲ نتیجه
تغییر `TradeAccessContext` به زوج **ساده‌ترین مسیر** است و قرارداد موجود دیگری این کار را نمی‌کند: Gate و Store فقط از همین نوع می‌خوانند و Adapter تنها نقطهٔ ساخت آن است. چون Schema تک‌ستونی با Context دوشناسه‌ای نمی‌خواند، این تغییر باید **داخل T1** (هم‌زمان با Migration) باشد؛ جدا کردن آن یک وضعیت میانی نامعتبر می‌سازد. **این یک تصمیم مسدودکننده نیست؛ لازمهٔ اجرای Q-4 است.** پیشنهاد: O-3 از فهرست تصمیم‌های باز مالک خارج و به‌عنوان جزء T1 ثبت شود (N-5b، یعنی `TradePeriodScope` و سیاست دوره، همچنان باز است).

### ۴.۳ قرارداد پیشنهادی
1. Host (Adapter) زوج را فقط از `WorkContext` سمت سرور و رابطهٔ DB `Company→Tenant` (پس از C4) می‌سازد؛ کوکی و Claim مرجع Tenant نیستند.
2. **سازگاری:** Tenant مشتق از Company باید با Tenant حاصل از Host canonical (D-103)/کاربر (D-118) برابر باشد؛ عدم تطابق ⇒ `null` Context، Fail-Closed و Audit.
3. زوج در یک مرحله ساخته می‌شود تا زوج ناسازگار ساخته نشود.
4. در Trade: هر Query و Insert هر دو شناسه را اعمال می‌کند؛ AK `(TenantId, CompanyId, Id)` و FK مرکب فرزندان.
5. **محدودیت شناخته‌شده:** FK/AK داخل Trade زوج ناسازگار در **ردیف ریشه** (`Party`/`Product`) را نمی‌گیرد؛ تنها کنترل آن Host و Store است (ثبت‌شده در `TRADE_MODULE.md`).
6. `DenyAllTradeModuleEntitlement` و همهٔ Gateها دست‌نخورده می‌مانند.

## ۵. O-4 — رکوردهای بدون نگاشت (سیاست `Proposed`)

مطابق سیاست پایهٔ مأموریت و در هم‌خوانی با ب′:
- **هیچ نگاشت حدسی:** فقط `Companies.TenantId` تأییدشدهٔ Core.
- **هیچ رکوردی بی‌صدا حذف/نادیده گرفته نمی‌شود:** رکورد بدون نگاشت (شامل `IsDeleted=1`، Company غیرموجود در Core یا Company با `TenantId` NULL) کل Backfill را متوقف می‌کند.
- **توقف دوجانبه:** (۱) فرمان Backfill با کد خروج غیرصفر؛ (۲) Guard Migration دوم که حتی در صورت اجرای مستقیم `--migrate` بدون Backfill، شکست می‌خورد.
- **گزارش قابل اصلاح:** فهرست `(CompanyId، جدول، تعداد)` بدون محتوای تجاری و بدون Secret.
- **اصلاح و اجرای مجدد:** اپراتور Company/Manifest را در Core اصلاح می‌کند یا (تصمیم جدا و باز) ردیف یتیم را مدیریت می‌کند؛ سپس Backfill را دوباره می‌زند (Idempotent).
- **سیاست حذف/غیرفعال‌سازی ردیف یتیم باز می‌ماند** و در این سند تصمیم گرفته نمی‌شود؛ سامانهٔ Workflow ساخته نمی‌شود.

## ۶. نقشهٔ اصلاح‌شدهٔ PRها (`Proposed`)

الگوی واحد برای Core و Trade: **expand (Migration) → Backfill (فرمان اپراتوری، نه Migration) → contract (Migration با Guard)**. ترتیب: `P1` موازی → `C1 → C2 → C3 → C4 → T1 → T2`.

| PR | Scope دقیق | فایل‌های درگیر | وابستگی | Migration | تست‌های لازم | خطر داده | Rollback | معیار پذیرش |
|---|---|---|---|---|---|---|---|---|
| **P1** | تست‌های قفل‌کننده (بدون Schema): ممنوعیت `HasColumnName("TenantId")`، تست Cross-Tenant با Fakeها | `tests/**` | ندارد | ندارد | Unit/Model | ندارد | حذف تست | فقط تست؛ هیچ Product کد تغییر نمی‌کند |
| **C1** (Core، expand) | Entity و جدول `Tenants`؛ `Companies.TenantId` nullable + FK + Index (بدون AK و بدون NOT NULL) | `Domain`، `Infrastructure/Persistence`، Migration Core | Issue/PR با تأیید Schema، G3 | **دارد** (Core) | Migration Up/Down روی SQL Server، Model | کم (ستون nullable جدید) | `Down` حذف ستون/جدول | هیچ Company بدون Tenant مجبور نمی‌شود؛ رفتار فعلی بدون تغییر |
| **C2** (Core، Backfill) | فرمان اپراتوری Manifest/dry-run (D-092): Tenant اپراتوری، نگاشت، Hash، توقف روی ناقص/اضافه/ناسازگار؛ **بدون Migration** | `Server/Infrastructure/Hosting` (فرمان)، تست | C1، G5 | ندارد | Integration SQL Server (dry-run، توقف، تکرار) | **متوسط** (نگاشت اشتباه = نشت دائمی؛ ازین‌رو dry-run و تأیید Hash) | Backup/بازگردانی؛ فرمان معکوس ندارد | نگاشت کامل؛ dry-run بدون نوشتن؛ اجرای مجدد بی‌اثر |
| **C3** (Core، contract) | `Companies.TenantId` NOT NULL + AK روی `Companies` (و FK مرکب مورد نیاز Core) با Guard | Migration Core | C2 تأییدشده | **دارد** | Up/Down، Guard با ردیف NULL | متوسط | `Down` برداشتن NOT NULL | هیچ Company بدون Tenant؛ Guard رد می‌کند |
| **C4** (Host) | `WorkContext` علاوه بر Company، Tenant را از DB resolve و با Host/کاربر تطبیق می‌دهد؛ ناسازگار ⇒ Fail-Closed + Audit | `Server/Infrastructure/Tenancy/*` | C3؛ D-103/D-118 برای منبع Host | ندارد | Forged Context، Cross-Tenant (Tenant-A/B، Company-A/B/C)، عضویت لغوشده | **کم** | بازگرداندن کد | زوج فقط از DB؛ عدم تطابق رد می‌شود |
| **T1** (Trade، اتمیک) | Migration expand (Rename + `TenantId` nullable)؛ فرمان Backfill Trade؛ Migration contract (Guard + NOT NULL + AK `(TenantId, CompanyId, Id)` + FK مرکب + Unique Company-scoped)؛ Entity/Config/Store/Service/تست؛ `TradeAccessContext`/`TradePeriodScope` دوشناسه‌ای؛ به‌روزرسانی نام Indexها و Storeها هم‌زمان | `Modules/Trade/**`، `Server/Api/Trade/*`، `TradeAuthorizationAdapter`، `Hosting` | C4؛ O-2 تأیید؛ N-5b تعیین‌تکلیف؛ D-095 برای نام قیدها؛ G0/G3/G5/G6 | **دارد** (دو Migration در یک PR) | Up/Down روی SQL Server؛ Backfill (dry-run، توقف روی رکورد بدون نگاشت، تکرار، همزمانی)؛ FK مرکب (زوج ناسازگار رد)؛ Isolation دو Company/دو Tenant؛ Forged Context | **بالا** (Rename + Backfill) | Backup الزامی؛ `Down` ساختار را برمی‌گرداند ولی `TenantId` ردیف‌های جدید را از دست می‌دهد؛ پس از Cutover Roll-forward | Up/Down اثبات‌شده؛ همهٔ آزمون‌های Isolation سبز؛ بدون `HasColumnName`؛ Gate/DenyAll دست‌نخورده |
| **T2** (Trade) | ساخت `PartyBankAccounts` و `ProductIdentifiers` مستقیماً با شکل نهایی (دو شناسه، AK/FK مرکب، Unique Company-scoped)؛ نیاز به Rename ندارند | `Modules/Trade/**`، Migration Trade | T1 | **دارد** (ایجاد جدول) | Up/Down، FK مرکب Supplier هم‌شرکت | کم (جدول جدید و خالی) | `Down` = حذف جدول (فقط پیش از ورود داده) | اعتبارسنجی Supplier هم‌Company در DB و Store |

### بازبینی نقشه نسبت به نسخهٔ قبل
- اصلاح C2/C3: «Backfill» از C3 به **C2 (فرمان اپراتوری، بدون Migration)** رفت و C3 فقط contract است؛ یکدست‌سازی با الگوی Trade و با A1/D1/A2 در S3-0h.
- T1 همچنان اتمیک است (Migration + کد + تست)، ولی با دو Migration و بدون Staging.
- N-5a (زوج در Context) از «تصمیم مسدودکننده» به «جزء T1» تبدیل شد.
- ۱۰ ستون legacy دیگر Core (غیر از Trade) خارج از این نقشه‌اند و باید با همان الگو در طرح جدا (S3-0h) پوشش داده شوند.

## ۷. پیش‌شرط‌های حل‌نشده

| گیت | وضعیت | چه لازم است |
|---|---|---|
| G0 | باز | تأیید مالک S3-0h و تعیین‌تکلیف OD-6/OD-6a/OD-13/OD-17 |
| G3 | باز | اجرای Up/Down، FK مرکب با NULL، Guard، تکرار، همزمانی روی SQL Server واقعی/آزمایشی |
| G5 | باز | Backup + رویهٔ بازیابی + مجوز جدا برای هر نصب هدف |
| G6 | باز | SP-1..SP-9 پیش از Tenant دوم |
| D-091 | Proposed | تصویب ترتیب اجرایی (جهت نام‌گذاری تأیید شده) |
| D-092 | Proposed | مرجعیت Manifest |
| D-095 | Proposed | نام فیزیکی قیدها (AK/FK؛ Index با `RenameIndex` شدنی است) |
| N-5b | باز | `TradePeriodScope` و سیاست دورهٔ مالی |
| سیاست حذف/غیرفعال‌سازی رکورد یتیم | باز | تصمیم مالک |

## ۸. ریسک‌های امنیتی و داده‌ای
- **زوج ناسازگار در ردیف ریشه** را FK/AK مرکب نمی‌گیرد؛ دفاع در عمق Host + Store + تست الزامی است.
- **نگاشت اشتباه Company→Tenant** نشت دائمی ایجاد می‌کند؛ پس dry-run، Hash و توقف.
- **Backfill روی ردیف‌های `IsDeleted=1`** هم باید اجرا شود وگرنه NOT NULL شکست می‌خورد.
- **جابه‌جایی نام Index بدون تغییر Store** باعث `500` به‌جای `409` می‌شود (نام Index در Store کدگذاری شده است).
- **Down پس از ورود داده** اطلاعات `TenantId` ردیف‌های جدید را از بین می‌برد؛ Backup و Roll-forward.
- **کنترل‌های موجود دست‌نخورده‌اند:** `DenyAllTradeModuleEntitlement`، `TradeAccessGate`، Anti-forgery، Cookie.

## ۹. تست‌ها
- **اجرا شد:** `git diff --check` و بررسی نبود Marker تداخل پس از ادغام main.
- **اجرا نشد:** `dotnet restore/build/test`، `check_static_assets.py`، `npm`، تست‌های SQL Server (تغییر فقط مستندات است؛ هیچ داده یا نصب مشتری خوانده نشد).
- **CI:** برای این Commit در انتظار (Pending).
