# تحویل وظیفه — ISSUE-491-hr-api-contract-hardening

- **Issue:** #491 (ممیزی و تثبیت Backend منابع انسانی پس از ادغام فاز ۳ و ۴؛ زیرمجموعه #444)
- **Agent/مسئول:** Z.ai (GLM) — از طرف مالک مخزن
- **تاریخ:** 2026-11-05
- **وضعیت:** انجام‌شده — Backend HR برای شروع فاز ۳ (UI) آماده است؛ ۴ نقص واقعی پیدا و با TDD اصلاح شد؛ قرارداد مصرف Frontend سند شد.

## هدف و معیارهای پذیرش
- [x] ممیزی مدل چهارگانه HR (`CompanyOrganizationalUnit`، `CompanyPosition`، `CompanyEmployee`، `CompanyEmployeeAssignment`) و `HrRules` روی آخرین `main` (`5715df82`).
- [x] ممیزی کامل ۲۵ Endpoint HR از کد واقعی (Route، DTO، Permission، Validation، Tenant Scope، Audit، Concurrency).
- [x] ممیزی امنیت: دوحالته D-071، کاتالوگ ۱۲ کد `hr.*` در Migration، Tenant Isolation با FK ترکیبی، Anti-forgery، Rate Limit، no-store، DTO Allowlist.
- [x] ۴ نقص واقعی شناسایی و با تست بازگشت (قرمز → سبز) اصلاح شد؛ هیچ تستی حذف/Skip/تضعیف نشد.
- [x] Migration و Snapshot سالم: `has-pending-model-changes` خالی؛ Migration جدید لازم نبود و ساخته نشد.
- [x] قرارداد تفصیلی مصرف Frontend در `docs/HR_FRONTEND_API_CONTRACT.md` ثبت شد (۲۵ Endpoint، همه `Verified`).
- [x] نتیجه CI واقعی آخرین `main` بررسی شد (۱۰۶۸ تست، ۰ ناموفق، با SQL Server + E2E ۴۶ موفق).

## نقص‌های پیدا‌شده و اصلاح‌شده
1. **دور زدن گارد غیرفعال‌سازی واحد از مسیر PUT:** `PUT /org/units/{id}` می‌توانست با `isActive: false` واحدی را که فرزند فعال یا انتساب فعال دارد غیرفعال کند، در حالی که `POST /deactivate` همان کار را با ۴۰۹ مسدود می‌کند — نقض قاعده دامنهٔ §4.1 سند طراحی/D-072. اصلاح: قاعدهٔ خالص `HrRules.ValidateUnitDeactivation` (با همان enum موجود `CompanyUnitDeactivationBlock`) و اعمال یکسان روی هر دو مسیر؛ ۴۰۹ با همان پیام‌ها.
2. **دور زدن گارد غیرفعال‌سازی سمت از مسیر PUT:** همان نقص برای `PUT /org/positions/{id}` با انتساب فعال. اصلاح: قاعدهٔ خالص `HrRules.ValidatePositionDeactivation` (عضو جدید `HrRuleViolation.PositionDeactivationBlocked = 16`) و اعمال روی هر دو مسیر.
3. **خاتمه‌همکاری ناسازگار با تاریخ استخدام:** `POST /org/employees/{id}/deactivate` قاعدهٔ دورهٔ همکاری را صدا نمی‌زد؛ کارمند با `HiredOnUtc` آینده‌نگر (که Create می‌پذیرد) با خاتمهٔ امروز، `TerminatedOnUtc < HiredOnUtc` می‌گرفت و ناوردای `InvalidPeriod` را در داده ذخیره می‌کرد. اصلاح: فراخوانی `HrRules.ValidateEmployment(Terminated, HiredOnUtc, now)` پیش از تغییر وضعیت؛ نقض → 400 fail-closed.
4. **Audit نادرستِ تغییر والد واحد:** در `HrUnitUpdated` فیلد `parentChanged` پس از انتساب محاسبه می‌شد و همیشه `false` بود. اصلاح: ثبت مقدار قبلی `ParentUnitId` پیش از mutation و گزارش صحیح تغییر.

