# گزارش کار و تحویل Agentها

> گزارش‌ها را به انتهای فایل اضافه کن؛ سابقه قبلی را حذف یا بازنویسی نکن.

## 2026-10-16 — Baseline review / scaffold
- **نوع کار:** بررسی ایستا و آماده‌سازی اسکلت.
- **یافته‌ها:** همه پروژه‌های اصلی روی `net6.0` بودند؛ NuGetها نسخه‌های قدیمی EF Core/Google Auth داشتند؛ Solution پروژه StateMachine را دربر نمی‌گرفت؛ تست پروژه‌ای وجود نداشت؛ `.editorconfig` چند هشدار Nullable را خاموش می‌کرد؛ appsettings حاوی MasterPassword hash و ActivationKeys بود؛ `Program.cs` محیط را براساس Debugger بازنویسی می‌کرد؛ PageModel ورود دارای نام کاربری/Claims hardcode بود؛ یک مدیر نمونه با اطلاعات تماس و رمز عبور شناخته‌شده نیز در EF seed شده بود.
- **تغییرات:** هدف‌گذاری `net9.0`، ایجاد پروژه Application، انتقال پروژه Data به `Infrastructure/Persistence` (با حفظ namespace فعلی)، Central Package Management با نسخه 9.0.21 برای بسته‌های Microsoft، حذف `System.Drawing.Common` بلااستفاده، تنظیم Build/Analyzer، تست پایه xUnit، CI و مستندات معماری/امنیت/Agent.
- **امنیت:** مقادیر MasterPassword/ActivationKeys حذف شدند؛ Login username به تنظیمات منتقل شد؛ Seed مدیر نمونه حذف شد؛ فعال‌سازی در Development غیرفعال و در محیط‌های دیگر فعال است.
- **تست:** Build/Test در بررسی اولیه اجرا نشد؛ محیط آن بررسی `dotnet` نداشت.

## 2026-10-16 — Repository audit and agent workflow hardening
- **دامنه:** بررسی ایستای مخزن و فایل‌های C#.
- **اصلاحات:** تکمیل AGENTS.md، مستندات گردش کار Agentها، کنترل CDN در CI، جلوگیری از Open Redirect پس از ورود، تنظیم امنیت Cookie، اصلاح Middleware قدیمی و مستندات معماری/امنیت.
- **تست:** در بررسی اولیه Restore/Build/Test و ارتقای واقعی assetها اجرا نشدند.

## 2026-10-16 — Multi-agent coordination documentation
- **نوع کار:** تکمیل مستندات گردش کار Agentها؛ بدون تغییر کد اجرایی.
- **وضعیت Git:** تغییرات مستنداتی قبلی روی شاخه مستقل ثبت شدند؛ Merge به main انجام نشده است.

## 2026-10-16 — Issue #7 secure localized login page
- **نوع کار:** بازطراحی محدود صفحه ورود و ثبت ریسک‌های احراز هویت.
- **شاخه:** `agent/UNNUMBERED-secure-login-page`
- **تغییرات:** صفحه ورود دو ستونه واکنش‌گرا با Bootstrap/Bootstrap Icons محلی، رنگ سرمه‌ای/فیروزه‌ای، فرم واقعی Razor Pages، نمایش/مخفی‌سازی رمز عبور، Loading، دسترس‌پذیری پایه و حذف لینک‌های غیرفعال OAuth/بازیابی.
- **امنیت:** ReturnUrl محلی و Cookie/Remember Me/Logout موجود حفظ شدند؛ SHA-256 خام و کاربر مرکزی به‌عنوان ریسک P0 باقی ماندند.
- **داده:** هیچ Schema یا Migration تغییر نکرد.

## 2026-10-16 — Issue #7 CI follow-up and MTP correction
- **تغییر:** پشتیبانی اجرای xUnit v3 با MTP فعال شد و global.json روی SDK `9.0.100` تثبیت شد.
- **وضعیت:** اجرای CI قبلی در زمان ثبت گزارش Pending بود؛ ریسک SHA-256 خام، نبود Rate Limiting/Account Lockout و نبود Integration Test واقعی باقی ماند.

