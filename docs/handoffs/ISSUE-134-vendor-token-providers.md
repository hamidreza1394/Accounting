# تحویل وظیفه — ISSUE-134-vendor-token-providers

- **Issue:** [#134](https://github.com/hamidreza1394/Accounting/issues/134)
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer skill
- **تاریخ:** 2026-10-26
- **وضعیت:** اصلاح در شاخه؛ منتظر CI

## هدف و معیارهای پذیرش
- [x] رفع محل نامعتبر فراخوانی `AddDefaultTokenProviders()` در Persistence class library.
- [x] حفظ ثبت واحد IdentityCore/EF stores و حفظ token providers.
- [ ] Vendor Release build موفق شود.
- [ ] تست Migration روی SQL Server و تست‌های احراز هویت/TOTP موفق شوند.
- [ ] Publish و بررسی artifact موفق شوند.

## خلاصه تغییرات
- ثبت `AddDefaultTokenProviders()` از Persistence به ASP.NET Core Host منتقل شد.
- ثبت دوبارهٔ `AddIdentityCore` اضافه نشد و token providers حذف نشدند.
- مستندات وضعیت و گزارش کار به‌روز شدند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Persistence/DependencyInjection.cs` — حذف extension فراخوانی‌ناپذیر از پروژهٔ Persistence.
- `Vendor/Vendor.Host/Program.cs` — ثبت token providers در Host.
- `docs/PROJECT_STATUS.md` — ثبت خطای واقعی CI و وضعیت اصلاح.
- `docs/WORK_LOG.md` — گزارش علت و اصلاح.
- `docs/handoffs/ISSUE-134-vendor-token-providers.md` — این تحویل.

## تصمیم‌های فنی
- Token providers در Host ثبت می‌شوند چون این لایه ASP.NET Core Web SDK و سطح API مربوط را در اختیار دارد؛ Persistence فقط تنظیمات هویت، نقش‌ها و storeها را ثبت می‌کند.
- هیچ Schema/Migration یا دیتابیس مشتری تغییر نکرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor CI run 40287063856 | ناموفق | Build خطای نبودن `IdentityBuilder.AddDefaultTokenProviders()` در Persistence داد. |
| Frontend typecheck/tests/build/budget در همان run | موفق | گام‌های فرانت‌اند پیش از Build .NET سبز بودند. |
| Build پس از اصلاح | اجرا نشده | نیازمند Vendor CI جدید |
| تست‌های SQL Server/TOTP پس از اصلاح | اجرا نشده | در run شکست‌خورده به علت Build رد شدند |

## امنیت، داده و سازگاری
- token providers حذف نشده‌اند؛ تست‌های TOTP/auth باید پس از Build مجدداً اجرا شوند.
- هیچ Migration یا دیتابیس حسابداری مشتری لمس نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- هنوز مشخص نیست این اصلاح همهٔ CI را سبز می‌کند؛ نتیجه باید از اجرای تازه دریافت شود.
- Production readiness پنل Vendor به این اصلاح محدود نیست؛ bootstrap امن، RBAC، audit checkpoint و APIهای تجاری هنوز جداگانه لازم‌اند.

## شناسه‌های Git
- Branch: `fix/vendor-identity-token-providers`
- Commit: در تاریخچهٔ شاخه ثبت می‌شود.
- Pull Request: پس از ثبت تغییرات
