# راهبرد آزمون

## رویکرد
- **TDD**: برای منطق جدید و پرریسک، ابتدا تست شکست‌خورده، سپس پیاده‌سازی و بازآرایی. لازم نیست هر تغییر نمایشی با TDD کامل انجام شود.
- **BDD**: معیارهای پذیرش با زبان قابل فهم کسب‌وکار نوشته شوند؛ در صورت نیاز به سناریوهای Given/When/Then از ابزار مناسب استفاده شود.
- Unit Test برای قوانین Domain/Application بدون دیتابیس یا شبکه.
- Integration Test برای EF Core، SQL Server، احراز هویت، Middleware و مسیرهای HTTP.
- تست‌های end-to-end فقط برای جریان‌های مهم و با محیط قابل تکرار.

## ساختار
`tests/Template.Tests` اکنون مجموعه‌ای از فایل‌های تست دارد (ورود، جداسازی شرکت، API، CSP، Master، Migration/مدل، DI دیتابیس، تست‌های Integration HTTP در `Integration/`)؛ ادعای قدیمی «فقط یک تست پایه» منسوخ است (2026-10-21). `BaselineTests.cs` فقط نمونه اتصال باقی مانده است. ۲۰۴ تست .NET در اجرای CI `39585708662` موفق گزارش شد؛ **وجود فایل تست به‌معنی اجرای موفق نیست** و این بازبینی مستنداتی تست اجرا نکرده است.

## فرمان‌ها
```bash
dotnet restore Template.sln
dotnet build Template.sln --no-restore --configuration Release
dotnet test Template.sln --no-build --configuration Release
```

## قاعده گزارش
نتیجه واقعی هر دستور، کد خروج، تست‌های اجراشده/نشده و علت شکست ثبت شود. اجرای موفق Build جایگزین تست عملکردی و بازبینی امنیتی نیست.


## تست‌های راه‌اندازی پایگاه داده (Issue #22؛ بازطراحی 2026-10-23)
وضعیت: `Implemented` در شاخه `feature/database-setup-redesign` (ادغام‌نشده). جزئیات رفتار: [`DATABASE_SETUP.md`](DATABASE_SETUP.md).

| فایل (`tests/Template.Tests/`) | محور | نیاز به SQL Server |
|---|---|---|
| `DatabaseSetupValidationTests` | پیش‌فرض‌ها (دیتابیس `Farano`، سرور خالی)، اعتبارسنجی همه فیلدها با هم، فهرست مجاز نام دیتابیس و نام سیستمی (NFKC)، طول‌ها، نبود نشت رمز در `ToString` | خیر |
| `DatabaseSetupServiceTests` | جدول طبقه‌بندی خطا، زنجیره گواهی/Authentication، پیام و راه‌حل هر کد، قواعد `ClassifyDatabaseState`، نبود `[Required]` ضمنی، میزبان اشتباه بدون ذخیره و بدون نشت راز، یک راه‌اندازی در لحظه، `SettingsCommand` | خیر |
| `DatabaseSetupPageTests` | رفتار `PageModel` (کد راه‌اندازی، خطای همه فیلدها، Windows بدون اعتبارنامه، فایل ناخوانا، Blocked بدون فرم، دروازه یک‌بارمصرف)؛ آزمون جهش Lead: با خاموش‌کردن اصلاح `ModelState`، ۴ تست شکست می‌خورد | خیر |
| `DatabaseSettingsStoreTests` | ماتریس رفتار Store، فایل قدیمی بایت‌به‌بایت دست‌نخورده، پشتیبان‌گیری و سقف ۱۰، شکست نوشتن اتمی، قفل، مجوز Unix، نبود نشت | خیر |
| `DatabaseSettingsLocationTests` | ترتیب مسیر، مسیر نسبی/داخل پوشه برنامه، مسیر واقعی (لینک)، `LocalApplicationData` خالی، ثابت‌های برند | خیر |
| `DatabaseSetupSqlServerTests` | ساخت دیتابیس+Migration+ذخیره رمزشده، دیتابیس منطبق/بیگانه/جدیدتر، نیمه‌Migration، رمز اشتباه، میزبان اشتباه | **بله** |
| `DatabaseSetupPageSqlServerTests` | جریان صفحه روی SQL Server واقعی | **بله** |
| `DatabaseSetupTestSupport.cs` | ابزار مشترک (پوشه موقت، ساخت تنظیمات قدیمی) | — |

