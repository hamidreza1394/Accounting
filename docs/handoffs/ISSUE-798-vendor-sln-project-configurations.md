# تحویل وظیفه — ISSUE-798-vendor-sln-project-configurations

- **Issue:** [#798](https://github.com/hamidreza1394/Accounting/issues/798)
- **Agent/مسئول:** Claude
- **تاریخ:** 2026-11-26
- **وضعیت:** انجام‌شده در شاخه؛ نتیجهٔ CI روی head نهایی در PR ثبت می‌شود

## هدف و معیارهای پذیرش
- [x] هر چهار پروژهٔ `Vendor.sln` (`Vendor.Host`، `Vendor.Host.Tests`، `Vendor.Persistence`، `Licensing.Contracts`) ردیف‌های معتبر Debug/Release Any CPU (ActiveCfg و Build.0) داشته باشند.
- [x] ردیف خراب `Release|Any CPU.Build.0` پروژهٔ `Vendor.Host.Tests` اصلاح شود.
- [x] اصلاح تاریخ ردیف WORK_LOG مربوط به #795 به‌صورت append-only ثبت شود.

## خلاصه تغییرات
- `Vendor.Persistence` (`{97E9A8B4-1D61-4E38-AD2A-4DD54B35E136}`) هیچ ردیف `ProjectConfigurationPlatforms` نداشت؛ چهار ردیف Debug/Release Any CPU افزوده شد.
- مقدار ردیف `{E0B7AF89-…}.Release|Any CPU.Build.0` از `{E0B7AF89-…}.Release|Any CPU` به `Release|Any CPU` اصلاح شد.
- در `docs/WORK_LOG.md` ردیف جدید ثبت شد که تاریخ واقعی کار #795/#796 (2026-11-26) و تکمیل موفق .NET CI آن را ثبت می‌کند؛ ردیف قبلی بازنویسی نشد.

## فایل‌های مهم تغییرکرده
- `Vendor.sln` — اصلاح ردیف‌های پیکربندی پروژه‌ها.
- `docs/WORK_LOG.md` — ثبت کار و اصلاح سابقهٔ #795.
- `docs/INDEX.md` — فهرست این handoff.
- `docs/handoffs/ISSUE-798-vendor-sln-project-configurations.md` — این گزارش.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. پروژه‌هایی که `Vendor.Host.Tests` به آن‌ها ارجاع دارد ولی در Solution نیستند (`Vendor.CheckpointProcessHelper` و `Server`) عمداً اضافه نشدند؛ افزودن `Server` کل برنامهٔ مشتری را وارد Solution Vendor می‌کند و خارج از دامنهٔ این Issue است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی ساختاری ردیف‌های پیکربندی با اسکریپت Python | موفق | هر چهار پروژه دقیقاً ۴ ردیف معتبر دارند |
| `dotnet sln Vendor.sln list` (SDK 10.0.404، Linux) | موفق | هر چهار پروژه فهرست شدند |
| `dotnet restore Vendor.sln` | موفق | |
| `dotnet build Vendor.sln --no-restore -c Release` | موفق | 0 خطا |
| `dotnet test Vendor.sln --no-build -c Release` (محلی) | ناموفق به‌دلیل محیط | total 467: succeeded 375، skipped 82، failed 10. هر ۱۰ مورد خطای 500 با پیام `Vendor persistence is not configured…` دارند، چون محیط محلی `FARANO_VENDOR_ConnectionStrings__VendorDb` و SQL Server ندارد؛ CI این متغیر و SQL Server را فراهم می‌کند. تغییر این Issue فقط فهرست Solution است و رفتار زمان اجرا را تغییر نمی‌دهد. |
| `git diff --check` | موفق | |
| Vendor CI و .NET CI روی head نهایی | در PR ثبت می‌شود | |

## امنیت، داده و سازگاری
- فقط فایل Solution و مستندات؛ بدون اثر روی امنیت، داده، Migration، csproj، Customer یا gateها.

## ریسک‌ها و کارهای باقی‌مانده
- `Vendor.CheckpointProcessHelper` و `Server` در Solution Vendor فهرست نیستند؛ Build از طریق ProjectReference سالم است. در صورت نیاز در Issue جدا بررسی شود.

## شناسه‌های Git
- Branch: `fix/vendor-sln-project-configurations`
- Commit: در PR ثبت می‌شود
- Pull Request: در PR ثبت می‌شود
