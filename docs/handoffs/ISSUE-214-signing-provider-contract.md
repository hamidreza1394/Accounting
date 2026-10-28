# تحویل وظیفه — ISSUE-214-signing-provider-contract

- **Issue:** [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- **Agent/مسئول:** ChatGPT / GitHub workflow
- **تاریخ:** 2026-10-28
- **وضعیت:** پیاده‌سازی قرارداد اولیه؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- [x] تعریف مرز provider-neutral برای امضای payload canonical، بدون انتخاب KMS/HSM خاص.
- [x] محدودکردن نتیجهٔ موفق به الگوریتم پذیرفته‌شده و قالب امضای ثابت‌طول قرارداد v1.
- [x] تست با fake/test-only signer و verifier موجود؛ کلید موقت فقط در تست.
- [x] مستندسازی وضعیت واقعی و محدودیت‌های تولید.
- [ ] اتصال به workflow صدور و ذخیره‌سازی پایدار envelope — خارج از این slice و هنوز انجام نشده.
- [ ] provider عملیاتی KMS/HSM و تست عملیاتی — وابسته به تصمیم استقرار و credential امن.

## خلاصه تغییرات
- افزودن `ILicenseSigningProvider` و `LicenseSigningResult` در `Vendor/Vendor.Host/Signing/ILicenseSigningProvider.cs`.
- تست امضای بایت‌های دقیق payload و رد payload تغییرکرده در `tests/Vendor.Host.Tests/LicenseSigningProviderContractTests.cs`.
- به‌روزرسانی `docs/PROJECT_STATUS.md`, `Vendor/README.md` و `docs/LICENSING_THREAT_MODEL.md`.
- Issue #214 دوباره باز شد چون ادغام PR #217 به‌صورت خودکار آن را بسته بود، درحالی‌که معیار پذیرش می‌گوید تا انتخاب و آزمون signer عملیاتی باز بماند.

## تصمیم‌های فنی
- قرارداد در Host Vendor تعریف شده و از SDK خاص ارائه‌دهنده مستقل است.
- provider باید بایت‌های ورودی را دقیقاً امضا کند؛ تغییر serialization ممنوع است.
- هیچ provider پیش‌فرض نرم‌افزاری، private key، endpoint فعال صدور، storage یا Migration جدیدی اضافه نشده است. مسیر صدور فعلی همچنان fail-closed باقی می‌ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی `dotnet test` | اجرا نشده | در این نشست ابزار اجرای مخزن محلی در دسترس نیست؛ CI لازم است. |
| Vendor CI | در انتظار | باید روی commit نهایی شاخه بررسی شود. |
| .NET CI | در انتظار | باید روی commit نهایی شاخه بررسی شود. |
| بازبینی diff | نیازمند بازبینی نهایی | پس از اتمام مستندات و پیش از PR بررسی شود. |

## امنیت، داده و سازگاری
- هیچ کلید خصوصی یا credential ایجاد/ثبت نشده است.
- هیچ Migration یا تغییری در `Server/**` یا دیتابیس مشتری وجود ندارد.
- این قرارداد به‌تنهایی صدور/ابطال لایسنس را فعال نمی‌کند و Production-ready نیست.

## ریسک‌ها و کارهای باقی‌مانده
- باید در یک slice جداگانه سرویس application برای ساخت payload، فراخوانی signer، اعتبارسنجی نتیجه و ذخیره envelope به‌شکل تراکنشی پیاده شود.
- انتخاب KMS/HSM یا سرویس امضای مورد تأیید و تنظیم credential خارج از repo ضروری است.
- آزمون‌های integration SQL Server برای rollback، رقابت RowVersion، self-approval و وضعیت‌های stale باید همراه wiring واقعی کامل شوند.

## شناسه‌های Git
- Branch: `feat/vendor-signing-provider-contract`
- Commits: see branch history
- Pull Request: pending
