# تحویل وظیفه — ISSUE-512-security-audit-report-api

- **Issue:** #512 (زیرمجموعه #444؛ #507؛ D-070)
- **مسئول:** ChatGPT — Backend/API
- **تاریخ:** 2026-11-03
- **وضعیت:** Implemented؛ Build، تست‌ها و CI هنوز تأیید نشده‌اند. Retention در D-070 با وضعیت Proposed باقی مانده است.

## هدف و معیارهای پذیرش
- [x] sort allowlist فقط `occurredAt` و `eventType` با جهت asc/desc؛ ورودی نامعتبر 400.
- [x] ترتیب پایدار با Id DESC؛ هیچ رشته ستون/SQL از کلاینت ساخته نمی‌شود.
- [x] فیلتر companyId و DateTimeOffset از قبل موجود حفظ شد.
- [x] خروجی `OccurredAtUtc` با Kind=UTC و پسوند Z.
- [x] endpoint فهرست نوع رویداد با code و label سمت سرور.
- [x] endpoint فهرست شرکت‌های نصب با صفحه‌بندی و فقط مدیر نصب.
- [x] retention به‌صورت Proposed مستند شد؛ هیچ purge، job یا delete API ساخته نشد.

## خلاصه تغییرات
- `GET /admin/security/audit-events` پارامترهای `sort` و `descending` دریافت کرد و با switch ثابت EF query را مرتب می‌کند.
- `GET /admin/security/audit-event-types` کدهای شناخته‌شده و کدهای ثبت‌شده را همراه label فارسی برمی‌گرداند؛ کد ناشناخته label برابر code دارد.
- `GET /admin/companies` فهرست صفحه‌بندی‌شدهٔ شرکت‌ها برای فیلتر گزارش را به مدیر نصب می‌دهد.
- تمام GETهای جدید/گزارش no-store و rate-limited و installation-admin-only هستند.
- D-070 برای retention و legal hold در DECISIONS ثبت شد.

## قرارداد API
- `GET /api/v1/admin/security/audit-events?eventType=&userId=&companyId=&fromUtc=&toUtc=&sort=occurredAt|eventType&descending=true|false&page=&pageSize=`.
- sort فقط دو مقدار allowlist؛ مقدار دیگر 400؛ پیش‌فرض occurredAt DESC؛ tie-breaker Id DESC.
- `GET /api/v1/admin/security/audit-event-types` → `[{ code, label }]`.
- `GET /api/v1/admin/companies?search=&page=&pageSize=` → `{ items: [{ id, name, isActive }], totalCount, page, pageSize }`.
- همهٔ مسیرها فقط مدیر نصب؛ unauthorized 401/403 طبق authentication/authorization helper.
- from/to از نوع DateTimeOffset و مقایسه پس از تبدیل به UTC؛ تمام OccurredAtUtcهای response با Kind UTC serialize می‌شوند.
- companyId filter موجود حفظ شد؛ response هیچ IP یا DetailsJson ندارد.

## Retention — Proposed / NeedsApproval
- D-070 گزینه‌های دوره ثابت، دوره بر اساس طبقه رخداد و legal hold/archive را ثبت می‌کند.
- مجوز پیشنهادی `security.audit.retention.manage` و رخدادهای Audit پیشنهادی در D-070 هستند؛ هنوز در permission catalog یا runtime فعال نشده‌اند.
- تا تصویب مالک/حقوقی، هیچ endpoint، schema، job، purge یا حذف از UI پیاده‌سازی نمی‌شود.

## فایل‌های مهم تغییرکرده
- `Server/Api/SecurityAuditAdministrationEndpoints.cs`
- `tests/Template.Tests/SecurityAuditAdministrationEndpointTests.cs`
- `tests/Template.Tests/Integration/SecurityAuditAdministrationSqlServerTests.cs`
- `docs/FRONTEND_API_CONTRACT_NEEDS.md`, `docs/DECISIONS.md` (D-070), `docs/SECURITY.md`, `docs/WORK_LOG.md`, `docs/INDEX.md` و این handoff.

## امنیت و داده
- sort key از allowlist ثابت انتخاب می‌شود؛ هیچ SQL dynamic/raw query اضافه نشده.
- شرکت‌ها و event types فقط به مدیر نصب برمی‌گردند؛ پاسخ allowlist و no-store است.
- EventType labelها سمت سرور هستند؛ unknown code با label برابر code نمایش داده می‌شود.
- retention فقط Proposed است؛ هیچ داده‌ای حذف یا Migration ایجاد نشده.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Unit tests sorting/catalog/company list | اجرا نشده | در محیط فعلی runtime محلی مخزن در دسترس نیست؛ CI باید اجرا کند. |
| SQL Server integration | اجرا نشده | تست موجود گسترش یافته؛ CI باید SQL Server واقعی را اجرا کند. |
| `dotnet restore Template.sln` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `python3 scripts/check_static_assets.py` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| Migration guards / pending model changes | اجرا نشده | هیچ Migration ایجاد نشده. |
| `git diff --check` | اجرا نشده | working tree محلی در دسترس نیست. |

## ریسک‌ها و کارهای باقی‌مانده
- کامپایل و SQL Server query translation تا CI Verified نیستند.
- فهرست event types شامل codes ثبت‌شده و registry شناخته‌شده است؛ برای code جدید ناشناخته label fallback همان code خواهد بود.
- Retention منتظر تصمیم مالک است؛ D-070 هیچ دوره‌ای را تصویب نکرده است.

## شناسه‌های Git
- Branch: `feat/512-security-audit-report-api`
- Commit نهایی و PR: پس از ساخت PR ثبت می‌شوند.
