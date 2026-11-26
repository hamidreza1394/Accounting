# تحویل وظیفه — ISSUE-795-vendor-sln-licensing-contracts

- **Issue:** [#795](https://github.com/hamidreza1394/Accounting/issues/795)
- **Agent/مسئول:** Codex
- **تاریخ:** 2026-11-28
- **وضعیت:** پیاده‌سازی در شاخه؛ CI از طریق PR در انتظار

## هدف و معیارهای پذیرش
- [x] پروژهٔ `Licensing.Contracts` که `Vendor.Host.csproj` به آن ارجاع می‌دهد در `Vendor.sln` ثبت شود.
- [x] پیکربندی‌های Debug و Release برای Any CPU ثبت شوند.
- [x] Restore، Build و Test متمرکز Vendor روی CI بررسی شوند.

## خلاصه تغییرات
- در main@791b99b تایید شد که `Vendor.Host.csproj` به `../../Licensing.Contracts/Licensing.Contracts.csproj` ارجاع دارد، اما پروژه در `Vendor.sln` فهرست نشده است.
- پروژهٔ قرارداد با GUID مستقل و پیکربندی‌های Debug/Release به solution افزوده شد.
- هیچ پروژهٔ Customer، دیتابیس، Migration، secret، gate یا تنظیم نصب سرویس تغییر نکرد.

## فایل‌های مهم تغییرکرده
- `Vendor.sln` — ثبت پروژهٔ ارجاع‌شده و پیکربندی‌های ساخت آن.
- `docs/INDEX.md` — فهرست handoff.
- `docs/WORK_LOG.md` — ثبت شواهد و نتیجهٔ بررسی.
- `docs/handoffs/ISSUE-795-vendor-sln-licensing-contracts.md` — این گزارش.

## تصمیم‌های فنی
- از GUID پروژهٔ C# موجود در Vendor.sln استفاده شد؛ GUID پروژهٔ جدید یکتا است.
- تغییر تنها در فهرست solution است؛ ProjectReference موجود در Host بدون تغییر باقی ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی `Vendor.sln` و `Vendor.Host.csproj` در main | موفق | ناسازگاری فهرست solution و ProjectReference تأیید شد |
| `dotnet restore Vendor.sln` | CI موفق | Vendor CI run #45856392286، گام Restore Vendor solution |
| `dotnet build Vendor.sln --no-restore -c Release` | CI موفق | Vendor CI run #45856392286، گام Build Vendor solution |
| `dotnet test Vendor.sln --no-build -c Release` | CI موفق | Vendor CI run #45856392286، گام Test Vendor solution |
| `git diff --check` | اجرا نشده | checkout محلی مخزن در این محیط موجود نیست |

## امنیت، داده و سازگاری
- فقط فایل Solution و مستندات تغییر می‌کنند؛ هیچ اثر داده‌ای، Migration، تغییر مرز امنیتی یا تغییر runtime برنامه در نظر گرفته نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- Vendor CI run [#45856392286](https://github.com/hamidreza1394/Accounting/actions/runs/45856392286) هر ۴ Job را موفق تکمیل کرد؛ restore/build/test متمرکز Vendor نیز موفق بودند. Semgrep run #45856392341 موفق است. .NET CI run #45856392373 روی commit قبلی در حال اجرا بود؛ نتیجهٔ آن و CI روی commit نهایی باید جداگانه بررسی شود. diff check محلی اجرا نشد.

## شناسه‌های Git
- Branch: `fix/vendor-sln-licensing-contracts`
- Commit اولیه: `cce785137c152d287262276bb923f9daab583541`; Vendor CI موفق: run `45856392286`.
- Pull Request: [#796](https://github.com/hamidreza1394/Accounting/pull/796)