## تغییرات تست
- ۱۵ تست جدید (بدون حذف/تغییر تست‌های موجود): ۶ تست قاعدهٔ خالص در `HrRulesTests` (۳۳)، ۹ تست Endpoint در `HrOrganizationalStructureEndpointTests` (۳۱) شامل پوشش مستقیم قبلیِ کم‌تست: `GetUnit`، `GetPosition`، `UnlinkEmployeeUser`، فهرست تخت واحدها. `HrWorkflowsEndpointTests` (۷) و SQL-gated (۵) دست‌نخورده.
- شواهد TDD: اجرای فیلترشده پیش از اصلاح → ۶۰ کل، ۵ ناموفق (دقیقاً تست‌های جدید رفتار ممنوع)؛ پس از اصلاح → ۷۱ کل، ۰ ناموفق.

## فایل‌های تغییرکرده
- `Domain/Hr/HrRules.cs` — دو قاعدهٔ خالص گارد غیرفعال‌سازی + عضو enum.
- `Server/Api/HrOrganizationalStructureEndpoints.cs` — گاردهای PUT واحد/سمت، اعتبارسنجی خاتمه، اصلاح Audit، استفاده از قواعد خالص در Deactivateها.
- `tests/Template.Tests/HrRulesTests.cs`، `tests/Template.Tests/HrOrganizationalStructureEndpointTests.cs` — ۱۵ تست جدید.
- `docs/HR_FRONTEND_API_CONTRACT.md` (جدید)، `docs/FRONTEND_API_CONTRACT_NEEDS.md` (بند ۵)، `docs/WORK_LOG.md`.

## بررسی و آزمون (نتیجه واقعی محلی، SDK 10.0.403)
| فرمان/آزمون | نتیجه | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 Error |
| `dotnet test Template.sln -c Release` (پیش از تغییر) | ۱۰۶۸ کل: ۱۰۰۴ موفق، ۲ ناموفق محیطی (`SafeExceptionRealApplicationTests` — در baseline هم تکرار می‌شود)، ۶۲ Skip (SQL-gated محلی) | مبنا |
| همان (پس از تغییر) | ۱۰۷۹ کل: ۱۰۱۵ موفق، همان ۲ ناموفق محیطی، ۶۲ Skip | +۱۵ تست جدید، بدون رگرسیون |
| `dotnet test --filter Hr*` | ۷۱/۷۱ موفق | ۳۳+۳۱+۷ |
| `python3 scripts/generate_migrations.py --check` | موفق | «The generated migrations are up to date.» |
| `python3 scripts/verify_migration_model.py` | موفق | ۱۵ Migration، Up و Down، سینتکس C# معتبر |
| `dotnet ef migrations has-pending-model-changes` | «No changes have been made to the model since the last migration.» | Migration جدید لازم نیست |
| تست‌های SQL Server (`FARANOU_REQUIRE_SQLSERVER=1`) | روی CI آخرین main موفق اجرا شده (run 41979174876: 1068 کل، 0 ناموفق، 8 Skip) | ۵ تست SQL-gated HR در همین مجموعه |
| E2E | روی CI آخرین main موفق (۴۶ passed) | `hr-structure.spec.mjs` در مجموعه CI |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی کاهنده‌ای رخ نداده؛ دو bypass قاعدهٔ دامنه بسته شد و رفتارهای ۴۰۹/۴۰۰ با پیام‌های موجود حفظ شد.
- بدون Migration، بدون تغییر Schema/Snapshot، بدون تغییر Entity/Configuration؛ تغییرها فقط Endpoint + قواعد خالص + تست + مستند.
- قرارداد مصرف Frontend فقط از DTOهای واقعی نقل می‌کند؛ هیچ فیلد حدسی اضافه نشده.

## ریسک‌ها و کارهای باقی‌مانده
- رفتار «فعال‌شدن دوبارهٔ واحد زیر والد غیرفعال از مسیر PUT» مستند شد (بخش ۷ قرارداد)؛ بستن آن تصمیم دامنهٔ مالک می‌خواهد، نه اصلاح خودکار.
- معنای دوگانهٔ null در `validFromUtc`/`validToUtc` و `hiredOnUtc` در قرارداد ثبت شد؛ اگر مالک معنای کامل-جایگزینی بخواهد، تغییر رفتار + تست لازم است.
- اجرای تست‌های SQL Server و E2E برای Commitهای این PR با CI انجام می‌شود؛ نتیجه در خود PR ثبت می‌گردد.

## شناسه‌های Git
- Branch: `feat/hr-backend-api-contract-hardening`
- Pull Request: `feat(hr): harden backend API contracts for frontend integration` به `main` (Merge فقط با اجازهٔ مالک).
