# Handoff — S3-0 PR-2b: پشتیبانی کنترل‌شدهٔ Rename در Migration Guard

- **Issue:** #397 (طرح مرجع پیشنهادی: `ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md` §6.3)
- **Agent:** Claude Code
- **مبنا:** `origin/main@9c744eed76b6dd717504a7a983a15e4522ed5661` (شامل PR-2a / #656)
- **وضعیت:** ابزار Guard و تست‌ها Implemented در شاخهٔ PR. هیچ Migration، Schema، SQL، Backfill یا دیتابیسی تغییر نکرد. هیچ تصمیم Proposed به Accepted نرفت؛ D-087 بدون تغییر.

## خلاصه
- `verify_migration_model.py`: `RenameColumn` و `RenameIndex` (فقط آرگومان نام‌دار `name/table/newName`) خوانده می‌شوند. فهرست بازبینی‌شدهٔ `ALLOWED_RENAMES` (کلید: شناسهٔ ۱۴ رقمی Migration؛ مقدار: ستون‌ها و ایندکس‌ها به‌صورت `(table, old, new)`) **خالی** است؛ هیچ Migration واقعی Rename ندارد.
- Rename خارج از فهرست رد می‌شود (پیام شامل `ALLOWED_RENAMES`). Migration مجاز فقط می‌تواند دقیقاً همان Renameها را در `Up` داشته باشد (ترکیب با Add/Drop/Alter یا تغییر نامرتبط مدل رد می‌شود) و `Down` باید معکوس دقیق باشد.
- مدل مورد انتظار پس از Rename (`apply_renames`) از مدل Designer قبلی با اعمال فقط Renameهای مجاز ساخته می‌شود؛ نام PK/AK/FK/Index ثابت می‌ماند (Pinهای PR-2a). اگر Pin حذف شود (نام قید تغییر کند) گزارش صریح با واژهٔ «pin» می‌آید و Rename به‌عنوان Drop/Add قید تلقی نمی‌شود. هر تفاوت دیگر مدل رد می‌شود.
- Rename به ستون موجود، از ستون/جدول ناموجود، `RenameTable`/`RenameSequence`، آرگومان موضعی و شناسهٔ کهنه در فهرست (بدون Migration متناظر) همگی fail-closed هستند.
- پشتیبانی PR-2a از `HasName/HasDatabaseName/HasConstraintName/HasColumnName` حفظ شد.

## تست (نتیجهٔ واقعی)
- RED (قبل از پیاده‌سازی): `test_migration_guard_rename.py` ۱۸ تست؛ ۱۷ ERROR (نبود `allowed_renames`/`ALLOWED_RENAMES`) و ۱ OK.
- GREEN: `test_migration_guard_rename.py` 18/18، `test_migration_guard_pinned_mappings.py` 22/22، `test_migration_tooling.py` 32/32، `test_migration_guard_mutations.py` 32/32.
- `generate_migrations.py --check`، `verify_migration_model.py` (۲۰ Migration) و `git diff --check` موفق.
- تغییر انتظار قدیمی: دو تست PR-2a و یک تست جهش که «Rename همیشه رد می‌شود با پیام does not verify» را می‌سنجیدند، به «Rename خارج از فهرست رد می‌شود (ALLOWED_RENAMES)» به‌روز شدند؛ رد شدن حفظ شده و Guard ضعیف نشد. `max_length` به‌تنهایی facet تفاوت مدل نیست (فقط `nullable`/`column_type`)، پس تست ترکیب با تغییر نامرتبط از `nullable` استفاده می‌کند.
- Mutation: دست‌کاری غیرمجاز، Rename خارج از فهرست، حذف Pin، Rename ناقص، `Down` نامتقارن و ترکیب عملیات همگی رد می‌شوند.
- CI: تست جدید به مرحلهٔ «Test the migration guards» اضافه شد.
- **اجرا نشد:** Build/Test .NET، `dotnet ef migrations has-pending-model-changes` و SQL Server (تغییر مدل/C# واقعی وجود ندارد).

## محدودیت مهم
تست‌های Guard **ثابت نمی‌کنند** SQL Server هنگام اجرای `RenameColumn/RenameIndex` به FK/AK/Index آسیب نمی‌زند. اجرای واقعی `Up/Down` روی SQL Server وظیفهٔ مرحلهٔ بعدی «SQL Server Validation» (گیت G3) است.

## Backlog (خارج از محدوده، غیرمسدودکننده)
- گام P (`HasColumnName` با Property≠ستون): نیازمند جداسازی نام Property از نام ستون در Generator/Verifier.
- اعتبارسنجی واقعی SQL Server (G3) برای Up/Down.
- پوشش Trade؛ Trade Migration Host و وابستگی Core→Trade بدون تصمیم نهایی دست‌نخورده ماند.
- Migration واقعی Rename برای Tenant/Company و هر Backfill.
