# تحویل وظیفه — ISSUE-UNNUMBERED-trade-stage0-status-audit

- **Issue:** UNNUMBERED (مأموریت «Master Mission — Farano Foreign Trade»، Stage 0)
- **Agent/مسئول:** Claude (Lead)، برای ماژول Trade؛ Vendor در مالکیت GLM است
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (Stage 0 فقط؛ مستندات‌محور)؛ ویرایش دوم 2026-11-22 پس از بازبینی مالک (بخش «اصلاحیه» در انتهای سند) — Stage 1 فنی تا تأیید صریح مالک مسدود است

## هدف و معیارهای پذیرش
- [x] ثبت SHA واقعی `main`، وضعیت working tree، PRها و Issueهای باز و ادغام‌شدهٔ مرتبط با Trade از GitHub واقعی
- [x] ماتریس وضعیت (Area / پیاده‌سازی فعلی / کار باقی‌مانده / وابستگی مسدودکننده / شواهد)
- [x] فهرست کار انجام‌شده (تکرار نشود)، نیمه‌کاره، باقی‌مانده، تصمیم‌های نیازمند تأیید مالک
- [x] فایل‌ها و PRهای هم‌پوشان با دامنهٔ Vendor (GLM) و ریسک تداخل با `main`
- [x] بدون هیچ تغییر کد، Schema، Migration یا runtime
- [x] بدون ویرایش فایل‌های مشترک (`ROADMAP`، `PROJECT_STATUS`، `WORK_LOG`، `INDEX`، `DECISIONS` و …)

## خلاصه تغییرات
- فقط دو سند handoff افزوده شد: همین سند و ضمیمهٔ تکمیلی `docs/handoffs/ISSUE-UNNUMBERED-trade-stage0-supplement-s3-0-audit.md` (تفکیک تصمیم‌های Tenant، شکاف‌های `CustomerReference`↔D-089، سری‌های OD، جدول S3-0a..h، برنامهٔ کم‌هزینه و فهرست تصمیم‌های مالک).
- هیچ ردیفی به `docs/WORK_LOG.md` و `docs/INDEX.md` اضافه نشد (انحراف آگاهانه از بند ۵ `AGENTS.md`؛ دلیل در بخش ۷). افزودن آن‌ها پس از تعیین ترتیب ادغام با PR #743 پیشنهاد می‌شود.

## فایل‌های مهم تغییرکرده
- `docs/handoffs/ISSUE-UNNUMBERED-trade-stage0-status-audit.md` — سند جدید
- `docs/handoffs/ISSUE-UNNUMBERED-trade-stage0-supplement-s3-0-audit.md` — ضمیمهٔ تکمیلی (سند جدید)

## ۱. شواهد پایه (راستی‌آزمایی‌شده در این اجرا)

| مورد | مقدار | منبع |
|---|---|---|
| SHA `origin/main` | `ac0ec74115ced3988bc609e1e50ba46866b0920f` (2026-11-21 17:43:55 +0100، «Stage 5: vendor lifecycle final audit …») | `git fetch --depth=1 origin main` + `git log` |
| Working tree | تمیز (`git status --short` خالی) | clone تازه و shallow (`--depth 1`)؛ **وضعیت working tree محلی مالک را نشان نمی‌دهد** |
| CI روی همان SHA | ۱۶ check run، همه `success`: build-and-test، client، e2e، publish، publish-windows، publish-scripts، build-test-package، Semgrep، secret-scan، چهار Analyze (CodeQL)، Windows CNG signer tests، دو regression چک‌پوینت | `gh api commits/<sha>/check-runs` |
| PR باز | فقط **#743** (`docs/pr0-align-vendor-docs`، «PR-0 — align current-state docs with main@ac0ec741»)، آخرین به‌روزرسانی 2026-11-22T05:28Z | `gh api pulls?state=open` |
| PR باز Trade | هیچ | همان |
| Issueهای باز مرتبط با Trade | #397 (مرز ماژول Trade؛ باز)، #401 (entitlement `TRADE_IMPORT`؛ آخرین به‌روزرسانی 2026-11-04) | `gh api issues` |
| Issueهای باز Vendor/Licensing (مالکیت GLM) | #393، #396، #394، #392، #389، #384، #364، #236، #235، #228، #214، #107 و چند Issue غیر Trade | همان |

