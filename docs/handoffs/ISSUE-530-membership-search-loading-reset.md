# تحویل وظیفه — ISSUE-530-membership-search-loading-reset

- **Issue:** [#530](https://github.com/hamidreza1394/Accounting/issues/530)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-04
- **وضعیت:** پیاده‌سازی و تست رگرسیون ثبت شد؛ اجرای تست‌ها و CI هنوز تأیید نشده است.

## هدف و معیارهای پذیرش
- [x] پاک کردن query هنگام pending بودن درخواست، UI را از loading خارج کند.
- [x] دکمه جستجو پس از لغو درخواست دوباره فعال شود.
- [x] جستجوی معتبر بعدی بتواند نتیجه جدید را نمایش دهد.
- [x] تست رگرسیون مسیر درخواست معلق، لغو با پاک‌کردن query و جستجوی مجدد را پوشش دهد.
- [x] بدون تغییر API، Backend، مجوزها، دیتابیس یا dependency.

## خلاصه تغییرات
- هنگام تغییر ورودی و خالی‌شدن query، `loading` بلافاصله به `false` بازنشانی می‌شود؛ مسیر ورودی نامعتبر نیز همین reset را انجام می‌دهد. effect همچنان درخواست قبلی را با AbortController لغو می‌کند.
- AbortController همچنان درخواست قبلی را لغو می‌کند و handlerهای قبلی نتیجه یا خطای درخواست لغوشده را به UI اعمال نمی‌کنند.
- تست رگرسیون درخواست اول را معلق نگه می‌دارد، query را پاک می‌کند، فعال‌شدن دکمه را بررسی می‌کند و سپس موفقیت جستجوی دوم را assert می‌کند.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/components/membership/CreateMembershipRequest.tsx`
- `ClientApp/src/components/membership/CreateMembershipRequest.test.tsx`
- `docs/WORK_LOG.md`
- `docs/INDEX.md`
- این handoff.

## تصمیم‌های فنی
- از همان state موجود استفاده شد؛ abstraction یا dependency جدید اضافه نشد.
- reset در handler تغییر ورودی و مسیر query نامعتبر انجام می‌شود تا UI بدون انتظار برای تکمیل درخواست قبلی به حالت قابل جستجو برگردد.
- تغییری در قرارداد API یا کنترل امنیتی ایجاد نشد.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تست رگرسیون Vitest | اجرا نشده | تست به شاخه افزوده شده؛ اجرای محلی در این محیط در دسترس نیست. |
| `npm run typecheck` | اجرا نشده | منتظر CI |
| `npm test` | اجرا نشده | منتظر CI |
| `npm run build` | اجرا نشده | منتظر CI |
| `npm run check:offline` | اجرا نشده | منتظر CI |
| `git diff --check` | اجرا نشده | Git working tree محلی در دسترس نیست؛ diff از طریق GitHub بازبینی می‌شود. |

## امنیت، داده و سازگاری
- هیچ تغییر در authorization، API، tenancy، داده، Migration، Cookie یا Anti-forgery نیست.
- درخواست لغوشده نباید پاسخ خود را روی state درخواست جدید اعمال کند؛ تست مسیر پاک‌کردن و جستجوی مجدد را بررسی می‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای واقعی Vitest، typecheck، build و check:offline باید در CI روی head نهایی PR تأیید شود.
- مرورگر واقعی و تست E2E اجرا نشده‌اند.

## شناسه‌های Git
- Branch: `fix/530-membership-search-loading-reset`
- Regression test commit: `15634dad38f617733b8c408f5f89db1a04e9107b`
- Implementation commit: `bf655e864ad95350068a4384de93f5e6589e649b`
- PR: [#531](https://github.com/hamidreza1394/Accounting/pull/531)
