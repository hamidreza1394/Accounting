# ضمیمهٔ تکمیلی Stage 0 — ممیزی معماری Tenant/Company و برنامهٔ باقی‌ماندهٔ S3-0

- **Issue:** UNNUMBERED (ادامهٔ Issue #397 / S3-0 و Stage 0 مأموریت Trade)
- **PR:** #744 (همان شاخهٔ `docs/trade-stage0-status-audit`؛ PR جدید ساخته نشد)
- **وضعیت این سند:** مستندات فقط‌خواندنی؛ هیچ کد اجرایی، Migration، Schema، API یا Backfill تغییر نکرده است.
- **قواعد وضعیت:** واژگان D-030. هیچ `Proposed` به‌عنوان `Accepted` فرض نشده است. این سند تصمیم جدید ثبت نمی‌کند؛ هرچه «پیشنهاد» است صراحتاً [P] برچسب دارد.
- **دامنهٔ مجاز:** فایل‌های مشترکِ در حال تغییر در PR #743 (`ROADMAP`، `PROJECT_STATUS`، `WORK_LOG`، `LICENSING_WORKFLOWS`، `VENDOR_LIFECYCLE_FINAL_AUDIT`) و فایل‌های Vendor (GLM) ویرایش نشده‌اند؛ ارجاع به آن‌ها فقط خواندنی و گزارشی است.
- **روش:** دو بازبین `architect` (فقط‌خواندنی) اسناد و کد را بررسی کردند؛ ادعاهای پرتأثیر آن‌ها توسط Lead مستقل با `grep`/`sed` روی همین Commit مبنا (`ac0ec74`) راستی‌آزمایی شد. گزارش Agent شاهد نیست؛ شاهد خط‌های ذکرشده در مخزن است. ارجاع شماره‌خط‌ها به Commit مبنا است و ممکن است پس از ادغام #743 جابه‌جا شود.

---

## ۱) بازتطبیق تصمیم‌های Tenant (D-089 دوباره باز نشده است)

### ۱.۱ قبلاً تصویب‌شده (`Accepted`)
| تصمیم | محتوا | پیاده‌سازی |
|---|---|---|
| D-087 | Tenant = مرز مالکیت/جداسازی داده مشتری؛ Company = شرکت عملیاتی داخل Tenant؛ دو استقرار Shared/Dedicated با مدل منطقی یکسان | `NotImplemented` (کد legacy: ستون `TenantId` عملاً `Company.Id` است) |
| D-088 | اختیارات مدیر نصب (OD-1) | `Partial` (سخت‌سازی نقش Admin و ممیزی در #663/#664 ادغام شده است) |
| D-089 | Tenant فقط توسط مدیر نصب ساخته می‌شود؛ نگاشت یکتای `CustomerReference → TenantId`؛ Provisioning هرگز Tenant نمی‌سازد؛ Tenant از `CompanyId`/`CustomerBoundaryId`/گروه/نام استنتاج نمی‌شود (OD-6، OD-6a) | `NotImplemented` |
| D-103 | مرز Tenant در CustomerAccount/فضای کاری؛ binding مشتری→Tenant از مسیر Provisioning معتبر | Accepted؛ اجرای Schema انجام نشده |
| D-105، D-111، D-114، D-115 | توپولوژی دیتابیس و Routing سمت سرور؛ بدون FK بین دیتابیس‌ها | D-115 به‌صورت Registry سمت سرور (#734) `Partial` است؛ انتخاب Resolver/Schema انجام نشده |
| D-104 | رابطهٔ یک‌به‌یک Proforma↔OrderRegistration | مرتبط با Trade، نه Tenant |

### ۱.۲ هنوز تعیین‌نشده (`Proposed`، همگی `NotImplemented`)
| تصمیم | موضوع | OD |
|---|---|---|
| D-090 | یکپارچگی Tenant/Company در Trade (Host-level + ستون Tenant، بدون FK به Core؛ `CompanyId` در Trade فقط در S3 با تأیید جدا) | OD-11 |
| D-091 | تحمل Downtime و Rename فیزیکی ستون legacy | OD-12 |
| D-092 | مرجعیت Manifest نگاشت Backfill | OD-13 |
| D-093 | مدل Shared و Multi-Tenant Runtime | OD-14 |
| D-094 | سیاست اجرای Migration برای Trade (پیشنهاد: H2 opt-in) | OD-17 |
| D-095 | نام فیزیکی قیدهای legacy پس از Pin | OD-15 |
| D-096 | اجرای Migration در استقرار مشترک چندنمونه‌ای | OD-16 |
| OD-2/3/4/5/7/8/9/10/18 | هنوز ثبت ADR ندارند؛ فقط در جدول §۱۰ طرح S3-0h پیشنهاد هستند | — |
| بلوک «شفاف‌سازی‌های بازبینی» داخل D-089 | صراحتاً `Proposed` است و متن Accepted بالای آن را تغییر نمی‌دهد | — |

> یادداشت: جدول §۱۰ و §۱۴ طرح S3-0h هنوز OD-6/OD-6a را «باز» نشان می‌دهند؛ این وضعیت با D-089 منسوخ است (بخش ۴ و ۶ همین سند).

### ۱.۳ تصمیم‌های فنی که Agent می‌تواند بدون تأیید جدید بگیرد
- اجرای گزارش‌ها/ممیزی‌های فقط‌خواندنی، تست‌های ایزوله (دیتابیس آزمایشی) و اصلاح مستندات هم‌ترازی (مگر فایل‌های مشترکِ #743 یا Vendor).
- پیاده‌سازی طبق قراردادهای Accepted که Schema/Migration ندارند (مثلاً تست‌های معماری، Guardهای کد-فقط، اصلاح پیام/لاگ بدون تغییر رفتار).
- انتخاب نام تست‌ها، ساختار پوشه‌ها و جزئیات داخلی که با D-087/D-089/D-115 تعارض ندارند.

### ۱.۴ تصمیم‌هایی که تأیید صریح مالک می‌خواهند
- هر جدول/ستون/FK/Index جدید Tenant، هر Migration، هر Backfill (گیت G0/S3-0g).
- پذیرش هر ADR با وضعیت Proposed (D-090..D-096) و گزینهٔ H1..H4 برای Trade.
- اتصال `ITradeModuleEntitlement` به Entitlement واقعی (Issue #401) و برداشتن `DenyAllTradeModuleEntitlement`.
- اجرای هر عملیات روی دیتابیس مشتری، تغییر Provisioning/Vendor، و هر تغییر مدل Licensing.

---

## ۲) تطبیق `CustomerReference` ↔ Tenant با D-089 — فقط شکاف‌های واقعی

D-089 قاعدهٔ یکتای ۱:۱ را تصویب کرده است؛ پس «آیا ۱:۱ باشد؟» بازگشایی نمی‌شود. شکاف‌های زیر مربوط به پیاده‌سازی/مستندات‌اند، نه تصمیم:

| # | شکاف | شاهد (Commit مبنا) | مالک | اقدام پیشنهادی |
|---|---|---|---|---|
| G-1 | به‌روزرسانی `CustomerNumber` در Vendor بدون Guard تغییرناپذیری (بررسی یکتایی وجود دارد: `customer_number_exists`)، و audit بدون مقدار قبلی/جدید/دلیل | `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs` حدود خطوط ۲۸۷–۳۱۱ | GLM/Vendor | فقط گزارش؛ تعیین مالک و Issue جدا توسط مالک. Agent Trade دست نمی‌زند. |
| G-2 | شکست verifier در Apply بدون ثبت audit بازمی‌گردد | `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs:113-114` | Server/Provisioning (پیوند با Vendor) | Issue مستقل پس از تأیید؛ بدون تغییر در Stage 0. |
| G-3 | سند Vendor مدل legacy را «Tenant / Implemented» برچسب زده است | `docs/VENDOR_ONBOARDING_AND_PROVISIONING.md:21,29` | Vendor-docs (GLM) | پیشنهاد اصلاح برچسب به «legacy: Tenant=Company.Id» پس از ادغام #743؛ ویرایش نشد. |
| G-4 | `CompanyAccessService` هنوز `membership.TenantId == companyId` را به‌کار می‌برد | `Server/Infrastructure/Tenancy/CompanyAccessService.cs` | S3-0 (PR-E1 طرح S3-0h اجرانشده؛ #663/#664 هم‌نام «PR-E1a/b» اند اما مربوط به R-16 و FileManager‌اند) | انحراف پیاده‌سازی از D-087؛ برطرف‌شدن فقط پس از PR-4 و PR-E2/E3؛ ثبت به‌عنوان «انحراف پیاده‌سازی» لازم است. |
| G-5 | نگاشت `CustomerReference→TenantId` نه جدول دارد نه Resolver؛ ابزار تأییدشده برای Manifest وجود ندارد | نبود Entity در `Server/Infrastructure`؛ OD-13 / D-092 Proposed | S3-0e | بدون اقدام تا تأیید D-092 و S3-0 فنی. |

نتیجه: شکافی در **متن تصمیم** D-089 یافت نشد؛ پنج شکاف بالا همگی پیاده‌سازی/مستندات هستند و هیچ‌کدام در Stage 0 اصلاح نمی‌شوند.

---

## ۳) ابهام شناسه‌های OD

### ۳.۱ سه سری مستقل
| سری | قالب | سند مرجع (authoritative) | محدوده |
|---|---|---|---|
| Licensing | `OD-01..OD-27` (دو رقم) | `docs/LICENSING_OPEN_DECISIONS.md` | تصمیم‌های Licensing/Vendor |
| Tenant S3-0 | `OD-1..OD-18` و `OD-6a` (بدون صفر) | `docs/handoffs/ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md` §۱۰ و ADRهای D-088..D-096 | Tenant/Company و Migration |
| Trade (محصول) | `OD-001..OD-022` (سه رقم) | `docs/TRADE_OPEN_DECISIONS.md` | قواعد دامنهٔ Trade (مثلاً OD-006 = IMO کشتی) |

### ۳.۲ پاسخ پرسش‌ها
- **سری مستقل؟** بله؛ سه سری مستقل‌اند و هیچ نگاشت خودکاری بین آن‌ها وجود ندارد.
- **مرجع نهایی؟** برای هر سری سند ستون بالا؛ برای Tenant، ADR (D-088..D-096) بر جدول handoff مقدم است (D-030).
- **ریسک ارجاع اشتباه:** `OD-6` (Tenant/D-089، Accepted) و `OD-006` (IMO در Trade) در متن بدون پیشوند شبیه‌اند؛ `OD-6/6a` در ROADMAP، PROJECT_STATUS (مثلاً خط ~۵۹۹) و برخی خطوط ROADMAP (~۱۳۴، ~۱۴۲) بدون برچسب سری آمده‌اند.
- **اصلاح بدون تغییر شناسهٔ تاریخی [P]:**
  1. افزودن جدول «راهنمای سری‌ها» (همین جدول ۳.۱) به `docs/TRADE_DECISION_RECONCILIATION.md` §۴ یا `docs/INDEX.md`.
  2. قاعدهٔ شناسهٔ جدید: ارجاع در متن همیشه با پیشوند سری (`Tenant:OD-6`، `Trade:OD-006`، `Licensing:OD-06`)؛ شناسه‌های موجود تغییر نمی‌کنند.
  3. یک خط توضیح بالای `TRADE_OPEN_DECISIONS.md` و `LICENSING_OPEN_DECISIONS.md` دربارهٔ استقلال سری.
  4. برچسب‌گذاری ارجاع‌های مبهم در ROADMAP/PROJECT_STATUS؛ این دو فایل بعد از ادغام #743؛ `WORK_LOG` فقط افزودنی.
  هیچ‌کدام بدون تأیید مالک اعمال نمی‌شود.

---

## ۴) جدول دقیق S3-0a..h

برچسب‌های لایهٔ نام‌گذاری: «سرفصل Roadmap» در برابر «خروجی کاری handoff #397» (ROADMAP همین دو لایه را ثبت کرده است؛ S3-0e/f/g در handoffها معنای متفاوتی از سرفصل‌های Roadmap دارند).

| مرحله | وضعیت واقعی | شواهد (کد/تست/PR/سند) | تکرار نشود | ناقص/وابستگی | تصمیم مالک | معیار خروج دقیق |
|---|---|---|---|---|---|---|
| S3-0a | `Implemented`/تکمیل | ROADMAP؛ `docs/TRADE_TENANT_COMPANY_AUDIT.md`، `ISSUE-397-s3-0-tenant-company-audit.md` | ممیزی ایستای Entityها | `TRADE_TENANT_COMPANY_AUDIT.md:67` به #705 اشاره می‌کند و کهنه است | — | هیچ (فقط اصلاح مرجع کهنه در بازبینی مستندات) |
| S3-0b | تکمیل | هم‌ترازی AGENTS/ARCHITECTURE/SECURITY/… با D-087 | بازنویسی معماری | — | — | هیچ |
| S3-0c-a | تکمیل | inventory Snapshot (۳۳ نگاشت هسته، ۲۰ Migration؛ ۵ نگاشت Trade، ۱ Migration) | شمارش دوباره | فقط Source؛ تاریخچهٔ زنده را ثابت نمی‌کند | — | هیچ |
| S3-0c-b | تکمیل (فقط Farano-Local) | خروجی اپراتور؛ ۱۶ Migration تا `AddUserSessions`؛ بدون جدول `trade` | اجرای دوبارهٔ DDL/DML ممنوع | timestamp خروجی نیازمند تأیید ساعت؛ فقط یک محیط | — | تکرار ممیزی زنده فقط هنگام آماده‌شدن Migration واقعی و با مجوز جدا |
| S3-0d-a | تکمیل | بازبینی مالکیت V3.2 | — | inventory کامل نیست | — | هیچ |
| S3-0d-b | **Done نشده** (PR #670 ادغام ۲۰۲۶-۱۱-۱۵) | `ISSUE-397-s3-0d-b-data-path-inventory.md`، #674 (تست‌های تأیید) | ماتریس مالکیت/Endpoint | بدنهٔ تست‌ها خط‌به‌خط خوانده شد نه اجرا؛ `.cshtml`/ClientApp/DB واقعی خارج بود | تأیید مالک برای «Verified» | تأیید مالک روی inventory + خروجی اجرای واقعی تست‌های مرتبط روی CI |
| S3-0e (Roadmap: نگاشت داده) | **Partial** | D-089 Accepted (`main@9eb281b`)؛ نگاشت اجرا نشده | تصمیم ۱:۱ | OD-13 / D-092 Proposed؛ ابزار Manifest نیست | تأیید D-092 | Manifest تأییدشده + dry-run گزارش‌شده؛ بدون اجرا تا G0 |
| S3-0f-بخش۱ (Migration Host) | `Implemented` (#665، `main@1c0eec0`) | `DatabaseMigrationHostSqlServerTests`؛ کدهای خروج `0..4` (کد ۳ دو معنا دارد) | Host و تست‌های آن | رفع ابهام کد ۳ (طراحی کوچک) | — | تست رفتار کد ۳ و ثبت واژگان؛ تغییر Host مشمول گیت S3-0g |
| S3-0f-بخش۲ (سیاست/استقرار) | **Open** | OD-17 / D-094 Proposed | — | اتصال Entitlement (#401)، Backup/Recovery، ممیزی اجرا | تأیید D-094 (H1..H4) | ADR پذیرفته + رویهٔ Backup مستند + ثبت ممیزی اجرا |
| S3-0g (خروجی و گیت) | **مسدود** (گیت) | ماتریس Current→Target و طرح در S3-0h؛ G3 در #675 با SQL Server اجرا شد | G3 | Tenant دوم/Backfill/PR-4 بدون تأیید | تأیید طرح S3-0h به‌عنوان خروجی | تأیید صریح مالک؛ بدون آن Accepted نیست |
| S3-0h (طرح تجمیعی) | `Proposed`/`NotImplemented` | `ISSUE-397-s3-0h-…plan.md` | بازنویسی طرح | جدول §۱۰ هنوز OD-6/6a را باز نشان می‌دهد (منسوخ با D-089)؛ مرزبندی PR-E1 با #663/#664 | تأیید طرح؛ تصحیح جدول با اجازهٔ مالک | G0 بسته؛ جدول OD با ADRها هم‌تراز |

### ۴.۱ کارهای ادغام‌شده که نباید دوباره پیاده‌سازی شوند
PR-2a (#656)، PR-2b (#660)، PR-SS-1 (#661)، PR-3 (#662، #673، #674)، G3 (#675)، Migration Host (#665)، #663/#664 (سخت‌سازی Admin/FileManager برای D-088 — نه PR-E1 طرح S3-0h)، D-115 Registry (#734). تاریخ‌های ادغام از `gh api` (۲۰۲۶-۱۱-۱۴ تا ۱۱-۲۰) گرفته شد.

---

## ۵) ناسازگاری‌های شناسایی‌شده (فقط گزارش)

| # | ناسازگاری | شاهد | پیشنهاد [P] |
|---|---|---|---|
| I-1 | ROADMAP: ردیف S3-0h می‌گوید OD-1..OD-18 هیچ‌کدام Accepted نیستند، ردیف بعد می‌گوید OD-1 و OD-6/6a با D-088/D-089 Accepted اند | `docs/ROADMAP.md` حدود ۲۶۸–۲۶۹ | هم‌ترازی پس از ادغام #743 |
| I-2 | OD-6/6a در ROADMAP/PROJECT_STATUS بدون سری | بخش ۳ | برچسب سری |
| I-3 | `.github/workflows/dotnet.yml` فقط Core را برای `has-pending-model-changes` بررسی می‌کند؛ Trade پوشش ندارد | حدود خطوط ۱۵۰–۱۶۰ | اضافه‌شدن در PR-1/CI با تأیید (CI بدون اجازه تغییر نمی‌کند) |
| I-4 | D-115 در سند `NotImplemented`، در کد Registry موجود است (#734) | `Server/Infrastructure/DatabaseTopology/*`، `DECISIONS.md` ~۳۱۴۵ | ثبت «انحراف: Partial» توسط مالک/ADR |
| I-5 | بلوک Proposed داخل D-089 ممکن است Accepted خوانده شود | `DECISIONS.md` ~۲۳۶۵ | یادداشت تفکیک بصری |
| I-6 | کد خروج ۳ دو معنا دارد | `Server` Migration Host | واژگان کد خروج در سند Host |
| I-7 | Trade fail-closed: `DenyAllTradeModuleEntitlement` ثبت بی‌قیدوشرط | `Server/Program.cs:285` | بدون تغییر تا تأیید #401 |

---

## ۶) برنامهٔ کم‌هزینهٔ باقی‌ماندهٔ S3-0 [P] — مطابق D-087/D-105/D-115

اصول: بدون جدول/لایهٔ/سرویس غیرضروری؛ سازگار با Shared و Dedicated؛ بدون Backfill حدسی؛ Rollback/Backup قابل‌اتکا؛ Migrationهای کوچک و قابل بازبینی؛ بدون تغییر Core/Vendor غیرضروری.

1. **گام ۰ (مستندات، بدون Schema):** تصویب مالک روی بسته (بخش ۷) + هم‌ترازی جدول OD و برچسب سری‌ها.
2. **گام ۱ (کد-فقط):** تکمیل PR-E1 (`WorkContext/CompanyAccess` با `TenantId` اختیاری)؛ بدون Schema.
3. **گام ۲ (Schema کوچک، پس از G0 و D-092 (OD-13)):** PR-4(A1): جدول `Tenants` حداقلی + `Companies.TenantId` nullable + FK؛ یک Migration، `Down` تست‌شده روی SQL Server، بدون AK.
4. **گام ۳:** PR-5 ثبت Tenant اپراتوری + فرمان Backfill با `dry-run` و audit؛ فقط از Manifest تأییدشدهٔ مالک (OD-13).
5. **گام ۴:** PR-6 Provisioning فقط resolve (D-089)؛ سپس اجرای D1 (عملیات، نه PR) با Backup و مجوز جدا (G5).
6. **گام ۵:** PR-4b(A2) NOT NULL + AK پس از تأیید D1.
7. **گام ۶ (ترتیب‌پذیر):** PR-P* (گام C# بدون Migration)، سپس فقط در صورت تأیید OD-12/OD-18، PR-R* (Rename فیزیکی)، سپس C2/D2/C3.
8. **Trade:** ستون `TenantId` بدون FK به Core (D-090 در صورت Accepted)؛ `DenyAll` تا Issue #401 دست‌نخورده.
9. **Rollback:** هر گام Backup پیش‌شرط + `Down` تست‌شده؛ Backfill با snapshot ممیزی؛ هیچ Migration منتشرشده بازنویسی نمی‌شود.

---

## ۷) فهرست تصمیم‌های نیازمند تأیید مالک (به ترتیب انسداد)

1. تأیید S3-0h به‌عنوان «خروجی S3-0g» و تصحیح جدول §۱۰/§۱۴ برای منسوخ‌بودن OD-6/6a (گیت G0).
2. تأیید/رد D-092 (OD-13): قالب و مرجع Manifest.
3. تأیید/رد D-094 (OD-17): H1..H4 (پیشنهاد H2) و پیوند با Entitlement (#401).
4. تأیید/رد D-091 (OD-12) و OD-10/OD-18 (Rename فیزیکی در برابر فقط گام P).
5. تأیید/رد D-090 (OD-11): Host-level بدون FK.
6. OD-4، OD-5 (دامنهٔ `CompanyRole` و Audit).
7. قبل از Tenant دوم: OD-3، OD-7، OD-9، D-093 (OD-14)، D-096 (OD-16).
7b. باقی‌ماندهٔ پیاده‌سازی D-088 (`Partial`؛ فیلد دلیل در Razor و محافظت سطح دیتابیس) کار پیاده‌سازی است، نه OD باز؛ OD-1 با D-088 بسته است.
8. بعداً/بدون انسداد: D-095 (OD-15)، OD-2، OD-8.
9. اجازهٔ اصلاح مستندات سری OD و هم‌ترازی ROADMAP/PROJECT_STATUS پس از ادغام #743.
10. تعیین مالک و Issue برای شکاف‌های G-1/G-2/G-3 (Vendor/Provisioning).
11. اجازهٔ شروع S3-0 فنی (PR-E1، PR-4) — تا آن زمان **هیچ‌چیز آغاز نمی‌شود**.

---

## ۸) محدودیت‌ها و عدم‌اجرا
- هیچ تستی اجرا نشد؛ این سند بازبینی ایستا است (Build/Test کد تغییری نکرده است).
- دیتابیس واقعی، `.cshtml` و ClientApp در این ضمیمه بازبینی نشد.
- شمارهٔ خط‌ها مربوط به Commit مبنا است.
- مصرف Agentها: `architect` A ≈ ۱۶۶٬۲۳۶ توکن (۳۰ فراخوانی ابزار)، `architect` B ≈ ۲۱۵٬۲۶۶ توکن (۳۷ فراخوانی ابزار)، دو رفت‌وبرگشت؛ اعداد مطابق گزارش خود ابزار.
