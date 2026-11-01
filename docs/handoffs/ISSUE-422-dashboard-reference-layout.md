# تحویل وظیفه — ISSUE-422-dashboard-reference-layout

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/422
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-01
- **وضعیت:** پیاده‌سازی در شاخه انجام شد؛ اعتبارسنجی CI/مرورگر در انتظار

## هدف و معیارهای پذیرش
- [x] بازچینی هدر بر اساس مرجع: برند، انتخاب شرکت با آیکون ساختمان، انتخاب دوره با آیکون تقویم، کاربر، اعلان و خروج.
- [x] سایدبار راست با پهنای دسکتاپ 238px، زمینه سرمه‌ای تیره و حالت فعال/غیرفعال روشن.
- [x] بنر پانورامیک عریض با تصویر محلی تهران/البرز/برج میلاد.
- [x] کارت‌های سفید و پنل‌های روشن با پالت آبی/فیروزه‌ای و ساختار نزدیک تصویر.
- [x] حفظ اطلاعات واقعی WorkContext و Session؛ عدم افزودن تصویر پروفایل یا آمار ساختگی.
- [x] حفظ ماژول‌های توسعه‌نیافته به‌صورت غیرفعال و برچسب‌دار.
- [x] دارایی تصویر داخل ClientApp؛ بدون CDN/hotlink خارجی.
- [ ] typecheck، Vitest، build و check:offline از CI.
- [ ] بازبینی E2E و viewport واقعی در CI/مرورگر.

## خلاصه تغییرات
- هدر و سایدبار با نسبت‌های نزدیک تصویر مرجع بازچینی شدند؛ در موبایل سایدبار به drawer تبدیل می‌شود.
- چون قرارداد Session فعلی فقط username/fullName/accountKind/isAdministrator دارد و تصویر پروفایل ندارد، به‌جای تصویر ساختگی، نشان اولیه نام با استایل avatar استفاده شد.
- داده‌های شرکت، دوره و نقش فقط از Session/WorkContext موجود می‌آیند؛ هیچ KPI یا مقدار مالی جعلی اضافه نشد.
- بنر از SVG محلی استفاده می‌کند؛ این artwork برداری سفارشی است، نه عکس عکاسی واقعی. در صورت نیاز به عکس واقعی دارای مجوز، مالک باید asset تصویری را تأمین/تأیید کند.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/components/AppShell.tsx` — هدر و سایدبار.
- `ClientApp/src/components/Dashboard.tsx` — بنر و سلسله‌مراتب محتوای داشبورد.
- `ClientApp/src/styles/index.css` — اندازه‌ها، رنگ‌ها، responsive و reduced-motion.
- `ClientApp/public/images/dashboard-tehran-panorama.svg` — تصویر محلی پانوراما.
- `ClientApp/src/components/AppShell.test.tsx` — تست ساختار هدر/ناوبری.
- `ClientApp/src/components/Dashboard.test.tsx` — تست تصویر محلی، ساختار و عدم جعل داده.
- `docs/handoffs/ISSUE-422-dashboard-reference-layout.md` و `docs/WORK_LOG.md`.

## تصمیم‌های فنی
- وابستگی جدیدی اضافه نشد؛ از Tailwind و lucide-react موجود استفاده شد.
- قرارداد API/Session گسترش داده نشد؛ عکس پروفایل تا زمان وجود API واقعی نمایش داده نمی‌شود.
- هیچ API، مجوز، احراز هویت، schema یا migration تغییر نکرد.
- همه دارایی‌ها محلی‌اند؛ SVG از public path کلاینت سرو می‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `npm run typecheck` | اجرا نشده | در این نشست checkout/runtime محلی در دسترس نیست؛ CI لازم است |
| `npm test` | اجرا نشده | تست‌های regression افزوده شدند؛ CI لازم است |
| `npm run build` | اجرا نشده | CI لازم است |
| `npm run check:offline` | اجرا نشده | CI لازم است |
| `git diff --check` | اجرا نشده | تغییرات از GitHub API ثبت شدند؛ CI/بازبینی diff لازم است |
| مرورگر و responsive | اجرا نشده | CI و مرورگر واقعی لازم است |

## امنیت، داده و سازگاری
- انتخاب شرکت/دوره همچنان callback قبلی را اجرا می‌کند؛ مرجع معتبر همچنان WorkContext سرور است.
- زیرسیستم‌های فاقد route/مجوز واقعی disabled باقی ماندند.
- بدون تغییر API، Session contract، database و migration.
- اعلان‌ها فقط آیکون اطلاع‌رسانی‌اند؛ badge/count جعلی نمایش داده نمی‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- avatar عکس واقعی در قرارداد فعلی Session وجود ندارد؛ فعلاً fallback حرف اول نام است.
- تصویر پانوراما SVG هنری است، نه عکس عکاسی؛ برای انطباق صددرصد با مرجع تصویری، عکس مجاز محلی باید بعداً جایگزین شود.
- اجرای CI و مرورگر لازم است تا این تغییر Verified تلقی شود.

## شناسه‌های Git
- Branch: `feat/issue-422-dashboard-reference-layout`
- Commit: در PR ثبت می‌شود.
- Pull Request: پس از ثبت تغییرات ساخته می‌شود.
