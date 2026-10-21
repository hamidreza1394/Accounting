# تحویل وظیفه — ISSUE-UNNUMBERED-company-fiscal-period-selection

- **Issue:** `UNNUMBERED` (شماره جعلی ساخته نشد)
- **Agent/مسئول:** Claude (Sonnet 5.5)
- **تاریخ:** 2026-10-21
- **وضعیت:** ناقص تا تأیید CI — پیاده‌سازی و تست محلی کامل؛ CI/E2E روی SQL Server اجرا نشده؛ ادغام نشده

## هدف و معیارهای پذیرش
- [x] یک شرکت با یک دوره معتبر → ورود مستقیم (تست: `App.test.tsx`، `WorkContextOptionsTests`)
- [x] چند شرکت یا چند دوره → صفحه انتخاب (تست‌های کلاینت و سرور)
- [x] بدون شرکت مجاز / بدون دوره معتبر → مسدود با پیام و گام بعدی
- [x] هر ورود جدید دوباره می‌پرسد؛ انتخاب قبلی بازیابی نمی‌شود (Cookie وابسته به `SessionId` هر ورود)
- [x] اعتبار فقط سمت سرور؛ دوره باید متعلق به همان شرکت باشد؛ دست‌کاری شناسه 404 یکسان؛ لغو عضویت فوراً اثر می‌کند
- [x] UI فارسی/RTL با حالت‌های بارگذاری، خطا، نشست منقضی، جلوگیری از ارسال دوباره، صفحه‌کلید/فوکوس
- [ ] CI و E2E روی SQL Server (اجرا نشده؛ فقط CI)
- [x] تصمیم کارفرما (D-032): Open=ورود عادی، Closed=فقط مشاهده/گزارش، Archived/بیگانه=ممنوع؛ تغییر زمینه حین نشست مجاز با ممیزی (تست: `WorkContextTests`، `WorkContextHttpTests`، `App.test.tsx`)

