# وضعیت پروژه

> **بازبینی مرجع 2026-10-21 (شاخه `docs/architecture-source-of-truth`)** — مبنا: `main` @ `1d39dbf9e0843045e00441bf2778267e9764a3f6`. بخش «وضعیت تأییدشده فعلی» زیر معتبر است؛ بخش‌های بعد از آن تا «هدف» **تاریخی و در موارد مشخص‌شده منسوخ** هستند و برای حفظ سابقه نگه داشته شده‌اند. در تناقض، ترتیب منابع حقیقت D-030 (`docs/INDEX.md`) ملاک است. واژگان وضعیت: `Accepted`, `Implemented`, `Verified`, `Proposed`, `NeedsApproval`, `Deprecated`, `Blocked`, `NotImplemented`.

## وضعیت تأییدشده فعلی (2026-10-21)

### شواهد اجرا
- **CI:** اجرای GitHub Actions شماره `39585708662` روی commit مبنا: هر ۶ Job موفق — Build، ۲۰۴ تست .NET (۰ رد، ۰ Skip) روی SQL Server 2022، گاردهای Migration (`generate_migrations.py --check`، `verify_migration_model.py`، `dotnet ef migrations has-pending-model-changes`)، Job کلاینت، E2E (۱۲ تست)، اسکن آسیب‌پذیری NuGet بدون مورد. این اجرا توسط CI انجام شده است.
- **این بازبینی مستنداتی Build/Test اجرا نکرده است** (`dotnet` در محیط این Agent نصب نیست). ادعاهای «Verified» به اجرای بالا استناد می‌کنند، نه به اجرای محلی.
- اجرای محلی گزارش‌شده در دور قبل (2026-10-21، SDK 10.0.113): ۲۰۴ تست .NET موفق، `check_static_assets.py` PASS، تست‌های کلاینت (۲۴) موفق — طبق `WORK_LOG.md`.