## 2026-10-16 — Issue #11 Taraz Ava login redesign
- **نوع کار:** بازطراحی حرفه‌ای صفحه ورود فارسی و RTL مطابق تصویر مرجع.
- **Issue:** https://github.com/hamidreza1394/Accounting/issues/11
- **شاخه:** `feature/login-redesign`
- **فایل‌های تغییرکرده:** `Server/Pages/Account/Login.cshtml`، `Server/wwwroot/css/rtl/taraz-login.css`، `Server/wwwroot/js/taraz-login.js`، `Server/wwwroot/images/taraz-ava-mark.svg`، `docs/login-redesign.md` و `docs/handoffs/ISSUE-11-login-redesign.md`.
- **تصمیم‌های طراحی:** ساختار دو بخشی مطابق منطق تصویر مرجع، داشبورد مالی SVG تزئینی، کارت شیشه‌ای ورود، پالت سرمه‌ای/آبی/فیروزه‌ای، فونت محلی IRANSansX، طراحی RTL و واکنش‌گرا، reduced-motion و بدون CDN یا پکیج جدید.
- **سازگاری و امنیت:** `Login.cshtml.cs`، `LoginViewModel`، Data Annotations، ValidationSummary، Anti-forgery، Remember Me، ReturnUrl، Cookie Authentication، Claims و Sign-in تغییر نکردند. هیچ Entity، Migration، جدول دیتابیس، Secret یا منطق حسابداری تغییر نکرد.
- **نتیجه بررسی Diff:** شاخه نسبت به `main` هفت Commit جلوتر و یک فایل مستنداتی دارای تغییرات هم‌زمانِ بعدی نبود؛ فایل‌های تغییرکرده در محدوده Login و مستندات مرتبط هستند. تغییرات PageModel و ViewModel در Diff وجود ندارد.
- **نتیجه CI:** آخرین Workflow با شناسه `38921084636` روی Commit `6ebaf2f52582f134605a5f59009aa5e63dd46940` موفق شد. Job `build-and-test` با نتیجه `success` پایان یافت و شامل static asset check، restore، build و test بود: https://github.com/hamidreza1394/Accounting/actions/runs/38921084636
- **Build/Test:** Build و Test در CI واقعی موفق شدند؛ اجرای محلی توسط Agent انجام نشد.
- **بررسی دارایی‌ها:** اسکریپت `scripts/check_static_assets.py` در Job موفق CI اجرا شد و خطای CDN گزارش نکرد.
- **بررسی بصری و اجرای برنامه:** انجام نشد؛ Browser Control در محیط Agent در دسترس نبود و اجرای برنامه به Connection String و Secretهای معتبر نیاز دارد.
- **ریسک‌های باقی‌مانده:** SHA-256 بدون salt، نبود Rate Limiting/Account Lockout و نبود Integration Test واقعی، ریسک‌های قبلی پروژه هستند و در این Issue تغییر نکردند. بررسی بصری مرورگر و اجرای end-to-end هنوز انجام نشده است.
- **وضعیت تحویل:** Draft PR پس از این ثبت Work Log ایجاد می‌شود؛ Merge به `main` انجام نشده است.


