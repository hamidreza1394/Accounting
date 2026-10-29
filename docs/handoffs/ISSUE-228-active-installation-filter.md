# تحویل وظیفه — ISSUE-228-active-installation-filter

- **Issue:** [#228 — Vendor onboarding](https://github.com/hamidreza1394/Accounting/issues/228)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-29
- **وضعیت:** PR #273 ادغام شد؛ CI نهایی موفق است

## هدف و معیارهای پذیرش
- [x] نصب‌های غیرفعال در فرم راه‌اندازی قابل انتخاب نباشند.
- [x] اولین نصب فعال به‌صورت پیش‌فرض انتخاب شود.
- [x] وقتی نصب فعال وجود ندارد، پیام راهنما دقیق باشد.
- [x] تست رگرسیون برای نصب غیرفعال/فعال اضافه شود.

## خلاصه تغییرات
- فهرست نصب‌های دریافتی از API در UI به وضعیت `Active` محدود شد.
- حفظ انتخاب فعلی فقط در صورتی انجام می‌شود که آن نصب همچنان فعال باشد؛ در غیر این صورت اولین نصب فعال انتخاب می‌شود.
- متن حالت خالی به نبود نصب فعال اشاره می‌کند، نه نبود هر نوع نصب.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Web/src/ProvisioningForm.tsx` — فیلتر نصب‌های فعال و پیام حالت خالی.
- `Vendor/Vendor.Web/src/ProvisioningForm.test.tsx` — تست نصب غیرفعال و پیش‌فرض نصب فعال.
- `docs/WORK_LOG.md` — ثبت یافته و محدوده.
- `docs/handoffs/ISSUE-228-active-installation-filter.md` — این تحویل.

## تصمیم‌های فنی
- فقط گزینه‌های `status === 'Active'` در UI ارائه می‌شوند؛ اعتبارسنجی سمت سرور همچنان منبع نهایی تصمیم است.
- هیچ تغییر API، دیتابیس، schema، migration، `Server/**` یا دیتابیس مشتری انجام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی Vitest | اجرا نشده | این نشست دسترسی اجرای محلی مخزن ندارد؛ CI باید تست را اجرا کند. |
| Vendor CI | موفق روی SHA نهایی | [run 40829071732](https://github.com/hamidreza1394/Accounting/actions/runs/40829071732) — موفق. |
| .NET CI / E2E / انتشار Windows | همهٔ jobها موفق روی SHA نهایی | [run 40829071746](https://github.com/hamidreza1394/Accounting/actions/runs/40829071746) — build-and-test، publish، E2E و Windows publish همگی موفق. |
| CodeQL / Semgrep | هر دو موفق روی SHA نهایی | CodeQL: [run 40829067402](https://github.com/hamidreza1394/Accounting/actions/runs/40829067402)؛ Semgrep: [run 40829071675](https://github.com/hamidreza1394/Accounting/actions/runs/40829071675). |

## امنیت، داده و سازگاری
- این تغییر UX را با محدودیت فعال بودن نصب هم‌راستا می‌کند؛ کنترل امنیتی سمت سرور حذف یا تضعیف نشده است.
- هیچ رمز، کلید یا راز جدیدی اضافه نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- فرض UI این است که API وضعیت را به‌صورت رشتهٔ `Active` برمی‌گرداند؛ تست و قرارداد فعلی همین مقدار را استفاده می‌کنند.
- اگر نصب فعال وجود نداشته باشد، کاربر باید از گردش مدیریت نصب آن را فعال کند.
- تغییر روی SHA نهایی بررسی شد و همهٔ CIهای ثبت‌شده موفق‌اند؛ تست محلی Vitest در این نشست اجرا نشده است.

## شناسه‌های Git
- **Branch:** `fix/228-hide-inactive-provisioning-installations`
- **پیاده‌سازی اصلی:** commit `ff80afcbd5193d879dd1ec6b2b36041b3baf5b35`.
- **Pull Request:** [#273 — fix(vendor): only allow active provisioning installations](https://github.com/hamidreza1394/Accounting/pull/273).
- **وضعیت ادغام:** ادغام‌شده؛ merge commit `a37b1c32b84d7622dbb7770f38aa852979058ae3`. PR #273 پس از موفقیت Vendor CI، .NET CI/E2E/انتشار Windows، CodeQL و Semgrep ادغام شد.
