# تحویل وظیفه — ISSUE-UNNUMBERED-faranou-modern-architecture

- **Issue:** `UNNUMBERED` (درخواست مستقیم کارفرما: ارتقای معماری و فناوری فرانو)
- **Agent/مسئول:** Claude (claude-opus-5-5)
- **تاریخ:** 2026-10-20
- **وضعیت:** مرحله اول انجام‌شده (Backend امن + API + کلاینت React)؛ مراحل بعدی در «کارهای باقی‌مانده»

## وضعیت پایه (مرحله A)
- شاخه پایه `main`، Commit `f539a36` (Merge PR #36). آخرین CI روی `main` موفق.
- Solution با ۱۱ پروژه، `net10.0`، EF Core 10.0.13، Central Package Management، ۸۰ تست xUnit + ۷ تست jsdom.
- جدول‌های `Companies`، `UserCompanyMemberships`، `FiscalPeriods`، `CompanyRoles`، `PermissionDefinitions`، `CompanyRolePermissions`، `SecurityAuditEvents` در Migrationها واقعاً ساخته می‌شوند (نه فقط `DbSet`).
- هیچ ماژول حسابداری و هیچ فیلد مبلغ در مدل وجود ندارد؛ بررسی `decimal`/گردکردن به ماژول‌های آینده موکول است. `FiscalPeriod` از `DateOnly` (ستون `date`) استفاده می‌کند — مستقل از تقویم نمایشی.
- **یافته‌های قطعی:** (۱) داشبورد خطای دیتابیس را بی‌صدا به فهرست خالی تبدیل می‌کرد؛ (۲) Claim شناسه کاربر صادر نمی‌شد و Tenantها در Cookie کپی می‌شدند (لغو عضویت تا پایان Cookie اثر نداشت)؛ (۳) ثبت‌نام بدون ذخیره، پیام موفقیت می‌داد؛ (۴) خروج با GET و بدون CSRF؛ (۵) API و سرویس سمت سرور Tenant وجود نداشت؛ (۶) D-001 با هدف React متناقض بود؛ منوی مدیریت به همه کاربران نمایش داده می‌شد.

## هدف و معیارهای پذیرش
- [x] صفحه ورود Razor (ظاهر، فیلدها، اعتبارسنجی، `ReturnUrl`، RememberMe، Anti-forgery) بدون تغییر و مسیر اصلی ورود باقی بماند.
- [x] Tenant فقط سمت سرور و از عضویت فعال تعیین شود؛ تست دسترسی بین شرکت‌ها.
- [x] API برای کلاینت جدید با Cookie هم‌مبدأ، Anti-forgery و پاسخ 401/403.
- [x] React + TypeScript strict + Vite + Tailwind، فارسی/RTL/Responsive، بدون داده ساختگی، بدون CDN.
- [x] CI برای Build و تست کلاینت.
- [ ] Global Query Filter، OpenAPI، انتقال ورود به React — عمداً در این مرحله انجام نشد (دلایل پایین).

## خلاصه تغییرات و فایل‌ها
| فایل | تغییر | دلیل |
|---|---|---|
| `Server/Infrastructure/Tenancy/CompanyAccessService.cs` | جدید | تنها نقطه تصمیم دسترسی به شرکت؛ کاربر/عضویت/شرکت/نقش فعال، بررسی در هر درخواست |
| `Server/Api/ApiEndpoints.cs` | جدید | API v1: session، antiforgery token، logout، companies، overview |
| `Server/Api/ApiResponseFilter.cs` | جدید | `no-store`؛ Log خطا و پاسخ عمومی 500 به‌جای افشای جزئیات |
| `Server/Api/ApiCookieAuthenticationEvents.cs` | جدید | 401/403 برای `/api`، حفظ Redirect برای صفحات |
| `Server/Program.cs` | ثبت سرویس، هدر Anti-forgery، رویدادهای Cookie، `MapFaranouApi`، Fallback `/app` | اتصال API و کلاینت؛ تنظیمات Cookie قبلی دست‌نخورده |
| `Server/Pages/Account/Login.cshtml.cs` | `NameIdentifier` اضافه، Claimهای `TenantId` حذف | D-013 |
| `Server/Pages/Index.cshtml(.cs)` | استفاده از `CompanyAccessService`، نمایش خطا به‌جای بلعیدن، پیوند `/app` | گزارش صادقانه خطا |
| `Server/Pages/Account/Logout.cshtml(.cs)` | GET فقط فرم تأیید؛ خروج با POST | جلوگیری از CSRF خروج |
| `Server/Pages/Shared/PartialViews/_LoginStatus.cshtml` | خروج با فرم POST؛ منوی مدیریت فقط برای Admin | CSRF؛ UI منطبق با مجوز سرور |
| `Server/Pages/Account/Register.cshtml.cs` | پیام صادقانه «ثبت‌نام عمومی فعال نیست» | حذف موفقیت ساختگی |
| `tests/Template.Tests/CompanyAccessTests.cs` | جدید | تست‌های Tenant، API، رویدادهای Cookie، خروج |
| `tests/Template.Tests/LoginAuthenticationTests.cs` | یک تست | Claim شناسه و نبود Claim شرکت |
| `ClientApp/**` | جدید | کلاینت React و تست‌ها، اسکریپت بررسی آفلاین |
| `scripts/check_static_assets.py` | پوشش `ClientApp/src` و `index.html` | ممنوعیت CDN در کد کلاینت |
| `.github/workflows/dotnet.yml` | Job `client`؛ Node 24 و `setup-node@v5` | CI کلاینت؛ npm 11 برای lockfile؛ رفع هشدار Node 20 |
| `.gitignore` | `Server/wwwroot/app/`، `ClientApp/node_modules/` | خروجی Build و پکیج‌ها در Git نیستند |
| `docs/*`, `AGENTS.md`, `README.md` | ADR D-011..D-013، وضعیت، امنیت، تست، توسعه، معماری، نقشه راه | هماهنگی مستندات با واقعیت |

## تصمیم‌های فنی
- D-011: React تدریجی در `/app`؛ Razor مسیر بازگشت. بدون Router/State Manager/UI kit (فعلاً یک صفحه).
- D-012: API هم‌مبدأ، Cookie + Anti-forgery؛ بدون Bearer/`localStorage`/CORS.
- D-013: Tenant از دیتابیس در هر درخواست؛ Global Query Filter به نخستین ماژول حسابداری موکول شد چون جریان ورود عمداً عضویت همه شرکت‌ها را می‌خواند و مدل «Tenant جاری» برای مسیرهای بدون شرکت هنوز تعریف نشده است.
- پروژه `Application` هنوز اسکلت است؛ سرویس جدید در کنار `AuthenticationService` قرار گرفت تا Interface/Repository بدون Use Case واقعی ساخته نشود (D-006).
- نسخه‌های npm دقیق (`--save-exact`) و قدیمی‌تر از دو هفته انتخاب شدند: react 19.3.0، vite 8.3.2، tailwindcss 4.3.3، typescript 7.0.2، vitest 5.0.3. `npm audit`: 0 آسیب‌پذیری.
- OpenAPI اضافه نشد: نیازمند پکیج جدید NuGet (`Microsoft.AspNetCore.OpenApi`) بود که در محیط Agent قابل Restore و آزمون نبود؛ قرارداد API در `docs/ARCHITECTURE.md` مستند شد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| CI `build-and-test` روی Commit `387e703` | موفق | Restore، Build Release، همه تست‌های .NET (شامل تست‌های جدید)، jsdom، Migration guards، `has-pending-model-changes` |
| CI روی Commitهای بعدی | به PR مراجعه شود | نتیجه نهایی در توضیح PR ثبت می‌شود |
| `dotnet restore/build/test` در محیط Agent | **اجرا نشد** | دسترسی به nuget.org مسدود بود؛ تأیید فقط از CI |
| `npm run typecheck` | موفق | TypeScript 7 strict |
| `npm test` | موفق — ۱۸ از ۱۸ | Vitest + Testing Library |
| `npm run build` | موفق | خروجی در `Server/wwwroot/app` (JS ~234KB / gzip ~73KB) |
| `npm run check:offline` | موفق | ۱۱ منبع هم‌مبدأ، همه موجود؛ هیچ منبع خارجی. با تزریق عمدی URL خارجی/فایل ناموجود شکست می‌خورد (تأیید شد) |
| `python3 scripts/check_static_assets.py` | موفق | |
| رندر در Chromium (Playwright) با API شبیه‌سازی‌شده | موفق | دسکتاپ 1366px و موبایل 390px؛ `dir=rtl`؛ فونت IRANSansX بارگذاری شد؛ صفر درخواست خارجی صفحه |
| آزمون End-to-End با سرور واقعی و SQL Server | **اجرا نشد** | SQL Server و Restore .NET در محیط Agent نبود |
| رگرسیون ظاهری صفحه ورود (Screenshot قبل/بعد) | **اجرا نشد** | فایل‌های صفحه ورود (`Login.cshtml`، `taraz-login.css/js`) تغییر نکرده‌اند |

## امنیت، داده و سازگاری
- **Schema:** هیچ Migration جدیدی لازم نبود و ساخته نشد؛ مدل دیتابیس تغییر نکرد.
- **سازگاری نشست:** Cookieهای قدیمی Claim شناسه ندارند؛ صفحات Razor کار می‌کنند (جست‌وجو با نام کاربری)، API برای آن‌ها 401 می‌دهد و کاربر یک‌بار دوباره وارد می‌شود.
- **رفتار تغییرکرده:** پیوند GET خروج اکنون فقط فرم تأیید نشان می‌دهد؛ ثبت‌نام عمومی صریحاً غیرفعال اعلام می‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- **بالا:** تست Integration HTTP (`WebApplicationFactory`) و SQL Server واقعی؛ آزمون دستی ورود → `/app` روی سرور واقعی؛ CSP برای `/app`؛ Security Stamp برای ابطال نشست‌های Razor پس از تغییر رمز/غیرفعال‌سازی.
- **متوسط:** OpenAPI؛ Global Query Filter همراه نخستین ماژول حسابداری؛ Rate Limiting برای API؛ اجرای `npm run build` در فرایند انتشار (MSBuild/Publish) تا `/app` همیشه در خروجی باشد.
- **پایین:** انتقال صفحه ورود و Layout به React پس از تست رگرسیون ظاهری؛ رفع هشدارهای Analyzer قدیمی (`Dtat`، `Constants`).

## شناسه‌های Git
- Branch: `feature/faranou-modern-architecture`
- Base: `f539a36`
- Commits: `387e703` (Backend)، `6a3f484` (Client/CI)، و Commit مستندات
- Pull Request: در توضیح PR (Merge نشده — منتظر تأیید مسئول مخزن)
