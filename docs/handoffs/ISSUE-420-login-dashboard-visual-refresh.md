# تحویل وظیفه — ISSUE-420-login-dashboard-visual-refresh

- **Issue:** [#420](https://github.com/hamidreza1394/Accounting/issues/420)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-01
- **وضعیت:** پیاده‌سازی‌شده؛ CI و رندر واقعی مرورگر نیازمند تأیید

## هدف و معیارهای پذیرش
- [x] انتقال پنل معرفی سمت چپ ورود در دسکتاپ عریض RTL به اندازه ۳cm به راست، ۲cm باریک‌تر و ۲cm بالاتر.
- [x] بازطراحی محسوس رنگ‌های داشبورد /app/ با آبی روشن‌تر و سازمانی، سطوح هماهنگ، کارت‌های واضح‌تر و رنگ ماژول‌های هم‌خانواده با برند.
- [x] حفظ ساختار RTL، داده‌های معتبر WorkContext، empty stateها و غیرفعال‌بودن قابلیت‌های هنوز پیاده‌سازی‌نشده.
- [x] عدم تغییر احراز هویت، API، permission، dependency و schema/migration.
- [ ] اجرای تست‌ها و بررسی بصری مرورگر در viewportهای 360، 768، 1280 و دسکتاپ عریض.

## خلاصه تغییرات
- در taraz-login.css فقط پنل visual صفحه ورود در breakpoint دسکتاپ عریض RTL تغییر هندسه داده است؛ عرض ستون و محتوای برند متناسب شده و نمودار داخل محدوده ستون باقی می‌ماند.
- در ClientApp/src/styles/index.css پالت اصلی محیط کاری، هدر، سایدبار، welcome banner، کارت‌های خلاصه، پنل‌ها، کارت‌های ماژول، empty-state و quick-actionها به‌صورت محسوس تقویت شده‌اند.
- رنگ کاشی‌های زیرسیستم در Dashboard.tsx از بنفش/نیلی به طیف آبی/فیروزه‌ای/سبزآبی/طلایی تغییر کرد.
- regression testهای CSS ورود و رنگ کاشی‌های داشبورد اضافه شدند.

## فایل‌های مهم تغییرکرده
- Server/wwwroot/css/rtl/taraz-login.css
- tests/Template.Tests/LoginAuthenticationTests.cs
- ClientApp/src/styles/index.css
- ClientApp/src/components/Dashboard.tsx
- ClientApp/src/components/Dashboard.test.tsx
- docs/WORK_LOG.md
- docs/handoffs/ISSUE-420-login-dashboard-visual-refresh.md

## تصمیم‌های فنی
- اندازه‌های ۳cm/۲cm در CSS به واحدهای فیزیکی CSS نوشته شده‌اند (معادل نظری 113.4px و 75.6px در CSS px).
- تغییر هندسه به breakpoint min-width: 1440px و min-height: 820px محدود است تا تبلت و لپ‌تاپ‌های کوچک از هم‌پوشانی ستون‌ها آسیب نبینند.
- نمودار تزئینی به right: 0 محدود شد تا پس از انتقال پنل وارد فضای فرم نشود.
- هیچ تصمیم معماری جدیدی گرفته نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Regression test ورود | نوشته شد؛ اجرا نشده | CI لازم است |
| Regression test داشبورد | نوشته شد؛ اجرا نشده | CI لازم است |
| npm run typecheck | اجرا نشده | runner محلی در این نشست در دسترس نبود |
| npm test | اجرا نشده | runner محلی در این نشست در دسترس نبود |
| npm run build | اجرا نشده | runner محلی در این نشست در دسترس نبود |
| npm run check:offline | اجرا نشده | runner محلی در این نشست در دسترس نبود |
| dotnet test | اجرا نشده | CI لازم است |
| رندر مرورگر در 360/768/1280/1440+ | اجرا نشده | باید با مرورگر واقعی بررسی شود |
| git diff --check | اجرا نشده | تغییر از طریق GitHub API ثبت شده است |

## امنیت، داده و سازگاری
- منطق ورود، Cookie، Remember Me، anti-forgery، PageModel و JavaScript دست‌نخورده‌اند.
- هیچ عدد مالی یا رویداد ساختگی به داشبورد اضافه نشد.
- داده‌های شرکت/دوره و read-only از WorkContext سمت سرور باقی ماندند.
- هیچ dependency، CDN، دارایی بیرونی، API، permission، schema یا migration تغییر نکرد.

## ریسک‌ها و کارهای باقی‌مانده
- CSS absolute units در مرورگر بر مبنای CSS reference pixel تفسیر می‌شوند؛ اندازه فیزیکی نهایی به مقیاس/نمایشگر بستگی دارد. تأیید دیداری روی نمایشگر واقعی کاربر ضروری است.
- انتقال ۳cm در دسکتاپ عریض باید از نظر فاصله پنل برند با کارت ورود در رندر واقعی بررسی شود.
- تا سبزشدن CI و بررسی viewportها، وضعیت Verified نیست.

## شناسه‌های Git
- **Branch:** feat/issue-420-login-dashboard-visual-refresh
- **Commit:** در شاخه ثبت شده؛ head نهایی پس از PR ثبت می‌شود.
- **Pull Request:** در انتظار ایجاد