## 2026-10-17 — Issue #16 multi-tenant database model and .NET 10
- **Issue:** https://github.com/hamidreza1394/Accounting/issues/16
- **Branch:** feature/issue-16-multitenant-database-net10
- **Scope:** اضافه‌شدن مدل‌های Company، UserCompanyMembership، FiscalPeriod، CompanyRole، PermissionDefinition، CompanyRolePermission و SecurityAuditEvent به Domain و DbContext؛ پیکربندی صریح EF Core؛ اتصال SQL Server از ConnectionStrings استاندارد؛ هدف‌گذاری .NET 10 و EF Core 10؛ به‌روزرسانی CI و مستندات.
- **Connection:** مقدار توسعه محلی Windows Integrated Authentication است؛ استقرار با User Secrets یا متغیر محیطی ConnectionStrings__ConnectionString انجام می‌شود و هیچ credential جدیدی commit نشده است.
- **Migration/Auth:** Migration رسمی هنوز تولید/اعمال نشده است. Database.EnsureCreated() legacy هنوز در سازنده DbContext باقی است. Login هنوز legacy است؛ UserCompanyMembership.UserId فعلاً scalar است و به ASP.NET Core Identity متصل نشده. Tenant isolation/authorization هنوز برای Production کامل نیست.
- **Build/Test:** این محیط امکان اجرای dotnet restore/build/test یا اتصال به SQL Server را نداشت؛ نتیجه واقعی CI باید قبل از ادغام بررسی شود. هیچ ادعای Build یا تست موفق محلی مطرح نمی‌شود.
- **Next:** بررسی CI، تولید و بازبینی Migration baseline با حفظ داده‌ها، مهاجرت Login به Identity، Tenant resolution و تست‌های امنیتی cross-tenant.


## 2026-10-17 — Issue #16 merged; CI follow-up and documentation correction
- **Issue:** https://github.com/hamidreza1394/Accounting/issues/16
- **Pull Request:** https://github.com/hamidreza1394/Accounting/pull/17 — merged into `main`; merge commit `1e0ec4e10799a3a949f33f73659f5b4e656aaff2`.
- **Feature branch final commit before merge:** `659140e43c8a51968c5b5ba73c17546083d3b1c2`.
- **Implemented and merged:** initial Company/Tenant model; user-to-company membership; fiscal periods; company-scoped roles and permission definitions; role-permission mapping; security audit event; EF Core entity configurations and DbSets; configurable SQL Server connection string; .NET 10 / EF Core 10 package and CI target updates; related architecture, development, database and agent handoff documentation.
- **Connection configuration:** local development uses Windows Integrated Authentication for the configured SQL Server named instance. Connection string can be overridden using User Secrets or the `ConnectionStrings__ConnectionString` environment variable. No production password was committed.
- **CI result for feature branch:** workflow `39006446070` failed during **Build** after Restore succeeded; Test was skipped. The observed compile errors are in `Server/Pages/Admin/FileManager/PartialViews/_DisplayPictures.cshtml`: `System.Drawing.Image` cannot be resolved because the `System.Drawing.Common` reference was removed while this existing view still uses `Image`, `Width`, `Height`, resolution and pixel-format members. This failure must be fixed before considering the migration validated. Workflow: https://github.com/hamidreza1394/Accounting/actions/runs/39006446070
- **Main CI at documentation time:** workflow `39006574777` for the merge commit was still in progress when checked; no successful result is claimed here. https://github.com/hamidreza1394/Accounting/actions/runs/39006574777
- **Not completed:** no EF Core Migration was generated or applied; legacy `Database.EnsureCreated()` remains; the current login has not yet migrated to ASP.NET Core Identity; the membership's `UserId` is not yet linked to Identity; request-level tenant resolution, global tenant query filters, authorization policies and cross-tenant integration tests are incomplete.
- **Safety:** do not run schema creation/update automatically against a database with existing data. Prepare and review a baseline/migration against a backup and a disposable test database first.
- **Next actions:** fix the System.Drawing compile errors (restore the required package only if compatible with the target/runtime, or replace the image metadata implementation with a supported library); rerun CI; then implement and test migrations, Identity integration and tenant isolation.


