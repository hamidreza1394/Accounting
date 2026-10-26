# ClientApp — رابط کاربری React فرانو

رابط کاربری جدید فرانو با React، TypeScript (strict)، Vite و Tailwind CSS. خروجی Build در `Server/wwwroot/app` قرار می‌گیرد و سرور ASP.NET Core آن را در مسیر `/app` و هم‌مبدأ با API و صفحه ورود Razor سرو می‌کند.

## استاندارد کامپوننت‌های UI

کامپوننت‌های جدید React بر اساس shadcn/ui طراحی می‌شوند. CLI نسخهٔ `4.22.1` در `devDependencies` پین شده؛ کامپوننت‌های `src/components/ui/button.tsx` و `card.tsx` از CLI رسمی تولید شده‌اند و وابستگی‌ها در `package-lock.json` قفل شده‌اند. در `Feedback.tsx` از Button برای retry و در `CompanyOverviewPanel.tsx` از Card برای پنل‌ها استفاده شده است. جزئیات RTL، دسترس‌پذیری و runtime آفلاین در [docs/UI_COMPONENTS.md](../docs/UI_COMPONENTS.md) آمده است.

برای افزودن کامپوننت، از `npx shadcn add <component> --yes` استفاده کنید، diff تولیدشده را بررسی کنید و سپس `npm ci`, `npm run typecheck`, `npm test`, `npm run build` و `npm run check:offline` را اجرا کنید. نسخهٔ CLI را بی‌دلیل به `latest` تغییر ندهید.

## پیش‌نیاز
- Node.js 22.12 یا بالاتر (CI از Node 24 و npm 11 استفاده می‌کند؛ `package-lock.json` با npm 11 ساخته شده است).

## دستورات
```bash
cd ClientApp
npm ci                 # نصب دقیق از روی lockfile
npm run dev            # Vite روی http://localhost:5173/app/ ؛ /api و /Account به سرور پروکسی می‌شوند
npm run typecheck      # بررسی TypeScript
npm test               # تست‌های Vitest + Testing Library
npm run build          # typecheck + build در Server/wwwroot/app
npm run check:offline  # اطمینان از نبود منبع خارجی در خروجی و وجود همه منابع محلی
```

آدرس سرور توسعه برای پروکسی با متغیر `FARANOU_SERVER_URL` تنظیم می‌شود (پیش‌فرض `https://localhost:5001`).

## قرارداد امنیتی
- ورود همچنان در صفحه Razor `/Account/Login` انجام می‌شود؛ نشست با Cookie از نوع HttpOnly نگه داشته می‌شود و هیچ توکنی در `localStorage` ذخیره نمی‌شود.
- درخواست‌های تغییردهنده (فعلاً فقط خروج) توکن Anti-forgery را از `GET /api/v1/antiforgery/token` می‌گیرند و در هدر `X-XSRF-TOKEN` می‌فرستند.
- پاسخ 401 کاربر را به صفحه ورود (با `ReturnUrl` محلی) می‌برد. شرکت انتخاب‌شده در آدرس فقط یک ترجیح است و سرور دسترسی را دوباره بررسی می‌کند.
- تاریخ‌ها به‌صورت تاریخ میلادی ISO (`yyyy-MM-dd`) از سرور می‌آیند و فقط در نمایش به تقویم شمسی تبدیل می‌شوند (`src/lib/date.ts`).

## استقلال از اینترنت
فونت IRANSansX و تصاویر از `Server/wwwroot` خوانده می‌شوند و هیچ CDN استفاده نمی‌شود. اینترنت فقط برای `npm ci` در زمان Build لازم است، نه در زمان اجرا. خروجی Build در Git ثبت نمی‌شود؛ پیش از انتشار باید `npm run build` اجرا شود.
