# تحویل وظیفه — ISSUE-126-vendor-ui-shell

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/126
- **Agent/مسئول:** ChatGPT / GitHub integration
- **تاریخ:** 2026-10-26
- **وضعیت:** PR #127 در 2026-10-26 ادغام شد. این handoff تاریخی است؛ پیگیری احراز هویت/CI در Issue #128 انجام می‌شود.

## هدف و معیارهای پذیرش
- [x] پروژه UI مستقل از ClientApp با lockfile جدا
- [x] RTL فارسی، صفحه ورود و shell واکنش‌گرا
- [x] اتصال صفحه ورود به endpointهای واقعی password/TOTP و ضدجعل درخواست
- [x] Host برای static assets و SPA routes و جلوگیری از fallback ناشناخته API
- [x] Vendor CI برای build/test UI و بسته‌بندی assetها به‌روزرسانی شد
- [x] PR #127 ادغام شد؛ [ ] CI اصلاحات بعدی و مرور بصری/مرورگری روی HTTPS هنوز لازم است
- [ ] اثبات نهایی جداسازی بسته مشتری با CIهای اصلی

## خلاصه تغییرات
- Vendor/Vendor.Web: React/TypeScript strict/Vite/Tailwind، شناسنامه shadcn، Button/Card source-owned، ورود MFA و پوسته مدیریتی اولیه.
- Vendor/Vendor.Host/Program.cs: سرویس‌دهی فایل‌های محلی و fallback برای مسیرهای UI؛ مسیرهای API/health ناشناخته همچنان 404 می‌مانند. فایل‌های دارای hash در `/assets` یک سال `immutable` cache می‌شوند و `index.html` همچنان `no-store` است.
- .github/workflows/vendor-ci.yml: npm ci/typecheck/test/build/audit، بررسی bundle در publish و بودجهٔ gzip (JS حداکثر 100 KiB، CSS حداکثر 20 KiB). اندازهٔ build قبلی: JS 90.03 KiB gzip، CSS 7.29 KiB gzip؛ گارد جدید هنوز در اجرای تازه تأیید نشده است.
- Vendor/README.md، docs/PROJECT_STATUS.md، docs/WORK_LOG.md: مستندسازی وضعیت واقعی و محدودیت‌ها.

## تصمیم‌های فنی
- UI فروشنده یک پروژه مستقل با dependency graph و lockfile جداست؛ هیچ ارجاعی از UI مشتری ایجاد نشده است.
- صفحه ورود از APIهای موجود استفاده می‌کند: /api/auth/antiforgery، /api/auth/password، /api/auth/totp/verify، /api/auth/me، /api/auth/logout.
- CSRF token فقط در حافظه است؛ نشست با Cookie هم‌مبدأ مدیریت می‌شود؛ هیچ credential یا token در Web Storage ذخیره نمی‌شود.
- صفحات تجاری فعلاً placeholder هستند؛ هیچ API یا داده جعلی برای مشتری/نصب/لایسنس ساخته نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| npm ci | موفق در run 40277399614 | وابستگی‌ها نصب شدند |
| npm run typecheck | موفق در run 40277399614 | TypeScript strict |
| npm test | موفق در run 40277399614 | تست‌های UI سبز شدند |
| npm run build | موفق در run 40277399614 | خروجی Vite ساخته شد؛ run 40277588682 اندازهٔ JS برابر 90.03 KiB gzip و CSS برابر 7.29 KiB gzip گزارش کرد |
| dotnet build Vendor.sln | ناموفق در run 40277399614 | usingهای لازم در Program.cs مفقود بودند؛ در commit 3d317ea اصلاح شد، اجرای مجدد در صف است |
| dotnet test Vendor.sln | شکست در run 40277588682 | ۷ تست به علت نبود `FARANO_VENDOR_ConnectionStrings__VendorDb` در محیط تست Host شکست خوردند؛ workflow اکنون آن را از SQL Server تست تنظیم می‌کند و نیاز به اجرای مجدد دارد |
| Asset gzip budget | افزوده شد، منتظر CI | JS حداکثر 100 KiB و CSS حداکثر 20 KiB؛ اندازهٔ baseline زیر سقف است |

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
- آخرین commit شاخه در زمان ثبت اولیه: 952c025ca21fb5b30fc4e1531598cfaece26b288؛ اصلاحات جدید منتظر CI هستند.
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/127 (Merged). پیگیری: https://github.com/hamidreza1394/Accounting/issues/128.
