# تحویل وظیفه — ISSUE-75-self-hosted-runner-smoke-test

- **Issue:** [#75](https://github.com/hamidreza1394/Accounting/issues/75)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-25
- **وضعیت:** پیاده‌سازی در شاخه مستقل؛ نتیجه اجرای واقعی Job هنوز تأیید نشده

## هدف و معیارهای پذیرش
- [x] افزودن Workflow جداگانه که فقط روی برچسب‌های self-hosted Windows X64 اجرا شود.
- [x] ثبت نسخه ابزارهای پایه بدون خواندن اسرار یا تغییر وضعیت سرور.
- [ ] تکمیل یک اجرای موفق واقعی روی رانر؛ این مورد به نتیجه GitHub Actions وابسته است.
- [x] عدم تغییر Workflow اصلی CI و عدم ادغام خودکار در main.

## خلاصه تغییرات
- Workflow مستقل برای آزمون کوتاه رانر اضافه شد؛ با push تغییر همین فایل و با workflow_dispatch قابل اجراست.
- آزمون فقط هویت رانر، نسخه PowerShell و نسخه‌های Git، .NET، Node.js و npm را گزارش می‌کند. نبودن ابزارهای فرعی هشدار می‌دهد و به‌تنهایی باعث شکست نمی‌شود.
- این Workflow هیچ دیتابیس، فایل پروژه، تنظیم سرویس یا اسراری را تغییر نمی‌دهد.

## فایل‌های مهم تغییرکرده
- `.github/workflows/self-hosted-runner-smoke-test.yml` — آزمون حداقلی رانر.
- `docs/handoffs/ISSUE-75-self-hosted-runner-smoke-test.md` — این گزارش.
- `docs/WORK_LOG.md` — ثبت کار و محدودیت آزمون.

## تصمیم‌های فنی
- استفاده از Workflow مستقل به‌جای تغییر `.github/workflows/dotnet.yml`؛ Jobهای فعلی به Ubuntu، Docker و Windows-hosted وابسته‌اند و انتقال ساده آن‌ها به یک Windows runner درست نیست.
- تصمیم معماری جدیدی گرفته نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی `AGENTS.md` و Workflow موجود | موفق | محدوده تغییر تأیید شد |
| اجرای Workflow روی GitHub Actions | در انتظار | هنوز نتیجه اجرا در دسترس نیست |
| `git diff --check` | اجرا نشده | تغییرات از GitHub Contents API ثبت شده‌اند؛ محیط Git محلی در این کار استفاده نشد |

## امنیت، داده و سازگاری
- دسترسی Workflow به مخزن فقط `contents: read` است.
- هیچ Secret یا توکن چاپ/خوانده نمی‌شود.
- این تغییر فقط در شاخه مستقل است و Workflow اصلی را تغییر نمی‌دهد.

## ریسک‌ها و کارهای باقی‌مانده
- محدودیت حساب/صورتحساب GitHub ممکن است حتی با self-hosted runner مانع شروع Job شود.
- اجرای موفق این Workflow، تنها اتصال و ابزارهای پایه رانر را تأیید می‌کند؛ به معنی موفقیت کل CI یا سازگاری کامل تست‌های SQL Server روی Windows نیست.

## شناسه‌های Git
- Branch: `test/self-hosted-runner-smoke`
- Commit: `8b3762fb44f3ece63cfba049c2a58aee9821a6ac` (اولین Commit Workflow)
- Pull Request: پس از تکمیل مستندات ثبت می‌شود.
