# تحویل — Issue #61: اجرای React هنگام Build در Visual Studio

## مسئله
پس از ورود موفق، کاربر شرکتی به `/app/` هدایت می‌شود. خروجی React در `Server/wwwroot/app` ساخته می‌شود و عمداً در Git ثبت نمی‌شود. در اجرای مستقیم پروژه Server با Visual Studio، اگر خروجی ساخته نشده باشد، مسیر ممکن است 404 بدهد.

## تغییر پیشنهادی
- افزودن MSBuild target افزایشی برای Build عادی Visual Studio؛ target در Design-time build اجرا نمی‌شود.
- اگر marker نصب npm وجود نداشته باشد، `npm ci` اجرا می‌شود؛ سپس `npm run build`.
- CLI/CI خارج از Visual Studio از گردش موجود استفاده می‌کند؛ اسکریپت‌های Publish همچنان مرجع انتشار هستند.
- مستندات توسعه محلی به‌روزرسانی شده است.

## فایل‌های تغییرکرده
- `Server/Server.csproj`
- `docs/DEVELOPMENT.md`
- `docs/WORK_LOG.md`
- `docs/handoffs/ISSUE-61-local-react-build.md`

## تست و محدودیت‌ها
- اجرای Visual Studio/Windows از این محیط ممکن نیست.
- CI باید target و Build/Tests را بررسی کند. پس از Pull، روی کامپیوتر توسعه یک بار Build و اجرای F5 را تأیید کنید.
- اگر npm registry در اولین Build در دسترس نباشد، `npm ci` شکست می‌خورد و باید بسته‌ها را با اتصال شبکه بازیابی کرد. زمان اجرا به CDN وابسته نیست.

## وضعیت
- Issue: #61
- Branch: `fix/local-react-build-on-server-build`
- Merge نشده؛ نیازمند بازبینی و نتیجه CI.
