# بستن مرحلهٔ صفر Trade — سابقه تصمیم‌ها و انتقال به S3 (بازبینی 2026-11-14)

- **تاریخ بازبینی:** 2026-11-14
- **وضعیت:** این سند اکنون سابقه تصمیم‌ها و اجرای S2 است. قواعد جاری S3 در D-086 (Accepted، پیاده‌سازی NotImplemented) قرار دارند. D-080 فقط در مواردی که D-086 جایگزین نکرده، از جمله P-1 (Accepted) و P-4/P-5 (باز)، معتبر است. S2-1 تا S2-4 ادغام شده‌اند؛ این سند دیگر گیت شروع S3 نیست.
- **مبنا:** main@b5a6caf6217a054643ba9aed04d7c1b7af831abe؛ وضعیت S2 در ROADMAP.md و PROJECT_STATUS.md؛ قواعد جدید در D-086.
- **مبنا:** `main` @ `fbe4b59f` (تغییرات پس از `0c9ecff7` فقط D-078 و بی‌ارتباط با Trade)؛ `docs/DECISIONS.md` (D-080، D-081)، `docs/ROADMAP.md`، `docs/MENU_FORM_CATALOG.md`، `ClientApp/src/components/shell/AppSidebar.tsx`.

## ۱. جدول تصمیم‌ها

### ۱.۱ قطعی (دستور صریح مالک؛ ثبت‌شده در D-081)
| # | تصمیم | مرجع |
|---|---|---|
| ق۱ | مسیر کلاینت: `ClientApp/src/features/trade` (جایگزین `modules/trade` در D-061/`TRADE_MODULE.md` بخش ۱) | D-081 بند ۱ |
| ق۲ | Routeها: `/trade/parties` و `/trade/products` (الگوی فهرست، `/new`، `/:id`، `/:id/edit`) | D-081 بند ۲ |
| ق۳ | نسخهٔ اول: درخواست خرید، استعلام، مقایسهٔ پیشنهاد و قرارداد **ساخته نمی‌شوند** (مجوزهای seedشده بدون تغییر می‌مانند) | D-081 بند ۳ |
| ق۴ | منو فقط پس از آماده‌شدن Route واقعی فعال می‌شود | D-081 بند ۴ |
| ق۵ | Modular Monolith و دیتابیس فیزیکی مشترک (schema `trade`) حفظ می‌شود | D-061، D-081 بند ۵ |
| ق۶ | S2 حداقلی: فقط `Party`، `PartyRoleAssignment`، `Product`؛ Country/Currency/Unit/Incoterm مرجع محلی فقط‌خواندنی؛ بدون جدول/API/زیرسیستم موازی؛ «Product مستقل از Inventory» بدون تأیید جدید تغییر نمی‌کند | D-081 بند ۶ |
| ق۷ | `PartyBankAccount` ← S5؛ `ProductIdentifier` ← مرحلهٔ نیاز واقعی | D-081 بند ۶ |
| ق۹ | P-1 (D-080): ثبت مجدد پس از ابطال ثبت‌سفارش = `ImportCase` جدید با `ReplacesImportCaseId` (گزینهٔ الف)؛ هر پرونده یک ثبت‌سفارش جاری | D-080 بند ۷ب (تأیید مالک) |
| ق۸ | استاندارد مشترک انتخاب اطلاعات (Select / Searchable Combobox / جستجوی سروری / Lookup Dialog) و ایجاد سریع (+) برای همهٔ زیرسیستم‌ها؛ منوی مستقل برای هر فهرست مرجع نیست | D-082 (تأیید مالک 2026-11-13) |

یادداشت: بند ق۶ در عمل پاسخ پرسش مالکیت دور قبل را «توسعهٔ Party/Product موجود Trade» می‌کند (نه زیرسیستم مشترک)؛ هر انتقال آینده ADR جدا می‌خواهد.

