# تحویل وظیفه — ISSUE-511-me-permissions

- **Issue:** #511 (زیرمجموعه #444؛ D-013، D-064، D-065)
- **Agent/مسئول:** ChatGPT — Backend/API
- **تاریخ:** 2026-11-03
- **وضعیت:** Implemented؛ Build، تست‌ها و CI هنوز تأیید نشده‌اند.

## هدف و معیارهای پذیرش
- [x] `GET /api/v1/me/permissions` بدون ورودی شرکت.
- [x] شرکت فقط از WorkContext محافظت‌شده همین نشست و با اعتبارسنجی مجدد از WorkContextService/CompanyAccessService تعیین شود.
- [x] فقط مجوزهای CompanyRole فعالِ عضویت فعال همان شرکت برگردد.
- [x] مدیر نصب از نقش فعال دیتابیس در هر درخواست محاسبه شود.
- [x] بدون زمینه معتبر، `company: null`؛ پاسخ no-store.
- [x] تست‌های unit برای شرکت‌های متعدد، پارامتر جعلی، عضویت/نقش غیرفعال، بدون شرکت و مدیر نصب.
- [x] تست SQL Server برای ترجمه پرس‌وجوی مجوز و جداسازی شرکت.
- [x] بدون Migration و بدون تغییر ClientApp یا E2E.

## خلاصه تغییرات
- قرارداد #509 در `docs/FRONTEND_API_CONTRACT_NEEDS.md` با دستور مالک هماهنگ شد: پارامتر `companyId` حذف است و شرکت از نشست جاری تعیین می‌شود.
- Endpoint جدید در `Server/Api/MePermissionsEndpoints.cs` اضافه و در `ApiEndpoints` ثبت شد.
- `CompanyAccessService` اکنون overload خواندن تمام مجوزهای نقش فعال همان شرکت را دارد؛ مسیر prefix قبلی حفظ شده است.
- تست‌های endpoint و SQL Server اضافه شدند.

## قرارداد API
- `GET /api/v1/me/permissions`
- پاسخ 200: `{ isInstallationAdministrator: boolean, company: { id: guid, permissionCodes: string[] } | null }`.
- هیچ `companyId` از query/route/body خوانده نمی‌شود؛ query پارامتر اضافی نادیده گرفته می‌شود.
- شرکت از کوکی محافظت‌شده WorkContext همین SessionId خوانده و با DB مجدداً اعتبارسنجی می‌شود.
- 401: هویت کاربر نامعتبر یا حساب غیرفعال؛ 200 با `company: null`: نشست فاقد زمینه معتبر.
- `Cache-Control: no-store` و `Pragma: no-cache`.
- API صرفاً برای منو/کنترل UI است؛ endpointهای عملیاتی باید authorization مستقل داشته باشند.

## فایل‌های مهم تغییرکرده
- `Server/Api/MePermissionsEndpoints.cs` — endpoint و DTO.
- `Server/Api/ApiEndpoints.cs` — ثبت route.
- `Server/Infrastructure/Tenancy/CompanyAccessService.cs` — overload مجوزهای شرکت.
- `tests/Template.Tests/MePermissionsEndpointTests.cs` — تست‌های unit/endpoint.
- `tests/Template.Tests/Integration/MePermissionsSqlServerTests.cs` — تست SQL Server.
- `docs/FRONTEND_API_CONTRACT_NEEDS.md`، `docs/WORK_LOG.md`، `docs/INDEX.md` و این handoff.

## تصمیم‌های فنی
- بدون Migration، cache، Claim مجوز یا تغییر مدل داده.
- پاسخ مجوزهای شرکت از نشست جاری است، نه شناسه شرکت ارسالی؛ تغییر نقش/عضویت در درخواست بعدی اثر می‌گذارد.
- `isInstallationAdministrator` از `Users.Role` فعال و `Role.IsActive` در DB محاسبه می‌شود، مطابق D-064.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست واحد endpoint | اجرا نشده | در محیط فعلی runtime محلی مخزن در دسترس نیست؛ CI باید اجرا کند. |
| SQL Server integration | اجرا نشده | تست افزوده شده؛ SQL Server واقعی فقط در CI/محیط متصل قابل تأیید است. |
| `dotnet restore Template.sln` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `python3 scripts/check_static_assets.py` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| Migration guards / pending model changes | اجرا نشده | هیچ Migration ایجاد نشده؛ گارد CI همچنان باید اجرا شود. |
| `git diff --check` | اجرا نشده | working tree محلی در دسترس نیست. |

## امنیت، داده و سازگاری
- ورودی شرکت وجود ندارد؛ endpoint به QueryString، route، body یا claim برای تعیین شرکت متکی نیست.
- `CompanyAccessService` عضویت، کاربر، شرکت و نقش فعال را در DB کنترل می‌کند.
- مجوزهای شرکت دیگر در پاسخ ظاهر نمی‌شود؛ هیچ داده حساس یا IP برگردانده نمی‌شود.
- تغییر API ناسازگار نیست؛ قرارداد قبلی Proposed بود و قبل از پیاده‌سازی نهایی اصلاح شد.

## ریسک‌ها و کارهای باقی‌مانده
- کامپایل و تست‌ها هنوز با CI اثبات نشده‌اند؛ وضعیت Verified نیست.
- Claude می‌تواند UI را پس از بررسی قرارداد نهایی مصرف کند؛ UI در این PR تغییر نکرده است.
- هر endpoint عملیاتی همچنان باید مجوز خودش را سمت سرور enforce کند.

## شناسه‌های Git
- Branch: `feat/511-me-permissions`
- Commit نهایی: در زمان ساخت PR ثبت می‌شود.
- Pull Request: پس از ساخت ثبت می‌شود.
