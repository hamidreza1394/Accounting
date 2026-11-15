# S3-0 — بسته تصمیم‌های معماری OD-11، OD-12، OD-13، OD-14، OD-17

- **Issue:** #397 (S3-0)
- **تاریخ:** 2026-11-15
- **مبنا:** `origin/main@9eb281b`
- **وضعیت همهٔ تصمیم‌ها:** `Proposed` — هیچ‌کدام Accepted نشده؛ تأیید مالک لازم است.
- **ماهیت:** مستندات. بدون Entity/جدول Tenant، Migration/DDL، Backfill، تغییر Runtime/API/Authorization/UI. Build/Test اجرا نشد.
- **مبنای معماری:** D-087 (مدل هدف، بدون تغییر)، D-088 و D-089 (Accepted). OD-6/OD-6a در D-089 بسته‌اند و در این سند دوباره باز نشدند.
- **منابع:** `ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md` (گزینه‌های موجود؛ بازطراحی نشد)، `DATABASE.md`، `TRADE_MODULE.md`، `DEPLOYMENT.md`، `CLOUD_CONCURRENCY_READINESS.md`، و کد واقعی `Server/Infrastructure/Hosting/DatabaseMigrationHost.cs` با تست‌های آن.

## OD-11 — یکپارچگی Tenant در Trade

**۱. وضعیت فعلی.** `TradeEntity.TenantId` تنها شناسهٔ مالکیت Trade است؛ `TradeAuthorizationAdapter` مقدار Company جاری را در آن می‌گذارد. `PartyStore`/`ProductStore` همهٔ Queryها (از جمله فرزند `Roles`) را با `TenantId` فیلتر می‌کنند. FK یا مرجع به Core وجود ندارد.

**۲. مسئله و شواهد.** در مدل هدف باید هم Tenant و هم Company کنترل شود، بدون ازبین‌بردن مرز ماژول (`TRADE_MODULE.md` §مسیر استخراج: انتقال schema `trade` به دیتابیس مستقل). تست‌های «OtherTenant» (`TradePartyHttpTests`، `TradeProductHttpTests`) امروز دو Company را می‌سنجند، نه دو Tenant.

**۳. گزینه‌ها.**
- **A:** Host-level validation + ستون‌های `(TenantId, CompanyId)` در Trade، بدون FK به Core.
- **B:** FK بین‌ماژولی یا Reference مستقیم به `Companies`.
- **C:** جدول مرجع محلی داخل Trade (`CompanyTenantRef`) که Host همگام می‌کند.
- **D:** وضعیت فعلی (فقط `TenantId = Company.Id`) تا پایان S3.

**۴. مزایا و معایب.** A: ساده، مرز ماژول و استخراج آینده حفظ می‌شود؛ ولی درج مستقیم SQL ناسازگار را متوقف نمی‌کند. B: تضمین سطح DB؛ ولی وابستگی Trade→Core را برخلاف `TRADE_MODULE.md` می‌سازد و استخراج به دیتابیس مستقل را غیرممکن می‌کند (FK بین دیتابیس‌ها نیست). C: تضمین محدود محلی؛ ولی نیازمند همگام‌سازی، منبع حقیقت دوم و ADR وابستگی. D: بدون هزینه؛ ولی مدل هدف را پیاده نمی‌کند.

**۵. ریسک امنیت و داده.** A: ریسک باقی‌مانده = ورود دستی/ابزاری داده ناسازگار؛ باید مستند و با تست Store/Use Case پوشش داده شود. B کمترین ریسک داده را دارد ولی هزینهٔ ساختاری بالا. D: ریسک تفسیر نادرست `TenantId` در Shared.

**۶. هزینه و پیچیدگی.** A کم (چند Store و یک Context)، نیازمند افزودن ستون `CompanyId` به Trade (Schema، PR جداگانه با تأیید). B متوسط‌تا‌بالا. C متوسط. D صفر.

**۷. پیشنهاد فنی (Proposed): A.** Host فقط `(TenantId, CompanyId)` اعتبارسنجی‌شده را به Trade می‌دهد؛ Storeها هر دو را در Query و Insert کنترل می‌کنند؛ تست SQL Server دو Tenant/Company را در سطح Store و Use Case جدا ثابت می‌کند؛ محدودیت «درج مستقیم SQL تضمین نمی‌شود» صریح مستند می‌شود. FK فقط با ADR جدید دربارهٔ وابستگی Core↔Trade.

