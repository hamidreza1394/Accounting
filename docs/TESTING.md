# راهبرد آزمون

## رویکرد
- **TDD**: برای منطق جدید و پرریسک، ابتدا تست شکست‌خورده، سپس پیاده‌سازی و بازآرایی. لازم نیست هر تغییر نمایشی با TDD کامل انجام شود.
- **BDD**: معیارهای پذیرش با زبان قابل فهم کسب‌وکار نوشته شوند؛ در صورت نیاز به سناریوهای Given/When/Then از ابزار مناسب استفاده شود.
- Unit Test برای قوانین Domain/Application بدون دیتابیس یا شبکه.
- Integration Test برای EF Core، SQL Server، احراز هویت، Middleware و مسیرهای HTTP.
- تست‌های end-to-end فقط برای جریان‌های مهم و با محیط قابل تکرار.

## ساختار
`tests/Template.Tests` فعلاً یک تست پایه برای رفتار تابع Hashing قدیمی دارد. این تست فقط نمونه اتصال xUnit و CI است؛ پوشش تجاری معنادار محسوب نمی‌شود. تست‌های بعدی باید با Use Caseهای واقعی و مستقل از جزئیات پیاده‌سازی نوشته شوند.

## فرمان‌ها
```bash
dotnet restore Template.sln
dotnet build Template.sln --no-restore --configuration Release
dotnet test Template.sln --no-build --configuration Release
```

## قاعده گزارش
نتیجه واقعی هر دستور، کد خروج، تست‌های اجراشده/نشده و علت شکست ثبت شود. اجرای موفق Build جایگزین تست عملکردی و بازبینی امنیتی نیست.


## تست‌های راه‌اندازی پایگاه داده (Issue #22)
- `DatabaseSetupValidationTests` ورودی‌های فاقد سرور، روش احراز هویت نامعتبر و SQL Login بدون رمز را بدون اتصال به SQL Server بررسی می‌کند.
- این تست‌ها Integration Test نیستند؛ مسیرهای Windows Authentication، SQL Login معتبر، ایجاد دیتابیس، اعمال Migration، Data Protection و دیتابیس موجود باید روی SQL Server واقعی در محیط ایزوله آزموده شوند.
- تا زمان اجرای CI و تست SQL Server، پیاده‌سازی راه‌اندازی اولیه تأیید نهایی نشده است.

## تست‌های جریان ورود (Issue #30)
- `LoginAuthenticationTests` جریان PageModel را با `DatabaseContext` روی provider حافظه‌ای (`Microsoft.EntityFrameworkCore.InMemory`، فقط آزمون) و یک `IAuthenticationService` ثبت‌کننده اجرا می‌کند: فیلد خالی، نام کاربری ناموجود، رمز اشتباه، حساب غیرفعال، قفل موقت، ورود موفق، Remember Me، ثبت `LoginLog`/`LastLoginDateTime`، ارتقای هش قدیمی، حساب زیرساختی، `ReturnUrl` محلی و خارجی، و خطای زیرساخت.
- صدور کوکی با ثبت فراخوانی `SignInAsync` (scheme و `IsPersistent`) بررسی می‌شود؛ این آزمون جای آزمون HTTP واقعی روی مرورگر را نمی‌گیرد.
- `PasswordHashServiceTests` قالب جدید، salt یکتا، رد رمز اشتباه، پذیرش و درخواست ارتقای هش قدیمی و رد مقدار نامعتبر را پوشش می‌دهد.
- `LoginConfigurationTests` به‌صورت ایستا بررسی می‌کند که مقصد موقت `[Authorize]` دارد و فرم/ماژول حسابداری ندارد، صفحه ورود Anti-forgery و toggle رمز را حفظ کرده، و تنظیمات Cookie/ترتیب Middleware تغییر نکرده است.
- اجرای Migration روی SQL Server واقعی همچنان آزمون‌نشده است.