## 2026-10-17 — Fix .NET 10 build failure in image file manager
- **Branch:** `fix/net10-file-manager-image-build`
- **Diagnosis:** CI run `39006699227` failed in `Server/Pages/Admin/FileManager/PartialViews/_DisplayPictures.cshtml` with CS1069/CS1061 errors because the view used `System.Drawing.Image` while `System.Drawing.Common` was intentionally absent. The view decoded each image on the server only to display metadata; this also made the feature dependent on a Windows-only API and unsuitable for cross-platform hosting. Microsoft documents that `System.Drawing.Common` is Windows-only in .NET 6 and later: https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only
- **Fix:** removed server-side image decoding and the `System.Drawing.Image` dependency from the Razor view. Images are rendered by the browser using the existing image URL; file size and attributes remain displayed. Added lazy loading, bounded preview height and `rel="noopener noreferrer"` for new tabs. Replaced locale-sensitive `ToLower()` with `ToLowerInvariant()`.
- **Trade-off:** server-computed width, height, DPI, pixel format and image Tag metadata are no longer shown. This avoids adding a platform-specific dependency; if these metadata fields are a confirmed requirement, implement them later through an explicitly selected cross-platform image library and tests.
- **Validation:** CI must run on this branch before the fix is considered verified. Do not claim Build/Test success until the new workflow result is inspected.


## 2026-10-17 — .NET 10 test runner CI follow-up
- **Observed CI:** main workflow `39007029455` — Build succeeded with 0 errors and 371 analyzer/package warnings; Test failed.
- **Exact failure:** `Microsoft.Testing.Platform.MSBuild.targets`: “Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new dotnet test experience.”
- **Root cause:** repository already uses xUnit v3 and `TestingPlatformDotnetTestSupport=true`, but `global.json` did not select the Microsoft.Testing.Platform runner for the .NET 10 `dotnet test` experience.
- **Attempted fix:** add `"test": { "runner": "Microsoft.Testing.Platform" }` to `global.json` on branch `fix/net10-test-runner-ci`.
- **Other CI finding:** Restore reported NU1902/NU1903 vulnerability warnings for transitive `System.Security.Cryptography.Pkcs` 9.0.11. Determine the dependency path and update to a patched compatible version; do not suppress the advisory.
- **Validation status:** awaiting CI on the follow-up PR. This change is not considered verified until Test succeeds.


## 2026-10-17 — Issue #22 first-run database setup
- **Issue:** https://github.com/hamidreza1394/Accounting/issues/22
- **Branch:** `feature/issue-22-database-first-run-setup`
- **Scope:** افزودن صفحه راه‌اندازی اولیه Razor Pages، تست اتصال SQL Server، ذخیره Connection String رمزنگاری‌شده خارج از مخزن، ساخت دیتابیس تازه و اعمال Migration اولیه، و جلوگیری از تغییر خودکار schema دیتابیس موجود.
- **Implementation:** `DatabaseConnectionSettingsStore` از ASP.NET Core Data Protection استفاده می‌کند؛ در Windows کلیدها با DPAPI محافظت می‌شوند و در Unix مجوز فایل/پوشه محدود می‌شود. کد یک‌بارمصرف در فایل محلی خارج از مخزن نگه‌داری می‌شود و در log چاپ نمی‌شود.
- **Safety:** اتصال‌های صریح Configuration Providers بر فایل محلی مقدم‌اند. فرم SQL Connection String را با builder استاندارد می‌سازد، ورودی نام دیتابیس را به پارامتر SQL می‌دهد و شناسه را با `QUOTENAME` می‌سازد. دیتابیس دارای جدول‌ها خودکار migrate نمی‌شود.
- **Validation:** Restore/Build/Test و اتصال به SQL Server در این محیط اجرا نشده‌اند. CI برای PR باید بررسی شود؛ این تغییر تا سبزشدن CI و آزمون SQL Server، تأییدشده/آماده Production تلقی نمی‌شود.
- **Known limitation:** برای دیتابیس قدیمی که با `EnsureCreated()` ساخته شده و فاقد `__EFMigrationsHistory` است، ابتدا باید schema و baseline بررسی شود؛ صفحه setup عمداً آن را خودکار تغییر نمی‌دهد.