**۸. تأیید مالک.** رد یا پذیرش FK؛ پذیرش محدودیت درج مستقیم SQL؛ تأیید افزودن `CompanyId` به Trade در S3 (Schema جداگانه).

**۹. معیار پذیرش.** Store با `CompanyId` متعلق به Tenant دیگر ردّ شود؛ نشت دو Tenant در List/Find/Update/Unique آزمون شود (SQL Server)؛ نبود وابستگی Trade→Core با تست معماری؛ مستند بودن محدودیت.

**۱۰. وابستگی.** پس از D-089 و طراحی Tenant (S3)؛ پیش از PR Trade (PR-9 در S3-0h)؛ وابسته به OD-17 برای نحوهٔ اجرای Migration ستون جدید.

## OD-12 — تحمل Downtime و پنجرهٔ نگهداری

**۱. وضعیت فعلی.** ستون‌های legacy `TenantId` (= `Company.Id`) هنوز نام فیزیکی خود را دارند. Guard مربوط (PR-2a/PR-2b: Pin، `HasColumnName`، Rename کنترل‌شده) ادغام شده است؛ اما در کد و Migrationهای تولیدی هیچ `HasColumnName` یا `RenameColumn` وجود ندارد (فقط در `tests/Template.Tests/RenameMigrationSqlServerTests.cs` و `RenameValidationMigrations.cs`). یعنی گام P هنوز اعمال نشده و هیچ Rename فیزیکی در مخزن نیست.

**۲. مسئله و شواهد.** S3-0h §6.5: افزودن ستون nullable با نسخهٔ قدیمی سازگار است؛ Rename فیزیکی و NOT NULL ناسازگارند. D-087 «عدم downtime غیرضروری» را می‌خواهد. هیچ اندازه‌گیری زمان Downtime در مخزن وجود ندارد؛ هیچ عددی حدس زده نمی‌شود.

**۳. گزینه‌ها.**
- **1:** حداقل Downtime — فقط گام P؛ نام فیزیکی `TenantId` حفظ شود (بدهی نام‌گذاری).
- **2:** گام P سپس Rename فیزیکی Rename-only در پنجرهٔ نگهداری مشخص.
- **3:** حذف دائمی Rename فیزیکی (حالت خاص گزینه ۱ با تصمیم صریح).

**۴. مزایا و معایب.** ۱: بدون تغییر Schema و Downtime؛ بدهی نام‌گذاری باقی می‌ماند. ۲: نام‌ها صحیح می‌شود؛ ولی نیازمند Backup، سازگاری نسخه‌ها، Maintenance Window و Rollback = Rename معکوس + کد قدیمی. ۳: همان ۱ با تصمیم قطعی.

**۵. ریسک.** ۲: نسخهٔ قدیمی برنامه با ستون تغییرنام‌یافته خطا می‌دهد؛ رفتار `sp_rename` با Pin روی SQL Server واقعی هنوز فقط با گیت G3 اثبات می‌شود (فرض [U1] S3-0h). ۱: ریسک کم، ولی خطای خوانش نام ستون توسط توسعه‌دهندگان.

**۶. هزینه و پیچیدگی.** ۱ کم؛ ۲ متوسط (Migration دستی بازبینی‌شده، رویهٔ استقرار، پنجرهٔ نگهداری)؛ ۳ کم.

**۷. پیشنهاد فنی (Proposed): گزینه ۱ (حداقل Downtime) تا اثبات ضرورت.** Rename فیزیکی اختیاری و جدا، فقط پس از G3 و تأیید مالک؛ نیاز به هیچ Downtime تا آن زمان نیست. مدت Downtime بدون اندازه‌گیری روی SQL Server واقعی اعلام نشود.

**۸. تأیید مالک.** حفظ بدهی نام‌گذاری یا پذیرش پنجرهٔ نگهداری؛ ترتیب OD-10/OD-15/OD-18.

**۹. معیار پذیرش.** (۱) هیچ Migration Rename بدون تأیید و G3؛ (۲) اگر گزینه ۲: اجرای `Up/Down` روی SQL Server واقعی، Backup/Restore و رویهٔ Rollback مستند؛ (۳) نسخهٔ قدیمی و جدید در پنجرهٔ استقرار آزمون شود.

**۱۰. وابستگی.** PR-R1..R4 (Rename-only) در S3-0h؛ گیت G2/G3/G5؛ OD-10، OD-15، OD-18.

