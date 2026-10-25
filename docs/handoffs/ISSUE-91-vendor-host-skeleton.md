# تحویل وظیفه — ISSUE-91-vendor-host-skeleton

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/91
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer skill
- **تاریخ:** 2026-10-25
- **وضعیت:** پیاده‌سازی اولیه روی شاخه؛ منتظر CI واقعی و بازبینی PR

## هدف و معیارهای پذیرش
- [x] ساخت Solution مستقل برای Vendor و Host حداقلی net10.0.
- [x] ساخت تست smoke برای endpoint زنده‌بودن و مسیر ناشناخته.
- [x] حفظ عدم وابستگی به Server مشتری و عدم تغییر Template.sln.
- [x] مستند استقرار ابری و سرور اختصاصی.
- [ ] Build/Test و CI روی commit نهایی موفق؛ هنوز شاهدی ثبت نشده.
- [ ] بررسی نهایی خروجی publish مشتری برای عدم بسته‌بندی Vendor.

## خلاصه تغییرات
- افزودن `Vendor.sln`، پروژهٔ `Vendor.Host` و تست‌های آن.
- افزودن `/health/live` با پاسخ حداقلی.
- ثبت معماری D-047 به‌عنوان Accepted پس از تأیید صریح کارفرما.
- هیچ دیتابیس، Identity، Migration یا KMS provider اضافه نشده است.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Vendor.Host.csproj` — Host مستقل net10.0.
- `Vendor/Vendor.Host/Program.cs` — لولهٔ وب و endpoint سلامت.
- `tests/Vendor.Host.Tests/*` — تست smoke.
- `Vendor.sln` — Solution مستقل.
- `Vendor/README.md` — راه‌اندازی و استقرار.
- `.github/workflows/dotnet.yml` — Build/Test مستقل Vendor.
- `docs/DECISIONS.md`, `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md` — تصمیم/وضعیت/تاریخچه.
- این handoff — گزارش تحویل.

## تصمیمات فنی
- Host مستقل و بدون ProjectReference به برنامهٔ مشتری.
- health endpoint فقط liveness است و عمداً دیتابیس/تنظیمات را گزارش نمی‌کند.
- ارائه‌دهندهٔ ابری، KMS/HSM، Identity و دیتابیس به مرحله‌های جدا واگذار شده‌اند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | اجرا نشده | در این مرحله ابزار اجرای محلی در دسترس نبود |
| `dotnet build Vendor.sln --no-restore --configuration Release` | شکست اولیه در CI | اجرای `40212637047` نشان داد xUnit v3 به `OutputType=Exe` نیاز دارد؛ در commit بعدی اصلاح شد و CI جدید لازم است |
| `dotnet test Vendor.sln --configuration Release` | اجرا نشده | منتظر CI |
| `git diff --check` | اجرا نشده | تغییرات از GitHub Contents API ایجاد شده‌اند |
| CI روی HEAD نهایی | در انتظار اجرای مجدد | شکست اولیه شناسایی و اصلاح شد؛ نتیجهٔ commit اصلاحی هنوز تأیید نشده |

## امنیت، داده و سازگاری
- دیتابیس مشتری و Migrationهایش تغییر نکرده‌اند.
- هیچ secret یا کلید خصوصی اضافه نشده است.
- این اسکلت Production-ready نیست؛ احراز هویت/مجوزدهی و کنترل‌های امنیتی قبل از هر مسیر مدیریتی لازم‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- تأیید کامپایل و APIهای تست توسط CI.
- افزودن گارد/بررسی خودکار برای عدم انتشار Vendor در بسته مشتری.
- مرحلهٔ بعد: تصمیم و پیاده‌سازی دیتابیس مستقل و ASP.NET Core Identity/MFA.
- پیش از Production: TLS/proxy, secret store, logging/alerting, backup/recovery, KMS/HSM runbook.

## شناسه‌های Git
- Branch: `feature/vendor-host-skeleton`
- Commit: پس از آخرین تغییر تعیین شود.
- Pull Request: پس از ثبت همه تغییرات ایجاد شود.
