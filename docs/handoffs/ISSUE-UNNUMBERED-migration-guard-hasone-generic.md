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
1. **شواهد EF:** روی `main` (`e9fef6a`) دستور `dotnet ef migrations has-pending-model-changes` (EF 10.0.13) می‌گوید «No changes have been made to the model since the last migration». پس اختلاف واقعی بین مدل و Snapshot/Migration وجود ندارد و Migration یا Snapshotِ معنایی تغییر نکرد.
2. **محدودیت پارسر:** `scripts/ef_model.py` فقط `.HasOne(` / `.HasMany(` را می‌شناخت، نه فرم عمومی بدون Navigation `HasOne<T>().WithMany()` که PR #68 برای `Users.DefaultCompanyId` آورد.
3. **عقب‌ماندن ابزارها:** پس از رفع پارسر، `generate_migrations.py` هنوز `AddCorporateGroups` را «آخرین Migration» می‌دانست و `verify_migration_model.py` پنجرهٔ `WidenUserPasswordHash → AddCorporateGroups` را؛ دو Migration بعدی (`AddUserDefaultCompany`، `AddCompanyCustomerBoundary`) را هیچ Guard نمی‌سنجید. تأیید می‌کرد که Designer قدیمی با مدل فعلی نمی‌خواند و `AddColumn` را هم نمی‌فهمید.
4. **ترتیب دستی در Snapshot:** Snapshot و Designer آخرین Migration (کامیت‌های `75af614`، `7ffe1c3`) دستی ویرایش شده بودند و ترتیب ایندکس/FK مربوط به `DefaultCompanyId` با خروجی مولد فرق داشت. همهٔ خطوط یکسان‌اند و فقط جابه‌جا شده‌اند.

## خلاصه تغییرات
- `scripts/ef_model.py`: پشتیبانی از `HasOne<T>().WithMany().HasForeignKey(...)` (فقط همین فرم بدون Navigation؛ بدون `HasForeignKey` یا با نوع ناشناخته خطا می‌دهد).
- `scripts/generate_migrations.py`: Migration جاری = `AddCompanyCustomerBoundary`؛ FK بدون Navigation به‌صورت `HasOne("…", null)` تولید می‌شود (همان شکل EF).
- `scripts/migration_model.py`، `scripts/verify_migration_model.py`: پنجرهٔ تأیید `AddUserDefaultCompany → AddCompanyCustomerBoundary`؛ پشتیبانی از `AddColumn`؛ حذف ستون هنوز نیازمند Migration دستی است.
- `scripts/tests/test_migration_tooling.py` (جدید) + گام CI «Test the migration guard tooling». تست‌ها روی پارسر قدیمی ناموفق بودند (۳ خطا، ۱ شکست) و روی جدید موفق‌اند.
- **کامیت جدا:** بازتولید `DatabaseContextModelSnapshot.cs` و `…AddCompanyCustomerBoundary.Designer.cs` فقط برای ترتیب (و هدر Designer). نیاز به نگاه ویژهٔ مالک.

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
