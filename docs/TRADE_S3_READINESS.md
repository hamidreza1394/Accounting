# نقشهٔ آمادگی S3 برای Trade (TRADE_S3_READINESS)

**تاریخ:** 2026-11-18
**مبنای مخزن:** `origin/main@89c0064`
**وضعیت سند:** مستندات. این سند مجوز کد، Schema، Migration یا Backfill نیست.
**مراجع:** `ROADMAP.md` بخش S3، `handoffs/ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md` (طرح مرجع، `Proposed`)، `TRADE_S3_ARCHITECTURE.md`، `TRADE_TENANT_COMPANY_AUDIT.md`، `TRADE_DECISION_RECONCILIATION.md`، D-087..D-104.
**برچسب‌ها:** **[V]** در این گذر با مخزن بررسی شد؛ **[S]** از ROADMAP/handoffهای قبلی نقل شد و دوباره راستی‌آزمایی نشد.

> **به‌روزرسانی 2026-11-24 (مبنا `main@35bd390`):** G3 با اجرای واقعی روی SQL Server **Verified** شد (کل مجموعه ۱۶۲۳ تست، ۱۶۱۳ موفق، ۰ ناموفق، ۱۰ Skip نامرتبط؛ شش کلاس G3 جدا اجرا شد؛ جزئیات و نگاشت پوشش در `handoffs/ISSUE-397-s3-0-gap-closure-g3-g6-owner-package-2026-11-24.md`). این Verified فقط درستی رفتار SQL Server را نشان می‌دهد و **مجوز اجرای Migration نیست**. G6 (SP-1..SP-9) با کد و تست طبقه‌بندی شد؛ هیچ SP بدون Tenant یا تصمیم مالک بسته‌شدنی نیست. D-118 (هویت Tenant-scoped کاربران مشتری؛ `Accepted`، `NotImplemented`) در ماتریس هویت (`ISSUE-397-s3-0i-…`) لحاظ است. **S3-0 هنوز بسته نیست.** باز می‌مانند (`Proposed`/`Open`): D-091 (ترتیب اجرایی)، D-092..D-096، D-083، G0، G4 (باقی‌مانده)، G5، G6، OD-13، OD-17 و N-5a/N-5b (O-3). بندهای قدیمی زیر «ماتریس تاریخی» هستند و فقط با این به‌روزرسانی supersede شده‌اند؛ حذف نشدند.

> **به‌روزرسانی 2026-11-23:** D-090 `Accepted` شد؛ D-119 (Party/Product Company-owned)، D-120 (Rename فیزیکی + `TenantId` واقعی) و D-121 (ترتیب Core سپس Trade) `Accepted` و `NotImplemented` هستند. بنابراین عبارت «D-090..D-096 همچنان Proposed» و بسته‌نبودن گام ۱ جدول بخش ۴ دیگر معتبر نیست؛ D-090 و مالکیت Party/Product تصمیم‌گیری شده‌اند؛ D-091..D-096 و گیت‌های G0، G4 (باقی‌مانده)، G5، G6 باز می‌مانند. شکاف G4/OD-11 اکنون تصمیم نیست؛ پیاده‌سازی است و مسدود به زیرساخت Tenant در Core (که در سورس وجود ندارد). ترتیب PRها: `handoffs/ISSUE-UNNUMBERED-trade-tenant-company-owner-decisions-2026-11-23.md` بخش ۵.

## ۱. خلاصه

