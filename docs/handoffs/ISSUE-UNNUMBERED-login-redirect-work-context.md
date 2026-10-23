# تحویل وظیفه — ISSUE-UNNUMBERED-login-redirect-work-context

- **Issue:** UNNUMBERED
- **Agent/مسئول:** Claude Code (Lead) + بازبین‌های مستقل `code-reviewer` و `security-reviewer`
- **تاریخ:** 2026-10-23
- **وضعیت:** انجام‌شده (Merge با مسئول مخزن)

## هدف و معیارهای پذیرش
- [x] پس از ورود موفق، کاربر شرکتی به انتخاب شرکت و دوره مالی (`/app/`) برود، نه صفحهٔ `/Account/LoginSuccess`.
- [x] بدون صفحهٔ جدید، احراز هویت دوم یا Cookie جدید؛ `ReturnUrl` فقط محلی؛ بدون تغییر Schema.

## علت ریشه‌ای
`/Account/LoginSuccess` عمداً «مقصد موقت» بود (هنوز ماژول حسابداری نبود) و هیچ هدایت بعدی نداشت. `Login.OnPostAsync` بدون `ReturnUrl` محلی همیشه به آن می‌رفت، در حالی که کلاینت React (`/app/`) و API (`/api/v1/context`، D-031/D-032) از قبل کامل بودند و مستندات جریان «ورود → /app/» را می‌گفتند. اتصال بین این دو هرگز برقرار نشده بود.

## خلاصه تغییرات
- ورود بدون `ReturnUrl` امن: کاربر شرکتی ← `/app/`؛ حساب Master (بدون شرکت، با صفحات Razor مدیریتی) ← `/` (`LoginModel.GetDefaultDestination`).
- `LoginSuccess` فقط با `[Authorize]` به همان مقصد Redirect می‌کند (لینک‌های قدیمی).
- `ReturnUrl` که به `/Account/Login`، `/Account/LoginSuccess` یا زیرمسیر آن برگردد نادیده گرفته می‌شود.
- `AddPageSuccess` حذف شد (پیام TempData که هیچ صفحه‌ای نمی‌خواند).

## فایل‌های مهم تغییرکرده
- `Server/Pages/Account/Login.cshtml.cs`، `LoginSuccess.cshtml(.cs)` — منطق هدایت.
- `tests/Template.Tests/LoginAuthenticationTests.cs`، `Integration/HttpSecurityTests.cs`، `tests/e2e/specs/faranou.spec.mjs` — تست‌ها.
- `docs/ARCHITECTURE.md`، `PROJECT_STATUS.md`، `SECURITY.md`، `WORK_LOG.md` — مستندات.
- `Server/wwwroot/css/rtl/taraz-login.css` — فقط کامنت.

## تصمیم‌های فنی
- تصمیم معماری جدیدی (ADR) گرفته نشد؛ قرارداد D-031 همان است. حساب Master به `/` می‌رود چون `/app/` برای آن همیشه «شرکتی در دسترس نیست» نشان می‌داد (D-015).
- منبع `LoginSucceededPendingFeatures` و CSS `shell--single` عمداً ماندند (حذف دستی Designer/CSS بی‌ضرورت؛ بی‌مصرف‌اند). ارجاع تاریخی handoffهای قبلی بدون تغییر.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | |
| `dotnet test Template.sln -c Release` روی SQL Server 2022 واقعی (Docker) | ۴۹۷ کل، ۴۹۵ موفق، ۲ Skip، ۰ شکست | اجرای نهایی پس از اصلاحات بازبینی |
| E2E Playwright (Chromium ↔ Publish ↔ SQL Server) | ۲۰ از ۲۰ موفق (نسخهٔ Publish نهایی) | شامل ورود چندشرکتی، ورود مستقیم، بدون شرکت/بدون دوره، Master، لینک قدیمی |
| `scripts/publish.sh` (typecheck، تست و build کلاینت، offline check) | موفق | |
| `check_static_assets.py`، `git diff --check` | موفق | |

پوشش ۹ سناریوی مأموریت: ۱–۴ و ۶–۸ با تست‌های موجود `WorkContextHttpTests` و E2E؛ ۵ با تست E2E انقضا؛ جدید: مقصد پس از ورود، `LoginSuccess`، `ReturnUrl` به صفحات ورود، Master.

## بازبینی
- `code-reviewer`: بدون Blocker؛ یافته‌های مستندات، ثابت جادویی، ثابت بی‌مصرف و تست‌های ضعیف اصلاح شد.
- `security-reviewer` (حساس: ورود/Redirect): بدون یافتهٔ Critical/High/Medium. Low: شکل‌های دیگر مسیر (`~/Account/Login`، `/./`) فیلتر متنی را دور می‌زنند؛ فقط UX (فرم ورود دوباره)، بدون اثر امنیتی؛ باقی ماند.

## امنیت، داده و سازگاری
- Open Redirect: `Url.IsLocalUrl` حفظ شد؛ مقصد پیش‌فرض ثابت است. Cookie/Anti-forgery بدون تغییر. دسترسی داده را همچنان سرور در `GET/POST /api/v1/context` تعیین می‌کند. بدون Schema/Migration.

## ریسک‌ها و کارهای باقی‌مانده
- مقصدهای مطلق `/app/` و `/` با `PathBase` سازگار نیستند (امروز `UsePathBase` ندارد).
- هدایت کاربر واردشده از `GET /Account/Login` انجام نشد (خارج از محدوده).
- Windows/IIS اجرا نشد.

## شناسه‌های Git
- Branch: `fix/login-redirect-work-context`
- Commit/PR: در گزارش نهایی
