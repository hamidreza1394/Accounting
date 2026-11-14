# تحویل وظیفه — ISSUE-UNNUMBERED-e2e-api-sensitive-429

- **Issue:** `UNNUMBERED` (شکست E2E گزارش‌شده در جریان کار PR #653؛ شماره Issue مستقل در اختیار نبود)
- **Agent/مسئول:** z.ai agent (zgo/glm-5.3-flash)
- **تاریخ:** 2026-11-14
- **وضعیت:** انجام‌شده

## هدف و معیارهای پذیرش

- [x] علت شکست E2E (HTTP 429 در `GET employee detail after deactivate`) با شواهد دقیق CI شناسایی شود.
- [x] کوچک‌ترین اصلاح درست، بدون تضعیف امنیت Production، اجرا شود.
- [x] سیاست‌های Rate Limiting در E2E همچنان فعال و با آزمون 429 اثبات شوند.
- [x] پیکربندی Production بدون تغییر بماند (فقط `tests/e2e/*` تغییر کرده است).
- [x] مستندات هم‌مسیر (README و handoff و WORK_LOG) به‌روزرسانی شود.

## ریشه‌یابی (شواهد CI)

منبع: لاگ کامل Job `e2e` اجرای 43585704506 (job 132420569913) روی head پرقدمت `d1e56230` — نتیجه: `1 failed / 48 passed`:

```
✘ 44 [chromium] › specs/hr-structure.spec.mjs:57:1
Error: GET employee detail after deactivate:
{"title":"تعداد درخواست‌ها بیش از حد مجاز است. ...","status":429}
→ hr-structure.spec.mjs:195 (Expected: 200, Received: 429)
```

- **Endpoint دقیق:** `GET /api/v1/companies/{companyId}/org/employees/{employeeId}`.
- **سیاست اعمال‌شده:** نه `api` گروهی، بلکه `api-sensitive` در سطح endpoint — `Server/Api/HrOrganizationalStructureEndpoints.cs` روی همه endpointهای HR صریحاً `.RequireRateLimiting(ApiSensitivePolicy)` دارد و طبق رفتار ASP.NET Core و `docs/SECURITY.md` (بند Issue #522/#444) سیاست endpoint جایگزین سیاست گروه می‌شود؛ `GlobalLimiter` هم تعریف نشده است.
- **کلید پارتیشن:** `api-sensitive|user:{NameIdentifier}` — `UseRateLimiter()` بعد از `UseAuthentication()` قرار دارد (`Server/Program.cs`) و `NameIdentifier` شناسه یکتای کاربر است؛ یعنی پارتیشن «به ازای هر کاربر» است و همه endpointهای حساس (آنتی‌فورجری، خروج، sessionها، نقش‌ها، عضویت، audit و HR) یک پنجره مشترک دارند.
- **حد فعلی:** `appsettings.json`: `ApiSensitive = 30 درخواست / 60 ثانیه` (پنجره ثابت، تصمیم مستند امنیتی؛ توسط `SensitiveReadRateLimitBehaviorTests` پوشش داده می‌شود).
- **تعداد درخواست‌ها پیش از رد:** خود تست #44 پیش از درخواست failing ۹ فراخوانی حساس با کاربر admin انجام داده بود (antiforgery، POST employee/unit/position، POST assignment ×2، GET detail، GET history، POST deactivate با 204). در همان پنجره ثابت ۶۰ ثانیه‌ای، specهای قبلی (به‌ویژه `access-ui.spec` تست‌های #1 تا #8 با مشاهده audit/directory و ساخت نقش از 10:39:17 تا 10:39:31) حداقل ۲۰+ درخواست حساس دیگر از همان کاربر admin مصرف کرده بودند؛ پنجره در درخواست دهم (GET بعد از deactivate در 10:40:14.8) تمام شد و 429 برگشت. پنجره حدود 10:40:17 تازه شد و به همین دلیل تست‌های بعدی (نقش‌ها و پروفایل) پاس شدند — این ناسازگاری زمانی علت «متناوب بودن» شکست است (کامنت خود تست: سه شکست متناوب قبلی CI).
- **سایر تست‌ها:** خیر؛ فقط همین ۱ تست از ۴۹ شکست خورد.
- **نتیجه‌گیری:** نه پیکربندی Production غلط است و نه نقص اپلیکیشن؛ مشکل، «بُعدبندی محیط E2E» است: یک سرور منتشرشده با محدودیت‌های Production + یک کاربر admin مشترک + اجرای ترتیبی ۴۹ تست = بیش از ۳۰ درخواست حساس در یک پنجره. الگوی حل همین مشکل برای سیاست `login` از قبل در `playwright.config.mjs` وجود دارد (`RateLimiting__Login__PermitLimit: '1000'`).

## خلاصه تغییرات

- در `tests/e2e/playwright.config.mjs` یک ثابت `apiSensitivePermitLimit = 100` تعریف شد که (۱) به‌صورت متغیر محیطی فقط برای نمونه سرور E2E اعمال می‌شود (`RateLimiting__ApiSensitive__PermitLimit`) و (۲) با `process.env.FARANOU_E2E_API_SENSITIVE_LIMIT` به spec محدودسازی نرخ رسانده می‌شود تا flood با حد پیکربندی‌شده هم‌گام بماند.
- تست flood در `tests/e2e/specs/membership-workflow.spec.mjs` («the membership endpoints keep their per-user sensitive rate limit») پارامتری شد: تعداد درخواست‌ها `permitLimit + 10` و ادعای «۴۲۹ نباید زودتر از حد پیکربندی رخ دهد» `firstLimited >= permitLimit - 10`. رفتار آزمون‌شده تغییر نکرد: 429 همچنان باید ظاهر شود، پنجره ثابت همچنان بررسی می‌شود و endpoint غیرحساس باید 200 بماند.
- توضیح کوتاه در `tests/e2e/README.md` اضافه شد.

## فایل‌های مهم تغییرکرده

- `tests/e2e/playwright.config.mjs` — حد `api-sensitive` فقط برای نمونه E2E + رساندن مقدار به spec.
- `tests/e2e/specs/membership-workflow.spec.mjs` — هم‌گام‌سازی تست flood با حد پیکربندی‌شده E2E.
- `tests/e2e/README.md` — مستندسازی دو override محیطی نرخ.
- `docs/WORK_LOG.md` — ثبت گزارش وظیفه.
- `docs/handoffs/ISSUE-UNNUMBERED-e2e-api-sensitive-429.md` — همین سند.

هیچ فایلی در `Server/`، `Domain/`، `Infrastructure/`، Migrationها یا `appsettings*.json` تغییر نکرده است.

## تصمیم‌های فنی

- **چرا بالابردن حد E2E به‌جای تغییر تست/کد Production؟** درخواست‌های تست‌ها همگی «لازم»اند (ادعاهای رفتاری واقعی) و تجمیع آن‌ها روی یک کاربر مشترک ذاتاً از حد Production فراتر می‌رود. تغییر metadata endpointها یا حد Production امنیت را تضعیف می‌کند و خارج از scope است. الگوی پذیرفته‌شده همین فایل برای `login` تکرار شد.
- **چرا مقدار 100؟** بیشینه مشاهده‌شده در یک پنجره حدود ۳۰ تا ۳۵ بود؛ 100 حاشیه ~۳ برابری می‌دهد و flood تست (110 درخواست) همچنان در ۱ تا ۳ ثانیه اجرا می‌شود.
- **چرا تست flood تغییر کرد؟** با حد 100، سیل ۴۰تایی قبلی هرگز به 429 نمی‌رسید و ادعای «the sensitive limit must eventually reject» شکست می‌خورد؛ پارامتری‌سازی، آزمون را مستقل از مقدار پیکربندی و در برابر تغییرهای بعدی مقاوم می‌کند.
- تصمیم معماری/ADR جدیدی گرفته نشد؛ D-087 و تصمیم‌های S3-0 دست نخورده باقی ماندند.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | exit 0 |
| `dotnet test --filter-class Template.Tests.Integration.SensitiveReadRateLimitBehaviorTests` | موفق | 2/2 پاس |
| `dotnet test --filter-class Template.Tests.Architecture.ApiSensitiveReadRateLimitTests` | موفق | 1/1 پاس |
| `dotnet test --filter-class Template.Tests.Integration.HttpSecurityTests` | موفق | 30/30 پاس (شامل 429 ورود/API) |
| `dotnet test tests/Template.Tests -c Release` (کامل) | موفق به‌جز ۲ شکست محیطی | 1242: 1141 پاس، 99 Skip (SqlServer)، ۲ شکست `SafeExceptionRealApplicationTests` که میزبان Real Application با `DatabaseSchemaMigrator` را بالا می‌آورد و به SQL Server رابطه‌ای نیاز دارد؛ پیش‌زمینه‌ای و بی‌ربط به این تغییر (در CI همان commit، job `build-and-test` موفق است) |
| `dotnet test tests/Vendor.Host.Tests -c Release` | اجرا نشد | پروژه VSTest-runner است و `dotnet test` این sandbox فقط Microsoft.Testing.Platform را می‌پذیرد؛ Vendor.sln جدا است و پوشش آن با CI (`vendor-ci.yml`) است |
| `node --check` روی دو فایل تغییرکرده | موفق | — |
| E2E کامل (job `e2e` CI) | موفق | اجرای [43591355980](https://github.com/hamidreza1394/Accounting/actions/runs/43591355980) روی commit `562d5d64` و PR #654: **49 passed / 0 failed**؛ تست #44 (`hr-structure.spec.mjs:57` — همان تست شکست‌خورده با GET بعد از deactivate) ✓ و تست #43 (آزمون flood محدودسازی حساس پس از تغییر) ✓ |
| `git diff --check` | موفق | بدون خطا |

محدودیت محیط: Docker و SQL Server در sandbox در دسترس نبود؛ `HrOrganizationalStructureSqlServerTests` و سایر تست‌های SqlServer به‌جای اجرا «Skip» شدند و اثبات واقعی E2E به CI پرکردگی شد.

## امنیت، داده و سازگاری

- هیچ سیاست، حد، metadata یا مسیر احراز/مجوزی در Production تغییر نکرد؛ تغییرها فقط متغیر محیطی نمونه سرور E2E است (همان الگوی `Login=1000` قبلی).
- محدودسازی نرخ در محیط E2E همچنان کاملاً فعال است و با آزمون flood در هر اجرا اثبات می‌شود (429، پنجره ثابت، جداسازی پارتیشن کاربر، بقای endpoint غیرحساس).
- بدون تغییر داده، Migration، Schema یا رفتار کسب‌وکار؛ بدون delay/retry مصنوعی در تست‌ها.

## ریسک‌ها و کارهای باقی‌مانده

- اگر در آینده تعداد درخواست‌های حساس suite از ۱۰۰ در پنجره فراتر رود، مشکل 429 بازخواهد گشت؛ مقدار const و تست flood هم‌بسته‌اند و هر دو باید با هم تغییر کنند (تست در صورت ناهم‌خوانی آشکارا شکست می‌خورد).
- سیاست `api` (۳۰۰/دقیقه) هنوز حاشیه کافی دارد؛ در صورت رشد SPA باید همان الگو بازبینی شود.

## شناسه‌های Git

- Branch: `fix/e2e-api-sensitive-rate-limit` (از `main`)
- Commit: `562d5d64c03de9e8d42bfc54d46dbeaca3190e01`
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/654 (هدف: `main`؛ merge نمی‌شود و در انتظار بازبینی است)
