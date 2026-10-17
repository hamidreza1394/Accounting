# تحویل وظیفه — ISSUE-16-multitenant-database-net10

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/16
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-17
- **وضعیت:** PR #17 ادغام شده، اما کار از نظر CI، Migration و امنیت چندمستاجری ناقص است.

## هدف و معیارهای پذیرش
- [x] مدل اولیه چندشرکتی و دوره مالی در Domain.
- [x] نگاشت‌های EF Core و ایندکس‌های اصلی.
- [x] اتصال SQL Server قابل تنظیم از Configuration Provider.
- [x] تنظیم هدف .NET 10 و EF Core 10.
- [ ] CI/Restore/Build/Test واقعی تأیید شود.
- [ ] Migration سازگار با دیتابیس فعلی تولید و بازبینی شود.
- [ ] هویت و کنترل Tenant در مسیر ورود عملیاتی شود.

## خلاصه تغییرات
- افزودن Company، UserCompanyMembership، FiscalPeriod، CompanyRole، PermissionDefinition، CompanyRolePermission و SecurityAuditEvent.
- افزودن Configurationهای EF Core و DbSetها.
- تنظیم Connection String توسعه محلی در بخش ریشه ConnectionStrings؛ override از User Secrets/Environment.
- ارتقای Target Framework و CI به .NET 10 و بسته‌های EF Core/Google Auth به نسخه 10.0.13.
- به‌روزرسانی راهنمای توسعه، تصمیمات، وضعیت، نقشه راه و استانداردهای کدنویسی.

## فایل‌های مهم تغییرکرده
- Domain/*.cs — مدل‌های جدید چندمستاجری.
- Infrastructure/Persistence/Configurations/*.cs — نگاشت‌های EF Core.
- Infrastructure/Persistence/DatabaseContext.cs — DbSetهای جدید.
- Server/appsettings.json و Server/appsettings.Development.json — اتصال محلی و ریشه استاندارد ConnectionStrings.
- Directory.Build.props، Directory.Packages.props، global.json، فایل‌های csproj و .github/workflows/dotnet.yml — .NET 10.
- docs/*، AGENTS.md و README.md — تصمیم‌ها و دستورالعمل‌ها.

## تصمیم‌های فنی
- یک Company همان Tenant است و شناسه Company.Id شناسه محدوده مستأجر است.
- عضویت با UserId/TenantId یکتا است؛ Role به شرکت محدود است.
- UserCompanyMembership.UserId در این مرحله scalar باقی می‌ماند تا مدل قدیمی User به‌طور ناخواسته به Identity جدید متصل نشود.
- .NET 10 LTS انتخاب شد؛ Visual Studio 2026 نسخه 18.0+ برای هدف‌گذاری رسمی لازم است.
- Connection String محرمانه در Git ذخیره نمی‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| dotnet restore Template.sln | اجرا نشده | SDK محلی در محیط ویرایش در دسترس نبود |
| dotnet build Template.sln --no-restore --configuration Release | اجرا نشده | باید CI بررسی شود |
| dotnet test Template.sln --no-build --configuration Release | اجرا نشده | باید CI بررسی شود |
| SQL Server / Migration | اجرا نشده | اتصال دیتابیس در این محیط وجود نداشت |

## امنیت، داده و سازگاری
- Login فعلی هنوز Legacy است و از ASP.NET Core Identity استفاده نمی‌کند.
- Global tenant filters و مجوزدهی کامل هنوز پیاده‌سازی نشده‌اند.
- Database.EnsureCreated() legacy باقی مانده؛ برای دیتابیس موجود، اضافه‌کردن DbSet به‌تنهایی جدول جدید نمی‌سازد.
- پیش از Production، Migration و مسیر حفظ داده‌ها باید بازبینی شود.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای CI واقعی و رفع خطاهای سازگاری net10.0.
- تولید Migration با baseline صحیح؛ از اجرای خودکار روی دیتابیس دارای داده خودداری شود.
- مهاجرت Login به Identity و اتصال Membership به User جدید.
- Tenant resolution، policy authorization و تست‌های cross-tenant.
- افزودن تست‌های Integration با SQL Server.

## شناسه‌های Git
- Branch: feature/issue-16-multitenant-database-net10
- Commit: در زمان ایجاد گزارش مشخص نشده است.
- Pull Request: پس از ثبت Commit ایجاد می‌شود.

## پیگیری پس از ادغام — 2026-10-17
- CI شاخه Feature: Restore موفق، Build ناموفق و Test اجرا نشده است.
- خطای ثبت‌شده: `Server/Pages/Admin/FileManager/PartialViews/_DisplayPictures.cshtml` به `System.Drawing.Image` وابسته است، اما مرجع `System.Drawing.Common` حذف شده؛ این مورد باید با انتخاب راهکار سازگار با .NET 10 رفع شود.
- اجرای CI روی Merge commit هنگام ثبت گزارش هنوز در حال اجرا بود؛ تا مشاهده نتیجه سبز، وضعیت موفق اعلام نشود.
- کارهای باقیمانده: Migration امن، حذف مسیر `EnsureCreated()` پس از طراحی rollout، اتصال Identity، tenant resolution، فیلترهای Tenant، authorization و تست‌های integration برای جلوگیری از دسترسی بین شرکت‌ها.


## CI build-failure follow-up — 2026-10-17
- **Branch:** `fix/net10-file-manager-image-build`
- **Root cause:** `_DisplayPictures.cshtml` called `System.Drawing.Image.FromFile` and accessed image metadata, but the project no longer references `System.Drawing.Common`. The CI compiler reported CS1069/CS1061 in that Razor view. Re-adding this package would restore compile-time types but retain a Windows-only runtime dependency, which is not suitable for a potentially Linux-hosted web application.
- **Change:** the view now relies on the browser to render the file URL and no longer decodes image files on the server. File length/attributes, file links and image cards are retained. Added lazy loading and safe new-tab link attributes. Extension normalization uses `ToLowerInvariant()`.
- **Known UI trade-off:** image width/height, DPI, pixel format and Tag fields are no longer displayed. These were derived from System.Drawing and are intentionally not replaced with guessed values.
- **Validation status:** awaiting the GitHub Actions run for this branch/PR. Build and tests are not marked successful until CI confirms them.


## Follow-up — .NET 10 test runner CI failure (2026-10-17)
- Latest main CI run `39007029455`: Build succeeded (0 errors, 371 warnings), but Test failed before test execution because Microsoft.Testing.Platform was invoked via the legacy VSTest target.
- Failure message states that .NET 10 requires opting into the new `dotnet test` experience when using Microsoft.Testing.Platform.
- Added the `test.runner = Microsoft.Testing.Platform` setting to `global.json` on branch `fix/net10-test-runner-ci`; the new CI run must verify the behavior before this fix is accepted.
- Restore also reports NU1902/NU1903 for transitive `System.Security.Cryptography.Pkcs` 9.0.11. Trace its dependency and update to a patched version in a separate reviewed change if possible.
- No claim of successful tests until CI passes.