نکتهٔ مالکیت: همهٔ commit/PRها با یک حساب GitHub ثبت می‌شوند؛ پس تفکیک «کار GLM» از «کار مالک» از روی نویسنده ممکن نیست. در این سند تمام PRهای با موضوع `vendor`/`licensing`/`provisioning` به‌عنوان دامنهٔ GLM/Vendor فرض شده‌اند (**فرض، نه واقعیت تأییدشده**).

## ۲. ماتریس وضعیت Trade

برچسب «بررسی‌شده در کد» یعنی در این اجرا فایل/ثبت را در `main@ac0ec741` دیدم؛ «از مستندات» یعنی ادعا فقط در docs آمده و دوباره راستی‌آزمایی نشده.

| Area | پیاده‌سازی فعلی | کار باقی‌مانده | وابستگی مسدودکننده | شواهد |
|---|---|---|---|---|
| مرز ماژول و پروژه‌ها | `Trade.Domain/Application/Infrastructure` در `Modules/Trade`، API در `Server/Api/Trade`، UI در `ClientApp/src/features/trade` | — | — | بررسی‌شده در کد؛ PR #398 (از `PROJECT_STATUS`) |
| Persistence | `TradeDbContext`، schema `trade`، DI و design-time factory؛ یک Migration (`20261113072823_InitialTradeMasterData`) فقط برای `Party`، `PartyRoleAssignment`، `Product`. `PartyBankAccount` و `ProductIdentifier` عمداً از Migration بیرون‌اند | هیچ جدول عملیاتی (ImportCase، Proforma، …) وجود ندارد | گیت S3-0g برای هر Schema وابسته به Tenant | بررسی‌شده در کد؛ ROADMAP S2-1 (#641) |
| وضعیت DB زنده | روی Farano-Local هیچ جدول `trade` و `__TradeMigrationsHistory` نبود | هیچ Migration Trade روی DB مشتری اجرا نشده | سیاست اجرای Migration (D-094 `Proposed`) | از مستندات: S3-0c-b (2026-11-14) |
| کاتالوگ مجوز | ۴۶ مجوز + Migration داده‌ای seed در Core | — | — | از مستندات (#402) |
| دروازهٔ دسترسی | `TradeAccessGate` (نشست ← عضویت ← WorkContext/دوره ← entitlement ← مجوز) + `GET /api/v1/trade/capabilities` | — | — | بررسی‌شده در کد؛ تست‌های Architecture/Integration موجودند |
| **Entitlement `TRADE_IMPORT`** | `DenyAllTradeModuleEntitlement` **بدون شرط محیط** ثبت است (`Server/Program.cs:285`)؛ endpointهای عملیاتی، وقتی بررسی‌های قبلی `TradeAccessGate` موفق باشند (ورود معتبر/عضویت فعال و context، و برای نوشتن، دورهٔ قابل‌نوشتن یا عملیات صراحتاً مستقل از دوره)، در همهٔ محیط‌ها `403 trade_module_unavailable` برمی‌گردانند؛ در درخواست نوشتن طی دورهٔ ReadOnly، پیش از بررسی entitlement ابتدا `409 trade_period_read_only` برمی‌گردد (`Modules/Trade/Trade.Application/Authorization/TradeAccessGate.cs`، `Server/Api/Trade/TradeAccessFilter.cs:139-140`). Fail-Closed حفظ می‌شود: در هیچ مسیری عملیات Trade بدون entitlement پذیرفته نمی‌شود. سمت Vendor/Licensing: `EntitlementPolicyEngine` فقط وقتی `TRADE_IMPORT` در فهرست modules شرکت باشد می‌پذیرد؛ `CustomerCompanyEntitlementResolver` در provisioning هست اما **به `ITradeModuleEntitlement` وصل نیست** | Resolver واقعی سمت Trade؛ تست‌های entitlement (معتبر، لغوشده، منقضی، ناشناخته) | اتصال runtime به Licensing: #401 (باز)، #384، #391؛ طبق WORK_LOG گیت v5 خاموش و Migration مشتری روی DB واقعی اجرا نشده (#228/#392) | بررسی‌شده در کد (Program.cs، فایل‌های resolver و engine)؛ باقی از مستندات |
| Party | UseCase + API `/api/v1/trade/parties` + UI `/trade/parties` (S2-3) | حذف/غیرفعال‌سازی (D-083 باز)؛ حساب بانکی طرف‌حساب؛ ممیزی نقص (Stage 3) | D-083؛ برای حساب بانکی: Schema | بررسی‌شده در کد؛ PR #643 |
| Product | UseCase + API `/api/v1/trade/products` + UI `/trade/products` (S2-4) | شناسه‌ها و ارتباط HS Code؛ ممیزی نقص | D-085 هنوز `Proposed`؛ Schema | بررسی‌شده در کد؛ PR #644 |
| داده‌های مرجع | `TradeReferenceData`، `CountryData`، endpoint مرجع (S2-2) | ممیزی کفایت | — | بررسی‌شده در کد؛ #640 |
| منطق خالص دامنه | `TradeDocumentNumber` (#702)، تبدیل واحد و Snapshot مقدار (#704)، `PaymentPlanCalculator`/`PaymentPlan` (#705، Issue #710) — همگی بدون Schema | استفادهٔ مجدد در Proforma؛ فرمول محاسبهٔ مبالغ Proforma و تلورانس تناژ تعریف نشده (`TRADE_S3_ARCHITECTURE.md:84`) | تصمیم مالک برای فرمول/تلورانس؛ P-4/P-5 از D-080 باز | بررسی‌شده در کد + مستند |
| Migration Host | `--migrate` فقط Core، `--migrate --include-trade` اول Core سپس Trade؛ قفل `sp_getapplock`؛ کد خروج `0..4` (#665، با دستور صریح مالک) | سیاست نهایی Trade در نصب فاقد مجوز، Shared/Dedicated، Backup/Recovery، ممیزی اجرا | D-094..D-096 (`Proposed`) | از مستندات؛ تست `DatabaseMigrationHostSqlServerTests` |
| گیت S3-0 | a، b، c-a، c-b (Farano-Local)، d-a انجام؛ d-b/e/f(باقی)/g/h باز یا `Proposed` | تأیید مالک بر طرح تجمیعی S3-0h و OD-ها | تصمیم مالک | ROADMAP سطرهای S3-0a..h |
| Guardهای Tenant/Company | #656 (PR-2a)، #660 (PR-2b)، #661 (PR-SS-1)، #662 (PR-3)، #673، #674، #675 (G3) ادغام‌شده (#663/#664 سخت‌سازی Admin-role/audit مربوط به D-088 است، نه PR-E1 طرح S3-0h) | PR-4 (Schema Tenant) به بعد | G0 (تأیید مالک)، OD-13 (OD-6/6a با D-089 بسته شده‌اند) | فهرست PRهای ادغام‌شده از GitHub |
| پروفایل فعالیت (S3-1)، ImportCase (S3-2)، Proforma (S3-3)، OrderRegistration (S3-4)، اسناد/Maker-Checker (S3-5) | **پیاده‌سازی نشده**؛ هیچ کلاس `ImportCase`/`Proforma`/`OrderRegistration` در کد نیست | همه | گیت S3-0g؛ D-086 (Accepted)؛ D-104 (Accepted، NotImplemented) | جست‌وجوی کد (نتیجهٔ خالی) + ROADMAP |
| S4 (Voyage، Shipment، گمرک)، ارز، هزینه‌یابی/گزارش | `NotImplemented` | همه | تکمیل S3 | ROADMAP |
| Excel Import | فقط طراحی `Proposed` (D-106، EXC-1..EXC-17 هیچ‌کدام Accepted نیست)؛ انتخاب کتابخانه باز | همه | تصمیم مالک دربارهٔ کتابخانه و EXC-ها؛ پایدار شدن Entityها | `EXCEL_IMPORT_DESIGN.md`، D-106 |
| E2E Trade | تست‌های `tests/Template.Tests` (۲۳ فایل مرتبط با Trade، شمارش فایل نه تعداد تست) و ۱۰+ فایل تست UI در `features/trade`؛ E2E مسیرهای کامل Trade ثبت نشده | Stage 12 | تکمیل Entityها | بررسی‌شده در کد |

## ۳. کار انجام‌شده که نباید تکرار شود
- پایهٔ ماژول، Persistence، Migration Party/Product، کاتالوگ مجوز، دروازهٔ دسترسی، endpoint capabilities.
- Party و Product (API + UI + تست) و داده‌های مرجع.
- `TradeDocumentNumber`، تبدیل واحد/Snapshot، اعتبارسنجی و تقسیم طرح پرداخت (Issue #710).
- Migration Host (#665) و Guardهای Migration (PR-2a/2b)، تست SQL Server کنترل‌شدهٔ Rename (PR-SS-1)، قراردادهای مالکیت و تست معماری (PR-3)، تست‌های G3.
- **نکتهٔ مهم برای Stage 1:** فهرست «کار بالقوه» در متن مأموریت (Migration guards، constraint-name pinning، migration generator validation) تا حد زیادی همین حالا در `main` است؛ دوباره ساخته نشود.

## ۴. نیمه‌کاره
- S3-0d-b: inventory (#670) و تست تأییدی (#674) ادغام شده، ولی ROADMAP آن را تا تأیید مالک «Done» نمی‌داند.
- S3-0f: Migration Host انجام؛ سیاست و استقرار باقی‌مانده باز.
- D-088: پیاده‌سازی `Partial`.
- Entitlement Trade: موتور تصمیم و resolver سمت مشتری هست، اتصال به `ITradeModuleEntitlement` نیست.

## ۵. تصمیم‌های نیازمند تأیید مالک (فهرست؛ جزئیات در خود ADR/سند)
- D-090..D-096 (`Proposed`؛ تأیید مالک لازم است): یکپارچگی Tenant در Trade (D-090)، Downtime/Rename (D-091)، مرجعیت manifest Backfill (D-092)، مدل Shared/Multi-Tenant (D-093)، سیاست Migration Trade (D-094)، نام قیدهای legacy (D-095)، Migration چندنمونه‌ای (D-096).
- D-088 و D-089 `Accepted` هستند و تصمیم باز نیستند (پیاده‌سازی D-088 `Partial`، D-089 `NotImplemented`). OD-6/OD-6a با D-089 بسته شده‌اند؛ «شفاف‌سازی‌های بازبینی» داخل D-089 که صراحتاً `Proposed` هستند در ضمیمهٔ تکمیلی فهرست شده‌اند.
- طرح تجمیعی S3-0h (سند handoff؛ `Proposed`).
- D-106 (Excel Import): EXC-1..EXC-17 و انتخاب کتابخانه.
- D-083 (حذف/غیرفعال‌سازی Party/Product)، D-085 (`Proposed`)، P-4/P-5 از D-080.
- `TRADE_OPEN_DECISIONS.md`: ۲۲ مورد `OD-001..OD-022` (قالب صفرپُر؛ مستقل از سری Licensing با `OD-01..OD-27` و سری S3-0h با `OD-1..OD-18` و `OD-6a`)، همگی `Open` (شمارش خودکار وضعیت‌ها).
- فرمول محاسبهٔ مبالغ Proforma و تلورانس تناژ: طبق `TRADE_S3_ARCHITECTURE.md:84` تا صریحاً تعریف نشود حدس زده نمی‌شود؛ P-4/P-5 از D-080 نیز باز است (`TRADE_S3_ARCHITECTURE.md:154-155`). این‌ها در سری `OD-00x` نیستند.
- فعال‌سازی Production برای `TRADE_IMPORT` (نیازمند تأیید صریح و اتصال تأییدشدهٔ Vendor).

## ۶. هم‌پوشانی با Vendor (GLM) و ریسک تداخل
- PR باز #743 این فایل‌های **مشترک/پرتعارض** را تغییر می‌دهد: `docs/PROJECT_STATUS.md`، `docs/ROADMAP.md`، `docs/WORK_LOG.md` (به‌علاوه `LICENSING_WORKFLOWS.md` و `VENDOR_LIFECYCLE_FINAL_AUDIT.md`). هر تغییر Trade روی این سه فایل پیش از ادغام #743 تعارض مستقیم می‌سازد.
- مسیرهای دامنهٔ Vendor که در Trade نباید ویرایش شوند: `Vendor/`، `Server/Infrastructure/Provisioning/`، `Licensing.Contracts/` (Trade فقط ثابت `LicenseSubsystemCodes.TradeImport` را مصرف می‌کند).
- این PR هیچ‌یک از فایل‌های بالا را تغییر نمی‌دهد (فقط دو فایل جدید در `docs/handoffs/`)؛ تداخل فایل با #743 صفر است.
- مرج‌پذیری روی Head اول (`d7bda0c1`) از GitHub خوانده شد: `mergeable=true` و `mergeable_state=clean` (2026-11-22). این مقدار با هر push جدید باید دوباره خوانده شود؛ مقدار Head نهایی در توضیحات PR ثبت می‌شود.

## ۷. تناقض‌ها، کهنگی مستندات و انحراف‌ها
1. **نام‌گذاری دو‌لایهٔ S3-0e/f/g/h:** سرفصل‌های ROADMAP با نام خروجی‌های handoff یکی نیستند؛ توضیح رسمی در ردیف S3-0h ROADMAP هست. در این سند هر جا لازم بود هر دو نام آمده است.
2. **کهنگی `PROJECT_STATUS.md`:** بخش‌هایی (مثلاً «PR #703 باز است») با GitHub فعلی نمی‌خواند (#703 در 2026-11-18 ادغام شده). فایل مشترک است و ویرایش نشد؛ #743 ممکن است بخشی را اصلاح کند.
3. **Entitlement در همهٔ محیط‌ها بسته است** (حتی Development)؛ طبق کامنت Issue #401 این سخت‌گیرانه‌تر از D-063 بند ۵ است و «تأیید مالک» در #407 درخواست شده بود. تأیید ثبت‌شده‌ای را در این بررسی نیافتم (**راستی‌آزمایی کامل نشد**).
4. **وضعیت Issueها:** چند Issue Vendor (#393، #396، #392 و …) باز هستند با وجود PRهای ادغام‌شده؛ دامنهٔ Vendor است و بررسی یا ویرایش نشد.
5. **انحراف از `AGENTS.md` بند ۵:** ردیف `WORK_LOG` و `INDEX` اضافه نشد چون متن مأموریت این فایل‌ها را پرتعارض اعلام کرده و #743 همین حالا `WORK_LOG` را تغییر می‌دهد. پیشنهاد: پس از ادغام #743، یک PR مستندات بسیار کوچک این دو ردیف را append کند.
6. **محدودیت‌های این بررسی:** clone سطحی (عمق ۱)؛ تاریخچهٔ طولانی و blame در دسترس نبود. اسناد حجیم (`DECISIONS`، `WORK_LOG`، `PROJECT_STATUS`، `ROADMAP`) با جست‌وجوی هدفمند و خواندن بخش‌های مرتبط بررسی شدند، نه خوانش کامل. متن `SECURITY.md`، `DATABASE.md`، `TESTING.md`، `TRADE_DESIGN_V3_2.md` و ADRهای غیرمرتبط در Stage 0 خوانده نشد؛ Stage بعدی باید بخش‌های لازم را بخواند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet restore/build/test` | **اجرا نشد** | Stage 0 مستندات‌محور است؛ هیچ کدی تغییر نکرد؛ `dotnet` در این اجرا نصب/اجرا نشد |
| `python3 scripts/check_static_assets.py` | **اجرا نشد** | بی‌ارتباط با تغییر مستندات |
| `npm` (typecheck/test/build) | **اجرا نشد** | بی‌ارتباط با تغییر مستندات |
| تست‌های SQL Server | **اجرا نشد** | هیچ Schema/کدی تغییر نکرد |
| CI روی `main@ac0ec741` | ۱۶ مورد `success` (شواهد GitHub) | مربوط به SHA پایه است، نه این PR؛ ادعای «۱۶۶/۱۶۶ تست Trade» در مستندات تاریخی است و دوباره راستی‌آزمایی نشد |
| CI روی head اول این PR (`d7bda0c1`) | ۱۴ مورد `success`، ۱ مورد `neutral` (CodeRabbit)، صفر شکست | جدول کامل در بخش «وضعیت CI» پایین |
| `git diff --check` | موفق (کد خروج 0) | پیش از commit اول اجرا شد |

## امنیت، داده و سازگاری
- هیچ تغییری در احراز هویت، مجوز، Tenant/Company، Schema، Migration یا داده نیست.
- `DenyAllTradeModuleEntitlement` دست‌نخورده و Trade همچنان fail-closed است.

## ریسک‌ها و کارهای باقی‌مانده
- **Stage 1 فنی مسدود است:** طرح S3-0h و D-090..D-096 `Proposed` هستند؛ طبق متن مأموریت و ROADMAP تا تأیید صریح مالک هیچ PR فنی Schema/Backfill مجاز نیست.
- ریسک تداخل مستندات مشترک با #743 تا زمان ادغام آن.
- Stage 2 (entitlement) به قرارداد Vendor وابسته است؛ باید خوانش دقیق `CustomerCompanyEntitlementResolver` و `EntitlementPolicyEngine` و ارزیابی «آیا قرارداد فعلی برای Trade کافی است» در آغاز Stage 2 انجام شود (در Stage 0 فقط وجود آن‌ها تأیید شد).
- ممیزی نقص Party/Product (Stage 3) انجام نشد؛ فقط inventory ثبت شد.

## وضعیت CI (شواهد واقعی GitHub)
Head اول PR: `d7bda0c1488a634f6b2eba8a71b59a3ddbe92405` (خوانده‌شده با `gh api repos/hamidreza1394/accounting/commits/<sha>/check-runs`، ۱۵ check run، همگی `completed`، 2026-11-22):

| Check | نتیجه |
|---|---|
| build-and-test | success |
| client | success |
| e2e | success |
| publish | success |
| publish-windows | success |
| publish-scripts | success |
| secret-scan | success |
| Semgrep scan | success |
| Semgrep OSS | success |
| CodeQL | success |
| Analyze (actions) | success |
| Analyze (csharp) | success |
| Analyze (javascript-typescript) | success |
| Analyze (python) | success |
| CodeRabbit | neutral (ربات بازبینی؛ نه شکست و نه تأیید تست) |

نکتهٔ حدود ادعا: این جدول فقط دربارهٔ Head اول است. با هر commit اصلاحیه، Head عوض می‌شود و CI از نو اجرا می‌شود؛ SHA و نتیجهٔ Head نهایی را نمی‌توان داخل همان commit نوشت، بنابراین در توضیحات PR (که بدون commit قابل ویرایش است) ثبت می‌شود.

## اصلاحیه پس از بازبینی مالک (2026-11-22)
موارد نادرست یا ناقص نسخهٔ اول این سند و اصلاح آن‌ها:
1. **OD-6/OD-6a اشتباهاً «تصمیم باز با اولویت P0» ثبت شده بود.** هر دو با D-089 (`Accepted`، 2026-11-15) بسته شده‌اند (متن D-089: «OD-6 و OD-6a … با این تصمیم بسته می‌شوند»). در بخش‌های «تصمیم‌های نیازمند تأیید مالک» و «ریسک‌ها» اصلاح شد.
2. **ابهام شناسه‌ها:** `OD-6` (سری S3-0، بدون صفر) با `OD-006` (سری محصول Trade در `TRADE_OPEN_DECISIONS.md`، موضوع IMO کشتی) دو سری مستقل‌اند؛ تحلیل در ضمیمهٔ تکمیلی.
3. **فرمول جمع Proforma** به‌اشتباه به سری `OD-00x` نسبت داده شده بود؛ مرجع درست `TRADE_S3_ARCHITECTURE.md:84` و P-4/P-5 از D-080 است.
4. **CI** در نسخهٔ اول ثبت نشده بود؛ اکنون ثبت شد.
5. اشارهٔ نادرست «(بخش ۸)» در سربرگ حذف شد (این سند بخش ۸ ندارد).
6. **پاسخ بازبینی CodeRabbit:** ردیف Entitlement دقیق‌تر شد؛ `trade_module_unavailable` فقط پس از موفقیت بررسی‌های قبلی Gate برمی‌گردد و نوشتن در دورهٔ ReadOnly ابتدا `409 trade_period_read_only` می‌دهد.

### یادداشت دربارهٔ مبنای تاریخی (2026-11-22)
ممیزی این سند و ضمیمهٔ آن **روی `main@ac0ec741`** انجام شد و پس از ادغام #743 دوباره اجرا نشده است. بعداً PR #743 در 2026-11-22T06:06:14Z ادغام شد و `main` به `eba5e3368dda0c2e859fd9f204603f073b065cf0` رسید؛ بنابراین عبارت‌های «PR باز #743» و «تداخل صفر با #743» در این سند به زمان ممیزی برمی‌گردند و وضعیت `ROADMAP`/`PROJECT_STATUS`/`WORK_LOG` روی `main` فعلی بازبینی نشده است. تطبیق دوبارهٔ مبنا (rebase/به‌روزرسانی ممیزی) فقط با دستور مالک انجام می‌شود.

## شناسه‌های Git
- Branch: `docs/trade-stage0-status-audit`
- Base SHA: `ac0ec74115ced3988bc609e1e50ba46866b0920f`
- Commit اول: `d7bda0c1488a634f6b2eba8a71b59a3ddbe92405`
- Commitهای اصلاحیه: در توضیحات PR ثبت می‌شوند (SHA یک commit را نمی‌توان داخل خودش نوشت)
- Pull Request: #744 (`https://github.com/hamidreza1394/Accounting/pull/744`)
