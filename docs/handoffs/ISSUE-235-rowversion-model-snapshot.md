# تحویل وظیفه — ISSUE-235-rowversion-model-snapshot

- **Issue:** [#235](https://github.com/hamidreza1394/Accounting/issues/235)
- **Agent/مسئول:** ChatGPT / GitHub connector
- **تاریخ:** 2026-10-29
- **وضعیت:** ناقص — اصلاحات روی شاخه ثبت شده‌اند؛ CI روی آخرین HEAD هنوز تأیید نشده است.

## هدف و معیارهای پذیرش
- [x] بازتولید خطای `PendingModelChangesWarning` از CI قبلی و تعیین علت ریشه‌ای.
- [x] افزودن تست EF برای metadata مربوط به `RowVersion`.
- [x] اصلاح استخراج مدل و تولید Snapshot برای SQL Server `rowversion`.
- [ ] تأیید سبز بودن `dotnet test`، گاردهای Migration و `dotnet ef migrations has-pending-model-changes` روی آخرین HEAD.
- [ ] تکمیل مصرف‌کننده واقعی provisioning؛ این اصلاح فقط خطای مدل/Snapshot را هدف می‌گیرد.

## خلاصه تغییرات
- علت خطای CI: `ProvisioningRequestClaim.RowVersion` در مدل runtime با `.IsRowVersion()` تنظیم شده بود، اما ابزار تولید مدل سفارشی ویژگی آرایه‌ای `byte[]` و semantics مربوط به rowversion را در Snapshot ثبت نمی‌کرد.
- `scripts/ef_model.py` اکنون `byte[]` را استخراج می‌کند، تنظیم `.IsRowVersion()` را می‌خواند و نوع SQL Server را `rowversion` ثبت می‌کند.
- `scripts/migration_model.py` نوع `byte[]` را هنگام خواندن Snapshotهای تولیدشده می‌شناسد و فقط فراخوانی دقیق `.ValueGeneratedOnAdd()` را با آن اشتباه نمی‌گیرد.
- `scripts/generate_migrations.py` در Snapshot و فایل Designer، `.IsRowVersion()` را تولید می‌کند.
- Snapshot و Designer مربوط به Migration موجود به‌روز شده‌اند؛ فایل Migration اجرایی یا Schema مشتری تغییر نکرده است.
- تست مدل EF اضافه شده تا نوع CLR، نوع ستون، concurrency token و `ValueGenerated.OnAddOrUpdate` را بررسی کند.

## فایل‌های مهم تغییرکرده
- `tests/Template.Tests/DatabaseModelTests.cs`
- `scripts/ef_model.py`
- `scripts/migration_model.py`
- `scripts/generate_migrations.py`
- `Infrastructure/Persistence/Migrations/DatabaseContextModelSnapshot.cs`
- `Infrastructure/Persistence/Migrations/20261029161402_AddCustomerProvisioningClaims.Designer.cs`

## تصمیم‌های فنی
- هشدار EF غیرفعال نشده، تستی حذف نشده و migration اجرایی/داده مشتری دست‌کاری نشده است.
- `RowVersion` باید در Snapshot به شکل SQL Server rowversion و concurrency token ثبت شود.
- PR #302 همچنان Draft می‌ماند؛ این تغییر به معنی تکمیل end-to-end provisioning نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| CI پیش از اصلاح، run 40913297367 | ناموفق | Build موفق؛ Test ناموفق؛ ۱۰۷ تست شکست خوردند؛ گاردهای Migration به‌علت شکست Test اجرا نشدند |
| `git clone` در محیط فعلی | ناموفق | شبکهٔ این محیط نتوانست `github.com` را resolve کند |
| `dotnet build` / `dotnet test` محلی | اجرا نشده | checkout و SDK قابل اجرا در این محیط در دسترس نبود |
| `scripts/generate_migrations.py --check` | منتظر CI | اجرای محلی انجام نشد |
| `dotnet ef migrations has-pending-model-changes` | منتظر CI | باید روی HEAD نهایی اجرا شود |
| CI روی آخرین HEAD | هنوز تأیید نشده | هیچ نتیجه سبزی برای آخرین commit در این تحویل ادعا نمی‌شود |

## امنیت، داده و سازگاری
- هیچ کلید، رمز، secret، connection string یا داده مشتری اضافه نشده است.
- هیچ migration روی دیتابیس مشتری اجرا نشده و هیچ Schema اجرایی جدیدی در این اصلاح ایجاد نشده است.
- خطر اصلی باقی‌مانده، تأییدنشده بودن نتیجه گاردهای تولید Snapshot و تست واقعی EF روی آخرین HEAD است.

## ریسک‌ها و کارهای باقی‌مانده
- بررسی CI و اصلاح هر شکست جدید پیش از هرگونه ادغام.
- تکمیل transactional provisioning، claim/replay واقعی، enrollment یک‌بارمصرف، اجبار تغییر رمز، acknowledgement امضاشده و نگاشت مرجع شرکت مادر.
- PR #302 نباید تا تکمیل معیارهای پذیرش خودش از Draft خارج یا merge شود.

## شناسه‌های Git
- **Branch:** `feat/customer-provisioning-consumer`
- **آخرین commit شناخته‌شده در زمان نوشتن handoff:** باید با HEAD جاری PR #302 تطبیق داده شود.
- **Pull Request:** [#302](https://github.com/hamidreza1394/Accounting/pull/302)


## پیگیری CI در 2026-10-29
- اجرای CI `40917960102`: Build موفق؛ .NET tests برابر 613 موفق و 0 شکست؛ `Check the generated migrations` موفق؛ اما `Test the migration guards (unit and mutation tests)` شکست خورد.
- علت اول: parser گارد، `byte[]` را در `table.Column<byte[]>` تشخیص نمی‌داد؛ به همین دلیل ستون `ProvisioningRequestClaims.RowVersion` در بررسی migration غایب دیده می‌شد.
- علت دوم: تست جهشی `test_newer_migration_than_the_constants` از فایل آزمایشی تاریخ‌دار `20261024000000` استفاده می‌کرد، در حالی که migration جاری `20261029161402` بود؛ فایل ساختگی از نظر ترتیب واقعاً جدیدتر نبود.
- اصلاحات روی شاخه: parser از انواع آرایه‌ای پشتیبانی می‌کند؛ تست رگرسیون rowversion اضافه شده و تاریخ fixture به `20261030000000` تغییر کرده است.
- **وضعیت:** منتظر CI جدید برای تأیید این اصلاحات. مصرف‌کننده واقعی provisioning، idempotency تراکنشی و E2E هنوز خارج از محدوده تکمیل‌شده‌اند؛ PR باید Draft بماند.