1. **پیش‌نیازهای کدمحور S3-0 تقریباً کامل‌اند [S]:** Guard Migration (PR-2a #656، PR-2b #660)، تست SQL Server ایزوله برای Rename و Migration Tenant (PR-SS-1، PR-D)، قراردادهای مالکیت (PR-3)، دروازهٔ Migration Host (#665) ادغام شده‌اند.
2. **مانع اصلی تصمیم است، نه کد:** *(تاریخی؛ از 2026-11-23 D-090 `Accepted` است و فقط D-091..D-096 `Proposed`ند)* D-090..D-096 همچنان `Proposed` هستند؛ تا تأیید صریح مالک، هیچ جدول Trade با `CompanyId`، ستون Tenant یا Backfill ساخته نمی‌شود.
3. **S3-1 (پروفایل فعالیت) هم جدول ماندگار می‌خواهد.** پس بدون بسته‌شدن گیت‌های زیر، مجاز نیست (بخش ۵).
4. **آنچه امروز بدون گیت ممکن است:** منطق دامنهٔ خالص و بدون Schema (مانند PR #704 و #705 و `TradeDocumentNumber`) و تست‌های غیرمخرب. این‌ها قابلیت تکمیل‌شدهٔ Proforma نیستند.
5. **D-104 (یک‌به‌یک Proforma/OrderRegistration)** سر راه S3-3 تغییر ایجاد می‌کند ولی مانع S3-0 تا S3-2 نیست.

## ۲. وضعیت هر زیرمرحلهٔ S3-0

| زیرمرحله | وضعیت ROADMAP | وضعیت واقعی/یادداشت | برچسب |
|---|---|---|---|
| S3-0a ممیزی ایستای اولیه | Done | — | [S] |
| S3-0b رفع تناقض اسناد معماری | Done | این مأموریت بررسی مجدد کرد؛ تعارض‌های باقی‌مانده در `TRADE_DECISION_RECONCILIATION.md` §۳ | [V] |
| S3-0c-a inventory مدل و Migration | Done | Snapshot هسته 33 نگاشت، Trade 5 نگاشت و یک Migration | [S] |
| S3-0c-b ممیزی زنده Schema (Farano-Local) | Done | هیچ جدول `trade` در آن دیتابیس نبود؛ نصب‌های دیگر Unknown | [S] |
| S3-0d-a بازبینی مالکیت V3.2 | Done | گروه B/C در V3.2 هنوز `TenantId=OwnerCompany` می‌نویسند (T-5) | [S]/[V] |
| S3-0d-b inventory کامل مسیرهای داده | **Not done** (آمادهٔ بازبینی) | بخش مسیرهای Trade در این گذر بازبینی شد؛ handoff موجود `ISSUE-397-s3-0d-b-data-path-inventory.md` نقطهٔ شروع است (`TRADE_TENANT_COMPANY_AUDIT.md` §۵)؛ HR/Admin/Export فقط از ممیزی قبلی | [V] جزئی |
| S3-0e نگاشت داده | **Not done** | منبع ایجاد Tenant: D-089 Accepted؛ مرجعیت Manifest: D-092 Proposed | [S] |
| S3-0f قرارداد Migration و استقرار | **Not done** (دو بخش) | Host (#665) Done؛ سیاست و استقرار: D-094..D-096 Proposed | [S] |
| S3-0g خروجی و گیت | **Not done** | این مأموریت ماتریس مالکیت، ERD و نقشه را به‌روز می‌کند؛ تأیید مالک لازم است | [V] |
| S3-0h طرح تجمیعی | Proposed | مرجع واحد پیاده‌سازی؛ بازنویسی نشد | [S] |

نتیجه: **S3-0 بسته نشده است.** بستن آن به تأیید مالک روی طرح S3-0h (گیت G0) و OD-13 و OD-17 بستگی دارد.

## ۳. گیت‌ها

| گیت | شرط | وضعیت |
|---|---|---|
| G0 | تأیید طرح S3-0h؛ OD-6/6a (D-089 Accepted)، OD-13 (D-092)، OD-17 (D-094) | **باز**: OD-6/6a بسته؛ OD-13/OD-17 `Proposed` |
| G1 | PR-2a ادغام و تست‌های Mutation سبز | ظاهراً بسته [S: #656] |
| G2 | PR-2b ادغام و تست‌های Rename سبز | ظاهراً بسته [S: #660] |
| G3 | اجرای واقعی روی SQL Server (`sp_rename` با Pin، رفت‌وبرگشت، FK مرکب با NULL، NOT NULL با Guid، شکست، اجرای تکراری، همزمانی) | **Verified (2026-11-24)** [V]: ۲۰ تست PR-D + ۵ + ۸ + ۳ + ۱۸ + ۱ روی SQL Server واقعی، همگی موفق؛ مجوز Migration نیست |
| G4 | OD-4، OD-5، OD-10، OD-11 (D-090)، OD-12 (D-091)، OD-18 | **باز** |
| G5 | Backup، رویهٔ بازیابی، Manifest بایگانی‌شده برای هر نصب هدف؛ مجوز جدا برای اجرا روی دیتابیس مشتری | **باز** |
| G6 | پیش‌نیازهای امنیتی SP-1..SP-9 پیش از Tenant دوم | **باز** (فقط فعال‌سازی Tenant دوم در Shared را متوقف می‌کند)؛ طبقه‌بندی SP با کد و تست در handoff 2026-11-24 [V] |

## ۴. موارد Blocked

| مورد | علت | چه چیزی بازش می‌کند |
|---|---|---|
| هر جدول Trade جدید با `CompanyId` (ImportCase، Proforma، OrderRegistration…) | *(به‌روز 2026-11-24: D-090 `Accepted` است)* منبع Company→Tenant و Core Tenant پیاده‌نشده (D-121) | Core Tenant + PR-4 (A1) + Schema مصوب |
| ستون `TenantId` واقعی روی `Companies` و جدول `Tenants` | G0، G4 | تأیید D-092/D-094 و طرح S3-0h |
| Backfill | نبود Manifest معتبر (D-092) و G5 | تأیید D-092، Backup |
| Rename فیزیکی ستون legacy | OD-12 و OD-18 | تأیید D-091 |
| ذخیرهٔ ماندگار واحد کالا (PR #704) و طرح پرداخت (PR #705) | وابسته به جدول‌های بالا و تصمیم مالکیت Party/Product | همان‌ها |
| P-4 و P-5 (رفتار انقضا و Replace) | `Proposed` | تصمیم مالک؛ پس از D-104 باید دوباره بررسی شوند |
| فرمول مبلغ، تلورانس تناژ، دقت اعشار | تصمیم‌نشده | تصمیم مالک (`TRADE_DECISION_RECONCILIATION.md` §۵) |
| Excel Import و مهاجرت داده تاریخی | خارج از محدودهٔ این مرحله | بعد از S3 |

## ۵. معیار خروج هر مرحله و ترتیب کم‌هزینه

**ترتیب پیشنهادی (از کم‌هزینه به پرهزینه؛ هر ردیف فقط بعد از ردیف قبلی):**

| گام | محتوا | هزینه | معیار خروج |
|---|---|---|---|
| ۱ | تأیید مالک روی D-090..D-096 (یا رد/اصلاح هر کدام) و مالکیت Party/Product | کم (تصمیم) | هر ADR `Accepted` یا `Rejected` با تاریخ؛ `TRADE_DECISION_RECONCILIATION.md` به‌روز |
| ۲ | بستن S3-0d-b: ممیزی مسیرهای HR/Admin/Export با شواهد کد | کم (مستندات/تست غیرمخرب) | جدول مسیرها بدون `Unknown` یا با دلیل |
| ۳ | تست دو-Tenant روی Store Trade با SQL Server (در محدودهٔ legacy: دو Company) | کم تا متوسط (تست) | Party/Product: List/Find/Update/Unique بین دو Company نشت نمی‌دهد؛ روی SQL Server اجرا شده |
| ۴ | تأیید G0 (طرح S3-0h) | کم (تصمیم) | مالک طرح را به‌عنوان «خروجی S3-0g» تأیید کند |
| ۵ | PR-1: سیاست Migration Trade طبق D-094 | متوسط | سیاست مصوب در Host؛ تست SQL Server |
| ۶ | PR-4 (A1): `Tenants` و `Companies.TenantId` nullable | **Schema** | Migration + تست؛ مجوز جدا |
| ۷ | PR-5/PR-6/D1 (ثبت Tenant اپراتوری، Provisioning، Backfill) و PR-4b (A2) | **Schema + داده** | Manifest تأییدشده، Backup (G5)، تست Idempotent |
| ۸ | S3-1: پروفایل فعالیت و نسخه‌گذاری | **Schema** | جدول با `TenantId` و `CompanyId` از ابتدا (پیشنهاد؛ `Proposed`) |

**ورود به S3-1:** گام‌های ۱ تا ۴ برای هر نوع Schema لازم‌اند؛ گام‌های ۶ و ۷ طبق طرح S3-0h لازم‌اند تا «تعلق Company به Tenant» از یک منبع معتبر دیتابیس resolve شود. برای جدول‌های **جدید** Trade پیشنهاد (Proposed) این است که از ابتدا `TenantId` و `CompanyId` مستقل داشته باشند (بدون Rename)، بدون FK به Core و با اعتبارسنجی زوج در Host (D-090)؛ پنج جدول فعلی Trade در گام‌های جدا (PR-P/R/C2) مهاجرت کنند.

**مسیر S3-1 تا S3-5 (از `TRADE_S3_ARCHITECTURE.md` §۱۴):** S3-1 پروفایل ← S3-2 پرونده و کالاها ← S3-3 پیش‌فاکتور و Revision ← S3-4 ثبت سفارش ← S3-5 فایل‌ها و Maker-Checker. با D-104، S3-3 و S3-4 به هم نزدیک می‌شوند: هر Proforma یک ثبت‌سفارش دارد، پس قید یکتا بین آن دو باید همزمان طراحی شود (اینکه Proforma بتواند موقتاً پیش از ثبت‌سفارش وجود داشته باشد، تصمیم‌نشده و باز است).

## ۶. آنچه این گذر عمداً انجام نداد

- هیچ Entity، جدول، ستون، Migration، Backfill، API، UI، Global Query Filter یا Tenant جدید.
- هیچ تغییری در Vendor، CustomerAccount، Installation، Licensing، Provisioning.
- هیچ تغییری در PRهای #704 و #705.
- هیچ تصمیم `Proposed` به `Accepted` نرفت (به‌جز ثبت D-104 که مالک تأیید کرده است).