- **قاعده Skip:** تست‌های SQL Server بدون `FARANOU_TEST_SQLSERVER` Skip می‌شوند؛ با `FARANOU_REQUIRE_SQLSERVER=1` نبود آن شکست است. دیتابیس‌های آزمون `FaranoTests_Setup_*` ساخته و حذف می‌شوند. دو تست مبتنی بر `chmod` (AccessDenied و LegacyUnreadable) روی Windows یا با root Skip می‌شوند؛ تست مجوز Unix روی Windows Skip می‌شود.
- **نتیجه اجرای Lead (2026-10-23، SDK 10.0.402):** `dotnet build Template.sln -c Release` ۰ خطا؛ `dotnet test Template.sln --no-build -c Release` با SQL Server 2022 واقعی: ۴۸۹ تست، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip (دو تست `chmod` با root). بدون SQL Server طبق گزارش Agent پیاده‌ساز: ۴۶۹ موفق، ۲۰ Skip (Lead دوباره اجرا نکرد). E2E (`publish.sh`، `--migrate`، seed، Playwright): ۱۸ موفق، ۰ ناموفق. ClientApp: typecheck، `npm test` ۴۸ از ۴۸، build، `check:offline`. `scripts/check_static_assets.py` PASS. `git diff --check` تمیز. بررسی دستی Chromium (دسکتاپ ۱۲۸۰ و موبایل ۳۷۵): بدون خطای Console/CSP، بدون درخواست خارجی، بدون اسکرول افقی.
- **E2E صفحه راه‌اندازی:** `tests/e2e/specs/faranou.spec.mjs` به نشانه‌گذاری جدید به‌روزرسانی شد و موفق بود (AUD-2026-10-23-01 حل‌شد).
- **اجرا نشد:** Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl` — فقط کامپایل شده)؛ Login SQL با رمز منقضی/غیرفعال (18470/18487/18488 فقط با جدول خالص)؛ CI؛ دو POST هم‌زمان روی HTTP (دروازه سرویس تست واحد دارد)؛ POST بوت‌استرپ Master روی HTTP؛ شکست تزریق‌شده وسط Migration (نیمه‌Migration با اعمال دستی اولین Migration شبیه‌سازی شده).

## تست‌های جریان ورود (Issue #30)
- `LoginAuthenticationTests` جریان PageModel را با `DatabaseContext` روی provider حافظه‌ای (`Microsoft.EntityFrameworkCore.InMemory`، فقط آزمون) و یک `IAuthenticationService` ثبت‌کننده اجرا می‌کند: فیلد خالی، نام کاربری ناموجود، رمز اشتباه، حساب غیرفعال، قفل موقت، ورود موفق، Remember Me، ثبت `LoginLog`/`LastLoginDateTime`، ارتقای هش قدیمی، حساب زیرساختی، `ReturnUrl` محلی و خارجی، و خطای زیرساخت.
- صدور کوکی با ثبت فراخوانی `SignInAsync` (scheme و `IsPersistent`) بررسی می‌شود؛ این آزمون جای آزمون HTTP واقعی روی مرورگر را نمی‌گیرد.
- `PasswordHashServiceTests` قالب جدید، salt یکتا، رد رمز اشتباه، پذیرش و درخواست ارتقای هش قدیمی و رد مقدار نامعتبر را پوشش می‌دهد.
- `LoginConfigurationTests` به‌صورت ایستا بررسی می‌کند که مقصد موقت `[Authorize]` دارد و فرم/ماژول حسابداری ندارد، صفحه ورود Anti-forgery و toggle رمز را حفظ کرده، و تنظیمات Cookie/ترتیب Middleware تغییر نکرده است.
- اجرای Migration روی SQL Server واقعی همچنان آزمون‌نشده است.


## تست‌های جداسازی شرکت و API (2026-10-20)
- (نام کلاس‌ها در `tests/Template.Tests/CompanyAccessTests.cs`) `CompanyAccessServiceTests`: دسترسی فقط با عضویت فعال در شرکت فعال با نقش فعال؛ عدم دسترسی به شرکت Tenant دیگر، شرکت ناموجود و `Guid.Empty`؛ حساب غیرفعال؛ اثر فوری لغو عضویت؛ نادیده‌گرفتن Claim روی هویت احراز‌نشده.
- `ApiEndpointTests`: فهرست شرکت‌ها فقط برای کاربر جاری، حساب زیرساختی بدون شرکت، 401 برای نشست قدیمی بدون شناسه، نمای کلی فقط با داده همان شرکت، 404 یکسان برای Tenant دیگر و شرکت ناموجود، بی‌اثر بودن Claim جعلی `TenantId`، مسیرهای API.
- `ApiCookieAuthenticationEventsTests`: پاسخ 401/403 برای `/api` و حفظ Redirect صفحه ورود برای صفحات مرورگر.
- `SessionHardeningTests` و `SuccessfulSignIn_IssuesTheUserIdentifier_AndNoCompanyClaims`: خروج فقط با POST، Claim شناسه کاربر و نبود Claim شرکت.
- این تست‌ها Endpointها را مستقیم با EF Core InMemory اجرا می‌کنند؛ تست HTTP کامل با `WebApplicationFactory` و SQL Server واقعی بعداً اضافه شد (بخش «تست‌های Integration HTTP» پایین).

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
| **E2E مرورگر** | `tests/e2e` (Playwright/Chromium ← سرور منتشرشده در Production ← SQL Server 2022 واقعی)، ۱۲ سناریو (بعدها گسترش‌یافته؛ در بازطراحی انتخاب‌گر 2026-10-23 به ۲۴ تست رسید: همنام‌ها، سال بایگانی، تغییر سریع سال، ورود بدون دوبار ارسال، منابع محلی) | CI Job `e2e` |

**سناریوهای E2E:** صفحه ورود با فونت محلی و بدون نقض CSP؛ رد رمز اشتباه و حساب غیرفعال؛ Cookie از نوع HttpOnly و نمایش فقط شرکت‌های خود در خانه Razor؛ Remember Me؛ داشبورد React با داده واقعی، تغییر شرکت و نادیده‌گرفتن شناسه شرکت بیگانه؛ 404 یکسان برای شرکت دیگران و شرکت ناموجود؛ رد خروج API بدون توکن و خروج با دکمه داشبورد؛ خروج Razor (GET خارج نمی‌کند)؛ بازگشت از نشست منقضی با `ReturnUrl`؛ پایان نشست باز پس از غیرفعال‌سازی؛ صفحات مدیریت Master؛ صفحه راه‌اندازی اولیه. همه زیر CSP اجباری. Seed با حساب‌ها و رمزهای تصادفی هر اجرا انجام می‌شود (`tests/e2e/seed.mjs`).

تست‌های Mock یا InMemory معادل E2E یا SQL Server معرفی نمی‌شوند. E2E فقط در CI اجرا شد؛ راهنمای اجرای محلی در `tests/e2e/README.md` است.


## وضعیت و شکاف‌ها (2026-10-21)
- **شاهد CI:** اجرای `39585708662` — ۲۰۴ تست .NET روی SQL Server 2022، ۱۲ E2E، اسکن NuGet بدون مورد. این سند اجرای جدید ادعا نمی‌کند.
- **ناهمخوانی دستور (حل‌شد، 2026-10-23):** علت تفاوت SDK بود، نه Runner: در SDK `10.0.113` (باند 10.0.1xx) فرمان `dotnet test Template.sln` خطای «should be via '--solution'» می‌داد؛ در SDK `10.0.402` که `global.json` (`rollForward: latestFeature`) می‌پذیرد و CI می‌گیرد، هر دو شکل `dotnet test Template.sln --no-build ...` و `dotnet test --solution Template.sln --no-build ...` اجرا شدند (شاهد در بخش «نتیجهٔ واقعی» پایین). فرمان رسمی همان `AGENTS.md` است؛ `scripts/dev/install-dotnet.sh` همان SDK CI را نصب می‌کند.
- **تست‌های لایسنس:** هیچ تستی وجود ندارد چون قابلیتی پیاده‌سازی نشده (`NotImplemented`). تست‌های پذیرش آینده در ADRهای D-022..D-028 و `docs/LICENSING_THREAT_MODEL.md` فهرست شده‌اند؛ هر کدام فقط پس از اجرای واقعی `Verified` می‌شود.
- تست صریح «نقش Admin قدیمی به داده شرکت غیرعضو دسترسی ندارد» برای افزودن پیشنهاد می‌شود (D-029).
- تست ایستا «بسته Publish مشتری فاقد کلید خصوصی/کد فروشنده است» برای آینده (D-021).

## تست‌های انتخاب شرکت و دوره مالی (D-031) — 2026-10-21
- **.NET** (`tests/Template.Tests/WorkContextTests.cs`، `Integration/WorkContextHttpTests.cs`): قواعد `EntryMode` (بدون شرکت، بدون دوره باز، ورود مستقیم، انتخاب لازم)، سیاست دوره (Open=ReadWrite، Closed=ReadOnly، Archived غیرقابل‌ورود؛ D-032)، انتخاب معتبر/شرکت غیرمجاز/دوره شرکت دیگر/دوره ناموجود/دوره بایگانی‌شده، ورود به دوره بسته به‌صورت فقط‌خواندنی، رویدادهای ممیزی Selected/Changed/Denied، اعتبارسنجی مجدد پس از تغییر وضعیت دوره، Cookie (دست‌کاری، انقضا، کاربر یا ورود دیگر)، لغو عضویت، Anti-forgery، 401 بدون `SessionId`، حساب زیرساختی. نتیجه محلی: ۲۵۳ از ۲۵۳ موفق، و ۴۸ از ۴۸ تست کلاینت (Provider حافظه‌ای؛ نه SQL Server).
- **کلاینت** (`App.test.tsx`، `WorkContextSelector.test.tsx`): ورود مستقیم، چند شرکت، چند دوره، مسدودی‌ها، لیست قدیمی (404/409)، خطای سرور بدون جزئیات، جلوگیری از ارسال دوباره، نشست منقضی، تغییر زمینه، آستانه جست‌وجو. نتیجه محلی: ۴۶ از ۴۶ موفق (شامل `lib/search.test.ts` و آزمون‌های چیدمان طرح جدید).
- **E2E** (`tests/e2e/specs/faranou.spec.mjs`، `seed.mjs` با کاربران `dave` و `erin`): اجرا فقط در CI با SQL Server؛ در این محیط اجرا نشد.
- **بررسی بصری:** با Chromium و API شبیه‌سازی‌شده (دسکتاپ ۱۲۸۰ و موبایل ۳۹۰، RTL، بدون اسکرول افقی، بدون خطای اسکریپت) برای صفحه انتخاب و هر دو صفحه مسدود؛ داشبورد و نوار زمینه با داده واقعی بصری بررسی نشد.


## آزمون بار و هم‌زمانی ابری (Issue #49، 2026-10-22)

- در ممیزی مخزن، سناریوی load/stress/soak قابل اجرا و گزارش معتبر برای 100 کاربر یافت نشد؛ بنابراین ظرفیت فعلی `NotVerified` است. تست‌های Unit/Integration/E2E یا CI سبز، جای آزمون بار را نمی‌گیرند.
- مرجع برنامه و خروجی الزامی: `docs/CLOUD_CONCURRENCY_READINESS.md`. پرامپ اجرایی: `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`.
- هدف اولیه پیشنهادی: ramp-up تدریجی تا 100 VU، آزمون رشد تا 150/200 VU و soak حداقل 30 دقیقه؛ p95 حداکثر 2 ثانیه برای endpointهای معمول منتخب و خطای غیرمنتظره کمتر از 1% به‌عنوان هدف اولیه، نه SLA تضمین‌شده.
- آزمون فقط در staging/محیط ایزوله با داده مصنوعی و اجازه صریح اجرا شود. ثبت commit SHA، مشخصات برنامه/میزبان/SQL، مدل بار، p50/p95/p99، throughput، خطاها، CPU/RAM، GC/ThreadPool، SQL waits، pool saturation و deadlock الزامی است.
- برای ماژول حسابداری که هنوز پیاده‌سازی نشده، آزمون نوشتن مالی واقعی ادعا نشود. وقتی ثبت سند/گزارش اضافه شد، تراکنش اتمیک، idempotency، شماره‌گذاری، رقابت نوشتن و invariantهای مالی باید در آزمون‌های جدا پوشش داده شوند.

## تست‌های گروه شرکتی (D-033/D-034) — 2026-10-22
- **قواعد دامنه (Unit):** هم‌پوشانی بازه عضویت/مالکیت، درصدها، جمع ≤ ۱۰۰، کنترل یگانه، چرخه کنترلی و مجاز بودن مالکیت متقابل اقلیت.
- **سرویس (Provider حافظه‌ای و SQL Server):** ایجاد گروه، افزودن/پایان عضویت، ثبت مالکیت، تعیین محدوده اجرا؛ تست‌های منفی دسترسی: بدون مجوز، مجوز فقط در مالک، شرکت غیرعضو، شناسه دست‌کاری‌شده گروه/شرکت/دوره/پروفایل، کاربر/عضویت/شرکت/نقش غیرفعال.
- **Migration (SQL Server):** اعمال روی دیتابیس آزمایشی دارای داده قبلی، قیدهای CHECK و FK ترکیبی دوره مالی، idempotent بودن درج کاتالوگ.
- نتایج واقعی اجرا در handoff همین کار ثبت می‌شود؛ گزارش تجمیعی عددی تست ندارد چون پیاده‌سازی نشده است.

### نتیجه واقعی (2026-10-22، .NET SDK 10.0.402، کانتینر SQL Server 2022)
- `dotnet test --solution Template.sln --no-build --configuration Release` با `FARANOU_TEST_SQLSERVER` و `FARANOU_REQUIRE_SQLSERVER=1` → `total: 305, failed: 0, succeeded: 305, skipped: 0` (پایه پیش از تغییر: ۲۵۳ از ۲۵۳).
- همان فرمان بدون SQL Server → ۳۰۱ موفق، ۴ Skip (چهار تست `CorporateGroupSqlServerTests` عمداً فقط با SQL Server اجرا می‌شوند؛ در CI با `FARANOU_REQUIRE_SQLSERVER=1` نبود SQL Server شکست است، نه Skip).
- تست هم‌زمانی (دو درخواست موازی با یک کد گروه، ۵ بار در هر اجرا) سه بار پشت‌سرهم سبز؛ همین تست خطای واقعی (Deadlock پیچیده در `InvalidOperationException`) را پیش از اصلاح آشکار کرد.
- کلاینت React تغییری نکرد و تست‌های آن در این کار اجرا نشدند.

## نتیجهٔ واقعی محیط Cloud با اسکریپت‌های `scripts/dev` (2026-10-23)
محیط: Claude Code Cloud، Ubuntu 24.04، SDK `10.0.402` (نصب‌شده با `scripts/dev/install-dotnet.sh`؛ SHA-512 با فراداده رسمی تطبیق داده شد، که کنترل یکپارچگی است نه امضا)، Node 22.22.0، SQL Server 2022 در Docker (`scripts/dev/test-sqlserver.sh up`، `127.0.0.1:14333`). کد مبنا: `main` در commit `19d608e`.

| فرمان | نتیجه |
|---|---|
| `dotnet restore Template.sln` / `dotnet build Template.sln --no-restore -c Release` | کد خروج ۰؛ ۰ خطا، ۹۲۱ هشدار (روی SDK 10.0.113: ۹۲۷؛ تفاوت از Analyzer نسخهٔ SDK) |
| `dotnet test Template.sln --no-build -c Release` با `FARANOU_TEST_SQLSERVER` و `FARANOU_REQUIRE_SQLSERVER=1` | `total: 306, failed: 0, succeeded: 306, skipped: 0` (۲۶ ثانیه) |
| همان فرمان با `--solution` و بدون SQL Server | ۳۰۶ تست، ۳۰۲ موفق، ۴ Skip (`CorporateGroupSqlServerTests`)، ۰ ناموفق |
| E2E: `scripts/publish.sh` ← `Server.dll --migrate` ← `npm run seed` ← `./check-servers.sh` ← `npx playwright test` (Chromium واقعی، سرور منتشرشده، SQL Server واقعی) | `18 passed` (۴۴ ثانیه)؛ `check-servers.sh` هر دو حالت OK |

اجرا نشد: `npm test`/`npm run build` کلاینت React (مرحلهٔ `client` CI)، Windows (`publish-windows`)، ویندوز/PowerShell، Firefox/Safari.

## تست‌های نام کاربری ورود (PR #67)
- `RememberedUsernameTests` (Integration، SQL Server واقعی): ویژگی‌های کوکی، نبود رمز، بازیابی/عدم بازیابی، مقدار مشکوک و کدگذاری خروجی، حذف با ورود ناموفق.
- `tests/e2e/specs/faranou.spec.mjs`: به‌خاطرسپاری، خاموش‌کردن، ورود ناموفق، صفات Password Manager و یادداشت.

## تست‌های مرز مشتری، شرکت پیش‌فرض و برچسب‌های رابطه (D-038..D-040) — 2026-10-23
فهرست زیر نام تست‌های افزوده/اصلاح‌شده در کد است؛ **نتیجه اجرا در این سند ثبت نمی‌شود** (نتیجه واقعی را Lead در PR/handoff می‌آورد). CI اجرا نشده (سقف صورتحساب GitHub). Firefox، Safari، axe و HTTPS پشت Proxy اجرا نشدند.

- **`Integration/WorkContextHttpTests.cs` (افزوده‌ها):** `AGroupMemberWithoutOwnership_IsShownAsGroupMember_NeverAsIndependentOrParent`؛ `AGroupSpanningTwoBoundaries_OrAnUnprovisionedOne_IsNeverClassified`؛ `ACompanyWithAProvisionedBoundaryAndNoRelationship_IsIndependent_AndWithoutBoundaryIsUnknown`؛ `ProvisioningIsRepeatable_AndRefusesToSplitAnExistingRelationshipAcrossBoundaries`؛ `ConcurrentProvisioningOfTheSameCompanyWithDifferentBoundaries_LetsExactlyOneWin`؛ `TheDiagnostics_AreOperatorOnly_ReadOnly_AndExposeNoCompanyNames`؛ `JointControl_IsLabelledPerSide_AndSignificantInfluenceAsInvestorAndAssociate`؛ `ProvisioningManyCompanies_WritesABoundedAudit_AndRejectionsAreAuditedToo`.
- **`CorporateGroupServiceTests.LegacyRelationshipsOfAnotherBoundary_NeverChangeTheOutcomeOfAValidWrite`:** ردیف‌های قدیمی مرز دیگر/Provision‌نشده بر چرخه/درصد/کنترل یگانه یک نوشتن معتبر اثر ندارند.
- **Vitest:** دو تست جدید در `WorkContextSelector.test.tsx`: `shows the server-provided relationship labels in each row, several labels together, and never invents one` و `marks the saved default company without entering automatically`.
- **اصلاح تست‌های شکسته از قبل روی `main` (حذف نشدند):** `CorporateGroupSqlServerTests` (تست Migration: شرکت با SQL درج می‌شود چون مدل ستونی داشت که در Schema قدیمی نبود)؛ شش تست Vitest و یک تست E2E که هنوز قاعده منسوخ «چیزی پیش‌انتخاب نمی‌شود» (D-031) را می‌سنجیدند، به قاعده پذیرفته‌شده (D-040) به‌روز شدند.

- `ConcurrentProvisioningOfTheSameCompanyWithDifferentBoundaries_LetsExactlyOneWin` فقط روی SQL Server اجرا می‌شود (Serializable و Deadlock روی Provider حافظه‌ای معنا ندارند؛ بدون `FARANOU_TEST_SQLSERVER` رد می‌شود). تراکنش Serializable تخصیص مرز فقط روی Provider رابطه‌ای باز می‌شود.


## آزمون‌های قرارداد لایسنس (Issue #80 / D-045)
- آزمون مستقل: `dotnet test tests/Licensing.Contracts.Tests/Licensing.Contracts.Tests.csproj --configuration Release`.
- CI باید همین مجموعه را روی Linux در `build-and-test` و روی Windows در Job `publish-windows` اجرا کند؛ موفقیت فقط روی یک سیستم‌عامل برای تأیید سازگاری کافی نیست.
- بردار ثابت RFC 6979 برای ECDSA P-256/SHA-256 و امضای IEEE P1363 با طول دقیق 64 بایت؛ آزمون تغییر حتی یک فاصله در Payload برای اثبات امضا روی بایت‌های دقیق JSON.
- آزمون‌های ساختاری برای کد زیرسیستم ناشناخته و کد تکراری. کلیدهای ثابت آزمون فقط کلید عمومی و دادهٔ تست شناخته‌شده‌اند؛ کلید خصوصی صدور محصول هرگز در مخزن قرار نمی‌گیرد.


## گارد جداسازی سامانه فروشنده (D-041، Issue #95)
- تست `tests/Template.Tests/Architecture/VendorIsolationTests.cs` گراف `ProjectReference` پروژهٔ `Server` را مستقیم و ترنزیتیو می‌پیماید، هر ارجاع به `Vendor/**` و هر `PackageReference` با نام Vendor-محور را رد می‌کند.
- تست جداگانه بررسی می‌کند که `Licensing.Contracts` هیچ `ProjectReference` و هیچ `Compile`/`Content Link` به فایل خارج از پوشهٔ خودش نداشته باشد.
- تست منفی از یک گراف XML مصنوعی و موقت استفاده می‌کند که وابستگی ترنزیتیو به `Vendor` دارد؛ تست باید آن را تشخیص دهد. این fixture کد اجرایی Vendor را build یا اجرا نمی‌کند.
- در `Licensing.Contracts` نیز هر `ProjectReference`، هر وابستگی ASP.NET Core/EF Core/Vendor و هر `Compile`/`Content Link` بیرون از پوشه رد می‌شود. این گارد بخشی از `Template.sln` است و با فرمان‌های معمول `dotnet test Template.sln --configuration Release` اجرا می‌شود. نتیجهٔ واقعی CI روی PR باید قبل از اعلام `Verified` بررسی شود.
- این تست جایگزین G2/G3 نیست؛ G2 خروجی Publish را بررسی می‌کند و G3 اسکن اسرار در CI است. G4/G5 و allow-list کامل Publish هنوز جداگانه باید تعیین/پیاده‌سازی شوند.

## ممیزی Vendor و checkpoint مستقل (Issue #105 / #107)
- `VendorAuditChainVerifierTests` آزمون‌های واحد برای رد metadata آزاد و شناسهٔ قابل‌شناسایی دارد.
- `VendorAuditChainIntegrationTests` در SQL Server واقعی یک دیتابیس موقت `FaranouVendorTests_<guid>` می‌سازد؛ ۱۶ نویسنده با DbContext مستقل هم‌زمان رخداد ثبت می‌کنند، زنجیره بررسی می‌شود و دستکاری محتوا باید با `event-content-mismatch` رد شود.
- در شاخهٔ `feat/issue-107-audit-checkpoint-contract`، `VendorAuditCheckpointTests` قرارداد canonical/versioned، نرمال‌سازی UTC، رد hash نامعتبر و تشخیص دستکاری را پوشش می‌دهد؛ `VendorAuditCheckpointIntegrationTests` روی SQL Server واقعی پیشروی checkpoint، جلوگیری از نوشتن تکراری و رد زنجیرهٔ دستکاری‌شده را بررسی می‌کند.
- CI باید `FARANO_VENDOR_TEST_SQLSERVER` و `FARANO_REQUIRE_VENDOR_SQLSERVER=1` را فراهم کند تا تست SQL Server بدون زیرساخت به Skip تبدیل نشود.
- **وضعیت این شاخه:** کد و تست‌ها ثبت شده‌اند، اما CI روی HEAD نهایی این PR هنوز اجرا/تأیید نشده است. هیچ store واقعی مستقل انتخاب یا ثبت نشده؛ fake store فقط برای تست قرارداد است و Production readiness اثبات نمی‌کند.


## Windows CNG customer acknowledgement signer (Issue #236)

- `tests/Template.Tests/Integration/WindowsCngCustomerProvisioningAcknowledgementSigningTests.cs` verifies key creation with export disabled, ECDSA P-256/64-byte P1363 signing, verification against the returned public SPKI, exact-payload binding, missing-key fail-closed behavior, explicit KSP selection and no TPM-to-Software-KSP fallback.
- The tests run in the Windows `publish-windows` job using `dotnet test tests/Template.Tests/Template.Tests.csproj --configuration Release --filter FullyQualifiedName~WindowsCngCustomerProvisioningAcknowledgementSigningTests`.
- The Windows CI fixture uses `Microsoft Software Key Storage Provider` because hosted runners do not guarantee a TPM. This proves Windows CNG API behavior only; it does not prove a real TPM or any particular Windows Server TPM firmware/provider combination. A hardware acceptance test remains mandatory before Production enablement.
- Linux runs skip the Windows-only test class; no Linux ACK provider is included in this stage.


## Customer ACK trust rotation and revocation (Issue #236)

- `CustomerProvisioningAcknowledgementTrustStoreTests` covers configured public-key resolution, explicit revocation while retaining the public key for audit, and overlap rotation where the new version remains trusted while the previous version is revoked.
- Revocation is deployment configuration under `Provisioning:RevokedAcknowledgementKeys:<keyId>=true`; a revoked KeyId cannot verify a newly produced ACK, so the customer apply transaction remains fail-closed (503) and persists no acknowledgement.
- The tests are pure configuration/cryptography tests and require no SQL Server or external provider.
