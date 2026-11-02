# تحویل وظیفه — ISSUE-427-light-luminous-dashboard

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/427
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-02
- **وضعیت:** پیاده‌سازی در شاخه؛ منتظر CI و بازبینی بصری واقعی

## هدف و معیارهای پذیرش
- [x] سایدبار روشن با زمینه سفید/آبی یخی، glow فیروزه‌ای/آبی و accent طلایی؛ متن و آیتم‌های غیرفعال خوانا.
- [x] هدر با طیف سرمه‌ای و درخشش‌های cyan/blue/gold برگرفته از CSS صفحه ورود.
- [x] بازطراحی کامل SVG پانوراما با برج میلاد بزرگ‌تر و دارای سرِ برج/سازه، خط آسمان تهران و کوه‌های البرز.
- [x] حفظ RTL، ساختار responsive، disabled navigation و کاهش حرکت سیستم.
- [x] بدون CDN، وابستگی، API، schema یا تغییر مجوز/احراز هویت.
- [ ] اجرای typecheck، Vitest، build و check:offline در CI.
- [ ] visual QA واقعی در 360/768/1280px و بررسی contrast/overlay.

## خلاصه تغییرات
- هدر به گرادیان سرمه‌ای برگرفته از پس‌زمینه صفحه ورود، با glowهای cyan/blue و accent طلایی تغییر کرد.
- سایدبار از تیره به روشن تغییر کرد: سفید، آبی یخی، نورهای فیروزه‌ای و طلایی، متن سرمه‌ای خوانا، آیتم فعال آبی و برچسب‌های «به‌زودی» با زمینه روشن.
- SVG پانورامای قبلی با تصویرسازی برداری جدید با نسبت پانورامای عریض ۷.۵:۱ (برای جلوگیری از بریده‌شدن آنتن و پایه برج در بنر) جایگزین شد: برج میلاد با آنتن، تاج/سازهٔ فوقانی، سکوی دید، شفت مخروطی و جزئیات سازه‌ای؛ کوه‌های البرز، برج‌ها و ساختمان‌های تهران، آسمان فیروزه‌ای و نور طلایی.
- overlay تیره بنر با لایه روشن جایگزین شد؛ متن تیتر سرمه‌ای و زیرعنوان آبی تیره است.
- تست‌های AppShell و Dashboard به variantهای بصری جدید حساس شدند.
- رنگ‌های UI در توکن‌های CSS تعریف شدند و از پالت taraz-login.css مشتق شده‌اند.

## فایل‌های مهم تغییرکرده
- ClientApp/src/components/AppShell.tsx
- ClientApp/src/components/AppShell.test.tsx
- ClientApp/src/components/Dashboard.tsx
- ClientApp/src/components/Dashboard.test.tsx
- ClientApp/src/styles/index.css
- Server/wwwroot/images/dashboard-tehran-panorama.svg
- docs/WORK_LOG.md
- docs/handoffs/ISSUE-427-light-luminous-dashboard.md

## تصمیم‌های فنی
- پالت از Server/wwwroot/css/rtl/taraz-login.css (cyan #5cebf6، sky #2fbbf5، blue #3590f5، gold #f4c96b، navy #050f2a) استخراج شد.
- سایدبار عمداً روشن است؛ هدر سرمه‌ای می‌ماند تا کنتراست، سلسله‌مراتب و پیوند بصری با صفحه ورود حفظ شود.
- تصویرسازی SVG به‌صورت محلی و مستقل از شبکه است؛ هیچ تصویر خارجی یا کتابخانه جدیدی اضافه نشده است.
- انتخاب شرکت/دوره، داده‌های Session و WorkContext، مسیرهای API و کنترل مجوز دست‌نخورده‌اند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Vitest | در انتظار CI | تست‌های variant به‌روزرسانی شدند؛ اجرای محلی در این نشست در دسترس نیست |
| npm run typecheck | در انتظار CI | |
| npm run build | در انتظار CI | |
| npm run check:offline | در انتظار CI | |
| SVG XML و رندر مرورگر | اجرا نشده | visual QA نهایی باید در محیط مرورگر انجام شود |
| git diff --check | اجرا نشده | تغییرات از GitHub Contents API ثبت شده‌اند، نه checkout محلی |

## امنیت، داده و سازگاری
- تغییرات فقط presentation layer هستند؛ هیچ API، authentication، authorization، tenant context، fiscal period، DB یا migration تغییر نکرده است.
- navigation فاقد route همچنان disabled است.
- تمام assets همچنان local هستند؛ حرکت‌های تعاملی تابع prefers-reduced-motion هستند.

## ریسک‌ها و کارهای باقی‌مانده
- باید خروجی واقعی در مرورگر بررسی شود؛ به‌ویژه خوانایی متن روی تصویر، رفتار mobile crop، و ترتیب لایه‌های SVG.
- تا سبز شدن CI و انجام visual QA، PR برای merge آماده تلقی نشود.

## شناسه‌های Git
- Branch: feat/issue-427-light-luminous-dashboard
- Commit: در شاخه ثبت شده؛ PR پس از ثبت گزارش ساخته می‌شود.
- Pull Request: در انتظار ایجاد
