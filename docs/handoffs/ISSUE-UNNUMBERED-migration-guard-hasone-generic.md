# تحویل وظیفه — ISSUE-UNNUMBERED-migration-guard-hasone-generic

- **Issue:** UNNUMBERED (شکست CI پس از Merge شدن PR #77، اجرای 40193465250)
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-10-25
- **وضعیت:** انجام‌شده (CI روی GitHub هنوز اجرا نشده؛ فقط محلی تأیید شد)

## هدف و معیارهای پذیرش
- [x] علت شکست گام «Check the generated migrations» مشخص شود.
- [x] هیچ Migration، مدل یا کد دامنه تغییر نکند؛ ابتدا خروجی EF Core با Snapshot مقایسه شود.
- [x] پارسر با تست رگرسیون برای `HasOne<Domain.Company>().WithMany().HasForeignKey(...)` اصلاح شود.
- [x] `generate_migrations.py --check`، `verify_migration_model.py` و `dotnet ef migrations has-pending-model-changes` سبز باشند.

## علت اثبات‌شده
1. **شواهد EF:** روی `main` (`e9fef6a`) دستور `dotnet ef migrations has-pending-model-changes` (EF 10.0.13) می‌گوید «No changes have been made to the model since the last migration». اختلاف واقعی بین مدل و Snapshot/Migration وجود ندارد؛ Migration، مدل و کد دامنه تغییر نکرد.
2. **محدودیت پارسر:** `scripts/ef_model.py` فقط `.HasOne(` / `.HasMany(` را می‌شناخت، نه `HasOne<T>().WithMany()` (PR #68).
3. **عقب‌ماندن گاردها:** مولد و تأییدکننده فقط یک پنجرهٔ ثابت را می‌سنجیدند و دو Migration بعدی را نه.
4. **ترتیب دستی در Snapshot:** Snapshot و Designer آخرین Migration دستی ویرایش شده بودند؛ فقط ترتیب ایندکس/FK `DefaultCompanyId` با مولد فرق داشت.

## بازبینی مستقل (2026-10-25) و اصلاح
بازبینی انتقادی نشان داد نسخهٔ اول PR فقط ثابت‌ها را جلو برده بود و پوشش را کم کرده بود (با تست جهشی اثبات شد: از ۳۰ تغییر غیرمجاز، گاردهای قبلی فقط ۳ مورد را رد می‌کردند). اصلاح:
- **SQL دست‌نویس همهٔ Migrationها** (`generate_migrations.py::check_reviewed_sql`): هر Migration باید دقیقاً SQL مرورشدهٔ خودش را داشته باشد (`AddCorporateGroups`، `AlignSchemaWithModel` پین‌شده)؛ Migration بدون ورودی نباید SQL داشته باشد؛ شکل غیرقابل‌مقایسه (`@"..."`، interpolated) رد می‌شود؛ Migration تازه‌تر از `CURRENT_MIGRATION_*` خطا می‌دهد.
- **زنجیرهٔ کامل** (`verify_migration_model.py`): زنجیره از پوشه خوانده می‌شود (بدون ثابت)؛ برای هر Migration از دوم به بعد، `Up` باید دقیقاً تفاضل Designer قبلی و Designer خودش و `Down` دقیقاً وارونهٔ آن باشد. مسیر «افزودن جدول» (`AddCorporateGroups`) هم Up و Down را می‌سنجد.
- **`AddColumn`/`AddForeignKey`** با سیاست fail-closed: آرگومان ناشناخته (`comment`، `precision`، ...) رد می‌شود؛ نوع CLR، نوع ستون، nullable، `maxLength`، `unicode`؛ `defaultValue` = فقط مقدار پیش‌فرض CLR برای ستون NOT NULL و هرگز برای ستون nullable؛ FK: جدول/ستون/مقصد/`onDelete` (فقط مقادیر نگاشت‌شده). `DropColumn`، `RenameColumn`، `DropTable`، `InsertData`، `DropForeignKey` در `Up`، حذف یا تغییر رابطه و حذف ستون رد می‌شوند.
- **تست جهشی** `scripts/tests/test_migration_guard_mutations.py` (۳۱ تست روی نسخهٔ موقت): تغییر SQL، حذف `CreateIndex`/`DropIndex`/FK، تغییر نوع/nullable/default/length، `Down` خالی/اشتباه/اضافه، دستکاری Designer تاریخی و Snapshot، Migration ثبت‌نشده و ...؛ هر کدام باید با دلیل درست رد شود. به‌علاوه تست‌های واحد مثبت/منفی `AddColumn` و FK و کشف زنجیره.
- کامیت جدا (فقط ترتیب Snapshot/Designer) مثل قبل باقی است.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. Guardها ضعیف نشدند؛ پوشش آن‌ها به دو Migration اخیر گسترش یافت.

## بررسی و آزمون
نتایج واقعی در گزارش جلسه و توضیح PR ثبت شده است.

## امنیت، داده و سازگاری
- Schema، Migration `.cs` و کد دامنه تغییر نکرد. اثر زمان اجرا ندارد.

## ریسک‌ها و کارهای باقی‌مانده
- بار بعدی که Migration اضافه شود باید ثابت‌های `CURRENT_MIGRATION_*` و `PREVIOUS/NEWEST_MIGRATION` به‌روز شوند (این وابستگی از قبل وجود داشت؛ بهتر است در آینده خودکار شود).
- ترتیب ایندکس/FK در Snapshot به‌دست‌نوشتن حساس است؛ همچنان مولد منبع حقیقت است.

## شناسه‌های Git
- Branch: `fix/migration-guard-hasone-generic`
