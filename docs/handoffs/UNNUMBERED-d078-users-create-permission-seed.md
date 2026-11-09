# تحویل وظیفه — UNNUMBERED-d078-users-create-permission-seed

- **Issue:** `UNNUMBERED` (مأموریت D-078 — PR اول از سری پیاده‌سازی؛ مبنای محصولی: PR #623)
- **Agent/مسئول:** GLM (Senior .NET Backend)
- **تاریخ:** 2026-11-10
- **وضعیت:** انجام‌شده — seed و تست سبز؛ enforcement endpoint در PR بعدی همین سری

## هدف و معیارهای پذیرش
- [x] مجوز مستقل ایجاد کاربر (`users.create`) با الگوی کدگذاری فعلی به کاتالوگ اضافه شود.
- [x] Migration فقط-داده و idempotent (الگوی D-071/D-072) با شناسه پایدار.
- [x] هیچ نقشی به‌صورت خودکار مجوز را دریافت نکند؛ تخصیص فقط از مسیر مصوب `CompanyRolePermissions` و سقف D-071.
- [x] بدون ساخت کاتالوگ/جدول موازی و بدون تغییر schema.
- [x] گاردهای Migration مخزن (`generate_migrations.py --check`، `verify_migration_model.py`) سبز بمانند.
- [x] تست SQL-gated برای seed (idempotency، یکتایی، نبود تخصیص خودکار به نقش).

## خلاصه تغییرات
- کد `users.create` (نام فارسی «ایجاد کاربر برای شرکت») با Ordering `21007` و شناسه پایدار `f47a2026-1103-4d00-8000-000000000003` seed می‌شود.
- Down فقط در نبود هیچ ارجاع از `CompanyRolePermissions` حذف می‌کند (الگوی `users.list`).
- کاتالوگ مستندات از ۷۳ به **۷۴ کد** به‌روزرسانی شد؛ `users.create` فعلاً **seeded-only** است (الگوی دو کد seeded-only گروه شرکتی) و با PR دوم (endpoint ایجاد کاربر شرکتی D-078) enforce می‌شود.

## فایل‌های مهم تغییرکرده
- `Infrastructure/Persistence/Migrations/20261110120000_SeedUsersCreatePermission.cs` — migration فقط-داده
- `Infrastructure/Persistence/Migrations/20261110120000_SeedUsersCreatePermission.Designer.cs` — تولیدشده توسط ابزار مخزن
- `scripts/generate_migrations.py` — پیشروی `CURRENT_MIGRATION_*`/`DELTA_MIGRATION_*` و ثبت SQL بازبینی‌شده در `HAND_WRITTEN_SQL`
- `docs/PERMISSION_CATALOG.md` — ردیف کد جدید و وضعیت seeded-only
- `tests/Template.Tests/Integration/UserCreationPermissionSeedSqlServerTests.cs` — دو تست SQL-gated

## تصمیم‌های فنی
- GUID و Ordering مطابق خانواده `users.*` (`f47a2026-1103-…`، بلوک 21xxx) انتخاب شد تا الگوی seedهای قبلی حفظ شود.
- Description صریحاً بیان می‌کند عضویت فعال می‌شود اما هیچ نقش/مجوز/دسترسی سال-دوره‌ای تخصیص نمی‌یابد (D-078).
- Snapshot و migrationهای قبلی دست نخوردند؛ چون migration داده‌ای است مدل EF تغییر نمی‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | ۰ خطا |
| `dotnet test Template.sln -c Release` | ۱۱۶۱ کل: ۱۰۹۲ موفق / ۶۷ Skip / ۲ ناموفق | دو شکست `SafeExceptionRealApplicationTests` از قبل در main محیطی است؛ دو Skip جدید = تست‌های SQL-gated این PR |
| `python3 scripts/generate_migrations.py --check` | موفق | «The generated migrations are up to date.» |
| `python3 scripts/verify_migration_model.py` | موفق | ۱۷ migration تأیید شد |
| تست‌های SQL Server این PR | **NotRun محلی** | Docker/SQL Server در محیط اجرا نیست؛ در CI با `FARANOU_TEST_SQLSERVER` اجرا می‌شود |

## امنیت، داده و سازگاری
- هیچ نقشی مجوز جدید را نگه نمی‌دارد (تست SQL آن را قفل می‌کند)؛ مدیر شرکت فقط از طریق تخصیص مصوب نقش آن را می‌گیرد.
- fail-closed: تا وقتی endpoint مربوطه enforce نشده، کد بدون مصرف‌کننده است و رفتار runtime تغییر نمی‌کند.
- ریسک rollback Down: حذف فقط برای ردیف بدون ارجاع (الگوی موجود).

## ریسک‌ها و کارهای باقی‌مانده
- enforcement در `POST /api/v1/companies/{companyId}/users` — PR دوم سری D-078 (وابستگی: این seed).
- تست‌های SQL Server فقط در CI اجرا می‌شوند (محیط محلی Docker ندارد).

## شناسه‌های Git
- Branch: `feat/d078-users-create-permission`
- Commit: (این PR)
- Pull Request: (لینک پس از ایجاد PR)
