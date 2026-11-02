# تحویل وظیفه — ISSUE-425-dashboard-vibrant-visuals

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/425
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-02
- **وضعیت:** پیاده‌سازی اولیه؛ اعتبارسنجی CI/مرورگر در انتظار

## هدف و معیارهای پذیرش
- [x] حذف نام کاربر از زیرعنوان خوشامدگویی و نمایش متن ثابت مصوب.
- [x] اصلاح پالت هدر، سایدبار، بنر و سطوح داشبورد با سرمه‌ای-سبزآبی، فیروزه‌ای و طلایی محدود.
- [x] حفظ RTL، آیتم‌های غیرفعال و ساختار responsive موجود.
- [x] حفظ دارایی تصویر محلی و عدم افزودن وابستگی خارجی.
- [ ] اجرای typecheck، Vitest، build و check:offline.
- [ ] بررسی رندر واقعی در عرض‌های 360/768/1280 و حالت reduced-motion.

## خلاصه تغییرات
- بنر خوشامدگویی با گرادیان‌های زنده‌تر و یک درخشش حرکتی بسیار ظریف (فقط در صورت عدم فعال بودن reduced-motion) به‌روزرسانی شد.
- زیرعنوان دقیقاً «مدیریت هوشمند کسب‌وکار با دقت، شفافیت و امنیت بیشتر.» شد و دیگر نام کاربر را در خود ندارد.
- هدر و سایدبار از آبی اشباع به سرمه‌ای-سبزآبی عمیق با glowهای ظریف، سطح فعال فیروزه‌ای و accent طلایی تغییر کردند.
- SVG محلی تهران با آسمان فیروزه‌ای، نور گرم و رنگ شهری هماهنگ‌تر شد.
- تست regression برای متن ثابت بنر و نبود نام کاربر در همان بنر اضافه شد.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/components/Dashboard.tsx`
- `ClientApp/src/components/Dashboard.test.tsx`
- `ClientApp/src/styles/index.css`
- `Server/wwwroot/images/dashboard-tehran-panorama.svg`
- `docs/WORK_LOG.md`
- `docs/handoffs/ISSUE-425-dashboard-vibrant-visuals.md`

## تصمیم‌های فنی
- بدون dependency، API، permission، authentication یا schema change.
- تصویر موجود و محلی حفظ شد؛ منبع جدید خارجی یا عکس پروفایل ساختگی افزوده نشد.
- انیمیشن صرفاً تزئینی، کم‌دامنه و تابع prefers-reduced-motion است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `npm run typecheck` | اجرا نشده | در این نشست محیط اجرای repo در دسترس نیست؛ CI باید اجرا کند |
| `npm test` | اجرا نشده | تست regression اضافه شده است |
| `npm run build` | اجرا نشده | نیازمند CI |
| `npm run check:offline` | اجرا نشده | نیازمند build و CI |
| مرورگر 360/768/1280 | اجرا نشده | نیازمند browser E2E/visual QA |

## امنیت، داده و سازگاری
- فقط UI تغییر کرده است؛ داده‌های Session و WorkContext بدون تغییر باقی مانده‌اند.
- نام کاربر همچنان در محل هویت کاربر و اطلاعات نشست قابل نمایش است؛ فقط از زیرعنوان بنر حذف شده است.
- ناوبری فاقد route/permission همچنان غیرفعال می‌ماند.

## ریسک‌ها و کارهای باقی‌مانده
- رنگ‌ها و کنتراست باید در رندر واقعی مرورگر بازبینی شوند.
- اجرای CI و اصلاح هر خطای typecheck/test/build/check:offline قبل از merge لازم است.

## شناسه‌های Git
- Branch: `feat/issue-425-dashboard-vibrant-visuals`
- Commit: در PR ثبت می‌شود
- Pull Request: پس از ثبت تغییرات