### آنچه `Implemented` است
- Razor Pages + Minimal API `/api/v1` + کلاینت React در `/app`، هدف `net10.0`، EF Core 10، SQL Server.
- مدل چندشرکتی: `Company`، `UserCompanyMembership`، `CompanyRole`، `PermissionDefinition`، `CompanyRolePermission`، `FiscalPeriod`، `SecurityAuditEvent`؛ تعیین دسترسی شرکت فقط با `CompanyAccessService` (D-013).
- **Migrationها وجود دارند:** `InitialCreate`، `AlignSchemaWithModel`، `WidenUserPasswordHash` (مسیر `Infrastructure/Persistence/Migrations`). اظهار قدیمی «Migration ساخته نشده» منسوخ است.
- ورود امن با `PasswordHasher<T>` (ارتقای هش SHA-256 قدیمی هنگام ورود)، Cookie + Anti-forgery، Rate Limiting، CSP/هدرهای امنیتی، اعتبارسنجی مجدد نشست.
- فرایند Publish قطعی و Smoke Test در CI؛ راه‌اندازی اولیه دیتابیس (Issue #22).

### آنچه `NotImplemented` است
- **هیچ ماژول حسابداری** (سند، سرفصل، گزارش) در کد نیست.
- **هیچ سیستم لایسنس/مشتری تجاری/نصب/زیرسیستم** (D-020..D-027؛ `docs/LICENSING.md`).
- **مصرف مجوز عملیاتی:** `PermissionDefinition`/`CompanyRolePermission` در دیتابیس هستند ولی در `Server/` بررسی نمی‌شوند (D-025).
- Global Query Filter (طبق D-013 عمداً وجود ندارد)، OpenAPI.

### وضعیت Activation قدیمی (اصلاح گزاره منسوخ)
`ActivationKeysHandlerMiddleware` **کد مرده است**: `UseActivationKeys()` در `Server/Program.cs` فراخوانی نمی‌شود (از commit `370fe67`، 2026-10-17، Issue #22). گزاره قدیمی «در محیط‌های دیگر فعال است» **منسوخ و نادرست** است. هیچ کنترل لایسنس در برنامه فعال نیست. وجود مشتری Production با Activation قدیمی: **نامشخص**. جزئیات: D-028.

### ریسک‌ها و تصمیم‌های باز
- هویت: چهار دامنه (صفحات `Admin`، Master زیرساختی، نقش شرکتی، مدیر فروشنده) باید طبق D-029 جدا بمانند؛ `UserCompanyMemberships.UserId` بدون FK است.
- دوره ارفاق ۳۰ روزه، فهرست زیرسیستم‌ها، الگوریتم امضا، مسیر گذار Activation قدیمی و کپی ماشین مجازی: `NeedsApproval` (ر.ک. `docs/LICENSING.md` بخش ۶). کپی کامل VM در نصب آفلاین قابل جلوگیری نیست.
- مسیر مرده Google OAuth در صفحه ورود (AUD-2026-10-21-02)؛ نبود فایل `LICENSE` در مخزن؛ ناهمخوانی نام‌ها (Template/TarazAva/Dtat/Faranou)؛ Branch protection مخزن نامشخص؛ شکاف دستور تست `dotnet test Template.sln` در CI با `--solution` لازم در SDK محلی 10.0.113 — ر.ک. `docs/REPOSITORY_AUDIT.md`.

---
**بخش‌های زیر تاریخی‌اند (وضعیت اولیه بازسازی). موارد منسوخ: ذکر «dotnet نصب نیست/Build اجرا نشده»، «Migration نیست»، «پروژه تست موجود نبود»، «Activation فعال است»، و «Google OAuth کامنت شده» (در واقع Handler ثبت نشده است).**

## هدف (تاریخی)
پایه‌سازی پروژه موجود ASP.NET Core Razor Pages برای توسعه مرحله‌ای و قابل بازبینی.

## وضعیت فعلی پس از بازسازی اولیه
- در شاخه `main` هدف‌گذاری به `net10.0` منتقل شده است؛ CI پس از Merge باید سبز شود.
- نسخه NuGetها با Central Package Management در `Directory.Packages.props` متمرکز شده است.
- تحلیلگرهای .NET و تنظیمات Nullable در سطح مخزن فعال شده‌اند.
- پروژه تست اولیه اضافه شده و به Solution افزوده شده است.
- گردش‌کار CI برای Restore، Build و Test اضافه شده است.
- مقادیر MasterPassword و ActivationKeys از فایل‌های تنظیمات حذف شده‌اند؛ مقادیر لازم باید در محیط مناسب پیکربندی شوند.
- حساب مدیر نمونه با اطلاعات تماس و رمز عبور ثابت از EF Core seed حذف شده است؛ ایجاد حساب واقعی باید از مسیر امن مدیریت هویت انجام شود.
- ~~در Development، Middleware فعال‌سازی اجرا نمی‌شود؛ در محیط‌های دیگر فعال است.~~ **منسوخ (2026-10-21):** پس از commit `370fe67` این Middleware به Pipeline متصل نیست (کد مرده)؛ D-028.
- `Program.cs` از محیط استاندارد ASP.NET Core پیروی می‌کند و نبود Connection String را با پیام روشن گزارش می‌دهد.

## محدودیت مهم
در محیط اجرای این بازسازی، `dotnet` نصب نیست؛ بنابراین Restore، Build، Test و بررسی هشدارهای کامپایل اجرا نشده‌اند. فایل‌ها و XML/JSON به‌صورت ایستا بررسی می‌شوند؛ CI باید اولین Build واقعی را انجام دهد.

## ریسک‌های باقی‌مانده
1. پروژه اکنون `.NET 10 LTS` را هدف گرفته است؛ خطاهای Build در CI باید قبل از ادامه توسعه برطرف شوند.
2. احراز هویت فعلی یک راهکار قدیمی با نام کاربری مرکزی و SHA-256 بدون salt است؛ برای تولید مناسب نیست و باید با ASP.NET Core Identity یا PasswordHasher استاندارد جایگزین شود.
3. پروژه `Application` به‌عنوان مرز آماده Use Case ایجاد شده است؛ persistence به `Infrastructure/Persistence` منتقل شده (با حفظ namespace قدیمی `Data`)، اما دسترسی مستقیم به EF Core در PageModelها هنوز وجود دارد و باید مرحله‌ای و همراه تست حذف شود.
4. در مخزن پروژه تست موجود نبود؛ تست اولیه فقط قرارداد تابع Hashing موجود را تثبیت می‌کند و جای تست‌های واقعی را نمی‌گیرد.
5. کتابخانه‌های فرانت‌اند مدیریت‌شده با LibMan هنوز به‌صورت جامع ارتقا داده نشده‌اند تا تغییر Major بدون آزمون UI وارد نشود.
6. صفحات About/Contact/SEO هنوز شامل نام و اطلاعات نمونه صاحب قالب هستند و باید قبل از انتشار با اطلاعات محصول جایگزین شوند.
7. مسیر Google OAuth در صفحه وجود دارد اما ثبت Handler در `Program.cs` کامنت شده است؛ باید فعال یا حذف شود.

## معیار ورود به فاز بعد
- اجرای Restore/Build/Test روی .NET 10 SDK و رفع خطاهای CI.
- بررسی Migration و حفظ داده‌های موجود.
- تعیین مدل احراز هویت، نقش‌ها و مجوزها.
- ثبت Issueهای مهاجرت معماری و افزودن تست برای مسیرهای حیاتی.


## بررسی تکمیلی 2026-10-16
- `docs/REPOSITORY_AUDIT.md` گزارش یافته‌های بررسی ایستا و ریسک‌های باقی‌مانده است.
- `docs/CLASS_REVIEW_INVENTORY.csv` فهرست 199 فایل C# و خروجی اسکن ساده است؛ این فایل به‌تنهایی جای بازبینی رفتاری خط‌به‌خط و تست را نمی‌گیرد.
- Redirect ورود به URL محلی محدود شد؛ Cookie تنظیمات پایه امن‌تری گرفت؛ Middleware قدیمی دیگر Exception را بی‌صدا نمی‌بلعد.
- اسکریپت کنترل دارایی CDN به CI افزوده شد.
- نسخه‌های فعلی Bootstrap (5.2.2) و Bootstrap Icons (1.9.1) هنوز نیاز به ارتقای واقعی فایل‌های محلی دارند؛ این کار به علت نبود شبکه در محیط بررسی انجام نشد.
- Restore/Build/Test هنوز باید روی دستگاه توسعه یا CI اجرا و نتیجه ثبت شود.


## 2026-10-17 — Issue #16: foundation for tenant data model and .NET 10
- Target framework و نسخه SDK در شاخه کاری برای .NET 10 LTS تنظیم شده‌اند؛ تأیید Build واقعی هنوز لازم است.
- مدل‌های Company، UserCompanyMembership، FiscalPeriod، CompanyRole، PermissionDefinition، CompanyRolePermission و SecurityAuditEvent و پیکربندی EF Core آن‌ها اضافه شده‌اند.
- Connection String پیش‌فرض توسعه به ریشه استاندارد `ConnectionStrings:ConnectionString` منتقل شده و از Environment/User Secrets قابل override است.
- **محدودیت:** Login فعلی هنوز Legacy است؛ عضویت جدید هنوز به ASP.NET Core Identity متصل نشده و کنترل کامل Tenant در درخواست‌ها فعال نشده است. Migration رسمی برای دیتابیس‌های موجود و آزمون SQL Server باید جداگانه تکمیل شود.
- **تست:** این تغییر از طریق GitHub API و بدون اجرای SDK در محیط ویرایش اعمال شده است؛ Restore/Build/Test محلی توسط این Agent اجرا نشده‌اند. CI باید نتیجه واقعی را ثبت کند.


## وضعیت پس از Merge مربوط به Issue #16 — 2026-10-17
- PR #17 در `main` ادغام شده است: https://github.com/hamidreza1394/Accounting/pull/17
- Merge commit: `1e0ec4e10799a3a949f33f73659f5b4e656aaff2`.
- مدل پایه چندشرکتی، دوره مالی، عضویت شرکت، نقش/مجوز، رویداد امنیتی، پیکربندی EF Core و تنظیمات قابل تغییر اتصال SQL Server در مخزن ثبت شده‌اند.
- هدف پروژه به .NET 10 / EF Core 10 منتقل شده است.
- **CI سبز نیست/هنوز تأیید نشده:** اجرای CI شاخه Feature با Restore موفق ولی Build ناموفق تمام شد؛ خطاهای مشاهده‌شده مربوط به استفاده از `System.Drawing.Image` در `Server/Pages/Admin/FileManager/PartialViews/_DisplayPictures.cshtml` پس از حذف مرجع `System.Drawing.Common` است. اجرای CI روی Merge commit در زمان ثبت این یادداشت هنوز در حال اجرا بود. وضعیت را از https://github.com/hamidreza1394/Accounting/actions بررسی کن.
- **مهم:** Migration ساخته یا اعمال نشده است؛ `Database.EnsureCreated()` قدیمی باقی مانده؛ Login هنوز Legacy است و ASP.NET Core Identity، tenant resolution، فیلترهای سراسری Tenant، policy authorization و تست‌های cross-tenant تکمیل نشده‌اند.
- این مرحله فقط پایه مدل و پیکربندی است و برای Production آماده نیست.


## 2026-10-17 — Issue #22 first-run database setup (branch in progress)
- پیاده‌سازی اولیه فرم راه‌اندازی دیتابیس، ذخیره محلی محافظت‌شده Connection String، کد یک‌بارمصرف setup، آزمایش اتصال و ایجاد/مهاجرت دیتابیس تازه در شاخه `feature/issue-22-database-first-run-setup` ثبت شده است.
- رشته اتصال صریح از User Secrets/Environment بر تنظیمات محلی مقدم است. رشته اتصال پیش‌فرض از `appsettings.Development.json` حذف شده تا فرم first-run در توسعه قابل استفاده باشد.
- Migration روی دیتابیس دارای جدول‌ها خودکار اجرا نمی‌شود تا دیتابیس قدیمی مبتنی بر `EnsureCreated()` ناخواسته تغییر نکند.
- **وضعیت تأیید:** Restore/Build/Test و اتصال واقعی SQL Server هنوز اجرا/تأیید نشده‌اند. تا بررسی CI و تست SQL Server، این تغییر برای Production تأییدشده نیست.
- گزارش handoff: `docs/handoffs/ISSUE-22-database-first-run-setup.md`.

## 2026-10-19 — Issue #30 تکمیل احراز هویت صفحه ورود
- جریان ورود با DI واقعی، Post/Redirect/Get، پاک‌سازی رمز از `ModelState` و مقصد موقت محافظت‌شده `/Account/LoginSuccess` تکمیل شد.
- رمز عبور به `PasswordHasher<T>` منتقل شد؛ هش قدیمی SHA-256 پذیرفته و پس از ورود موفق ارتقا می‌یابد.
- Migration `WidenUserPasswordHash` ستون `Users.Password` را به `varchar(255)` بزرگ می‌کند.
- **تأییدشده:** `dotnet restore`, `dotnet build -c Release` (0 error), `dotnet test` (90/90), `scripts/check_static_assets.py`, `git diff --check`.
- **تأییدنشده:** اجرای Migration روی SQL Server واقعی و آزمون End-to-End روی مرورگر در این محیط انجام نشده است.
- گزارش: `docs/handoffs/ISSUE-30-secure-login-authentication.md`.


## 2026-10-20 — ارتقای معماری (شاخه `feature/faranou-modern-architecture`)
- **مبنا:** `main` در `f539a36` (CI سبز).
- **Backend:** سرویس `CompanyAccessService` (تعیین Tenant از عضویت فعال در دیتابیس)، API نسخه 1 در `/api/v1` (نشست، توکن Anti-forgery، خروج، شرکت‌ها، نمای کلی شرکت)، پاسخ 401/403 برای API، `Cache-Control: no-store` و پاسخ خطای عمومی با Log. Claim شناسه کاربر صادر می‌شود و Claimهای `TenantId` حذف شدند.
- **اصلاحات:** داشبورد Razor خطای دیتابیس را دیگر نمی‌بلعد؛ خروج فقط با POST و Anti-forgery؛ ثبت‌نام دیگر موفقیت ساختگی گزارش نمی‌کند؛ منوی مدیریت فقط برای Admin نمایش داده می‌شود (کنترل اصلی همچنان `[Authorize(Roles = Admin)]` است).
- **Frontend:** `ClientApp` با React 19.3، TypeScript 7 (strict)، Vite 8، Tailwind 4؛ داشبورد راست‌چین فارسی در `/app` با داده واقعی (شرکت‌ها و دوره‌های مالی)، حالت خالی/خطا، تاریخ شمسی فقط در نمایش، فونت محلی.
- **تأییدشده:** CI GitHub Actions روی شاخه (Build، همه تست‌های .NET، jsdom، Migration guards، `has-pending-model-changes`، و Job کلاینت) — نتیجه در PR ثبت شده است. محلی: `npm run typecheck`، `npm test` (۱۸ تست)، `npm run build`، `npm run check:offline`، `scripts/check_static_assets.py`، رندر واقعی در Chromium (دسکتاپ/موبایل) با API شبیه‌سازی‌شده.
- **تأییدنشده:** اجرای برنامه روی SQL Server واقعی و آزمون End-to-End ورود → `/app` روی مرورگر با سرور واقعی؛ Build/Test .NET در محیط Agent (NuGet در دسترس نبود) — فقط CI.
- **هنوز وجود ندارد:** هیچ ماژول حسابداری (سند، سرفصل، گزارش)، OpenAPI، Global Query Filter، انتقال صفحه ورود به React.
- گزارش کامل: `docs/handoffs/ISSUE-UNNUMBERED-faranou-modern-architecture.md`.
- **به‌روزرسانی:** PR #38 در `main` ادغام شد (`207d81f`).

## 2026-10-20 — بازبینی امنیتی پس از PR #38 (شاخه `security/post-pr38-hardening`)
- اعتبارسنجی مجدد نشست در هر درخواست، CSP و هدرهای امنیتی، Rate Limiting قابل تنظیم، Exception Handler اختصاصی API، `no-cache` برای index.html کلاینت.
- فرایند انتشار قطعی (`scripts/publish.sh`/`publish.ps1`)، توقف Publish بدون کلاینت، Smoke Test خروجی Publish در CI.
- بررسی آفلاین بازنویسی و با تست پوشش داده شد.
- ۲۷ تست Integration HTTP با `WebApplicationFactory` روی **SQL Server 2022 واقعی در CI**.
- **تأییدشده در CI:** Restore/Build، ۱۶۴ تست .NET (۰ رد، ۰ Skip) با SQL Server، jsdom، گاردهای Migration، `has-pending-model-changes`، اسکن آسیب‌پذیری NuGet (موردی نیست)، Job کلاینت، Publish و Smoke Test.
- **اجرانشده در محیط Agent:** Build/Test .NET (NuGet مسدود)، `publish.ps1` (ویندوز).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-post-pr38-security-review.md`.


## 2026-10-20 — پیگیری آمادگی Production (شاخه `security/production-readiness-followup`)
- پایه: `main` @ `2511907` (ادغام PR #39).
- محافظ پوشه خروجی در `publish.sh`/`publish.ps1` با تست؛ اجرای واقعی `publish.ps1` روی Windows در CI.
- Proxy مورد اعتماد، بررسی `AllowedHosts`، دستور تولید اعتبارنامه Master و امکان خاموش‌کردن قالب قدیمی آن.
- حذف inlineهای Razor و اجباری‌شدن CSP آن‌ها (به‌جز PageManager)، Endpoint گزارش CSP (پیش‌فرض خاموش)، اصلاح نام فونت‌ها برای میزبان حساس به حروف.
- E2E مرورگر واقعی ← سرور منتشرشده ← SQL Server 2022.
- **تأییدشده در CI:** ۲۰۴ تست .NET (۰ رد، ۰ Skip) روی SQL Server؛ ۱۲ تست E2E؛ تست‌های محافظ Bash و PowerShell (Linux و Windows)؛ انتشار Linux + Smoke؛ انتشار Windows؛ Job کلاینت؛ اسکن NuGet.
- **نامعین:** دامنه، Proxy و تعداد نمونه Production (پرسش‌های باز در `docs/DEPLOYMENT.md`).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-production-readiness-followup.md`.

## 2026-10-21 — ممیزی جامع، تکمیل تیم Agent، تست واقعی (شاخه `fix/comprehensive-quality-hardening`)
- پایه: `main` @ `8244bf3`.
- تیم Subagent به ۹ نقش رسید (۳ مورد جدید: `database-engineer`, `devops-release-engineer`, `code-reviewer`)؛ هیچ Agent قبلی حذف نشد.
- تناقض نسخه .NET 9/10 در `docs/AGENT_START_PROMPT.md`، `docs/prompts/CODEX_PROMPT.md`، `docs/prompts/COPILOT_PROMPT.md` رفع شد (مطابق D-007 واقعی).
- **شکست CI Job `publish` روی `main`@`8244bf3` (Run 39483239598) با شواهد واقعی Flaky تشخیص داده شد، نه Regression:** بازتولید محلی ۶/۶ موفق + `gh run rerun` همان Run با نتیجه `success` در تمام Jobها روی همان commit. جزئیات کامل: `docs/REPOSITORY_AUDIT.md` (AUD-2026-10-21-01).
- **تست‌های واقعی این دور (همگی اجراشده و سبز):**
  - `dotnet test --solution Template.sln --configuration Release` → `total: 204, failed: 0, succeeded: 204, skipped: 0`. (نکته ابزار: سینتکس `--solution` به‌جای مسیر مستقیم Sln در .NET SDK 10.0.113 لازم است.)
  - `python3 scripts/check_static_assets.py` → PASS.
  - `dotnet format Template.sln --verify-no-changes --no-restore` → بدون تغییر لازم.
  - `npm ci && npm run typecheck && npm test -- --run && npm run build && npm run check:offline` در `ClientApp` → همه موفق (۲۴ تست Vitest Pass؛ Build و بررسی آفلاین بدون خطا).
- **ریسک باقی‌مانده بدون تغییر:** مسیر مرده Google OAuth (`AUD-2026-10-21-02`، نیاز به تصمیم حذف/تکمیل)، آسیب‌پذیری Low در `vite` (`AUD-2026-10-21-03`، فقط ابزار توسعه)، عدم امکان تست SQL Server واقعی در این محیط (`FARANOU_TEST_SQLSERVER` اجرا نشد).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-comprehensive-quality-hardening.md`.

## انتخاب شرکت و دوره مالی (D-031) — 2026-10-21
- `Implemented` در شاخه `feature/company-fiscal-period-selection` (PR باز، ادغام‌نشده): `/api/v1/context`، Cookie زمینه کاری، صفحه انتخاب، صفحات مسدود، نوار زمینه، کیت UI کوچک.
- `Verified` فقط محلی: ۲۵۳ تست .NET (Provider حافظه‌ای)، ۴۸ تست کلاینت، Build کلاینت و بررسی آفلاین، بررسی بصری با API شبیه‌سازی‌شده. CI و E2E روی SQL Server هنوز اجرا نشده‌اند.
- `Accepted` (D-032، تصمیم کارفرما): Open = ورود عادی، Closed = فقط مشاهده/گزارش، تغییر زمینه حین نشست با ممیزی؛ `Implemented` در شاخه. `NotImplemented`: اِعمال فقط‌خواندنی روی Endpointهای نوشتن آینده.
- `NeedsApproval`: رفتار دوره `Archived` و دوره‌های آینده.


## آمادگی کاربران هم‌زمان ابری (Issue #49، ممیزی 2026-10-22)

- **وضعیت ظرفیت 100 کاربر:** `NotVerified`. در این دور فقط ممیزی مخزن و مستندات انجام شده؛ آزمون بار روی محیط واقعی اجرا نشده است.
- **شواهد معماری موجود:** Modular Monolith، .NET 10/EF Core 10/SQL Server، Razor Pages + API هم‌مبدأ `/api/v1` + React، Rate Limiting و تنظیمات Reverse Proxy. جزئیات در `docs/CLOUD_CONCURRENCY_READINESS.md`.
- **شکاف‌ها:** آزمون بار قابل تکرار در مخزن یافت نشد؛ تعداد نمونه/مشخصات Production باز است؛ limiter به‌ازای هر نمونه است؛ ماژول‌های حسابداری و آزمون صحت نوشتن مالی هنوز پیاده‌سازی نشده‌اند.
- **محدودیت این ممیزی:** فایل‌های کد و مستندات از GitHub بررسی شدند؛ Build/Test/Load Test در این دور اجرا نشده‌اند. هیچ نتیجه runtime از روی جست‌وجوی کد استنباط نشده است.
- **گام بعدی:** اجرای پرامپ `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md` در شاخه مستقل Issue #49، با رعایت کامل `AGENTS.md` و بدون merge خودکار.

## گروه شرکتی، گزارش تجمیعی و تلفیقی (D-033..D-035) — وضعیت پس از ادغام PR #53 (2026-10-22)
- **وضعیت Git:** PR #53 ادغام شده است؛ Merge commit در `main`: `1d30436d25b4f1e76b95e76a764afe249fd84f6f`. [CI](https://github.com/hamidreza1394/Accounting/actions/runs/39826940050): همه Jobهای گزارش‌شده موفق.
- `Implemented`: جداول گروه/عضویت/مالکیت/پروفایل/اجرا/محدوده اجرا، Migration افزایشی `20261022150943_AddCorporateGroups`، قواعد دامنه، `CompanyAccessService.HasPermissionAsync` و `CorporateGroupService`. گزارش محلی قبلی ۳۰۵/۳۰۵ تست SQL Server 2022 در handoff تاریخی ثبت شده است؛ این سطر ادعای اجرای مجدد محلی در این بازبینی نیست.
- `NotImplemented`: API/UI گروه، تخصیص مجوز به نقش‌ها، دفتر/کدینگ، تعدیلات و تطبیق درون‌گروهی؛ `Blocked`: گزارش عددی تا وجود دفتر و کدینگ؛ مرز `CustomerAccount` در ابر مشترک و چرخه کنترلی همچنان `NeedsApproval`.
- Migration روی دیتابیس مشتری/تولید اعمال نشده است.
- گزارش اولیه: `docs/handoffs/ISSUE-UNNUMBERED-corporate-groups-consolidation.md`.

## بازطراحی راه‌اندازی دیتابیس و تنظیمات اتصال (D-036) — 2026-10-23
- **وضعیت Git:** شاخه `feature/database-setup-redesign`؛ Commit/Push/PR انجام نشده و ادغام نشده است. به‌همین دلیل `main` هنوز مسیر قدیمی `TarazAva` را دارد.
- `Implemented` (فقط در شاخه): پوشه تنظیمات `Farano` با سیاست مسیر، رمزنگاری با `ApplicationName=Farano`، مهاجرت فقط‌خواندنی از `TarazAva`، حالت‌های Configured/Setup/Blocked، فرمان `--settings status|migrate`، مراحل و کدهای خطای راه‌اندازی، رد دیتابیس بیگانه، رفع دو علت پیام عمومی «اطلاعات فرم را بررسی کنید»، بازطراحی UI. جزئیات: [`DATABASE_SETUP.md`](DATABASE_SETUP.md).
- **`Verified` محلی توسط Lead (2026-10-23، SDK 10.0.402، SQL Server 2022 واقعی):** build ۰ خطا؛ ۴۸۹ تست، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip (دو تست `chmod` با root)؛ E2E ۱۸ از ۱۸؛ ClientApp ۴۸ از ۴۸؛ `check_static_assets.py` PASS. بدون SQL Server (گزارش Agent پیاده‌ساز، دوباره اجرا نشد): ۴۶۹ موفق/۲۰ Skip. CI هنوز باز نشده.
- `NeedsApproval`: تصمیم D-036 و املای Farano/Faranou.
- **اجرا نشده:** رفتار Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl` — فقط کامپایل شده)؛ Login SQL با رمز منقضی/غیرفعال؛ CI؛ دو POST هم‌زمان روی HTTP؛ POST بوت‌استرپ Master روی HTTP.
- **بازبینی:** `code-reviewer` یک دور بدون High؛ `security-reviewer` دو دور، پایان بدون Critical/High/Medium؛ `architect` روی Diff نهایی اجرا نشد.
- **یافته باز:** `AddDataProtection` سراسری تنظیم نشده (AUD-2026-10-23-02). E2E صفحه راه‌اندازی به‌روز و موفق شد (AUD-2026-10-23-01 حل‌شد).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-database-setup-redesign.md`.
