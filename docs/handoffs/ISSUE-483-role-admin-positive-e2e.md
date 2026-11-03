# تحویل وظیفه — ISSUE-483-role-admin-positive-e2e

- **Issue:** #483 (زیرمجموعه #444)
- **Agent/مسئول:** ChatGPT — Backend verification / E2E
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی تست انجام شد؛ Verified فقط پس از CI سبز

## هدف و معیارهای پذیرش
- [x] ایجاد حساب مدیر نصب اختصاصی برای آزمون؛ بدون ارتقای حساب‌های عادی موجود.
- [x] احراز مدیر با خواندن نقش فعال Admin از دیتابیس در درخواست واقعی.
- [x] خواندن Permission Catalog و نقش‌های یک شرکت.
- [x] رد mutation فاقد Anti-forgery.
- [x] ایجاد نقش با کد مجوز موجود در کاتالوگ و بازخوانی مقدار ذخیره‌شده.
- [x] به‌روزرسانی مجوزها و بازخوانی نتیجه.
- [x] رد شناسه نقش متعلق به شرکت دیگر با 404.
- [x] اجرای سناریو در job موجود E2E با سرور Production منتشرشده و SQL Server.
- [ ] اجرای موفق CI نهایی روی SHA این PR؛ تا دریافت نتیجه، وضعیت Verified نیست.

## خلاصه تغییرات
- Seed آزمون یک نقش سراسری Admin و کاربر اختصاصی با رمز تصادفی اضافه می‌کند.
- تست Playwright مسیر API واقعی را با Cookie احراز هویت‌شده اجرا می‌کند؛ API از مرورگر با same-origin fetch فراخوانی می‌شود.
- مجوز نمونه corporate-groups.view از کاتالوگ موجود migration استفاده می‌شود؛ هیچ مجوزی به عضویت یا کاربر اعطا نمی‌شود.
- پس از آزمون نقش غیرفعال می‌شود؛ دیتابیس E2E یک‌بارمصرف است و در پایان job پاک می‌شود.

## فایل‌های مهم تغییرکرده
- `tests/e2e/seed.mjs` — seed مدیر نصب و RoleId فقط در دیتابیس disposable.
- `tests/e2e/specs/faranou.spec.mjs` — سناریوی مثبت API، Anti-forgery و cross-company IDOR.
- `docs/WORK_LOG.md` — ثبت append-only.
- `docs/INDEX.md` — ارجاع handoff.

## تصمیم‌های فنی
- حساب Infrastructure Master با حساب کاربری دارای Role.Name == Admin یکی نیست؛ برای اثبات مسیر AuthorizeInstallationAdministratorAsync، کاربر واقعی جدول Users با Role فعال seed می‌شود.
- از permission موجود migration استفاده شد؛ هیچ کاتالوگ، role یا schema جدیدی در Production ساخته نمی‌شود.
- تست‌ها به ترتیب اجرا می‌شوند (workers: 1 در Playwright config) و حساب‌های دیگر را تغییر نمی‌دهند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| اجرای محلی Playwright | اجرا نشده | این محیط GitHub clone را به‌علت DNS مسدود کرد؛ .NET SDK نیز نصب نیست. |
| CI .NET CI job e2e | در انتظار | باید روی PR head اجرا شود؛ فقط پس از موفقیت گزارش Verified می‌شود. |
| Build / Unit / Integration | اجرا نشده محلی | هیچ production code یا migration تغییر نکرده است؛ CI همچنان تمام suite را اجرا می‌کند. |

## امنیت، داده و سازگاری
- رمز مدیر E2E تصادفی است و در فایل state محلی git-ignored نگهداری می‌شود؛ هیچ credential به مخزن افزوده نشده است.
- حساب‌های عادی Alice/Bob و Master تغییر نکرده‌اند.
- درخواست‌های تغییر‌دهنده با توکن Anti-forgery واقعی انجام می‌شوند؛ درخواست بدون توکن باید 400 شود.
- role ID شرکت دیگر باید در مسیر شرکت جاری 404 شود.
- هیچ تغییر Production، Schema، Migration یا مجوز عملیاتی واقعی ایجاد نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای CI روی PR باید ثابت کند SQL insert مربوط به Role/RoleId با schema جاری سازگار است و سناریو در Production E2E پاس می‌شود.
- این PR جای تست concurrency/RowVersion یا Delegation Ceiling را نمی‌گیرد.

## شناسه‌های Git
- Branch: agent/483-role-admin-positive-e2e
- Commit: در ایجاد PR ثبت می‌شود.
- Pull Request: در ایجاد PR ثبت می‌شود.
