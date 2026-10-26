# تحویل وظیفه — ISSUE-126-vendor-ui-shell

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/126
- **Agent/مسئول:** ChatGPT / GitHub integration
- **تاریخ:** 2026-10-26
- **وضعیت:** پیاده‌سازی اولیه در شاخه؛ CI اجرا نشده؛ نیازمند بازبینی

## هدف و معیارهای پذیرش
- [x] پروژه UI مستقل از ClientApp با lockfile جدا
- [x] RTL فارسی، صفحه ورود و shell واکنش‌گرا
- [x] اتصال صفحه ورود به endpointهای واقعی password/TOTP و ضدجعل درخواست
- [x] Host برای static assets و SPA routes و جلوگیری از fallback ناشناخته API
- [x] Vendor CI برای build/test UI و بسته‌بندی assetها به‌روزرسانی شد
- [ ] CI واقعی سبز و مرور بصری/مرورگری روی HTTPS
- [ ] اثبات نهایی جداسازی بسته مشتری با CIهای اصلی

## خلاصه تغییرات
- Vendor/Vendor.Web: React/TypeScript strict/Vite/Tailwind، شناسنامه shadcn، Button/Card source-owned، ورود MFA و پوسته مدیریتی اولیه.
- Vendor/Vendor.Host/Program.cs: سرویس‌دهی فایل‌های محلی و fallback برای مسیرهای UI؛ مسیرهای API/health ناشناخته همچنان 404 می‌مانند.
- .github/workflows/vendor-ci.yml: npm ci/typecheck/test/build/audit و بررسی وجود فایل‌های bundle در خروجی publish.
- Vendor/README.md، docs/PROJECT_STATUS.md، docs/WORK_LOG.md: مستندسازی وضعیت واقعی و محدودیت‌ها.

## تصمیم‌های فنی
- UI فروشنده یک پروژه مستقل با dependency graph و lockfile جداست؛ هیچ ارجاعی از UI مشتری ایجاد نشده است.
- صفحه ورود از APIهای موجود استفاده می‌کند: /api/auth/antiforgery، /api/auth/password، /api/auth/totp/verify، /api/auth/me، /api/auth/logout.
- CSRF token فقط در حافظه است؛ نشست با Cookie هم‌مبدأ مدیریت می‌شود؛ هیچ credential یا token در Web Storage ذخیره نمی‌شود.
- صفحات تجاری فعلاً placeholder هستند؛ هیچ API یا داده جعلی برای مشتری/نصب/لایسنس ساخته نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| npm ci | اجرا نشده | قرار است در Vendor CI اجرا شود |
| npm run typecheck | اجرا نشده | نیازمند CI |
| npm test | اجرا نشده | تست‌های UI اضافه شده‌اند؛ نتیجه هنوز نامعلوم است |
| npm run build | اجرا نشده | خروجی publish هنوز با CI تأیید نشده است |
| dotnet build Vendor.sln | اجرا نشده | نیازمند Vendor CI |
| dotnet test Vendor.sln | اجرا نشده | نیازمند Vendor CI/SQL Server integration |

## امنیت، داده و سازگاری
- APIهای احراز هویت هم‌مبدأ و ضدجعل درخواست هستند؛ APIها همچنان authorization سمت سرور دارند.
- فایل‌های استاتیک UI عمومی‌اند و فقط کد رابط را شامل می‌شوند؛ endpointهای محافظت‌شده همچنان از policy سرور تبعیت می‌کنند.
- هیچ تغییر دیتابیس/Migration مشتری انجام نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- CI ممکن است خطاهای TypeScript یا npm را نشان دهد؛ تا اجرای واقعی، صحت build تأیید نشده است.
- باید UI را در HTTPS واقعی بررسی کرد؛ کوکی‌های __Host- به HTTPS نیاز دارند.
- bootstrap نخستین مدیر، RBAC کامل، audit checkpoint مستقل و عملیات تجاری هنوز آماده نیستند؛ این پنل Production-ready نیست.

## شناسه‌های Git
- Branch: feat/issue-126-vendor-ui-shell
- آخرین commit شاخه در زمان تحویل: 952c025ca21fb5b30fc4e1531598cfaece26b288.
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/127 (Draft; CI در انتظار اجرا/تکمیل).