### ۱.۲ باز (فقط مالک تصمیم می‌گیرد؛ هیچ‌کدام مانع S2 نیست)
| # | موضوع | وابستگی |
|---|---|---|
| ب۱ | D-080 (Proforma/ثبت‌سفارش) به‌جز P-1: `Proposed` + `NeedsApproval` | شرط شروع S3 |
| ب۳ | P-2..P-5 در D-080 (بدون تغییر، باز) | S3 |
| ب۴ | OD-007 (سطح تعهد ارزی)؛ پیشنهاد مشروط در `TRADE_OPEN_DECISIONS.md`؛ وضعیت `Open` | S5 |
| ب۵ | OD-001..OD-022 دیگر: بدون تغییر `Open` | هر مرحله در زمان خودش |
| ب۶ | D-083 (چرخهٔ عمر/حذف اطلاعات پایه، `Proposed`) و Q-1..Q-5؛ به‌ویژه Q-1 (تعارض با «بدون حذف فیزیکی» V3.2) و Q-2 (مجوز غیرفعال‌سازی) | قبل از پیاده‌سازی عملیات حذف/غیرفعال‌سازی؛ مانع ساخت فهرست/ایجاد/ویرایش در S2 نیست |

### ۱.۳ موکول‌شده
| موضوع | به |
|---|---|
| `PartyBankAccount` | S5 |
| `ProductIdentifier` (GTIN/IRC) | مرحلهٔ نیاز واقعی (Issue جدا در همان زمان) |
| بندر، نوع مجوز، کشتی | S4 |
| بانک (نقش `PartyRole.Bank`) | S5 |
| درخواست خرید، استعلام، مقایسه، قرارداد | پس از نسخهٔ اول؛ با ADR/Issue جدا |
| منوهای محموله/ترخیص/ارز/هزینه/گزارش | همان مرحله‌ها (S4..S6)؛ پرسش جدید دربارهٔ آن‌ها اکنون مطرح نمی‌شود |
| اتصال entitlement واقعی (Production) | #401 ← پس از #392/#228 → #384 → اتصال #391 |
| هم‌ترازی بقیهٔ جدول `MENU_FORM_CATALOG.md` با `AppSidebar.tsx` | مالک آن سند (در یادداشت همان سند ثبت شد) |

## ۲. ساختار منو

### ۲.۱ قاعده
هر آیتم Trade فقط با Route واقعی، ثبت در `appRoutes` و تست `menuRouteConsistency` فعال می‌شود (ق۴)؛ تا آن زمان همان placeholder فعلی «واردات و صادرات» می‌ماند و آیتم جعلی/«به‌زودی» جدید اضافه نمی‌شود. گیت منو hint است؛ سرور مرجع است (D-064). برچسب‌ها فارسی؛ دارایی‌ها محلی (D-048/D-049).

### ۲.۲ جدول منوی گروه «بازرگانی خارجی»
| ترتیب | بخش | آیتم | Route (+الگوی `/new`، `/:id`، `/:id/edit`) | Permission گیت | مرحله |
|---|---|---|---|---|---|
| ۱ | اطلاعات پایه بازرگانی | طرف‌حساب‌ها | `/trade/parties` | `trade.form.party.view` (+`create`/`update`/`delete`) | **S2** |
| ۲ | اطلاعات پایه بازرگانی | کالاها | `/trade/products` | `trade.form.product.view` (+`create`/`update`/`delete`) | **S2** |
| ۳ | پرونده‌ها | پرونده‌های واردات | `/trade/cases` | `trade.form.import-case.view` (+`create`/`update`/`approve`/`close`) | S3 (پس از D-080) |
| ۴.. | محموله و ترخیص؛ ارز و بانک؛ هزینه و تسهیلات؛ گزارش‌ها | طبق ROADMAP S4..S6 | `/trade/shipments`، `/trade/customs`، `/trade/forex`، `/trade/costs`، `/trade/facilities`، `/trade/reports` (نام‌ها پیشنهادی؛ در همان مرحله قطعی می‌شوند) | کدهای همان مرحله | S4..S6 |

