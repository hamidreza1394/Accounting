# تحویل وظیفه — ISSUE-498-membership-permission-delegation-ceiling

- **Issue:** [#498](https://github.com/hamidreza1394/Accounting/issues/498)
- **Agent/مسئول:** ChatGPT — Backend/Security
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی و تست‌ها اضافه شدند؛ اجرای محلی و CI روی commit نهایی هنوز تأیید نشده است.

## هدف و معیارهای پذیرش
- [x] تأیید عضویت فقط وقتی مجاز است که تمام PermissionIdهای نقش مقصد زیرمجموعه مجوزهای مؤثر تأییدکننده در همان شرکت باشد.
- [x] نقش‌های System از مسیر تأیید عضویت بین‌شرکتی قابل تخصیص نیستند.
- [x] عدم افشای مجوز مفقود؛ پاسخ رد عمومی 403 است.
- [x] در رد واگذاری، درخواست Pending می‌ماند، عضویت ایجاد/فعال نمی‌شود و رویداد تأیید ثبت نمی‌شود.
- [x] نقش بدون مجوز قابل واگذاری است.
- [x] تست‌های جلوگیری از نشت مجوز بین شرکت‌ها اضافه شد.
- [x] تست SQL Server برای ترجمه Query و عدم تغییر داده در رد واگذاری اضافه شد.

## خلاصه تغییرات
- ApproveAsync پس از revalidation مجوز users.membership.approve، PermissionIdهای نقش‌های فعال تأییدکننده را فقط در شرکت مقصد می‌خواند.
- PermissionIdهای نقش مقصد با مجموعه مجوزهای تأییدکننده مقایسه می‌شوند؛ نام و کد نمایشی مجوز مبنای تصمیم نیست.
- نقش IsSystemRole مستقل از subset بودن مجوزها رد می‌شود.
- پاسخ 403 هیچ جزئیاتی درباره مجوزهای مفقود یا نام آن‌ها افشا نمی‌کند.

## فایل‌های مهم تغییرکرده
- Server/Api/CompanyMembershipRequestEndpoints.cs — enforce کردن delegation ceiling در API.
- tests/Template.Tests/CompanyMembershipRequestEndpointTests.cs — چهار تست برای subset مجاز، مجوز اضافه، نقش System و جداسازی شرکت.
- tests/Template.Tests/Integration/CompanyMembershipRequestSqlServerTests.cs — تست SQL Server برای رد نقش با مجوز خارج از سقف و عدم ایجاد عضویت/تغییر وضعیت/ثبت Audit موفق.
- docs/handoffs/ISSUE-498-membership-permission-delegation-ceiling.md — این تحویل.
- docs/INDEX.md و docs/WORK_LOG.md — ثبت مسیر تحویل و سابقه.

## تصمیم‌های فنی
- از CompanyAccessService.HasPermissionAsync موجود برای اعتبارسنجی مجدد عضویت و مجوز تأیید استفاده شده است.
- مجموعه مجوزهای واگذاری با Join روی UserCompanyMemberships، CompanyRoles و CompanyRolePermissions و محدودیت صریح TenantId == companyId ساخته می‌شود.
- هیچ Schema، Migration، UI یا قرارداد درخواست/پاسخ موفق تغییر نکرده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تست‌های واحد CompanyMembershipRequestEndpointTests | اضافه شد؛ اجرا نشده | محیط این نشست امکان اجرای .NET ندارد |
| تست SQL Server CompanyMembershipRequestSqlServerTests | توسعه داده شد؛ اجرا نشده | باید در CI با FARANOU_REQUIRE_SQLSERVER=1 اجرا شود |
| dotnet build Template.sln -c Release | اجرا نشده | نتیجه CI پس از ایجاد PR بررسی می‌شود |
| dotnet test Template.sln -c Release | اجرا نشده | نتیجه واقعی CI باید روی SHA نهایی ثبت شود |
| git diff --check | اجرا نشده | تغییرات از GitHub API ثبت شدند؛ در این محیط working tree محلی وجود ندارد |

## امنیت، داده و سازگاری
- فقط permission IDهای نقش فعال همان شرکت در سقف واگذاری لحاظ می‌شوند؛ مجوزهای شرکت دیگر هیچ اثری ندارند.
- مجوزهای اضافی نقش مقصد و System Role به‌صورت fail-closed رد می‌شوند.
- رد واگذاری قبل از mutation عضویت/درخواست و قبل از ثبت Audit موفق انجام می‌شود.
- هیچ Migration یا تغییر داده‌ای نیاز نیست.

## ریسک‌ها و کارهای باقی‌مانده
- باید CI روی commit نهایی، به‌ویژه تست SQL Server واقعی و تست‌های موجود membership، موفق شود.
- کنترل هم‌زمان تغییر Permissionهای نقش حین تأیید همچنان تابع transaction/مکانیزم concurrency موجود است؛ Issue #496 برای RowVersion نقش و عضویت، کار مستقل بعدی است.

## شناسه‌های Git
- Branch: agent/498-membership-permission-delegation-ceiling
- Commit پیاده‌سازی: 80cff8fa58e44c24b6bcc15be0ea6d6c1ca02186
- Commit همگام‌سازی با main: ad6d3bfc25c555c6484f31e52e889e4879db0f14
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/498
