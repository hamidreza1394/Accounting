# تحویل وظیفه — ISSUE-UNNUMBERED-production-readiness-followup

- **Issue:** `UNNUMBERED` (بازبینی تکمیلی Production پس از PR #38 و #39)
- **Agent/مسئول:** Claude (claude-opus-5-5). کار در دو جلسه Claude روی همین شاخه انجام شد: جلسه اول ۱۱ کامیت (`5ed971d`..`218e9bf`) زد و CI آن در تست E2E قرمز ماند. جلسه دوم آن کار را بازبینی کرد، خطای E2E را رفع کرد و مستندات را تکمیل کرد.
- **تاریخ:** 2026-10-20 · **وضعیت:** انجام‌شده؛ پرسش‌های باز Production در `docs/DEPLOYMENT.md`

## مبنا
- PR #38 (`207d81f`) و PR #39 (`2511907`) ادغام‌شده‌اند. مبنا: `main` @ `2511907` با CI سبز. شاخه: `security/production-readiness-followup`. `main` تغییری نکرده است.

## تغییرات و علت
| فایل | علت |
|---|---|
| `scripts/lib/publish-output.sh`، `scripts/lib/PublishOutput.ps1`، `scripts/publish.sh`، `scripts/publish.ps1` | اولویت ۱: مسیر خروجی پیش از حذف canonical می‌شود. ریشه، مخزن، خانه، پوشه جاری، والدهای آن‌ها، منابع داخل مخزن (به‌جز `artifacts/<name>`)، پیوندهای نمادین، فایل‌ها و پوشه‌های غیرخالی بدون نشانگر انتشار رد می‌شوند. قواعد Bash و PowerShell یکسان است |
| `scripts/tests/publish-output.test.sh`، `scripts/tests/publish-output.tests.ps1` | تست مسیرهای مجاز و غیرمجاز با فایل نگهبان (هیچ حذفی در حالت رد) |
| `Server/Infrastructure/Hosting/ReverseProxy.cs`، `Server/Program.cs`، `Server/appsettings.json` | اولویت ۲: ForwardedHeaders فقط برای Proxyهای مورد اعتماد (پیش‌فرض هیچ)، توقف با ورودی نامعتبر، هشدار شروع برنامه برای `AllowedHosts=*` و نبود Proxy |
| `Server/Infrastructure/DatabaseSetup/MasterCredentialCommand.cs`، `AuthenticationService.cs`، `ApplicationSettings.cs` | اولویت ۳: دستور `--create-master-credential` و تنظیم `AllowLegacyMasterPasswordHash` |
| صفحات Razor (`_DisplayToastMessages`، `DatabaseSetup`، `MenuItems/Create|Update`، `PageManager/Delete`، `Login`، `LoginSuccess`، `_DisplayPictures`، پاورقی/اسکریپت/استایل‌های Ltr و Rtl) و `wwwroot/js|css` جدید | اولویت ۴: انتقال inlineها به فایل محلی |
| `Server/Infrastructure/Security/SecurityHeaders.cs`، `CspReportEndpoint.cs`، `RateLimitingPolicies.cs` | CSP اجباری Razor با استثنای قابل تنظیم و Endpoint گزارش امن (پیش‌فرض خاموش) |
| `Server/wwwroot/css/rtl/fontiran.css` | نام فونت با حروف کوچک روی Linux پاسخ 404 می‌داد (E2E آن را یافت) |
| `tests/Template.Tests/ContentSecurityPolicyTests.cs`، `MasterCredentialTests.cs`، `Integration/DeploymentConfigurationTests.cs` و به‌روزرسانی‌ها | تست رگرسیون |
| `tests/e2e/**` | E2E مرورگر واقعی ← سرور منتشرشده ← SQL Server |
| `.github/workflows/dotnet.yml` | Jobهای `publish-scripts`، `publish-windows` و `e2e`، و گزارش جزئیات خطا |
| `docs/DEPLOYMENT.md` (جدید)، `DECISIONS` (D-017..D-019)، `SECURITY`، `TESTING`، `ROADMAP`، `PROJECT_STATUS`، `WORK_LOG`، `README` | مستندات |

## نتایج واقعی تست (CI روی `1b8035a` و بعد)
| دسته | نتیجه |
|---|---|
| Bash | ۲۲ از ۲۲ حالت محافظ (CI Linux و اجرای محلی Agent) |
| PowerShell | تست‌های محافظ روی PowerShell 7 در Linux و روی **Windows واقعی** موفق؛ `publish.ps1` کامل روی Windows موفق و خروجی (`Server.dll`، `wwwroot/app/index.html`، فونت، نشانگر) بررسی شد |
| .NET | ۲۰۴ کل، ۲۰۴ موفق، ۰ ناموفق، ۰ Skip، روی SQL Server 2022. اسکن NuGet بدون مورد |
| کلاینت | Job `client` موفق (npm ci، typecheck، تست، build، offline، audit) |
| انتشار Linux و Smoke | موفق |
| E2E | ۱۲ از ۱۲ موفق (Chromium ← سرور Production ← SQL Server 2022) |

## اجرانشده یا محدود
- Build و تست .NET و E2E در محیط Agent اجرا نشد (NuGet و SQL Server در دسترس نبود). همه نتایج از CI است.
- مقایسه Screenshot پیکسلی صفحه ورود انجام نشد. تنها تغییر ظاهری ممکن، انتقال سه رنگ inline به کلاس با همان مقادیر است و E2E رندر صفحه، فونت و نبود نقض CSP را بررسی می‌کند.
- اجرای چندنمونه‌ای و Proxy واقعی آزموده نشد. رفتار Proxy با تست Integration (آدرس فرستنده شبیه‌سازی‌شده) پوشش داده شده است.

## ریسک‌های باقی‌مانده
1. مقادیر واقعی `AllowedHosts` و `ReverseProxy` تعیین نشده‌اند (پرسش‌های باز در `docs/DEPLOYMENT.md`).
2. Rate Limiting درون‌پردازه‌ای است و کلیدهای Data Protection برای چند نمونه مشترک نیستند.
3. `/Admin/PageManager` به‌دلیل TinyMCE هنوز Report-Only است.
4. هش‌های SHA-256 کاربرانی که وارد نشوند باقی می‌مانند و برنامه حذف نیازمند تصمیم است. Master با ابزار جدید قابل مهاجرت است.

## شناسه‌های Git
- Base: `2511907` · Branch: `security/production-readiness-followup` · PR: در توضیح PR (Merge نشده)