## خلاصه تغییرات
- سرور: `WorkContextService`/`WorkContextCookie`/`FiscalPeriodSelectionPolicy` و endpointهای `GET|POST|DELETE /api/v1/context`؛ `SessionId` Claim در ورود؛ حذف Cookie در ورود/خروج.
- کلاینت: `WorkContextSelector`، `BlockedAccess`، نوار زمینه در `AppShell`، `components/ui` (Button، Badge، Alert، ChoiceCard، آیکون‌ها)، `?company=` حذف شد.
- E2E: `seed.mjs` (کاربران `dave` و `erin`) و `faranou.spec.mjs` با جریان جدید.
- **Schema/Migration: بدون تغییر** (دلیل در `docs/DATABASE.md`).

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Tenancy/WorkContext.cs`، `WorkContextCookie.cs`، `WorkContextService.cs`، `Server/Api/WorkContextEndpoints.cs`، `Server/Api/ApiEndpoints.cs`، `Server/Program.cs`، `Server/Pages/Account/Login.cshtml.cs`، `Logout.cshtml.cs`
- `ClientApp/src/App.tsx`، `api/client.ts`، `api/types.ts`، `components/*`، `components/ui/*`
- `tests/Template.Tests/WorkContextTests.cs`، `Integration/WorkContextHttpTests.cs`، `Integration/FaranouApplicationFactory.cs`
- `tests/e2e/seed.mjs`، `tests/e2e/specs/faranou.spec.mjs`
- مستندات: `DECISIONS.md` (D-031)، `ARCHITECTURE.md`، `SECURITY.md`، `TESTING.md`، `DATABASE.md`، `PROJECT_STATUS.md`، `ROADMAP.md`، `PRODUCT_REQUIREMENTS.md`

## طرح تصویری تأییدشده (به‌روزرسانی)
- صفحه انتخاب مطابق طرح کارفرما بازسازی شد: `FaranouLogo`، سربرگ، گام ۱ شرکت (جست‌وجوی نرمال‌شده)، پنل دوره سمت راست، نوار پایین چسبان، حالت بارگذاری دکمه، نشان انتخاب فقط با رنگ نیست.
- **عمداً نمایش داده نمی‌شود:** وضعیت مجوز، تعداد کاربران، زیرسیستم‌ها، سطح دسترسی، زنگ اعلان (داده/قابلیت واقعی ندارند؛ لایسنس `NotImplemented`).
- متن توضیح صفحه با ترتیب واقعی (ابتدا شرکت) اصلاح شد؛ عنوان «انتخاب دوره مالی و شرکت» طبق طرح حفظ شد.
- «انصراف» در اولین ورود = خروج از حساب؛ هنگام تغییر زمینه = بازگشت به داشبورد.
- رنگ اصلی `Button` در کیت کلاینت آبی `#2468D8` شد و هدر `AppShell` از لوگوی جدید استفاده می‌کند.
- نتیجه محلی: ۴۶ از ۴۶ تست کلاینت، build و check:offline موفق؛ بررسی بصری دسکتاپ/موبایل با Chromium و API شبیه‌سازی‌شده؛ E2E اجرا نشد.

## تصمیم‌های فنی
- ADR **D-032**: اعتبار دوره (Open/Closed/Archived)، `accessMode`، تغییر زمینه حین نشست و ممیزی `WorkContextSelected/Changed/SelectionDenied` (جدول موجود `SecurityAuditEvents`)؛ بدون تغییر Schema.
- ADR **D-031**: قواعد ورود، Cookie محافظت‌شده وابسته به ورود، اعتبارسنجی مجدد با دیتابیس، «دوره معتبر = Open» (`NeedsApproval`)، بدون تغییر Schema.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | ۰ خطا، ۷۴۷ هشدار |
| `dotnet test --solution Template.sln --no-build -c Release` | موفق | total 253، failed 0، skipped 0 — **Provider حافظه‌ای، نه SQL Server** |
| `npx tsc --noEmit` (کلاینت) | موفق | بدون خطا |
| `npx vitest run` | موفق | ۴۸ از ۴۸ |
| `npm run build` / `npm run check:offline` | موفق | ۱۲ منبع محلی، بدون مرجع خارجی |
| `python3 scripts/check_static_assets.py` | موفق | بدون مرجع CDN |
| E2E Playwright (`tests/e2e`) | **اجرا نشد** | SQL Server/Docker در محیط نیست؛ فقط `node --check` سینتکس |
| بررسی بصری | انجام شد با محدودیت | Chromium + API شبیه‌سازی‌شده؛ دسکتاپ/موبایل، RTL، بدون اسکرول افقی؛ داشبورد با داده واقعی دیده نشد |
| Migration روی SQL Server | اجرا نشد / لازم نبود | Schema تغییر نکرده |

## امنیت، داده و سازگاری
- 404 یکسان، 409 برای دوره غیرقابل‌انتخاب، 400 برای Anti-forgery، 401 برای نشست بدون `SessionId` (باید دوباره وارد شود). بدون داده حساس در پاسخ/Log. ادعای امنیت کامل نمی‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای CI و E2E روی شاخه (E2E شامل سناریوهای جدید دوره بسته و تغییر زمینه است و فقط در CI اجرا می‌شود).
- رفتار `Archived` و دوره‌های آینده `NeedsApproval`. اِعمال `AllowsWrites` روی Endpointهای نوشتن آینده الزامی و فعلاً `NotImplemented` است (Endpoint نوشتن وجود ندارد).
- CHECK/عدم هم‌پوشانی دوره‌ها در دیتابیس وجود ندارد (پیشنهاد جداگانه).
- این شاخه روی `docs/architecture-source-of-truth` (PR #46) پایه‌گذاری شده است.

## شناسه‌های Git
- Branch: `feature/company-fiscal-period-selection`
- Commit: `f6e57d4` (سرور)، `175837b` (کلاینت)، و commit مستندات/E2E این تحویل
- Pull Request: در گزارش نهایی