## تست‌های جداسازی شرکت و API (2026-10-20)
- `CompanyAccessServiceTests`: دسترسی فقط با عضویت فعال در شرکت فعال با نقش فعال؛ عدم دسترسی به شرکت Tenant دیگر، شرکت ناموجود و `Guid.Empty`؛ حساب غیرفعال؛ اثر فوری لغو عضویت؛ نادیده‌گرفتن Claim روی هویت احراز‌نشده.
- `ApiEndpointTests`: فهرست شرکت‌ها فقط برای کاربر جاری، حساب زیرساختی بدون شرکت، 401 برای نشست قدیمی بدون شناسه، نمای کلی فقط با داده همان شرکت، 404 یکسان برای Tenant دیگر و شرکت ناموجود، بی‌اثر بودن Claim جعلی `TenantId`، مسیرهای API.
- `ApiCookieAuthenticationEventsTests`: پاسخ 401/403 برای `/api` و حفظ Redirect صفحه ورود برای صفحات مرورگر.
- `SessionHardeningTests` و `SuccessfulSignIn_IssuesTheUserIdentifier_AndNoCompanyClaims`: خروج فقط با POST، Claim شناسه کاربر و نبود Claim شرکت.
- این تست‌ها Endpointها را مستقیم با EF Core InMemory اجرا می‌کنند؛ تست HTTP کامل با `WebApplicationFactory` و SQL Server واقعی هنوز اضافه نشده است.

## تست‌های کلاینت React (`ClientApp`)
```bash
cd ClientApp && npm ci && npm run typecheck && npm test && npm run build && npm run check:offline
```
- `src/api/client.test.ts`: ارسال Cookie هم‌مبدأ، 401 → `UnauthenticatedError`، 404 بدون جزئیات، عدم نمایش متن خطای 500، رد پاسخ ناسازگار با قرارداد، Escape شناسه شرکت، ارسال توکن Anti-forgery در خروج، `ReturnUrl` فقط محلی.
- `src/App.test.tsx`: نمایش داده واقعی و تاریخ شمسی، حالت خالی (بدون شرکت/بدون دوره)، نمایش خطا به‌جای فهرست خالی و تلاش دوباره، انتقال به صفحه ورود، نادیده‌گرفتن شرکت آدرس که سرور برنگردانده، تغییر شرکت.
- `src/lib/date.test.ts`: تبدیل تاریخ میلادی ذخیره‌شده به شمسی بدون جابه‌جایی منطقه زمانی.
- `npm run check:offline` خروجی Build را برای منبع خارجی و وجود همه فایل‌های محلی (فونت، تصویر) بررسی می‌کند.


## تست‌های Integration HTTP (2026-10-20)
- `tests/Template.Tests/Integration` با `WebApplicationFactory<Program>` کل Pipeline واقعی را اجرا می‌کند: هدرها، Exception Handler، Cookie Authentication و اعتبارسنجی مجدد، Rate Limiting، Authorization، Anti-forgery، Routing، Razor Pages و API.
- **دیتابیس:** با متغیر `FARANOU_TEST_SQLSERVER` تست‌ها روی **SQL Server واقعی** اجرا می‌شوند؛ Schema با Migrationهای EF Core ساخته می‌شود و نام دیتابیس باید با `FaranouTests` شروع شود (حذف و دوباره ساخته می‌شود). CI یک Container از `mcr.microsoft.com/mssql/server:2022-latest` بالا می‌آورد و با `FARANOU_REQUIRE_SQLSERVER=1` اگر Provider واقعاً SQL Server نباشد یا Migration معلق باشد شکست می‌خورد. بدون متغیر، Provider حافظه‌ای استفاده می‌شود که **معادل SQL Server نیست** (بدون Constraint و ترجمه SQL).
- سناریوها: 401 بدون HTML برای API ناشناس؛ حفظ Redirect صفحات؛ ورود موفق با Cookie از نوع HttpOnly/Secure/SameSite=Lax و هویت درست؛ Remember Me؛ `ReturnUrl` محلی/خارجی؛ رمز اشتباه؛ رد فرم ورود بدون توکن؛ رد خروج API بدون توکن یا با توکن جعلی؛ خروج API و Razor فقط با POST و توکن معتبر؛ پایان نشست پس از غیرفعال‌سازی یا تغییر رمز؛ فقط شرکت‌های دارای عضویت/شرکت/نقش فعال؛ قطع دسترسی در درخواست بعد از لغو عضویت؛ 404 یکسان برای شرکت دیگران و ناموجود؛ شناسه غیر GUID؛ نبود داده شرکت دیگر در پاسخ؛ پاسخ خطای عمومی بدون اسرار؛ هدرها و CSP؛ 429 برای API و ارسال فرم ورود؛ حساب زیرساختی.
- اجرای محلی با SQL Server (فقط دیتابیس آزمایشی):
  ```powershell
  $env:FARANOU_TEST_SQLSERVER = "Server=localhost;Database=FaranouTests;Trusted_Connection=True;TrustServerCertificate=True"
  dotnet test Template.sln --configuration Release
  ```

