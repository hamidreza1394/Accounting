# تحویل وظیفه — ISSUE-483-role-admin-positive-e2e

- **Issue:** #483 (زیرمجموعه #444)
- **مسئول:** ChatGPT — Backend verification / E2E
- **تاریخ:** 2026-11-03
- **وضعیت:** CI کامل سبز روی SHA اولیه؛ PR پس از همگام‌سازی با main مجدداً در حال CI

## هدف و معیارهای پذیرش
- [x] ایجاد حساب مدیر نصب اختصاصی برای آزمون؛ بدون ارتقای حساب‌های عادی موجود.
- [x] احراز مدیر با خواندن نقش فعال Admin از دیتابیس در درخواست واقعی.
- [x] خواندن Permission Catalog و نقش‌های یک شرکت.
- [x] رد mutation فاقد Anti-forgery.
- [x] ایجاد نقش با کد مجوز موجود در کاتالوگ و بازخوانی مقدار ذخیره‌شده.
- [x] به‌روزرسانی مجوزها و بازخوانی نتیجه.
- [x] رد شناسه نقش متعلق به شرکت دیگر با 404.
- [x] اجرای سناریو در job موجود E2E با سرور Production منتشرشده و SQL Server.
- [x] اصلاح race تست عنوان صفحهٔ Forbidden با waitFor؛ هیچ رفتار Production تغییر نکرد.
- [ ] اجرای موفق CI نهایی روی SHA همگام‌شده با main.

## خلاصه تغییرات
- Seed آزمون یک نقش سراسری Admin و کاربر اختصاصی با رمز تصادفی اضافه می‌کند.
- تست Playwright مسیر API واقعی را با Cookie احراز هویت‌شده اجرا می‌کند؛ API از مرورگر با same-origin fetch فراخوانی می‌شود.
- مجوز نمونه corporate-groups.view از کاتالوگ موجود migration استفاده می‌شود؛ هیچ مجوزی به عضویت یا کاربر اعطا نمی‌شود.
- پس از آزمون نقش غیرفعال می‌شود؛ دیتابیس E2E یک‌بارمصرف است و در پایان job پاک می‌شود.

## بررسی و آزمون
| مورد | نتیجه |
|---|---|
| CI اولیه | کاملاً سبز: Semgrep، secret scan، client، build-and-test، publish-scripts، SQL Server E2E، publish و publish-windows |
| CI پس از همگام‌سازی | در حال اجرا |
| اجرای محلی | اجرا نشده؛ GitHub DNS و .NET SDK در محیط در دسترس نیست |

## امنیت و سازگاری
- رمز مدیر E2E تصادفی است و در فایل state محلی git-ignored نگهداری می‌شود.
- حساب‌های عادی Alice/Bob و Master تغییر نکرده‌اند.
- درخواست‌های تغییر‌دهنده با توکن Anti-forgery واقعی انجام می‌شوند؛ درخواست بدون توکن باید 400 شود.
- role ID شرکت دیگر باید در مسیر شرکت جاری 404 شود.
- هیچ تغییر Production، Schema، Migration یا مجوز عملیاتی واقعی ایجاد نشده است.

## فایل‌ها
- tests/e2e/seed.mjs
- tests/e2e/specs/faranou.spec.mjs
- ClientApp/src/AppRouting.test.tsx
- docs/WORK_LOG.md
- docs/INDEX.md
- این handoff
