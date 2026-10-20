# تحویل وظیفه — ISSUE-UNNUMBERED-post-pr38-security-review

- **Issue:** `UNNUMBERED` (بازبینی امنیتی و فنی پس از ادغام PR #38)
- **Agent/مسئول:** Claude (claude-opus-5-5)
- **تاریخ:** 2026-10-20
- **وضعیت:** انجام‌شده؛ موارد باقی‌مانده در پایان

## مبنا
- `main` @ `207d81f` (Merge PR #38). CI همان Commit روی `main` در زمان شروع در صف بود؛ شاخه جدید `security/post-pr38-hardening` از همین Commit ساخته شد. تاریخچه `main` تغییر نکرده است.
- **Target Framework:** `net10.0` در `Directory.Build.props` و همه ۱۱ فایل `.csproj`؛ SDK `10.0.100` (rollForward latestFeature) در `global.json`؛ CI `10.0.x`. ناسازگاری فقط در مستندات بود (ROADMAP: «.NET 9») که منسوخ علامت خورد. Framework تغییر نکرد.

## یافته‌ها
| # | شدت | محل | شرح و بازتولید | اثر | وضعیت |
|---|---|---|---|---|---|
| F1 | بالا | `Server/Program.cs` (AddCookie) | Cookie بدون اعتبارسنجی مجدد: پس از غیرفعال‌سازی حساب یا تغییر رمز، Cookie موجود (و «مرا به خاطر بسپار») تا انقضا معتبر می‌ماند. بازتولید: ورود → `IsActive=false` → `GET /` همچنان 200. | ادامه دسترسی حساب مسدودشده به صفحات Razor | اصلاح شد (D-014، `SessionValidator`)؛ تست Integration |
| F2 | متوسط | `Server/Program.cs`, `Server/Api/ApiResponseFilter.cs` | Exception پیش از اجرای Endpoint Filter (ساخت سرویس/Binding) به `UseExceptionHandler("/Errors/Error")` می‌رسید و API صفحه HTML 500 برمی‌گرداند (در Development صفحه Stack Trace). | پاسخ ناسازگار و در Development افشای جزئیات | اصلاح شد: Exception Handler اختصاصی `/api` با problem+json؛ تست Integration با Exception حاوی Connection String ساختگی |
| F3 | متوسط | کل برنامه | هیچ CSP یا هدر امنیتی (nosniff، Referrer-Policy، …) ارسال نمی‌شد. | سطح حمله XSS/Clickjacking بالاتر | اصلاح شد (D-015): `/app` و `/api` اجباری؛ Razor پایه اجباری + سخت‌گیرانه Report-Only |
| F4 | متوسط | API و `/Account/Login` | Rate Limiting وجود نداشت؛ قفل فقط به ازای نام کاربری بود و Password Spraying روی نام‌های مختلف محدود نمی‌شد. | Brute-force و مصرف منابع | اصلاح شد: سیاست‌های `api`، `api-sensitive`، `login` قابل تنظیم؛ تست 429 |
| F5 | متوسط | `.gitignore`، فرایند انتشار | خروجی React در Git نیست و هیچ مرحله انتشاری آن را نمی‌ساخت؛ `dotnet publish` بی‌صدا خروجی بدون `/app` تولید می‌کرد (404 در Production). | انتشار ناقص | اصلاح شد (D-016): اسکریپت انتشار، توقف Publish بدون کلاینت، Job `publish` + Smoke Test |
| F6 | پایین/متوسط | `ClientApp/scripts/check-offline-build.mjs` | URL نسبی CSS نسبت به فایل CSS resolve نمی‌شد، `srcset`/`@import` نسبی/`new URL` بررسی نمی‌شد، Query string فقط برای مسیر مطلق حذف می‌شد، تست نداشت. | خطای «قبولی» کاذب | بازنویسی و ۶ تست؛ روی خروجی Build و Publish اجرا می‌شود |
| F7 | پایین | Fallback `/app` | `index.html` بدون هدر Cache بود و ممکن بود پس از انتشار نسخه قدیمی از Cache بارگذاری شود. | کلاینت کهنه پس از انتشار | اصلاح شد: `Cache-Control: no-cache` |
| F8 | اطلاعاتی | `docs/ROADMAP.md` | ناسازگاری .NET 9 در مستندات. | — | اصلاح شد |
| R1 | متوسط (باقی) | صفحات Razor | اسکریپت/سبک/Handler inline (فهرست در `docs/SECURITY.md`) مانع اجبار CSP سخت‌گیرانه. | — | Report-Only؛ انتقال inlineها کار بعدی |
| R2 | متوسط (باقی) | `PasswordHashService` | هش‌های SHA-256 قدیمی برای حساب‌هایی که وارد نشوند باقی می‌مانند. | — | برنامه ۹۰ روزه در `docs/SECURITY.md`؛ نیازمند تصمیم مسئول |
| R3 | پایین (باقی) | Rate Limiting | پشت Reverse Proxy بدون ForwardedHeaders همه کاربران یک پارتیشن آدرس دارند. | محدودسازی بیش‌ازحد پشت Proxy | مستند شد |

## فایل‌های تغییرکرده
| فایل | علت |
|---|---|
| `Server/Infrastructure/Security/SessionValidator.cs` (جدید) | F1 |
| `Server/Infrastructure/Security/AuthenticationService.cs`, `Server/Pages/Account/Login.cshtml.cs` | صدور Claim `CredentialStamp` (پس از ارتقای احتمالی هش) |
| `Server/Infrastructure/Security/SecurityHeaders.cs` (جدید) | F3 |
| `Server/Infrastructure/Security/RateLimitingPolicies.cs` (جدید) | F4 |
| `Server/Api/ApiEndpoints.cs` | سیاست‌های Rate Limiting و `WriteUnexpectedErrorAsync` (F2) |
| `Server/Program.cs` | ترتیب Middleware، `OnValidatePrincipal`، Exception Handler API، Rate Limiter، `no-cache`، `public partial class Program` برای تست |
| `Server/appsettings.json` | مقادیر پیش‌فرض قابل تنظیم `RateLimiting` و `SecurityHeaders` |
| `Server/Server.csproj` | F5: توقف Publish بدون کلاینت |
| `scripts/publish.sh`, `scripts/publish.ps1`, `scripts/smoke-published-app.sh` (جدید) | F5 |
| `ClientApp/scripts/offline-check.mjs`, `offline-check.test.mjs` (جدید), `check-offline-build.mjs` | F6 |
| `tests/Template.Tests/Integration/*` (جدید) | ۲۷ تست HTTP |
| `tests/Template.Tests/SessionValidatorTests.cs` (جدید) | ۸ تست واحد اعتبارسنجی نشست و هدرها |
| `Directory.Packages.props`, `tests/Template.Tests/Template.Tests.csproj` | `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 (فقط تست) |
| `.github/workflows/dotnet.yml` | SQL Server Service، الزام SQL Server، گزارش تعداد تست، اسکن NuGet، Job `publish` |
| `docs/*`, `README.md` | D-014..D-016، امنیت، تست، توسعه، معماری، وضعیت، نقشه راه |

## دستورات و نتایج واقعی
| دستور/آزمون | محل اجرا | نتیجه |
|---|---|---|
| `dotnet restore/build -c Release` | CI (`bba0f96` و بعد) | موفق |
| `dotnet test` (با `FARANOU_TEST_SQLSERVER`، `FARANOU_REQUIRE_SQLSERVER=1`) | CI، SQL Server 2022 Container | **164 کل، 164 موفق، 0 ناموفق، 0 Skip** |
| `dotnet list package --vulnerable --include-transitive` | CI | هیچ پکیج آسیب‌پذیری گزارش نشد |
| jsdom، `generate_migrations.py --check`، `verify_migration_model.py`، `has-pending-model-changes` | CI | موفق |
| Job `client` (npm ci، typecheck، test، build، check:offline، audit) | CI | موفق |
| `scripts/publish.sh` + `scripts/smoke-published-app.sh` | CI (Job `publish`) | موفق |
| `npm run typecheck`، `npx vitest run` (۲۴ تست)، `npm run build`، `npm run check:offline` | محلی (Agent) | موفق |
| `npm audit --omit=dev` (Runtime) / `npm audit` (شامل ابزار Build) | محلی | هر دو: 0 آسیب‌پذیری |
| کلاینت زیر CSP اجباری در Chromium (Playwright، API شبیه‌سازی‌شده) | محلی | بدون خطای CSP/Console؛ فونت محلی بارگذاری شد |

## اجرانشده (محدودیت محیط)
- Build/Test .NET در محیط Agent: دسترسی NuGet مسدود بود؛ همه نتایج .NET از CI است.
- `scripts/publish.ps1` اجرا نشد (محیط ویندوز در دسترس نبود)؛ معادل `publish.sh` نوشته شده است.
- آزمون مرورگر End-to-End روی سرور واقعی با دیتابیس (ورود ← `/app`)؛ Smoke Test بدون دیتابیس و تست‌های Integration بدون مرورگر هستند.
- لاگ کامل GitHub Actions از این محیط قابل دانلود نبود؛ نتایج از وضعیت Jobها و Annotationها خوانده شد.

## اثر بر کاربران
- پس از استقرار، همه نشست‌های فعلی یک‌بار باطل می‌شوند (Claim مهر ندارند).
- تغییر رمز یا غیرفعال‌سازی حساب، نشست‌های همان حساب را در درخواست بعدی پایان می‌دهد.
- Schema دیتابیس تغییر نکرده و Migration جدیدی وجود ندارد.

## اقدام بعدی پیشنهادی
1. انتقال inlineهای Razor و فعال‌کردن `SecurityHeaders:EnforceRazorContentSecurityPolicy`.
2. تصمیم و اجرای برنامه حذف هش SHA-256 و جایگزینی `MasterPassword` قدیمی.
3. ForwardedHeaders برای Proxy مورد اعتماد در محیط استقرار.
4. آزمون مرورگر End-to-End (Playwright) روی سرور منتشرشده با SQL Server در CI.
5. OpenAPI و نخستین ماژول حسابداری همراه Global Query Filter (طبق D-013).

## شناسه‌های Git
- Base: `207d81f` · Branch: `security/post-pr38-hardening` · PR: در توضیح PR (Merge نشده)