## OD-13 — مرجعیت Backfill Manifest

**۱. وضعیت فعلی.** هیچ نگاشت معتبر Company→Tenant وجود ندارد؛ هیچ Backfill اجرا یا نوشته نشده. D-089 منبع ایجاد Tenant و نگاشت `CustomerReference→TenantId` را تعیین کرده ولی پیاده نشده است.

**۲. مسئله و شواهد.** S3-0h §6.4 فقط «manifest تأییدشدهٔ مالک» را منبع می‌داند؛ `CustomerBoundaryId`، گروه، مالکیت و نام هرگز منبع یا شاهد نیستند؛ `ProvisioningRequestClaims.CustomerReference` شاهد تطبیقی غیرمرجع است.

**۳. گزینه‌ها.**
- **A:** Manifest تأییدشدهٔ مالک (فایل ساده، Hash) تنها مرجع؛ Claimها فقط شاهد.
- **B:** Claimهای Provisioning مرجع مستقل.
- **C:** استنتاج از داده‌های موجود DB (Boundary/گروه/نام).

**۴. مزایا و معایب.** A: ساده، قابل ممیزی؛ نیازمند کار دستی مالک. B: خودکار؛ ولی `CustomerReference` شناسهٔ Tenant نیست (D-089) و Claim ممکن است ناقص باشد. C: ممنوع (D-087، D-089 بند ۷).

**۵. ریسک.** نگاشت اشتباه = نشت دائمی داده بین Tenantها؛ لذا هر رکورد مبهم رد شود.

**۶. هزینه.** A کم (بدون سامانهٔ جدید مدیریت Manifest)، Dry-run/گزارش لازم است.

**۷. پیشنهاد فنی (Proposed): A.** Manifest شامل Company Id و Tenant Id هدف، امضا یا SHA256 و هویت تأییدکننده؛ ردیف‌ها: هر Company دقیقاً یک‌بار؛ Company ناشناخته یا بدون ردیف ⇒ توقف کامل؛ Company با Tenant متفاوت از قبل ⇒ توقف (بازتخصیص ممنوع). فرمان اپراتوری با `--dry-run` پیش‌فرض، تراکنش Serializable و `sp_getapplock`، رویداد ممیزی با Hash و شمارها، اجرای مجدد = no-op با گزارش. اصلاح Manifest = Manifest جدید با دلیل و تأییدکننده. هیچ Backfill عملیاتی در این مرحله.

**۸. تأیید مالک.** قالب و محل بایگانی Manifest؛ هویت تأییدکننده؛ پذیرش «توقف کامل برای هر مبهم».

**۹. معیار پذیرش.** Dry-run بدون نوشتن؛ گزارش اختلاف/شمار قبل و بعد؛ توقف برای Manifest ناقص/اضافه/ناسازگار؛ اجرای مجدد بی‌اثر؛ آزمون SQL Server؛ ثبت ممیزی بدون Secret.

**۱۰. وابستگی.** پس از D-089 و طراحی Schema Tenant (A1)؛ PR-5/PR-D1 در S3-0h؛ گیت G5 (Backup و مجوز جدا برای دیتابیس مشتری).

## OD-14 — مدل Shared و Multi-Tenant Runtime

**۱. وضعیت فعلی.** `DEPLOYMENT.md`: برنامه «فعلاً برای یک نمونه طراحی و تست شده»؛ Rate Limiting درون‌پردازه‌ای و Data Protection Keys باید مشترک شوند. `CLOUD_CONCURRENCY_READINESS.md`: ظرفیت ۱۰۰ کاربر هم‌زمان `NotVerified`، آزمون بار اجرا نشده. Entity Tenant وجود ندارد؛ Provisioning تک‌مشتری است.

**۲. مسئله و شواهد.** S3-0h §9: تا SP-1..SP-9 با تست دو-Tenant روی SQL Server واقعی بسته نشود، Tenant دوم در دیتابیس مشترک ممنوع است. مدل هدف D-087 (Shared DB و Dedicated DB با مدل منطقی یکسان) تغییر نمی‌کند.

**۳. گزینه‌ها.**
- **A:** چند Tenant در یک نمونهٔ اجرایی (Shared Runtime) روی دیتابیس مشترک.
- **B:** نمونهٔ مستقل به‌ازای هر Tenant؛ دیتابیس مشترک یا اختصاصی.
- **C:** نصب اختصاصی کامل (نمونه + دیتابیس) به‌ازای Tenant.