- طرف‌حساب و کالا زیر گروه Trade و بخش «اطلاعات پایه بازرگانی» می‌آیند (پیامد ق۲) و فرم/مسیر مدیریتی مستقل خود را دارند. در فرم‌های دیگر Trade انتخاب آن‌ها با کنترل مشترک D-082 (جستجوی سمت سرور برای فهرست بزرگ) انجام می‌شود و «ایجاد سریع» (+) فقط برای کاربر دارای `trade.form.party.create`/`trade.form.product.create` و با همان UseCase/API ایجاد مجاز است؛ هیچ فرم مدیریتی دوم ساخته نمی‌شود. درخواست خرید/استعلام/مقایسه/قرارداد منو ندارند (ق۳).
- پیش‌فاکتور، ثبت‌سفارش، Amendment، مجوز و اسناد منوی جدا ندارند؛ تب‌های `/trade/cases/:id` هستند (S3/S4).
- مرجع‌ها (کشور، ارز، واحد، Incoterm) صفحهٔ مدیریت و منو ندارند.

### ۲.۳ تطبیق با `MENU_FORM_CATALOG.md` و ClientApp
ردیف ۳.۲ کاتالوگ با کد (گروه مستقل با placeholder) هم‌تراز شد؛ بقیهٔ جدول کاتالوگ با `AppSidebar.tsx` تفاوت دارد و در همان سند یادداشت شد. هر آیتم فعال‌شده همان لحظه ردیف کاتالوگ، ردیف `SUBSYSTEM_CATALOG`، Route و تست سازگاری می‌گیرد. الگوی صفحات مانند `/hr/org/units`؛ مسیر پوشه `features/trade` (مانند `features/hr`).

## ۳. برنامهٔ دقیق شروع S2

