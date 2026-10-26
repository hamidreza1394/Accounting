# استاندارد رابط کاربری فرانو — shadcn/ui

## وضعیت

- تصمیم معماری: پذیرفته‌شده برای رابط React جدید در `ClientApp`.
- وضعیت پیاده‌سازی: CLI رسمی `shadcn@4.22.1` در `devDependencies` پین شده؛ `Button` و `Card` از CLI تولید و در مخزن نگهداری می‌شوند؛ وابستگی‌ها و `package-lock.json` با npm هماهنگ‌اند. این تغییر از Button در retry خطا و از Card در پنل اطلاعات شرکت/دوره مالی استفاده می‌کند. CI نهایی این تغییر باید همه تست‌ها را دوباره اجرا کند.
- مبنای primitives: Base UI (`@base-ui/react`) با سبک `base-nova`؛ RTL در `components.json` فعال است.
- تابع ادغام کلاس‌ها از بستهٔ رسمی `cn` استفاده می‌کند (`src/lib/utils.ts` آن را re-export می‌کند). بسته‌های بلااستفاده `clsx` و `tailwind-merge` نگه داشته نشده‌اند.
- دامنه: فقط کلاینت React؛ صفحات Razor قدیمی را بدون نیازمندی مستقل بازنویسی نکنید.

## اصول انتخاب‌شده

1. **Source-owned components:** کامپوننت‌های shadcn/ui باید در مخزن و زیر `ClientApp/src/components/ui` نگهداری شوند؛ این پروژه یک UI runtime خارجی یا hosted service نیست.
2. **پشته موجود را ارتقا ندهید صرفاً برای shadcn:** مبنا React 19، TypeScript strict، Vite و Tailwind CSS v4 است. نسخه‌های موجود را حفظ کنید مگر CI/سازگاری رسمی دلیلی برای تغییر نشان دهد.
3. **RTL فارسی از ابتدا:** پیکربندی shadcn باید RTL را فعال کند؛ از کلاس‌های منطقی CSS/Tailwind، جهت صحیح برای portalها و اصلاح جهت آیکون‌های جهت‌دار استفاده کنید. وجود `dir="rtl"` روی سند به‌تنهایی همهٔ popover/dialog/tooltipها را تضمین نمی‌کند.
4. **Offline-first:** در runtime هیچ CSS، JavaScript، فونت یا آیکونی از CDN یا اینترنت بارگیری نشود. کد کامپوننت‌ها و وابستگی‌های runtime باید در npm lockfile ثبت و در build bundle شوند.
5. **استفاده هدفمند:** فقط کامپوننت‌های موردنیاز را اضافه کنید؛ از `add --all` و تغییر گستردهٔ ظاهر بدون نیازمندی خودداری کنید. UI موجود را یک‌باره بازنویسی نکنید.
6. **طراحی مالی حرفه‌ای:** رنگ‌ها و توکن‌های طراحی باید با هویت سرمه‌ای/طلایی فرانو، خوانایی اعداد مالی، وضعیت‌های هشدار/خطا و تراکم اطلاعات ERP سازگار باشند. رنگ به‌تنهایی نباید وضعیت را منتقل کند.
7. **دسترس‌پذیری:** صفحه‌کلید، focus قابل مشاهده، labelهای واقعی، نام accessible، کنتراست و رفتار dialog/menu/combobox باید بررسی شوند.
8. **امنیت:** کامپوننت UI جایگزین authorization سمت سرور، validation، anti-forgery یا کنترل tenant نیست. اطلاعات محرمانه در DOM، log یا storage مرورگر ذخیره نشود.

## مسیر و قراردادهای پیشنهادی

- پیکربندی: `ClientApp/components.json`
- کامپوننت‌های تولیدشده/سفارشی: `ClientApp/src/components/ui/`
- توابع کمکی کلاس‌ها: `ClientApp/src/lib/utils.ts` (re-export از بستهٔ `cn`)
- استایل اصلی: `ClientApp/src/styles/index.css`
- تست‌ها: Vitest + Testing Library؛ تست تعاملی برای کامپوننت‌های مهم و تست RTL برای رفتارهای جهت‌دار.
- آیکون‌ها: مجموعهٔ محلی بسته‌بندی‌شده؛ استفاده از URL خارجی ممنوع.
- نسخه‌ها: تغییر `package.json` و `package-lock.json` باید با هم و با `npm ci` معتبر انجام شود.

## فرآیند افزودن کامپوننت

1. مشخص کنید کدام صفحه/جریان واقعاً به کامپوننت نیاز دارد.
2. نسخهٔ CLI و peer dependencyها را از مستندات رسمی و package metadata بررسی کنید؛ نسخهٔ preview را وارد Production نکنید.
3. CLI پین‌شده را با `npx shadcn add <component> --yes` اجرا کنید؛ فقط کامپوننت‌های لازم را اضافه کنید و diff تولیدشده را بازبینی کنید.
4. `npm ci`، `npm run typecheck`، `npm test`، `npm run build` و `npm run check:offline` را اجرا کنید.
5. رفتار RTL، صفحه‌کلید، responsive، ظاهر فارسی و نبود درخواست خارجی را بررسی کنید.
6. در صورت تغییر توکن‌ها یا قراردادها همین سند و `docs/DECISIONS.md` را به‌روز کنید.

## منابع رسمی

- [نصب برای Vite و پروژهٔ موجود](https://ui.shadcn.com/docs/installation/vite)
- [پشتیبانی RTL](https://ui.shadcn.com/docs/rtl)
- [راهنمای Tailwind v4](https://ui.shadcn.com/docs/tailwind-v4)

نسخهٔ CLI در `package.json` پین شده تا تولید کامپوننت‌ها در توسعه تکرارپذیر باشد. برای به‌روزرسانی، نسخه را آگاهانه تغییر دهید و CI را اجرا کنید.