**۴. مقایسه.**
| معیار | A | B | C |
|---|---|---|---|
| امنیت و جداسازی | وابسته به درستی همهٔ فیلترها؛ هر باگ = نشت بین مشتری | جداسازی فرایند؛ دیتابیس مشترک ریسک متوسط | بیشترین |
| هزینهٔ زیرساخت | کمترین | متوسط | بیشترین |
| پیچیدگی Deployment | کم برای نمونه، بالا برای گیت‌های امنیتی | متوسط (چند نمونه، Provisioning هر نمونه) | بالا برای هر مشتری |
| ۱۰۰ کاربر هم‌زمان | بار همهٔ Tenantها روی یک نمونه؛ اثبات‌نشده | بار مستقل؛ اثبات‌نشده | بار مستقل؛ اثبات‌نشده |
| مانیتورینگ/عیب‌یابی | همه‌چیز مخلوط؛ Noisy neighbor | جدا | جدا |
| Backup/Recovery | Restore یک Tenant از DB مشترک دشوار | DB مشترک: همان دشواری؛ اختصاصی: ساده | ساده |
| مقیاس آینده | افقی با چند نمونه نیازمند آماده‌سازی (`DEPLOYMENT.md` §۵) | خطی | خطی |

**۵. ریسک امنیت و داده.** A بالاترین ریسک و نیازمند SP-1..SP-9 + G6. B/C مرز فیزیکی دارند.

**۶. هزینه و پیچیدگی.** برای هدف ~۱۰۰ کاربر هم‌زمان و اصل «ساده، کارآمد، کم‌هزینه»، بار هر مشتری کوچک است و عددی برای تفاوت هزینهٔ A و B/C اندازه‌گیری نشده؛ نمی‌توان صرفه‌جویی A را اثبات کرد.

**۷. پیشنهاد فنی (Proposed): گزینه B/C — نمونه (و در صورت نیاز DB) مستقل به‌ازای Tenant، با همان مدل منطقی D-087.** A فقط پس از بسته‌شدن گیت G6 و آزمون بار واقعی (CC-01) به‌عنوان تصمیم جدا. هیچ تغییری در مدل هدف D-087 و در Provisioning فعلی این PR نیست.

**۸. تأیید مالک.** مدل پیش‌فرض عملیاتی؛ شروط اجازهٔ Shared Runtime؛ مسئول آزمون بار.

**۹. معیار پذیرش.** (۱) برای A: SP-1..SP-9 و آزمون‌های منفی دو-Tenant روی SQL Server واقعی؛ آزمون بار ۱۰۰ کاربر در محیط isolated (طبق `CLOUD_CONCURRENCY_READINESS.md`)؛ (۲) برای B/C: رویهٔ Provisioning هر نمونه، Backup/Restore مستند، تنظیم Data Protection/Rate Limit بین نمونه‌ها.

**۱۰. وابستگی.** OD-1 (D-088 Accepted، پیاده‌سازی Partial)، OD-3، OD-7، OD-9، OD-16؛ گیت G6؛ D-089 (Provisioning فقط اتصال).

## OD-17 — سیاست Migration برای Trade

