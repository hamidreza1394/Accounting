# تحویل وظیفه — ISSUE-492-customer-boundary-authz-tests

- **Issue:** #492 (زیرمجموعه #444)
- **مسئول:** ChatGPT — Security regression tests
- **تاریخ:** 2026-11-03
- **وضعیت:** تست‌ها اضافه شدند؛ Verified منوط به CI سبز

## یافته
دو endpoint عملیاتی در `CustomerBoundaryEndpoints` وجود دارند:
- `POST /api/v1/operator/customer-boundaries/assign`
- `GET /api/v1/operator/customer-boundaries/diagnostics`

Handlerها حساب‌های غیر-Infrastructure را با 404 رد می‌کنند. پیش از این فایل تست اختصاصی برای اثبات این مرز امنیتی در Template.Tests وجود نداشت.

## تغییرات
- `tests/Template.Tests/CustomerBoundaryEndpointTests.cs`
  - Company user برای diagnostics و assignment پاسخ 404 می‌گیرد.
  - assignment غیرمجاز قبل از Anti-forgery یا mutation متوقف می‌شود.
  - diagnostics فقط برای Infrastructure قابل دسترسی است و اطلاعات نام/شناسه ملی شرکت را افشا نمی‌کند؛ شناسه‌ها فقط وقتی رابطهٔ حل‌نشده وجود دارد در allowlist خروجی قرار می‌گیرند.
  - درخواست Infrastructure بدون Anti-forgery با 400 رد و رخداد rejection audit می‌شود.
  - تخصیص معتبر موفق و audit می‌شود.
  - تخصیص مجدد شرکت به boundary متفاوت با 409 رد می‌شود و مقدار قبلی حفظ می‌شود.

## امنیت و سازگاری
- هیچ رفتار Production، API contract، schema، migration یا UI تغییر نکرد.
- تست‌ها از InMemory provider استفاده می‌کنند؛ هیچ query خاص provider یا migration در این برش تغییر نکرده است.
- تست‌های SQL Server و full CI باید پیش از Verified شدن موفق باشند.

## آزمون
| مورد | وضعیت |
|---|---|
| اجرای محلی | در این محیط اجرا نشده؛ GitHub clone/DNS در دسترس نیست |
| CI | اجرای اولیه شکست کامپایل تست را نشان داد؛ اصلاح انجام و اجرای مجدد در انتظار است |
| Merge | انجام نشده؛ نیازمند تأیید Maintainer |

## جزئیات اصلاح CI
- unionهای strongly-typed از `Results<...>` به `IStatusCodeHttpResult` cast نمی‌شوند؛ تست اکنون نتیجه را با `IResult.ExecuteAsync` روی `DefaultHttpContext` اجرا می‌کند و status واقعی HTTP را بررسی می‌کند.
- پاسخ diagnostics به JSON واقعی serialize می‌شود و propertyهای allowlisted بررسی می‌شوند.

## فایل‌ها
- `tests/Template.Tests/CustomerBoundaryEndpointTests.cs`
- `docs/WORK_LOG.md`
- `docs/INDEX.md`
- این handoff