### ۳.۱ حقایق مبنا (از کد؛ برای جلوگیری از بازسازی)
- موجود و **ساخته نمی‌شود**: Entityهای `Party`، `PartyRoleAssignment`، `Product` (+ `PartyBankAccount`، `ProductIdentifier` در دامنه)، نگاشت EF، `TradeDbContext` (DI و design-time factory؛ #404/#405)، ۸ کد مجوز party/product (برای هر کدام `view/create/update/delete`)، `TradeAccessFilter`/`TradeEndpoints` (`GET /api/v1/trade/capabilities`)، تست‌های دامنه و نگاشت (`TradeMasterDataDomainTests`، `TradeMasterDataModelTests`).
- نیست: Migration جدول‌های `trade`، UseCase، API، UI، فهرست مرجع.
- **S2-1 و مدل EF (بررسی‌شده؛ آزمایش روی کپی بیرون از مخزن):** `Party.BankAccounts` و `Product.Identifiers` ناوبری `HasMany` با FK مرکب `(TenantId, …Id)` و `Restrict` دارند؛ مدل فعلی ۵ جدول می‌سازد. روش انتخاب‌شده (کم‌تغییر؛ D-081): `ExcludeFromMigrations()` روی `ToTable` دو نگاشت `PartyBankAccount` و `ProductIdentifier`؛ نتیجه: ۳ جدول، مدل/ناوبری/تست‌های نگاشت موجود دست‌نخورده؛ در S5/نیاز همان پرچم برداشته می‌شود. `Ignore` رد شد.

### ۳.۲ Issueها (فرزند Epic #397؛ GitHub وارسی شد: Issue باز S2 وجود ندارد، پس تکراری ساخته نمی‌شود)
| شناسه | عنوان | وابستگی | معیار پذیرش |
|---|---|---|---|
| S2-0 | تأیید مالک روی D-081/این سند و ساخت Issueهای زیر | مالک | Issueها ساخته و به #397 وصل؛ هیچ کدی پیش از آن |
| S2-1 | Migration افزایشی `trade` برای `Parties`، `PartyRoleAssignments`، `Products` | S2-0، تأیید صریح Schema | ۱) فقط افزودن `ExcludeFromMigrations()` به `ToTable` نگاشت `PartyBankAccount` و `ProductIdentifier` (بدون Ignore/حذف ناوبری)؛ **تست دروازه (مشخصات؛ در S2-1 نوشته می‌شود، در این دور نوشته نشد):** `TradeDbContext` با ارائه‌دهندهٔ SQL Server و رشتهٔ اتصال ساختگی (بدون اتصال) ساخته شود؛ `Database.GenerateCreateScript()` دقیقاً سه `CREATE TABLE` (`Parties`، `PartyRoleAssignments`، `Products`) و هیچ `PartyBankAccounts`/`ProductIdentifiers` داشته باشد، و `Model.GetEntityTypes()` همچنان پنج Entity؛ تست‌های `TradeMasterDataModelTests` بدون تغییر سبز بمانند؛ UseCaseهای S2 به حساب بانکی/شناسهٔ کالا دست نزنند؛ ۲) Migration فقط سه جدول با `dbo.__TradeMigrationsHistory`؛ ایندکس‌های یکتای فیلترشدهٔ موجود در نگاشت (`(TenantId, Code)` برای هر دو و `(TenantId, PartyId, Role)` برای نقش‌ها، با `[IsDeleted] = 0`) عیناً در Migration بیایند و رد تکرار روی SQL Server اثبات شود؛ ۳) Snapshot سازگار؛ ۴) Up و Down روی SQL Server 2022 واقعی (CI و `scripts/dev/test-sqlserver.sh`)؛ ۵) اجرا نشدن روی دیتابیس مشتری |
| S2-2 | مرجع محلی فقط‌خواندنی: Country، Currency، Unit، Incoterm | S2-0 | کد/داده در مخزن؛ نام فارسی و کد استاندارد؛ بدون جدول، منو یا API مدیریتی؛ فقط API/Provider فقط‌خواندنی برای فرم‌ها؛ بدون CDN؛ تست؛ مقدار پیش‌فرض واحد `TON` در `Product` معتبر بماند |
| S2-3 | برش عمودی طرف‌حساب‌ها | S2-1، S2-2 | UseCase + API فقط برای **فهرست، ایجاد، جزئیات و ویرایش** طرف‌حساب (`GET` فهرست/جزئیات، `POST`، `PUT`) در `/api/v1/trade/parties` (روی `TradeEndpoints`/`TradeAccessFilter`)؛ **بدون حذف، غیرفعال‌سازی و فعال‌سازی** (تا تصمیم نهایی D-083 خارج از محدوده)؛ فقط `CompanyAccessService`؛ `TenantId` از کلاینت نه؛ ۴۰۴ یکسان برای ناموجود/غیرمجاز؛ Anti-forgery؛ DTO جدا از Entity؛ صفحات `/trade/parties[/new|/:id|/:id/edit]` در `features/trade`؛ منو؛ ردیف کاتالوگ؛ `UI_COMPONENTS.md`؛ تست cross-tenant روی SQL Server؛ تست API با test double برای `ITradeModuleEntitlement`؛ `menuRouteConsistency` سبز |
| S2-4 | برش عمودی کالاها | S2-3 | همان معیارها، فقط برای **فهرست، ایجاد، جزئیات و ویرایش** کالا در `/api/v1/trade/products` و `/trade/products`؛ بدون حذف/غیرفعال‌سازی/فعال‌سازی؛ `HsCode` متن (بدون حذف صفر ابتدایی)؛ نقش‌ها/هویت‌ها (GTIN/IRC) خارج از دامنه |

یادداشت D-082/D-083: فرم‌های S2-3/S2-4 به انتخاب کشور/واحد (فهرست متوسط) نیاز دارند؛ کنترل مشترک Combobox در `ClientApp/src/components/ui` باید پیش از آن‌ها یا در همان Issue ساخته شود (تصمیم هنگام ساخت Issueها؛ مهاجرت `UserPicker`/`CompanyPicker` بخشی از S2 نیست). **هیچ API یا عملیات حذف، غیرفعال‌سازی یا فعال‌سازی Party/Product در S2 ساخته نمی‌شود** و تا تصمیم نهایی D-083 خارج از محدوده است؛ S2-3/S2-4 فقط فهرست، ایجاد، جزئیات و ویرایش را شامل می‌شوند.

