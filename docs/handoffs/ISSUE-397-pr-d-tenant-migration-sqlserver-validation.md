# Handoff — S3 PR-D: اعتبارسنجی عملیات Migration Tenant روی SQL Server واقعی (گیت G3)

- **تاریخ:** 2026-11-15
- **مبنا:** `origin/main@53dd2ea`؛ #656، #660، #673، #674 ادغام‌شده (از GitHub تأیید شد).
- **محدوده:** فقط تست و Migration آزمایشی داخل `tests/`. هیچ Schema، Migration عملیاتی، Entity، جدول Tenant، Backfill یا Workflow تغییر نکرد.
- **وضعیت:** تست‌ها روی SQL Server 2022 واقعی (کانتینر موقت `scripts/dev/test-sqlserver.sh`) اجرا شدند؛ CI همان سرویس `mssql` موجود را با `FARANOU_REQUIRE_SQLSERVER=1` استفاده می‌کند (سرویس جدیدی ساخته نشد).

## فایل‌ها
- `tests/Template.Tests/TenantMigrationSqlServerValidationTests.cs` — ۲۰ تست (D1 تا D6).
- `tests/Template.Tests/TenantValidationMigrations.cs` — Context و Migrationهای آزمایشی (`Tv1..Tv3`) فقط در Assembly تست.
- `tests/Template.Tests/TenantValidationUnpinnedModels.cs` — دو مدل Before/After برای R1.
- `tests/Template.Tests/RenameMigrationSqlServerTests.cs` — فقط `private const` ← `internal const` برای دو Query ساختار (استفادهٔ مجدد، بدون تغییر رفتار).

## یافته‌ها (هرکدام با اجرای واقعی؛ هیچ فرضی پذیرفته نشد)
| # | سناریو | نتیجهٔ مشاهده‌شده | فرض S3-0g |
|---|---|---|---|
| D1 | `RenameColumn`+`RenameIndex` با نام‌های Pin روی ستون legacy `TenantId`، AK، FK، Unique Index، Index، FK وابسته به AK | فقط نام ستون/Indexهای تغییرنام‌یافته عوض شد؛ AK و FK با نام فیزیکی قدیمی (حاوی `TenantId`) ماندند؛ Object Identity همهٔ قیدها بدون تغییر؛ اسکریپت فقط `sp_rename` | U1 ✅ |
| D2 | Up → Down | ساختار، Identity، دادهٔ ردیف‌ها و تعداد رکوردها (۲/۳/۳) دقیقاً برگشت؛ تاریخچهٔ EF برگشت | U4 ✅ |
| D1b | `sp_rename` ستونی که **Filtered Index** نام آن را در Predicate دارد | **خطای 5074 و هیچ تغییری** (اتمیک). اگر Filter فقط ستون‌های دیگر را نام ببرد، Rename موفق است | **یافتهٔ جدید** |
| D1c | نام‌های legacy نگه‌داشته‌شدهٔ AK/FK/Index (OD-15) | ساخت Index جدید با نام قدیمی روی همان جدول ← **خطای 1913** (تعارض نام با ستون واقعی `TenantId` آینده). تغییر نام AK/FK با `sp_rename 'dbo.<نام قید>' … 'OBJECT'` (نام با Schema، نه `table.name`؛ شکل `table.name` خطای «ambiguous» می‌دهد) ممکن است، Identity قیدها ثابت می‌ماند و FK وابسته به AK درست ادامه می‌دهد. EF عملیاتی برای آن ندارد (فقط SQL دستی) | ورودی OD-15 |
| D3 | FK مرکب `(CompanyId, TenantId)` با `TenantId` nullable | جفت معتبر ✔؛ جفت نامعتبر (Tenant دیگر/جابه‌جا/Company ناموجود/Company با Tenant=NULL) ✘ با خطای 547؛ **اگر `TenantId` در فرزند NULL باشد، FK اصلاً بررسی نمی‌شود** (حتی Company ناموجود پذیرفته می‌شود) و حذف والد با فرزند NULL مجاز است | U3 ✅ (MATCH SIMPLE) |
| D4 | NOT NULL برای `Guid` | Migration دستی با `THROW` تا وجود NULL → خطای 50001 و **هیچ تغییری** (بدون Default، بدون تبدیل NULL به `Guid.Empty`، تاریخچه ثبت نشد). پس از تکمیل داده موفق؛ دادهٔ ردیف‌ها و FK دست‌نخورده؛ NULL جدید → 515 | U7 ✅ |
| D4b | `ALTER COLUMN … NOT NULL` وقتی **Index وابسته** دارد | **خطای 5074** حتی با داده کامل؛ فقط با Drop Index → Alter → Create Index موفق می‌شود (Identity Index عوض می‌شود). `Tv3` همین را صریح می‌نویسد | **یافتهٔ جدید** |
| D4c | SQL تولیدی EF | بدون Default مقدار: فقط `ALTER COLUMN` (SQL Server با NULL رد می‌کند، 515). با `defaultValue = Guid.Empty` (پیش‌فرض Scaffold برای Guid غیر‌nullable): `UPDATE … = '0000…'` که NULLها را بی‌صدا به مالک ناموجود تبدیل می‌کند ← استفاده ممنوع | U7 ✅ |
| D5 | R1: Rename property بدون Pin (EF Differ واقعی) | EF `RenameColumn` + `Drop/AddUniqueConstraint` + `Drop/AddForeignKey` + `RenameIndex` تولید می‌کند. با FK وابسته به AK: **خطای 3725، Rollback کامل** (ساختار/Identity/داده بدون تغییر). بدون FK وابسته موفق می‌شود اما **همهٔ قیدها Object جدید** می‌شوند و نام‌ها عوض می‌شود | U2 ✅ |
| D6 | ایزولاسیون | هر تست پایگاه `FaranouTests_Tv_*` می‌سازد و حتی با شکست تست حذف می‌کند؛ قبل از ساخت، مقصد اعتبارسنجی می‌شود (Server محلی + نام `FaranouTests*`، پیام بدون Connection String)؛ بدون SQL Server ← Skip، نه Pass | ✅ |

