# تحویل وظیفه — UNNUMBERED-hr-next-employee-number

- **Issue:** UNNUMBERED (مأموریت رابط مدیریت سیستم، PR شماره ۲ — برش مستقل Backend برای فرم کاربر)
- **Agent/مسئول:** Claude Code (Lead) + backend-developer + code-reviewer + security-reviewer
- **تاریخ:** 2026-11-08
- **وضعیت:** انجام‌شده

## هدف و معیارهای پذیرش
- [x] پیشنهاد خودکار شماره پرسنلی برای تب «اطلاعات اصلی» فرم کاربر
- [x] یکتایی در سطح شرکت؛ بدون رزرو و بدون شمارندهٔ جدید
- [x] بدون Migration/Entity/Index جدید؛ مجوز و Tenant Isolation طبق الگوی HR
- [ ] تغییر شماره پس از ایجاد — **NotImplemented / NeedsApproval** (آزادشدن شمارهٔ قدیمی با سیاست «تخصیص مجدد ممنوع» تعارض دارد)

## خلاصه تغییرات
- `GET /api/v1/companies/{companyId}/org/employees/next-number` → `{ "suggestedEmployeeNumber": "..." }`؛ مجوز `hr.employees.create`؛ `no-store`؛ زیر `ApiSensitivePolicy`.
- الگوریتم: بزرگ‌ترین شمارهٔ کاملاً عددی همان شرکت (با کارکنان خاتمه‌یافته) + ۱ با حفظ عرض صفرهای پیشرو، با `BigInteger`؛ سقف ۳۰ رقم (در سرریز `409`).
- پیشنهاد رزروکننده نیست؛ مرجع یکتایی فقط Unique Index `(TenantId, EmployeeNumber)` و `409` در `POST /org/employees` است.

## فایل‌های مهم تغییرکرده
- `Server/Api/HrOrganizationalStructureEndpoints.cs`
- `tests/Template.Tests/HrOrganizationalStructureEndpointTests.cs`، `Integration/HrOrganizationalStructureSqlServerTests.cs`، `Architecture/ApiSensitiveReadRateLimitTests.cs`
- `docs/HR_FRONTEND_API_CONTRACT.md`

## تصمیم‌های فنی
- بازبینی مستقل پیشنهاد کرد شاخهٔ `IsRelational()` و حلقهٔ retry حذف شود؛ پیاده شد: فقط شماره‌های همان شرکت (`varchar(30)`) خوانده و در حافظه فیلتر می‌شود؛ یک مسیر کد برای تست و تولید.
- `SetNoStore` پیش از محاسبه (پوشش 409).
- نقش «فقط ایجاد» بیشینهٔ شمارهٔ عددی را می‌بیند؛ در سند پذیرفته‌شده ثبت شد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln --no-restore -c Release` | موفق | ۰ خطا |
| `dotnet test` فیلتر HrOrganizationalStructure + ApiSensitiveReadRateLimit، روی SQL Server واقعی (Docker) | موفق: ۵۵/۵۵، Skip ۰ | شامل تست SQL Server یکتایی/Race |
| `dotnet test Template.sln` کامل (اجرای Subagent بدون SQL Server) | ۱۰۸۹ موفق، ۲ ناموفق، ۶۷ Skip | ۲ شکست: `SafeExceptionRealApplicationTests` (نبود provider رابطه‌ای در Development)؛ قبلاً در handoff مرحلهٔ سوم #444 به‌عنوان محدودیت محیط ثبت شده؛ این PR بدون ارتباط است |
| `git diff --check` | موفق | |

## امنیت، داده و سازگاری
- بررسی امنیتی مستقل: یافتهٔ مهم/متوسط نداشت؛ Authorization، Tenant Isolation، عضو Pending/Inactive، IDOR، نشت داده، rate limit بررسی شد. بدون Migration.

## ریسک‌ها و کارهای باقی‌مانده
- تغییر شماره پرسنلی پس از ایجاد (NeedsApproval).
- مقایسهٔ ارقام در حافظه به `EmployeeNumberPattern` به‌عنوان تنها مسیر درج وابسته است.

## شناسه‌های Git
- Branch: `feat/hr-next-employee-number` (روی `feat/ui-sidebar-system-menus`)
- Commit / Pull Request: در گزارش PR
