# تحویل وظیفه — ISSUE-UNNUMBERED-work-context-selector-redesign

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** Claude (Lead) + `frontend-developer` (پیاده‌سازی)، `code-reviewer`، `security-reviewer`
- **تاریخ:** 2026-10-23
- **وضعیت:** انجام‌شده (PR ادغام‌نشده)

## هدف و معیارهای پذیرش
- [x] بازطراحی فقط صفحه انتخاب شرکت و دوره مالی؛ اول دوره مالی (راست)، بعد شرکت‌های مجاز (چپ)
- [x] بدون تغییر API/Schema/امنیت سرور؛ فقط داده واقعی؛ بدون CDN
- [x] لوگوی رسمی (نه ساختگی)، فونت محلی با مجوز مستند
- [x] حالت‌های خالی/خطا/بارگذاری/غیرفعال؛ واکنش‌گرا
- [x] تست واحد، .NET روی SQL Server واقعی، E2E، اندازه‌گیری قبل/بعد
- [ ] بازبینی انسانی کارفرما روی مرورگر واقعی

## خلاصه تغییرات
- انتخاب‌گر بازنویسی شد (`WorkContextSelector`، `SelectionLayout`، `lib/fiscalYears.ts`)؛ نشان رسمی بریده‌شده، Vazirmatn متغیر، توکن‌های رنگ جدید، پس‌زمینهٔ CSS.
- پیش‌انتخاب سخت‌گیرانه‌تر (فقط یک دورهٔ باز و یک شرکت).
- `BlockedAccess`، `Feedback` (اسکلت بارگذاری)، `AppShell` (لوگو) هماهنگ شدند.
- Seed E2E: کاربر `frank` و شرکت‌های d/e/f (نام بلند، همنام‌ها، سه سال).
- مستند: `docs/WORK_CONTEXT_SELECTOR_UI.md`.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/components/{WorkContextSelector,SelectionLayout,BlockedAccess,Feedback,AppShell}.tsx` و `ui/*`، `lib/fiscalYears.ts`، `styles/index.css` (+ تست‌ها)
- `Server/wwwroot/fonts/vazirmatn/*`، `Server/wwwroot/images/faranou-brand-mark.webp`
- `tests/e2e/seed.mjs`، `tests/e2e/specs/faranou.spec.mjs`
- `docs/{WORK_CONTEXT_SELECTOR_UI,ARCHITECTURE,PROJECT_STATUS,TESTING,INDEX,WORK_LOG}.md`

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد (ADR ثبت نشد). «سال مالی» در کلاینت از `startDate|endDate` دوره‌های واقعی گروه‌بندی می‌شود چون دوره‌ها در API به هر شرکت تعلق دارند.
- Vazirmatn (OFL) به‌جای IRANSansX (تجاری) برای کلاینت React.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 خطا، 610 هشدار |
| `dotnet test` (SQL Server 2022 واقعی، Docker) | موفق | 497 کل، 495 موفق، 2 Skip، 0 شکست |
| `npm run typecheck` / `npm test` / `npm run build` | موفق | 76 تست؛ JS 260.5KB (gzip 79.9KB) |
| `npm run check:offline` | موفق | 5 منبع محلی |
| `python3 scripts/check_static_assets.py` | موفق | |
| E2E Playwright/Chromium روی SQL Server واقعی | موفق | 24/24 |
| `git diff --check` | موفق | |
| lint | اجرا نشد | اسکریپت lint در `package.json` نیست |
| axe/صفحه‌خوان/Safari/Firefox/دستگاه فیزیکی/بار | اجرا نشد | ابزار یا محیط در دسترس نبود |

اندازه‌گیری قبل/بعد در `docs/WORK_CONTEXT_SELECTOR_UI.md` بخش ۶ (FCP حدود ۵۶ms بیشتر؛ علت راستی‌آزمایی نشد).

## بازبینی مستقل
- `code-reviewer` (sonnet، ≈۶۷٬۰۰۰ توکن): بدون یافتهٔ مسدودکننده. اصلاح‌شده: اسکرول خودکار هنگام حرکت با کلید جهت‌دار (فقط انتخاب با نشانگر)، افزودن شناسه ملی به خلاصهٔ پایین، handoff/WORK_LOG. تست خطای 404/409 از قبل وجود داشت. پذیرفته‌شده بدون تغییر: `selection` پس از بازخوانی فهرست دوباره اعتبارسنجی نمی‌شود، اما `year`/`selectedEntry` از داده جدید مشتق می‌شوند و دکمه غیرفعال می‌ماند؛ کلید دقیق `startDate|endDate` ممکن است سال‌های مرزبندی‌شده متفاوت را جدا نشان دهد (در مستند آمده).
- E2E و تست‌ها پس از این اصلاحات دوباره اجرا شدند (۲۴/۲۴؛ ۷۶ تست کلاینت).

## امنیت، داده و سازگاری
- بدون تغییر سمت سرور؛ `security-reviewer` (opus): یافتهٔ مهم ندارد. یافتهٔ Low: شناسه‌های ملی ثابت در seed با قالب ۱۱ رقمی واقعی (فقط دیتابیس E2E یک‌بارمصرف)؛ اصلاح نشد.
- تغییر فونت سراسری روی داشبورد هم اثر دارد.

## ریسک‌ها و کارهای باقی‌مانده
- بازبینی انسانی نهایی؛ نسخهٔ SVG رسمی لوگو؛ مرجع‌های سپیدار/راهکاران/حسابفا به منبع رسمی نرسید.
- `AGENTS.md`/`CLAUDE.md` هنوز «ADR بعدی D-036» دارند (واقعی D-037)؛ بدون تأیید تغییر داده نشد.

## شناسه‌های Git
- Branch: `feature/work-context-selector-redesign`
- Commit / Pull Request: در گزارش نهایی و PR ثبت می‌شود
