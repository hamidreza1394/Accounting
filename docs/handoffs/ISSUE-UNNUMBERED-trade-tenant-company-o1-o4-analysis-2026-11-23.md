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

> **اصلاح (مأموریت تکمیلی، §۱۰):** در بند ۳ بالا، «تراکنش Serializable + قفل» توقف نویسنده‌ها را تضمین نمی‌کند و Backfill باید **همهٔ ردیف‌ها (نه فقط NULL)** را راستی‌آزمایی کند. هرجا این بخش با §۱۰ فرق دارد، §۱۰ مقدم است.

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

## ۱۰. اصلاحیهٔ مأموریت تکمیلی: هم‌زمانی Backfill، Transaction، Manifest و نقشه (2026-11-23)

> وضعیت همهٔ بندهای این بخش: `Proposed`. هیچ ADR تغییر نکرد و هیچ کد، Migration، SQL اجرایی یا Backfill ساخته/اجرا نشد.

### ۱۰.۱ وضعیت واقعی (دوباره از GitHub)
- `main` اکنون `3c0a61e` است (PR #782: فقط مستندات Vendor/Trade-entitlement).
- #780: باز، غیر Draft، `dirty` (پایه `62b0c3c`، عقب‌تر از main)، هر ۱۵ Check روی head آن `success`. فقط تعارض‌ها گزارش شد؛ تغییر یا Merge نشد.
- #784: Draft، `dirty` نسبت به main جدید (فقط `WORK_LOG.md`/`INDEX.md` افزایشی). Checkهای کد (`build-and-test`، `publish`، CodeQL، Semgrep، `secret-scan`، `client`) `success` بودند؛ `e2e` و `publish-windows` هنگام بررسی هنوز `in_progress` بودند.
- تازه: #782 نشان می‌دهد چرخهٔ لایسنس Staging با `TRADE_IMPORT` در حال آماده‌سازی است، ولی Runtime entitlement در Trade `NotImplemented` و `DenyAllTradeModuleEntitlement` دست‌نخورده است. این یعنی پنجرهٔ «جدول‌های Trade خالی‌اند» ممکن است زودتر بسته شود؛ طرح باید به خالی‌بودن تکیه نکند (همان‌طور که هست).

### ۱۰.۲ ترتیب واقعی Migrationها و Transaction

**از سورس (تأییدشده):**
- `DatabaseMigrationHost.RunAsync`: قفل `sp_getapplock` ← Core (`MigrateAsync`) ← اگر `--include-trade`: `trade.CheckPreconditionAsync` ← Trade (`MigrateAsync`). اگر Core شکست بخورد Trade شروع نمی‌شود (کد ۱)؛ شکست Trade کد ۲، قفل کد ۳، وضعیت ناسازگار Trade کد ۴.
- `RunStepAsync` فقط `GetPendingMigrationsAsync` و `Database.MigrateAsync()` را صدا می‌زند؛ **Migration هدف (Target) ندارد**. پس وقتی Expand و Contract هر دو pending باشند، یک اجرا هر دو را پشت‌سرهم اعمال می‌کند و روی Guard می‌ایستد. پیام شکست از قبل می‌گوید «دوباره اجرا کن تا از مرحلهٔ شکست‌خورده ادامه یابد».
- هشدار «Migration Trade در انتظار» هنگام شروع برنامه وجود ندارد (فقط Core؛ `DATABASE.md`)؛ یعنی برنامه‌ای که پس از Expand و پیش از Contract بالا بیاید، خودش هشداری نمی‌دهد.
- Python Guard (`verify_migration_model.py`، `ALLOWED_RENAMES` خالی) فقط Migrationهای Core را می‌سنجد؛ در آن اسکریپت‌ها چیزی دربارهٔ Migrationهای Trade نیست (grep). پس Rename در Trade هیچ Guard ماشینی ندارد.

**Unknown (مستند رسمی EF/SQL Server در این جلسه دریافت نشد، چون درخواست تأیید WebFetch بی‌پاسخ ماند؛ Docker Daemon و SQL Server هم در این محیط نیست):**
- اینکه EF Core 10 هر Migration را در Transaction جدا اجرا می‌کند و ردیف History همان Transaction را دارد.
- اینکه `THROW` داخل `migrationBuilder.Sql` کل Migration دوم را Rollback می‌کند و ردیف History نمی‌ماند.
- سازوکار قفل داخلی `MigrateAsync` در EF 10 و اینکه با قفل Session خود Host تداخل ندارد.
- اینکه Rename + افزودن ستون هم‌نام (`TenantId`) در یک Migration بدون تعارض اجرا می‌شود (سابقهٔ خطای 1913 در D-095).

**وضعیت مورد انتظار (فرضیه، نه اثبات):** پس از موفقیت Expand و شکست Guard در Contract: ستون `CompanyId` و `TenantId` (nullable) وجود دارد، History فقط ردیف Expand را دارد، Contract ثبت نشده. اجرای فرمان Backfill و سپس `--migrate --include-trade` فقط Contract را pending می‌بیند و ادامه می‌دهد. **اثبات این رفتار شرط G3 است** و باید با تست SQL Server واقعی (موارد در §۱۰.۸) انجام شود؛ تا آن زمان Unknown.

### ۱۰.۳ قفل و نویسنده‌های هم‌زمان

| موضوع | یافته (از سورس) |
|---|---|
| قفل | `sp_getapplock`، Resource = `Farano.DatabaseMigration:<InitialCatalog>`، `LockOwner='Session'`، `LockTimeout=0`، روی اتصال اختصاصی بدون Pool |
| عمر قفل | از ورود به `RunAsync` تا `sp_releaseapplock`/بسته‌شدن اتصال؛ فقط همان فرمان |
| قفل فرمان مستقل | ممکن است (Resource ثابت و قابل ساخت)، ولی توابع قفل در `DatabaseMigrationHost` `private` هستند؛ فرمان Backfill یا باید همان منطق را بازاستفاده/استخراج کند یا بخشی از Host باشد. امروز قابل‌استفاده مستقیم نیست |
| چه کسانی از قفل پیروی می‌کنند | **فقط** کدی که خودش `sp_getapplock` بگیرد (Migration Host). API، Store، Job و SQL مستقیم پیروی نمی‌کنند؛ قفل Advisory است |
| مسیرهای نوشتن Trade | API (`TradePartyEndpoints`/`TradeProductEndpoints` ← Service ← Store ← `SaveChangesAsync`)؛ Job: ندارد (`BackgroundService`/`IHostedService`/`ExecuteSql`/`FromSql`/`ExecuteUpdate` در Trade و `Server/Api/Trade` یافت نشد)؛ Import/Export: پیاده نشده (طراحی)؛ SQL مستقیم: غیرقابل کنترل. Migration Core فقط `PermissionDefinitions` را Seed می‌کند، نه جدول‌های `trade` |
| نمونهٔ قدیمی فعال هنگام Backfill | (استنباط از سورس؛ نیازمند اثبات SQL Server) پس از Expand، INSERT قدیمی ستون `CompanyId` (NOT NULL، بدون Default) را نمی‌فرستد ⇒ شکست؛ SELECT/UPDATE قدیمی با `TenantId=@companyId` روی ستون جدید می‌خورد که NULL یا Tenant واقعی است ⇒ صفر ردیف. **ولی** اگر شناسهٔ یک Tenant برابر شناسهٔ یک Company باشد، نمونهٔ قدیمی همان Company را «می‌بیند» و می‌تواند تغییر دهد |
| درج رکورد بین Expand و Backfill | کد جدید همیشه هر دو شناسه را از Context می‌گذارد (غیر-NULL). کد قدیمی شکست می‌خورد. فقط SQL مستقیم می‌تواند ردیف با `TenantId` NULL یا **زوج نادرست غیر-NULL** بسازد. پس Backfill که فقط NULLها را پر کند کافی نیست |
| آیا Serializable جلوی نویسنده‌ها را می‌گیرد؟ | **خیر.** (از دانش SQL Server؛ مستند رسمی در این جلسه بازیابی نشد) Serializable فقط روی دادهٔ خوانده/نوشته‌شدهٔ همان Transaction و فقط تا Commit قفل Range می‌گذارد. پس از Commit هیچ‌چیز مانع درج ردیف تازه نیست و با Migration Contract (Transaction جدا) هماهنگ نمی‌شود. `sp_getapplock` هم Advisory است. هیچ‌کدام معادل توقف نویسنده‌ها نیست |

**راهکار کم‌هزینهٔ پیشنهادی (چهار لایه، بدون سامانهٔ جدید):**
1. **Maintenance Window صریح از قبل از Backfill تا پایان Contract:** توقف همهٔ نمونه‌های برنامه (همهٔ نسخه‌ها) برای آن دیتابیس (D-096) و ممنوعیت SQL دستی؛ در Runbook اپراتور. ممنوعیت موقت عملیات Trade با `DenyAll` فعلی کافی تلقی نمی‌شود چون وابسته به محیط است.
2. **حصار دادهٔ داخل Transaction Backfill:** `TABLOCKX, HOLDLOCK` روی سه جدول `trade` و `LOCK_TIMEOUT` کوتاه؛ هر نویسنده/خواننده‌ای که همان لحظه به این جدول‌ها دست بزند منتظر می‌ماند یا فرمان بدون تغییر شکست می‌خورد. این به همکاری برنامه وابسته نیست، ولی فقط تا Commit دوام دارد.
3. **راستی‌آزمایی کامل پیش از Contract:** Backfill/Verify همهٔ ردیف‌ها را می‌سنجد (NULL ⇒ پر می‌شود؛ غیر-NULL ⇒ باید برابر نگاشت باشد، وگرنه توقف).
4. **Contract خودش دوباره سنجش می‌کند:** `ALTER COLUMN … NOT NULL` (Sch-M lock) در SQL Server با وجود NULL شکست می‌خورد؛ Guard صریح فقط پیام روشن‌تر می‌دهد. شکاف باقی‌مانده: ردیف با زوج نادرست غیر-NULL که بین Verify و Contract درج شود؛ فقط Maintenance Window آن را می‌بندد.

### ۱۰.۴ قرارداد فرمان Backfill Trade

| کنترل | ارزیابی | توضیح |
|---|---|---|
| پیش‌فرض Dry-run و `--apply` صریح | **لازم** | Dry-run همان Verify است (یک مسیر کد، بدون نوشتن) |
| بررسی وضعیت Migrationها پیش از اجرا | **لازم** | Core: Migration C3 اعمال‌شده (`Companies.TenantId` NOT NULL). Trade: Expand اعمال‌شده و Contract **اعمال‌نشده**؛ وگرنه توقف با کد مخصوص. از `GetAppliedMigrations` با نام Migration |
| کنترل کامل نگاشت Company→Tenant | **لازم** | هر `CompanyId` موجود در `trade.*` باید در `Companies` باشد، `TenantId` آن غیر-NULL و Tenant موجود باشد |
| پردازش همهٔ ردیف‌ها (شامل `IsDeleted=1`) | **لازم** | وگرنه NOT NULL شکست می‌خورد |
| All-or-nothing | **لازم** | یک Transaction؛ هر ردیف بدون نگاشت یا زوج ناسازگار ⇒ Rollback کامل |
| SQL پارامتری | **لازم** | نام جدول‌ها ثابت (Const)، هیچ ورودی کاربر الحاق نمی‌شود |
| Idempotency | **لازم** | پر کردن فقط `TenantId IS NULL`؛ سپس Verify همهٔ ردیف‌ها؛ اجرای دوم بدون تغییر |
| گزارش بدون داده حساس | **لازم** | فقط نام جدول، شناسهٔ `CompanyId` و تعداد؛ بدون نام/کد/IBAN/GTIN |
| کد خروج غیرصفر | **لازم** | کدهای جدید جدا برای «وضعیت Schema نامعتبر» و «ردیف بدون نگاشت/ناسازگار» (در کنار ۰..۴ موجود) |
| جلوگیری از اجرای هم‌زمان | **لازم** | همان Resource قفل Host؛ کد ۳ |
| حصار `TABLOCKX` | **لازم** | بند ۲ در §۱۰.۳ |
| جلوگیری از اجرا در «نسخهٔ نامعتبر برنامه» | **غیرعملی در کد** | فرمان نسخهٔ نمونه‌های دیگر را نمی‌داند؛ از Maintenance Window و Runbook می‌آید، نه از کد |
| ثبت رویداد Audit در `SecurityAuditEvents` | اختیاری | خط Log کافی است؛ Audit Core فقط اگر مالک بخواهد (D-092 برای Backfill Core آن را می‌خواهد) |
| بررسی Session دیگران با DMV | **غیرضروری** | نیازمند مجوز `VIEW SERVER STATE`؛ پیچیدگی بیشتر از ارزش |
| Batch/Resume، Staging، Hash، Workflow | **غیرضروری** | جدول‌های Master Data کوچک‌اند؛ **حجم واقعی Unknown** است؛ اگر بزرگ بود Batch با تصمیم جدا |

### ۱۰.۵ Manifest و Hash
- **Hash اثبات صحت نگاشت نیست.** فقط شناسایی و یکپارچگی فایل Manifest تأییدشده است؛ صحت از تأیید مالک، اعتبارسنجی پیش از نوشتن و بازبینی Dry-run می‌آید.
- **Trade به Manifest و Hash نیاز ندارد.** منبع نگاشت در Trade، `Companies.TenantId` تأییدشدهٔ Core است؛ مقایسهٔ Hash با Manifest قدیمی با Companyهایی که بعد از C2 ساخته شده‌اند Alarm کاذب می‌دهد. کنترل Trade همان راستی‌آزمایی ردیف‌به‌ردیف است.
- **فقط در C2 (Core) Hash لازم است**، با این حداقل:
  - **ورودی:** فهرست `(CompanyId, TenantId)`.
  - **Canonicalization:** Guid با فرمت `D` و حروف کوچک، مرتب‌شده صعودی بر `CompanyId`، JSON فشرده UTF-8 بدون BOM با LF، فیلد `manifestVersion` (عدد صحیح) داخل محتوا.
  - **SHA-256 مورد انتظار:** پیش‌تر از فایل تأییدشدهٔ مالک (بایگانی G5) محاسبه و در رکورد تأیید ثبت می‌شود؛ اپراتور آن را با `--expected-sha256` می‌دهد و فرمان از روی فایل دوباره محاسبه و مقایسه می‌کند.
  - **تغییر Manifest پس از تأیید:** Hash نمی‌خواند ⇒ توقف؛ Manifest جدید یعنی تأیید جدید.
- **قواعد اعتبارسنجی C2 (پیش از نوشتن، در هر حال):** هر Company موجود دقیقاً یک‌بار؛ Company ناشناخته ⇒ توقف؛ Company بدون ردیف ⇒ توقف؛ Tenant بدون Company ⇒ توقف؛ Company که از قبل `TenantId` دیگری دارد ⇒ توقف (بدون بازتخصیص؛ D-103 بند ۱۱).
- **قاعدهٔ تازه R1:** شناسهٔ هر Tenant باید Guid تازه باشد و با هیچ `Company.Id` برابر نباشد (چون نمونهٔ قدیمی ستون را بازتفسیر می‌کند؛ §۱۰.۳).

### ۱۰.۶ نقشهٔ C1..T2 پس از تطبیق با سورس

| PR | اصلاح/تصریح |
|---|---|
| **C1** | `Tenants` + `Companies.TenantId` nullable + FK + Index؛ بدون AK و بدون NOT NULL. باید از Python Guard Core بگذرد (فهرست عملیات مجاز؛ Rename در C1 نیست) |
| **C2** | فرمان Manifest (§۱۰.۵) + قاعدهٔ R1؛ بدون Migration؛ فقط ستون‌های NULL را پر می‌کند |
| **C3** | `Companies.TenantId` NOT NULL + **AK `(TenantId, Id)` روی `Companies`**. Company→Tenant از سمت Company یک‌به‌یک (N:1) است چون یک ستون FK دارد؛ پس هیچ Company هم‌زمان به دو Tenant تعلق ندارد، و Manifest تکراری رد می‌شود. این AK فقط برای FKهای مرکب خودِ Core (مثل عضویت‌ها) است؛ **Trade به آن FK نمی‌گیرد** (D-090) |
| **تغییر ناپذیری `Companies.TenantId`** | D-103 بند ۱۱ انتقال Company بین Tenantها را فرآیند جدا می‌خواهد، ولی سازوکار جلوگیری از تغییر تصادفی (Trigger، قاعدهٔ برنامه یا مجوز UPDATE) تصمیم‌گرفته نشده است ⇒ **باز؛ در بخش ۱۰.۹** |
| **C4** | Tenant از همان Query که `FindAccessibleCompanyAsync` عضویت فعال را می‌سنجد، از ستون `Companies.TenantId` می‌آید و زوج در یک مرحله ساخته می‌شود؛ `TenantId` NULL، Tenant غایب یا معلق ⇒ `null` و Fail-Closed. **تطبیق با Host/کاربر امروز منبع دوم ندارد** (Host canonical D-103 و `Users.TenantId` D-118 پیاده نیستند)؛ پس معیار C4 «زوج از DB» است و تطبیق با Host/کاربر معیار PR پیاده‌سازی D-103/D-118 می‌شود و **پیش از Tenant دوم (G6) و پیش از هر فعال‌سازی Entitlement الزامی** است. توجه: دسترسی داده همچنان فقط با عضویت فعال ممکن است؛ تطبیق Tenant دفاع در عمق است (با FKهای مرکب D-118 حالت «کاربر Tenant A با عضویت در Company از Tenant B» اصلاً بازنمایی‌پذیر نمی‌شود) |
| **T1** | AK و FKهای دقیق: AK `(TenantId, CompanyId, Id)` روی `Parties` و `Products`؛ FK مرکب `(TenantId, CompanyId, PartyId)` در `PartyRoleAssignments`، `(TenantId, CompanyId, PartyId)` در `PartyBankAccounts`، `(TenantId, CompanyId, ProductId)` و `(TenantId, CompanyId, SupplierPartyId)→Parties` در `ProductIdentifiers`. `TradeEntity` (الان فقط `TenantId`) هر دو شناسه را می‌گیرد؛ همهٔ predicateها در `PartyStore` (۸ ارجاع)، `ProductStore` (۴)، `PartyService`/`ProductService` (۶+۶)، Configurationها و ۱۲ فایل تست هر دو را اعمال می‌کنند؛ Insert هر دو را از Context می‌گذارد. Migrationها باید **دست‌نویس Rename** باشند، نه Scaffold (Scaffold تغییر نام را Drop+Add می‌کند و داده را از بین می‌برد) و چون Python Guard Trade را نمی‌پوشاند، تست SQL Server باید بقای مقادیر پس از Rename را اثبات کند. نام Indexها: تا تصمیم D-095، یا نگه‌داشتن نام (Store سالم می‌ماند) یا تغییر هم‌زمان نام + ثابت‌های `ActiveCodeIndex`/`ActiveRoleIndex` + تست‌ها. **محدودیت واقعی ثبت‌شده:** زوج ناسازگار در ردیف ریشه `Party`/`Product` (مثلاً `(TenantB, CompanyA)`) را هیچ AK/FK نمی‌گیرد؛ فقط Host و Store |
| **T2** | بدون تغییر نسبت به §۶ |

**N-5b و T1:** مانع T1 نیست و از وابستگی‌های اجباری T1 خارج می‌شود. دلیل از سورس: `TradePeriodScope` فقط در `ITradeAuthorization.HasPermissionForAllCompanyPeriodsAsync` و Adapter استفاده می‌شود که طبق کامنت «تا مسیر چندشرکتیِ آگاه از entitlement وجود ندارد نباید از هیچ Endpoint بیرون برود»؛ Endpointهای فعلی فقط `TradeAccessContext` می‌گیرند. پس T1 فقط `TradePeriodScope.TenantId` را به `CompanyId` تغییر نام می‌دهد (مقدار آن امروز Company است؛ Tenant سمت سرور از رابطه می‌آید، نه از درخواست) بدون هیچ تغییر رفتار. **سیاست** دورهٔ مالی (`PeriodIndependent` و نوشتن در دورهٔ بسته) جدا و باز می‌ماند و فقط برای PR ای که مسیر گزارش گروهی را Expose کند یا سیاست دوره را عوض کند وابستگی است. (اگر مالک بخواهد T1 رفتار دوره را هم تغییر دهد، N-5b مانع می‌شود؛ این در پیشنهاد نیست.)

### ۱۰.۷ O-2 — دامنهٔ یکتایی هر مورد (Company-scoped حفظ شد؛ شاهد تجاری خلاف آن نیست)

| مورد | دامنه | یادداشت |
|---|---|---|
| `Party.Code` | Company | `(CompanyId, Code)` فعال |
| `Product.Code` | Company | `(CompanyId, Code)` فعال |
| `PartyRoleAssignment` | Company + Party + Role | `(CompanyId, PartyId, Role)` فعال؛ Party خودش Company را مشخص می‌کند |
| `IBAN` | Company، بین همهٔ Partyهای آن Company | یک IBAN در یک Company به یک Party؛ دو Company یک Tenant می‌توانند هر دو آن را داشته باشند |
| `AccountNumber` | **Party** (و در نتیجه Company) | `(CompanyId, PartyId, AccountNumber)` فعال؛ بین Partyهای مختلف تکرارشدنی؛ چون FK مرکب `PartyId` را به Company گره می‌زند، دامنهٔ Company ضمنی است |
| `GTIN` | Company | `(CompanyId, Gtin)` فعال، NOT NULL |
| `IRC` | Company | `(CompanyId, Irc)` فعال، NOT NULL |

هیچ Index، Store یا Migration تغییر نکرد. چون کلید فعلی در عمل Company است، تغییر دامنه برای داده‌های موجود اثری ندارد.

### ۱۰.۸ Rollback و استقرار (C1، C3، T1)

| مرحله | Up / Down | از دست رفتن داده | Rollback ناایمن می‌شود وقتی | Roll-forward بهتر است وقتی | سازگاری نسخه |
|---|---|---|---|---|---|
| **C1** | Up: جدول `Tenants`، ستون nullable، FK/Index؛ Down: حذف عکس | Down، ردیف‌های `Tenants` و مقادیر `Companies.TenantId` را حذف می‌کند | پس از اجرای C2 (نگاشت واقعی ثبت شده) | پس از C2 | نمونهٔ قدیمی با ستون nullable ناآشنا کار می‌کند (INSERT/SELECT صریح؛ بررسی SQL Server لازم) |
| **C3** | Up: NOT NULL + AK؛ Down: حذف AK و nullable | خود Down مقدار حذف نمی‌کند | پس از Cutover کد جدید؛ نمونه‌های قدیمی Company بدون `TenantId` می‌سازند و Contract بعدی شکست می‌خورد | پس از C4/T1 | **ناسازگار:** پس از NOT NULL، INSERT قدیمی Company شکست می‌خورد ⇒ نمونه‌های قدیمی باید پیش از C3 متوقف/تعویض شوند |
| **T1** | Up: Expand ← Backfill ← Contract؛ Down: Contract را برمی‌گرداند سپس Expand را (Rename معکوس) | `TenantId` ردیف‌های نوشته‌شده پس از Cutover (قابل بازسازی از `Companies.TenantId` اگر Core جلوتر Rollback نشده) | پس از ورود داده با کد جدید یا Rollback Core پیش از Trade | پس از اولین نوشتن واقعی با کد جدید | **ناسازگار** از لحظهٔ Expand (Rename)؛ نسخهٔ قدیمی و جدید نباید هم‌زمان روی همین دیتابیس باشند |

برای هر مرحله:
- **پیش‌شرط توقف:** Backup تأییدشده؛ همهٔ نمونه‌ها متوقف؛ وضعیت Migration مورد انتظار (از `GetAppliedMigrations`)؛ Dry-run سبز.
- **معیار موفقیت:** ردیف History مرحله؛ شمارش ردیف‌ها قبل/بعد برابر؛ هیچ ردیف `TenantId` NULL؛ آزمون ایزولاسیون سبز؛ Down/Up روی نسخهٔ بازیابی‌شده اثبات‌شده.
- **روش بازیابی:** پیش از ورود داده جدید: `Down`؛ پس از آن: بازیابی Backup یا Roll-forward با Manifest/Migration اصلاحی؛ ترتیب Down همیشه Trade پیش از Core.
- **تأیید Backup/Restore قبل از نصب مشتری (G5، Runbook امروز وجود ندارد):** بازیابی Backup در دیتابیس Scratch ایزوله، شمارش ردیف و `CHECKSUM_AGG` ستون‌های کلیدی نسبت به منبع، و اجرای تمرینی کل مسیر (Up/Backfill/Contract/Down) روی همان نسخهٔ بازیابی‌شده. این تمرین قوی‌ترین شاهد است و فقط با مجوز جدا روی نسخهٔ مشتری انجام می‌شود.

**موارد تست G3 که اثبات رفتار Unknown را می‌دهند (اجرا نشد):** Expand موفق + Guard شکست ⇒ وضعیت History و Schema؛ اجرای مجدد پس از Backfill؛ بقای مقادیر پس از Rename؛ INSERT قدیمی پس از Expand؛ `TABLOCKX` در برابر نویسندهٔ هم‌زمان؛ `ALTER NOT NULL` با NULL؛ اجرای هم‌زمان دو Backfill (کد ۳)؛ قفل داخلی EF در کنار قفل Host.

### ۱۰.۹ وضعیت O-1..O-4 و تأییدهای لازم از مالک
- **O-1:** پیشنهاد «Expand → Backfill اپراتوری → Contract بدون Staging» با اصلاحات بالا (Verify کل ردیف‌ها، حصار، Maintenance Window، قاعدهٔ R1) هنوز `Proposed`.
- **O-2:** Company-scoped برای هر هفت مورد، `Proposed`.
- **O-3:** جزء T1 است، تصمیم مسدودکننده نیست؛ تطبیق با Host/کاربر وابسته به پیاده‌سازی D-103/D-118 است.
- **O-4:** توقف کامل؛ اکنون شامل «غیر-NULL و نابرابر با نگاشت» هم می‌شود.
- **تأیید لازم از مالک (واقعی):** (۱) پذیرفتن Maintenance Window (توقف همهٔ نمونه‌ها از Backfill تا پایان Contract) به‌عنوان شرط استقرار؛ (۲) سازوکار تغییرناپذیری `Companies.TenantId`؛ (۳) تأیید O-1 و O-2.
- **Unknown و واگذارشده:** رفتار دقیق EF/SQL Server (تا تست G3)؛ داده و حجم جدول‌های Trade در نصب‌ها؛ هیچ ادعایی دربارهٔ خالی‌بودن نشده است.