**۱. وضعیت فعلی (از کد #665).** `DatabaseMigrationHost` + `TradeMigrationRunner`: `--migrate` فقط Core؛ `--migrate --include-trade` اول Core سپس Trade. شکست Core ⇒ Trade شروع نمی‌شود (`CoreFailed=1`)؛ شکست Trade ⇒ `TradeFailed=2`؛ قفل `sp_getapplock` (`LockNotAcquired=3`)؛ جدول Trade بدون History ⇒ توقف (`TradeStateInconsistent=4`)؛ بدون Baseline خودکار. تست‌ها: `DatabaseMigrationHostSqlServerTests` (هشت تست؛ نام‌ها از جمله `CoreThenTrade_WithExplicitRequest_UsesSeparateHistories_AndRerunRepeatsNothing`، `LegacyTradeTablesWithoutHistory_StopWithoutBaselineOrChange`، `SecondConcurrentRun_IsRefused…`، `TradeFailure_IsNotSuccess_AndARerunContinuesFromTheFailedStep`). اتصال به Entitlement و Deployment Mode **پیاده نشده** است (`--migrate` Entitlement را نمی‌خواند).

**۲. مسئله و شواهد.** S3-0h §7: چهار گزینهٔ H1..H4. اجرای Migration Host ≠ سیاست عملیاتی. Entitlement تولید `DenyAll` است.

**۳. گزینه‌ها (از S3-0h §7.2).** H1: همیشه Trade را هم Migrate کن. H2: فقط صریح (`--include-trade`). H3: فقط وقتی Entitlement فعال است. H4: Host تغییر نکند؛ Trade فقط ابزار دستی/CI.

**۴. پیامدها به‌تفکیک سناریو.**
| سناریو | H1 | H2 (پیاده‌شده) | H3 | H4 |
|---|---|---|---|---|
| نصب بدون لایسنس Trade | Schema خالی `trade` و History جدا | بدون تغییر | بدون تغییر | بدون تغییر |
| نصب Dedicated | یکنواخت | اپراتور بعد از خرید اجرا می‌کند | خودکار پس از اتصال مجوز | دستی |
| نصب Shared (یک DB) | یک‌بار برای کل DB | یک‌بار برای کل DB هنگام اولین مشتری Trade | وابسته به مجوز کدام Tenant؟ مبهم | دستی |
| اجرای مجدد | Idempotent | Idempotent (تست) | وابسته | وابسته |
| Backup/Recovery | Schema و Backup بیشتر در همه نصب‌ها | فقط نصب‌های Trade | فقط مجوزدارها | نامنظم |
| تغییر نسخه/Rollback | شکست Trade روی همه اثر می‌گذارد | فقط نصب‌های Trade | وابسته | خطای انسانی |
| خطای Core/Trade | Core ماندگار؛ Trade ناموفق ⇒ کد ۲ | همان (تست‌شده) | همان | بدون Host |
| ممیزی اجرا | لاگ Host؛ ثبت `SecurityAuditEvent` پیاده نیست | همان | همان | ندارد |
H3 امروز ممکن نیست (Entitlement `DenyAll`، وابسته به #391/#401).

**۵. ریسک.** H2: فراموشی اجرا ⇒ Trade کار نمی‌کند، ولی fail-closed است. H1: Schema برای مشتری بدون مجوز بدون تأیید محصولی. H3: وابستگی ساختی Licensing↔Migration.

**۶. هزینه.** H2 کمترین (پیاده‌شده)؛ H1 کم؛ H3 بالا؛ H4 بدون کد ولی ریسک انسانی.

**۷. پیشنهاد فنی (Proposed): H2 (opt-in صریح) بماند** تا Entitlement و Deployment Policy معتبر وصل شود؛ سپس H3 با ADR جدا. اجرا روی دیتابیس مشتری همیشه با مجوز عملیاتی جداگانه و Backup. در Shared: یک اجرا برای کل دیتابیس نه به‌ازای Tenant. ثبت رویداد ممیزی اجرای Migration (Backlog، نه در این PR).

**۸. تأیید مالک.** ابقای H2؛ تأیید محصولی اگر H1 خواسته شود؛ مسئول اجرای `--include-trade` و رویهٔ Backup.

**۹. معیار پذیرش.** تست‌های موجود `DatabaseMigrationHostSqlServerTests` سبز؛ رویهٔ عملیاتی (Backup/Recovery، مجوز اجرا، ثبت ممیزی) مستند؛ اتصال Entitlement فقط با ADR/PR جداگانه.

**۱۰. وابستگی.** اتصال Entitlement (#391/#401)؛ OD-14 (Deployment Policy)؛ OD-16 (چندنمونه‌ای)؛ OD-11 (ستون جدید Trade).

## جمع‌بندی برای مالک
| OD | پیشنهاد | نیاز به تأیید |
|---|---|---|
| OD-11 | A: Host-level + `(TenantId, CompanyId)`، بدون FK | بله |
| OD-12 | حداقل Downtime؛ Rename فیزیکی اختیاری و جدا | بله |
| OD-13 | Manifest تأییدشدهٔ مالک، Claim فقط شاهد | بله |
| OD-14 | نمونه/DB مستقل به‌ازای Tenant؛ Shared Runtime پس از G6 و آزمون بار | بله |
| OD-17 | H2 opt-in باقی بماند | بله |

ADRهای `Proposed` متناظر در `docs/DECISIONS.md`: D-090 تا D-094. هیچ‌کدام Accepted نشده‌اند.