ترتیب: S2-0 ← (S2-1 ∥ S2-2؛ فایل مشترک ندارند) ← S2-3 ← S2-4. نمی‌تواند با S1-1 (#401) مسدود شود. تست یکپارچه با test double (الگوی `RecordingEntitlement`)؛ هیچ پرچم bypass در Production.

### ۳.۳ تقسیم کار پیشنهادی (طبق CLAUDE.md)
S2-1: `database-engineer` + `qa-engineer`، بازبینی `code-reviewer` و (چون Schema) `architect`/`security-reviewer`. S2-2: یک `backend-developer`. S2-3/S2-4: هرکدام یک `backend-developer` برش عمودی سرور + `frontend-developer` پس از ثابت‌شدن قرارداد API، بازبینی `code-reviewer` و `security-reviewer` (Tenant/ورودی کاربر).

## ۴. سایر Issueهای S0/S1 (بدون تغییر نسبت به دور قبل، خلاصه)
| شناسه | عنوان | وضعیت |
|---|---|---|
| S0-1 | تصمیم دربارهٔ **فقط بندهای باقی‌ماندهٔ D-080** (P-1 قبلاً `Accepted` شده؛ P-2..P-5 و سایر بندها `Proposed`) و **OD-007** | منتظر مالک (شرط S3) |
| S0-2 | رفع ناسازگاری مستندات | **انجام شد در همین تغییر** (INDEX، PROJECT_STATUS، ROADMAP، یک ردیف MENU_FORM_CATALOG، TRADE_MODULE) |
| S0-3 | تأیید ساختار منو | با ق۲/ق۴ و بخش ۲ این سند؛ منتظر تأیید نهایی |
| S0-4 | ممیزی پایه | **یک بار اجرا شد** (نتیجه در handoff و PROJECT_STATUS) |
| S1-1 | اتصال entitlement (#401، موجود) | مانع فقط Production؛ Issue جدید ساخته نمی‌شود |
| S1-2 | نمایش و تخصیص ۴۶ مجوز | بدون تغییر؛ پس از S2 با اولین فرم‌ها وارسی شود |


## ۵. بازبینی 2026-11-14 — وضعیت جاری و انتقال به S3

- S2-1 تا S2-4 تکمیل و ادغام شده‌اند؛ جزئیات PR/SHA در Roadmap و Project Status مرجع است. برنامه‌های S2 این سند برای سابقه نگهداری می‌شوند و نباید دوباره اجرا شوند.
- D-086 تصمیم جاری و Accepted برای قواعد پرونده، کالا، پیش‌فاکتور، ثبت سفارش، پروفایل فعالیت، فایل‌ها، گردش کار و قواعد پایه حمل/گمرک است.
- D-080 در بخش‌های متعارض با D-086 جایگزین شده است؛ P-1 همچنان Accepted. P-4 و P-5 باز هستند و رفتارهای وابسته به آن‌ها از محدوده S3 کنار گذاشته می‌شوند.
- D-083 درباره حذف/غیرفعال‌سازی Party/Product همچنان Proposed/NeedsApproval و خارج از S3 است.
- D-085 درباره نوشتن Product همچنان Proposed/NeedsApproval؛ این وضعیت مانع S3 نیست.
- Migrationهای Trade هنوز باید از نظر مسیر اعمال در نصب مشتری بررسی شوند؛ این سند مجوز تغییر migration host یا اجرای Migration روی دیتابیس مشتری نیست.
- گیت مستنداتی S3: این بسته باید از طریق PR بازبینی و در main ادغام شود. پس از Merge، S3 با Issueهای کوچک شروع می‌شود؛ این بسته کد یا Migration ندارد.