## Mutation (اثبات توان شکست)
افزودن Drop/Add یک FK به `Tv2.Up` ← `D1` (Identity)، `D1_ThePinnedRenameScripts…` (اسکریپت) و `D2` هر سه شکست خوردند؛ سپس برگردانده شد.

## نتیجهٔ اجرا (واقعی)
- با SQL Server: `TenantMigrationSqlServerValidation` ۲۰ اجرا، ۲۰ موفق، ۰ شکست، ۰ Skip. همراه `RenameMigrationSqlServerTests`، `DatabaseModelTests`، `LegacyTenantIdEntityContractTests`: ۴۶ موفق، ۰ شکست.
- `DatabaseMigrationHostSqlServerTests` (قفل همزمانی، اجرای تکراری، شکست Core/Trade از #665): ۸ موفق.
- بدون SQL Server: همان ۲۰ تست ← ۷ موفق (بدون نیاز به سرور: اعتبارسنجی مقصد)، ۱۳ Skip (**NotRun**).

## تأثیر بر Guard و طراحی (توصیه، نه تصمیم)
1. **Guard باید Rename ستونی را که در Predicate یک Filtered Index آمده، پیش از Migration رد کند** (یا Index صریح Drop/Recreate شود)؛ در مخزن امروز Index فیلتردار روی ستون Tenant وجود ندارد، ولی Unique Index با ستون nullable (مثلاً `SecurityAuditEvent.TenantId`) فیلتر خودکار `IS NOT NULL` می‌گیرد.
2. **NOT NULL (A2):** قالب = `THROW` گارد + Drop Index وابسته + `AlterColumn` بدون Default + Create Index؛ هرگز `defaultValue: Guid.Empty`.
3. **FK مرکب با Tenant nullable کافی نیست:** تا قبل از NOT NULL، NULL در فرزند بررسی FK را خاموش می‌کند؛ مسیر امن، NOT NULL پس از Backfill و تأیید است (D3 + D4).
4. **R1 بی‌خطر نیست و فقط به‌خاطر FK وابسته رد می‌شود؛** Pin (R2) باید الزامی بماند (Guard فعلی R1 را رد می‌کند: `scripts/tests/test_migration_guard_pinned_mappings.py::test_a_name_difference_between_two_models_is_a_rename_and_is_refused`).

## NotRun / محدودیت
- U6 (عملکرد با بار)، رفتار روی حجم داده واقعی و SQL Server غیر 2022: اجرا نشد.
- ساختار نمونه است؛ Migration عملیاتی Tenant و اجرای Trade/Core واقعی با Tenant در این PR نیست.
- CI: نتیجهٔ اجرای این PR در Workflow موجود در گزارش PR آمده است.