## بررسی منابع آفلاین کلاینت
`ClientApp/scripts/offline-check.mjs` (تست‌ها: `offline-check.test.mjs`) روی خروجی واقعی Build و نیز روی خروجی Publish اجرا می‌شود. HTML (`src`/`href`/`srcset`)، CSS (`url()`/`@import`، نسبت به خود فایل CSS)، JS (`import()` با رشته ثابت، `new URL(..., import.meta.url)`، فهرست Preload ویت، و رشته‌های ثابت با پسوند منبع) بررسی می‌شوند؛ Query string و Fragment برای یافتن فایل حذف می‌شوند. **محدودیت:** آدرس‌هایی که در زمان اجرا ساخته می‌شوند (الحاق رشته، داده API، محتوای کاربر) با اسکن ایستا قابل کشف نیستند؛ قبولی این بررسی شاهد است، نه تضمین کامل استقلال از اینترنت.

## Publish و Smoke Test
`scripts/publish.sh` و سپس `scripts/smoke-published-app.sh` در Job `publish` اجرا می‌شوند: `/app/` و مسیر داخلی کلاینت (همان index.html با CSP سخت‌گیرانه و `Cache-Control: no-cache`)، همه Bundleهای ارجاع‌شده، فونت و تصویر محلی، 401 برای API ناشناس و رندر فرم ورود. Smoke test به دیتابیس متصل نمی‌شود.


## تست‌های آمادگی Production (2026-10-20)
| حوزه | تست | محیط اجرا |
|---|---|---|
| محافظ پوشه خروجی Bash | `scripts/tests/publish-output.test.sh` (۲۲ حالت، هر حالت ردشده بقای فایل نگهبان را بررسی می‌کند) | CI Linux، و محلی |
| محافظ پوشه خروجی PowerShell | `scripts/tests/publish-output.tests.ps1` | CI: PowerShell 7 روی Linux و **Windows واقعی** |
| انتشار کامل با `publish.ps1` | Job `publish-windows` | CI Windows |
| Proxy، HSTS، HTTPS، Rate Limiting، AllowedHosts | `Integration/DeploymentConfigurationTests` | CI، SQL Server |
| مهاجرت Master | `MasterCredentialTests` | CI |
| نبود inline، گزارش CSP، حروف دقیق URLهای CSS | `ContentSecurityPolicyTests` | CI |
| **E2E مرورگر** | `tests/e2e` (Playwright/Chromium ← سرور منتشرشده در Production ← SQL Server 2022 واقعی)، ۱۲ سناریو | CI Job `e2e` |

**سناریوهای E2E:** صفحه ورود با فونت محلی و بدون نقض CSP؛ رد رمز اشتباه و حساب غیرفعال؛ Cookie از نوع HttpOnly و نمایش فقط شرکت‌های خود در خانه Razor؛ Remember Me؛ داشبورد React با داده واقعی، تغییر شرکت و نادیده‌گرفتن شناسه شرکت بیگانه؛ 404 یکسان برای شرکت دیگران و شرکت ناموجود؛ رد خروج API بدون توکن و خروج با دکمه داشبورد؛ خروج Razor (GET خارج نمی‌کند)؛ بازگشت از نشست منقضی با `ReturnUrl`؛ پایان نشست باز پس از غیرفعال‌سازی؛ صفحات مدیریت Master؛ صفحه راه‌اندازی اولیه. همه زیر CSP اجباری. Seed با حساب‌ها و رمزهای تصادفی هر اجرا انجام می‌شود (`tests/e2e/seed.mjs`).

تست‌های Mock یا InMemory معادل E2E یا SQL Server معرفی نمی‌شوند. E2E فقط در CI اجرا شد؛ راهنمای اجرای محلی در `tests/e2e/README.md` است.
