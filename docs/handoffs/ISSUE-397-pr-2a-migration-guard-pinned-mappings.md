# Handoff — S3-0 PR-2a: تقویت Migration Guard برای Pin نام‌ها

- **Issue:** #397 (S3-0؛ طرح مرجع پیشنهادی: `ISSUE-397-s3-0h-consolidated-tenant-company-migration-plan.md` §6.3)
- **Agent:** Claude Code
- **وضعیت:** ابزار Guard و تست‌ها Implemented در شاخهٔ PR؛ هیچ Schema/Migration/دیتابیسی تغییر نکرد. تصمیم‌های Proposed دست‌نخورده‌اند.

## خلاصه
- `ef_model.py`: زنجیرهٔ Fluent هر دستور پیکربندی (`chain_calls`) استخراج و با فهرست مجاز هر نوع دستور (`ALLOWED_CHAIN_CALLS`) سنجیده می‌شود. هر فراخوانی خارج از فهرست `ModelExtractionError` با نام متد و فایل می‌دهد (قبلاً بی‌صدا نادیده گرفته می‌شد).
- شناخته‌شده: `HasKey/HasAlternateKey(...).HasName`، `HasIndex(...).HasDatabaseName`، رابطه `.HasConstraintName`، `Property(...).HasColumnName`. مدل اکنون `primary_key_name`، `alternate_key_names`، نام Index و نام FK را نگه می‌دارد؛ نام Pin‌شدهٔ برابر پیش‌فرض EF همان مدل بدون Pin است. نام‌های تکراری رد می‌شوند.
- `generate_migrations.py`: Snapshot/Designer فقط نام‌های غیرپیش‌فرض را می‌نویسند (`.HasName`, `.HasDatabaseName`, `.HasConstraintName`)، مثل EF.
- `migration_model.py` / `verify_migration_model.py`: فایل‌های تولیدشده fail-closed خوانده می‌شوند؛ نام‌ها در مقایسهٔ مدل با Snapshot/Designer و در `compute_operations` وارد شدند. تغییر نام Key/Index/FK بین دو Designer (Rename) و `HasColumnName` به ستونی با نام دیگر رد می‌شود. `RenameColumn/RenameIndex` همچنان پذیرفته نمی‌شوند.

## تصمیم محدوده
`HasColumnName` فقط وقتی پذیرفته می‌شود که نام ستون با نام Property برابر باشد (Pin بدون تغییر مدل). نگاشت Property به ستون دیگر Rename است و برای PR-2b (و گام P از طرح S3-0h) می‌ماند، چون نیازمند جداسازی نام Property از نام ستون در سراسر Generator/Verifier است.

## تست
- تست جدید: `scripts/tests/test_migration_guard_pinned_mappings.py` (19 تست؛ Parser و Guardهای واقعی روی کپی موقت مخزن، بدون Mock). پیش از پیاده‌سازی: 15 FAIL + 1 ERROR؛ سه تست «Pin هم‌نام با پیش‌فرض» پیش از تغییر هم پاس بودند چون Pin نادیده گرفته می‌شد و پس از تغییر به‌دلیل درست پاس می‌شوند. پس از پیاده‌سازی: 19/19.
- قبلی‌ها: `test_migration_tooling.py` 32/32، `test_migration_guard_mutations.py` 31/31، `generate_migrations.py --check` و `verify_migration_model.py` (20 Migration) موفق.
- اجرا نشد: `dotnet ef migrations has-pending-model-changes`، Build/Test .NET، SQL Server (تغییر مدل واقعی وجود ندارد).
- CI: تست جدید به مرحلهٔ «Test the migration guards» در `.github/workflows/dotnet.yml` اضافه شد.

## باقی‌مانده برای PR-2b
`RenameColumn`/`RenameIndex` کنترل‌شده با فهرست بازبینی‌شده، تطبیق `Down` معکوس، جداسازی نام Property/ستون (گام P)، پوشش Trade، و تست روی SQL Server واقعی (گیت G3).
