# طرح اجرایی مرحلهٔ عملیاتی بعدی Trade (Workstream C)

- **Issue:** UNNUMBERED (ادامهٔ S3-2..S3-4 / Issue #397)
- **مبنا:** `main@5484758722d0a316511806cf3097c5c691875599`. کد Trade از `ac0ec741` تغییر نکرده است.
- **وضعیت سند:** فقط مستندات و طراحی؛ هیچ کد، Schema یا Migration تغییر نکرده است. همهٔ بندهای «پیشنهاد» [P] تا تأیید مالک اجرا نمی‌شوند.
- **برچسب:** [V] در کد دیده شد؛ [D] فقط در مستندات؛ [P] پیشنهاد؛ [O] قاعده‌ای که مالک در متن مأموریت اعلام کرده ولی هنوز در ADR ثبت نشده است.
- **روش:** بازبین `architect` فقط‌خواندنی؛ ادعاهای پرتأثیر او (نبود کلاس‌های ImportCase/Proforma/OrderRegistration، `TradeEntity.TenantId`=Company.Id، کاتالوگ مجوزها، `OD-017` سری Trade، شماره‌های D-080/D-086/D-104/D-105) توسط Lead با کد/مستندات تطبیق داده شد.

## ۰) قواعد اعلام‌شده توسط مالک و وضعیت ثبت آن‌ها

قواعد زیر در متن مأموریت مالک (2026-11-22) به‌عنوان «تصمیم‌های کسب‌وکاری مصوب» آمده‌اند. در جست‌وجوی `docs/` شاهد ADR برای موارد ۱–۴ پیدا نشد؛ لذا وضعیت آن‌ها [O] است و طرح آن‌ها را مبنا می‌گیرد، اما **ثبت رسمی در ADR (با تأیید صریح مالک) پیش‌شرط PR-2** است. هیچ ADR جدیدی با وضعیت Accepted در این PR ثبت نشده است.

| # | قاعده | شاهد در مخزن |
|---|---|---|
| ۱ | یک Proforma ↔ یک OrderRegistration | D-104 (`Accepted`) [D] `docs/DECISIONS.md:2715` |
| ۲ | Proforma چندکالایی با HS Codeهای متفاوت؛ خودکار پروندهٔ جدا نمی‌سازد | [O]؛ `TRADE_S3_ARCHITECTURE.md:39` می‌گوید «پیش‌فاکتور یک کالا دارد» (پیش‌فرض فعالیت فعلی، D-086 §3.2 آن را تنظیم‌پذیر می‌داند) |
| ۳ | تمدید صرف اعتبار = رویداد تاریخچه، بدون Revision؛ تغییر شرایط تجاری = Revision | [O] |
| ۴ | مالکیت Voyage شرکت‌محور | D-063 بند ۱ [D] (`docs/DECISIONS.md:1048`) |
| ۵ | کالا (Product) تا طراحی کاتالوگ Inventory در Trade می‌ماند | [O] |
| ۶ | `TradeOperationalPeriod` ساخته نشود | دستور مأموریت؛ D-086 §7: نوشتن مستقل از دوره |

---

## ۱) وضعیت فعلی [V]
- هیچ کلاس `ImportCase`/`Proforma`/`OrderRegistration` در `Modules`/`Server` وجود ندارد؛ `TradeDbContext` فقط Party، Product و فرزندانشان را دارد (`TradeDbContext.cs:15-23`).
- Trade در همهٔ محیط‌ها Fail-Closed است (`Server/Program.cs:284-285`، `DenyAllTradeModuleEntitlement`)؛ این طرح به آن دست نمی‌زند.

## ۲) گیت‌ها — دقیق
- **تمام Schemaهای S3 مسدود است، نه فقط ستون‌های Tenant/Company:** ROADMAP می‌گوید S3-0 باید پیش از هر کدنویسی تغییردهندهٔ Schema بسته شود (`docs/ROADMAP.md:257`) و تا تصویب S3-0g/h هیچ جدول/ستون Tenant جدید یا Backfill مجاز نیست (`:269`).
- **دلیل فنی [V]:** هر جدول جدید از `TradeEntity` ارث می‌برد که `TenantId` را نگه می‌دارد و در عمل `Company.Id` است (`TradeEntity.cs:25-26`، `TradeAuthorizationAdapter.cs:45`). ساخت جدول پیش از D-090 (`Proposed`) بدهی legacy می‌افزاید و پس از هر تغییر، کلید/ایندکس `(TenantId, …)` باید بازطراحی شود.
- **مسدودکننده‌ها:** بسته‌شدن S3-0 (G0..G5؛ جزئیات در بستهٔ آمادگی `ISSUE-UNNUMBERED-trade-s3-0-readiness-decision-package.md` (PR #747))، و در Trade: D-090، D-094/OD-17 (Tenant)، D-095، و OD-017 سری Trade (دقت ستون‌های مالی/تناژ؛ `Open`) [D] `docs/TRADE_OPEN_DECISIONS.md:165`.
- **بدون گیت قابل‌انجام:** کلاس‌های دامنهٔ خالص بدون نگاشت EF، Use Caseهای Application با port باریک و Store جعلی در پروژهٔ تست، تست واحد، ADR و مستندات. پیش از Migration نباید EF Configuration یا `DbSet` اضافه شود (ناهمخوانی با Snapshot).

## ۳) منطق موجود برای استفادهٔ مجدد (بازنویسی نمی‌شود) [V]
| کلاس | مسیر | کاربرد [P] |
|---|---|---|
| `TradeEntity` + `TradeEntityConfiguration` | `Trade.Domain/Common/TradeEntity.cs`، `Trade.Infrastructure/Persistence/Configurations/TradeEntityConfiguration.cs` | پایهٔ Entityهای جدید (پس از G0) |
| `TradeDocumentNumber.TryCreate` | `Trade.Domain/Common/TradeDocumentNumber.cs` | شمارهٔ پرونده، Proforma، ثبت‌سفارش (`…Display`/`…Normalized`) |
| `PaymentPlanCalculator.TryBuild`، `PaymentPlan` | `Trade.Application/Proformas/` | اعتبارسنجی پیش از ساخت Revision؛ بازیابی با بازسازی و مقایسه. **پیش از wire شدن تصمیم تخصیص لازم است** (بازبینی D: بیش‌تخصیص، انحراف درصد، `IRR`) |
| `TradeUnitConversion`، `TradeQuantitySnapshot.TryRestore`، `TradeRoundingPolicy` | `Trade.Domain/Units/*` | ستون‌های Snapshot مقدار روی خط Proforma |
| `TradeReferenceData.*` | `Trade.Application/ReferenceData/` | اعتبارسنجی ارز/Incoterm/واحد/کشور/روش پرداخت (مرجع بندر وجود ندارد → متن آزاد کنترل‌شده) |
| `TradeAccessGate` + `RequireTradePermission(periodIndependent: true)` | `Server/Api/Trade/` | همهٔ endpointهای جدید؛ نوشتن مستقل از دوره (D-086 §7) |
| الگوی Service + port باریک + Outcome + RowVersion | `Trade.Application/Products/ProductService.cs`، `IProductStore.cs` | `ImportCaseService`، `ProformaService`، `OrderRegistrationService`؛ Repository عمومی ساخته نمی‌شود |
| مجوزهای `trade.form.proforma.*` و `trade.form.import-case.*` | `TradePermissionCatalog.cs:35,37` | بدون Migration داده‌ای؛ برای `order-registration` فرمی در کاتالوگ نیست |

یادداشت لایه: `PaymentPlan` در Application است و Domain نمی‌تواند به آن ارجاع دهد؛ Entity مراحل را به شکل primitive می‌گیرد و Service پیش از ساخت `TryBuild` را صدا می‌زند [P].

## ۴) Entityهای پیشنهادی [P]
همه **Company-owned** هستند (D-105 بند ۱). امروز تنها ستون موجود `TenantId` (=`Company.Id`) است و ایندکس‌ها با همین ستون نوشته می‌شوند؛ معنای آن‌ها کسب‌وکاری Company-scoped است. همه مستقل از FiscalPeriod‌اند (D-086 §7.1). همهٔ شناسه‌های Tenant/Company فقط از `TradeAccessContext` می‌آیند؛ رکورد شرکت دیگر `404`؛ FKها `Restrict` و مرکب؛ حذف فیزیکی ندارند. **همهٔ جدول‌ها Migration لازم دارند و تا باز شدن گیت‌ها مسدودند.**

| Entity | هدف | فیلدهای کلیدی | قیود یکتایی | مجوز | ممیزی |
|---|---|---|---|---|---|
| ImportCase | پروندهٔ متناظر سامانهٔ جامع تجارت | شمارهٔ خارجی (Display/Normalized)، `Status` (v1: `Open`/`Cancelled`)، `ProfileVersion`، `ReplacesImportCaseId`، `GoodsLockedAtUtc` | `UX(TenantId, ExternalCaseNumberNormalized)` بدون فیلتر؛ `UX(TenantId, ReplacesImportCaseId)` WHERE NOT NULL | `trade.form.import-case.*` | Created/Updated؛ ابطال با دلیل/کاربر/زمان |
| Proforma (Header) | سرفصل پیش‌فاکتور | `ImportCaseId` (تغییرناپذیر)، شماره، `SupplierPartyId`، `Status` (`Draft`/`IssuedToBuyer`/`Accepted`/`Cancelled`/`Superseded`)، **`ValidUntil` روی Header**، `CurrentRevisionNumber` | `UX(TenantId, NumberNormalized)` فیلترشده روی وضعیت‌های فعال؛ یک Proforma جاری به‌ازای پرونده | `proforma.view/create/update` | تاریخچه در HistoryEvent |
| ProformaRevision | شرایط تجاری تغییرناپذیر | `RevisionNumber`، ارز، Incoterm، مبالغ مستقل (کل پرونده/محموله/کرایه)، `PaymentPlanFinalAmount`، `ChangeReason` | `UX(TenantId, ProformaId, RevisionNumber)`؛ AK برای FK مرکب | — | خود ردیف سابقه است |
| ProformaLine | خط کالا (چند HS مجاز) | Snapshot HS/نام، Snapshot مقدار (`TradeQuantitySnapshot`)، `UnitPrice` | `UX(TenantId, RevisionId, LineNumber)` | — | تغییرناپذیر |
| ProformaPaymentStage | مراحل طرح پرداخت | `Sequence`، `MethodCode`، `Percent?`، `Amount` | `UX(TenantId, RevisionId, Sequence)` | — | جزء Revision |
| ProformaHistoryEvent | رویداد نوع‌دار append-only | `Kind` (`Issued`/`Accepted`/`Cancelled`/`ValidityExtended`/`RevisionCreated`)، تاریخ‌های قبل/بعد، دلیل، کاربر | — | — | `ValidityExtended` فقط `ValidUntil` را عوض می‌کند و Revision نمی‌سازد |
| OrderRegistration | ثبت‌سفارش (۱:۱ با Proforma) | `ProformaId`، `BasedOnProformaRevisionId`، شماره، تاریخ‌ها، `Status` (v1: `Registered`/`Cancelled`) | `UX(TenantId, ProformaId)` بدون فیلتر؛ `UX(TenantId, NumberNormalized)` بدون فیلتر؛ FK مرکب به Revision | **فرم `order-registration` در کاتالوگ نیست** → Migration داده‌ای Core + تأیید جدا | ثبت قفل کالا در همان تراکنش |
| OrderRegistrationAmendment | تمدید/کاهش تناژ | نوع، قبل/بعد، دلیل، کاربر | — | — | append-only (بعد از اولین گردش) |

- `Expired` وضعیت ذخیره‌شده نیست و از `ValidUntil` مشتق می‌شود (P-4 باز است).
- قفل کالا پس از ثبت‌سفارش در Application و با rowversion پرونده اعمال می‌شود (CHECK میان‌جدولی ممکن نیست) → تست رقابتی SQL Server لازم است.
- به تعویق: `ImportCaseItem` و جدول تاریخچهٔ کالا (کالا روی `ProformaLine`؛ این انحراف از «راهنمای مفهومی» D-086 تأیید مالک می‌خواهد)، جدول پروفایل شرکت (v1: ثابت کدی با `ProfileVersion="v1"`)، Replace، Maker-Checker، اسناد.
- ممیزی: چارچوب عمومی ساخته نمی‌شود؛ سابقه با ردیف‌های تغییرناپذیر (Revision/HistoryEvent/Amendment) نگه داشته می‌شود. `SecurityAuditEvent` مخصوص رویدادهای امنیتی است و استفاده نمی‌شود. دقت اولیهٔ ستون‌ها (پیشنهاد، تابع OD-017): مبلغ `decimal(28,8)`، مقدار `(20,6)`، فی `(20,8)`، درصد `(7,4)`.

## ۵) توالی PRها [P]
ترتیب: ImportCase → Proforma (Revision، Line، PaymentStage در یک aggregate) → تاریخچهٔ Proforma → OrderRegistration → Amendment. شماره‌گذاری، مجوز و ممیزی جزء هر برش‌اند.

| PR | محتوا | وضعیت | Agent | معیار پذیرش |
|---|---|---|---|---|
| PR-1 | ADR: ثبت قواعد ۲، ۳، ۵ بخش ۰ + تعویق ImportCaseItem + پروفایل v1 | اکنون، **به شرط تأیید صریح مالک** | `documentation-engineer`، بازبین `architect` | ADR با شمارهٔ بعدی واقعی؛ Accepted فقط با تأیید مالک؛ بدون کد |
| PR-2 | Domain خالص (بدون EF/DbSet) + تست واحد | اکنون (پس از PR-1) | `backend-developer` | تست: شمارهٔ نامعتبر، Revision بدون mutator، تمدید بدون Revision، قفل کالا پس از OR، ۱:۱؛ مدل EF/Snapshot بدون تغییر |
| PR-3 | Application: Serviceها + portهای باریک + Store جعلی فقط در تست؛ بدون endpoint/DI | اکنون (پس از تصمیم تخصیص PaymentPlan) | `backend-developer` | Context فقط از `TradeAccessContext`؛ باقیماندهٔ دقیق؛ بازیابی طرح ناسازگار رد شود |
| PR-4 | EF Configuration + Migration جدول‌ها + کنترل `Deleted` | **Blocked**: بسته‌شدن S3-0 (G0..G5 مطابق `ISSUE-UNNUMBERED-trade-s3-0-readiness-decision-package.md`) به‌علاوهٔ موارد ویژهٔ Trade: D-090، D-094، D-095، OD-017 | `database-engineer` | Up/Down روی SQL Server؛ کدهای 2601/2627 برای هر یکتایی؛ دو شرکت با شمارهٔ یکسان مجاز |
| PR-5 | Store + endpointهای `/api/v1/trade/*` + تست HTTP | Blocked (PR-4) | `backend-developer` | `TradeEndpointGateTests` سبز؛ `404` شرکت دیگر؛ Anti-forgery؛ تست رقابتی |
| PR-6 | UI در `features/trade` | Blocked (PR-5) | `frontend-developer` | `UI_COMPONENTS.md`؛ typecheck/test/build/offline |
| PR-7 | Migration داده‌ای Core برای `trade.form.order-registration.*` | NeedsApproval (Core، DB مشتری) | `database-engineer` | فقط درج ردیف؛ تست کاتالوگ مجوز |
| PR-8 | OrderRegistration: Migration/Store/API/قفل کالا | Blocked (PR-4، PR-7) | `backend-developer`+`database-engineer` | OR دوم برای یک Proforma رد شود؛ FK مرکب کار کند |
| PR-9 | Amendment | Blocked (PR-8) | `backend-developer` | append-only |

بازبینی: `qa-engineer` + `code-reviewer` در همه؛ برای PR-4/5/7/8 علاوه بر آن `security-reviewer` و `architect` (opus).

## ۶) کوچک‌ترین گردش عملیاتی واقعی [P]
کاربر مجاز شرکت جاری: (۱) پرونده با شمارهٔ خارجی می‌سازد؛ (۲) Proforma با شماره، فروشنده، تاریخ‌ها، ارز، Incoterm، یک یا چند خط (Snapshot HS و مقدار پایه)، فی، سه مبلغ مستقل و طرح پرداخت ثبت می‌کند (Revision 1)؛ (۳) آن را مشاهده می‌کند. شمارهٔ تکراری در همان شرکت رد و در شرکت دیگر پذیرفته می‌شود. این گردش فقط پس از PR-5 «واقعی» است؛ پیش از آن فقط در سطح Application با Store جعلی قابل اثبات است و جای تست SQL Server را نمی‌گیرد.

## ۷) تناقض‌های مستندات/کد
1. یکتایی شمارهٔ Proforma: D-080 §3/§9 `(TenantId, SupplierPartyId, Normalized)` در برابر D-086 §3.3 کل شرکت؛ D-086 مقدم است.
2. یکتایی شمارهٔ ثبت‌سفارش: D-080 §9 «تا P-2 تعریف نمی‌شود» در برابر D-086 §4.2/§4.4؛ متن D-080 کهنه است.
3. FiscalPeriod: V3.2 به OrderRegistration/ImportCase ستون دوره می‌دهد؛ D-086 §7 هر دو را مستقل از دوره می‌داند.
4. نام مجوزها: V3.2 `trade.form.orderregistration.*` در برابر کاتالوگ (`import-case` دارد؛ `order-registration` ندارد).
5. تعداد کالا: `TRADE_S3_ARCHITECTURE.md:39` «یک کالا» در برابر قاعدهٔ مالک (چندکالایی) و D-086 §3.2 (تنظیم‌پذیر).
6. کاردینالیتی: D-086 §4.1 ظرفیت را تنظیم‌پذیر می‌داند اما ایندکس یکتا و ۱:۱ مقدار ۱ را در DB قفل می‌کند.
7. Fail-Closed Entitlement در همهٔ محیط‌ها با D-063 بند ۵ و D-074 (`Accepted`) سازگار است: D-074 صراحتاً «حفظ DenyAll تا آماده و تست‌شدن مسیر واقعی اعتبارسنجی» را می‌خواهد (`docs/DECISIONS.md:1513`). تناقضی وجود ندارد و هیچ اقدامی برای شل‌کردن آن پیشنهاد نمی‌شود.

## ۸) پرسش‌های باز مالک (پیش‌فرض پیشنهادی)
1. ثبت قواعد ۲، ۳، ۵ بخش ۰ در ADR تأیید می‌شود؟ — پیش‌فرض: PR-2 تا تأیید شروع نشود.
2. تعویق `ImportCaseItem` و نگهداشتن کالا فقط روی خطوط Proforma؟ — بله.
3. پروفایل v1 ثابت کدی؟ — بله.
4. `PaymentPlanFinalAmount` کل پرونده است یا محموله؟ — مبلغ صریح واردشده توسط کاربر، بدون استنتاج.
5. شمارهٔ Proforma لغوشده/جایگزین‌شده آزاد می‌شود؟ — بله (ایندکس فیلترشده).
6. Revision پس از ثبت‌سفارش؟ — ممنوع (Fail-Closed).
7. مجوز ابطال؟ — `…update` + دلیل اجباری؛ `delete` استفاده نشود.
8. واحد مقدار v1 فقط واحد پایهٔ کالا؟ — بله.
9. `Draft`/`Submitted` برای ثبت‌سفارش؟ — خیر.
10. روش تخصیص `PaymentPlan` (largest-remainder) و MinorUnits ریال (بازبینی D)؟ — largest-remainder؛ MinorUnits `IRR` تصمیم مالک.

## ۹) ریسک‌ها
- بازکاری پس از D-090 (جدا شدن `CompanyId`): کاهش با باز نکردن PR-4 پیش از G0.
- قفل کالا فقط در Application: نیاز به تست رقابتی SQL Server.
- درج مستقیم SQL ناسازگار با این طرح قابل جلوگیری نیست (D-090).
- دقت ستون‌ها تابع OD-017 است.

## ۱۰) محدودیت‌ها
Build/Test اجرا نشد؛ هیچ کدی نوشته نشد؛ مصرف توکن Agent ≈۱۹۱٬۶۷۸ توکن، ۳۳ فراخوانی ابزار (از اعلان تکمیل).
