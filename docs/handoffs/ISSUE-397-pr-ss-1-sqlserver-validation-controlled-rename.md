# Handoff — S3-0 PR-SS-1: اعتبارسنجی SQL Server برای Rename کنترل‌شده

- **Issue:** #397 (طرح مرجع پیشنهادی: `ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md`؛ PR-2a/PR-2b ادغام‌شده)
- **Agent:** Claude Code
- **مبنا:** `origin/main@03eafd038ca3278cda989e8db071d9fe86fac477` (شامل #656 و #660)
- **وضعیت:** تست‌ها Implemented در شاخهٔ PR. هیچ Migration اپلیکیشن، Schema، Backfill یا داده‌ای تغییر نکرد؛ Trade Migration Host دست‌نخورده؛ هیچ تصمیم Proposed به Accepted نرفت.

## خلاصه
تست جدید `tests/Template.Tests/RenameMigrationSqlServerTests.cs` با سه Migration دست‌نویس فقط در Assembly تست (`RenameValidationMigrations.cs`) روی **SQL Server واقعی** اجرا می‌شود. از زیرساخت موجود استفاده شد: همان متغیر `FARANOU_TEST_SQLSERVER`/`FARANOU_REQUIRE_SQLSERVER`، همان Service Container مرحلهٔ `build-and-test` (ubuntu، SQL Server 2022) و همان قاعدهٔ Skip؛ تغییری در CI لازم نبود و زیرساخت جدید ساخته نشد.

ساختار نمونه: `RenameOwners` ← `RenameChildren` (ستون `OwnerId` با FK، Alternate Key `(OwnerId, Code)` و Index) ← `RenameGrandChildren` (FK مرکب که به همان AK ارجاع می‌دهد). هر تست دیتابیس `FaranouTests_Rename_*` خودش را می‌سازد و در `finally` حذف می‌کند.

## سناریوها و نتیجه (اجرای واقعی)
1. RenameColumn Up: ساختار (ستون‌ها، Indexها با ستون‌های کلید، قیدهای کلیدی، FKها با ستون‌های والد/مرجع و Index مرجع) دقیقاً برابر «ساختار قبلی با فقط نام ستون عوض‌شده» است؛ داده‌ها حفظ؛ FK، AK و FK مرجع به AK همچنان اعمال می‌شوند. ✔
2. Down: ساختار و هویت اشیاء دقیقاً برابر وضعیت اولیه. ✔
3. RenameIndex Up/Down: فقط نام Index تغییر می‌کند. ✔
4. R1: `object_id`/`create_date` همهٔ PK/UQ/FK قبل و بعد Rename یکی است (Drop/Add شدن آن‌ها را آشکار می‌کند)؛ اسکریپت EF (`GenerateScript`) فقط `sp_rename` دارد و `DROP/ADD CONSTRAINT`، `DROP/CREATE INDEX`، `DROP TABLE/COLUMN` ندارد. ✔
5. کنترل منفی: Drop و Create دوبارهٔ همان قیدها ساختار را تغییر نمی‌دهد ولی بررسی هویت آن را تشخیص می‌دهد. ✔
6. پاک‌سازی: حتی وقتی بدنهٔ تست Exception می‌اندازد دیتابیس حذف می‌شود؛ پس از اجرا هیچ دیتابیس `FaranouTests_Rename_*` باقی نبود. ✔
- Mutation: افزودن `DropIndex`/`CreateIndex` به Migration نام‌گذاری ستون، تست اسکریپت را شکست داد (4 موفق، 1 شکست)؛ سپس برگردانده شد.
- **محدودیت شناخته‌شده:** Index ساده که Drop و دوباره Create شود ممکن است همان `index_id` را بگیرد؛ بررسی هویت برای آن کافی نیست و پوشش R1 برای Indexها با بررسی اسکریپت است (در کد مستند شده).

## نسخه‌ها
- SQL Server: Microsoft SQL Server 2022 (RTM-CU28) 16.0.4325.4، Developer، Linux (Docker `mcr.microsoft.com/mssql/server:2022-latest`، اجرای محلی ایزوله روی 127.0.0.1).
- EF Core: `Microsoft.EntityFrameworkCore.SqlServer` 10.0.14 (`Directory.Packages.props`)؛ .NET SDK 10.0.404 (محلی).

## تست
- اجرا و موفق: `RenameMigrationSqlServerTests` 5/5؛ کل `Template.Tests` با SQL Server واقعی و `FARANOU_REQUIRE_SQLSERVER=1`: 1247 تست، 1237 موفق، 0 ناموفق، 10 Skip (تست‌های SQL Server در Skip نیستند چون با REQUIRE=1 شکست می‌خوردند).
- در CI: همین تست‌ها در مرحلهٔ Test از `build-and-test` (Runner ubuntu با Service Container) اجرا می‌شوند؛ نتیجهٔ CI در PR ثبت می‌شود.
- اجرا نشد: Runner ویندوزی با SQL Server (در مخزن چنین Runner/Serviceای وجود ندارد؛ `publish-windows` فقط Publish و تست‌های Windows دارد)، تست‌های `Vendor.Host.Tests`، E2E و ClientApp (بی‌ارتباط با تغییر).

## آنچه این تست‌ها ثابت نمی‌کنند / Backlog
- اجرای Rename روی Schema واقعی Tenant/Company و داده‌های حجیم/قفل‌ها؛ این کار با Migration واقعی و تأیید جداگانه است.
- گام P (`HasColumnName` با Property≠ستون) و پوشش Trade همچنان باز است.
- SQL Server روی Windows (Runner ویندوزی) تأیید نشد؛ رفتار `sp_rename` در Linux و Windows یکی است ولی آزمون نشده.
