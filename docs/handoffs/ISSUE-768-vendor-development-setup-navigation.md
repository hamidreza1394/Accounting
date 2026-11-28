# تحویل وظیفه — Vendor Development first-run navigation and setup UX

- **Issue:** [#768](https://github.com/hamidreza1394/Accounting/issues/768) (بخشی از دامنه Local Development)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-28
- **وضعیت:** تغییرات این PR آماده بررسی؛ اجرای محلی Windows/Visual Studio در این محیط ممکن نبود.

## هدف و معیارهای پذیرش

- [x] بررسی مسیر شروع Vendor و علت بازشدن صفحه Setup پس از تنظیم دیتابیس.
- [x] اصلاح launch profile تا هر دو حالت Setup-only و Host کامل از یک مقصد شروع صحیح استفاده کنند.
- [x] روشن‌کردن ورودی Named Instance/Port در فرم SQL Server.
- [x] مستندسازی انتخاب Solution، Startup Project، مسیر Setup/Login، پورت اشغال‌شده و 401 مورد انتظار.
- [ ] اجرای عملی در Visual Studio روی Windows مالک.
- [ ] اجرای کامل Vendor CI و SQL Server integration روی HEAD این PR.

## یافته‌های کد و علت

1. `Vendor/Vendor.Host/Properties/launchSettings.json` مقدار `launchUrl=development/database-setup` داشت؛ Visual Studio حتی پس از آماده‌شدن دیتابیس هم مرورگر را به همان مسیر می‌فرستاد.
2. در `Program.cs`، وقتی Development Setup فعال است ولی Connection String موجود نیست، Host در حالت setup-only بالا می‌آید و `/` را به صفحه Setup redirect می‌کند.
3. وقتی اتصال موجود است، Host کامل بالا می‌آید؛ صفحه ورود عمومی در `/` سرو می‌شود، اما endpointهای Setup در حالت کامل anonymous نیستند و زیر سیاست احراز هویت قرار دارند. بنابراین 401 روی صفحه Setup در این وضعیت، fail-closed است و نباید با تضعیف Authorization رفع شود.
4. پورت HTTPS محلی 5001 ثابت است. اجرای هم‌زمان چند نمونه Host می‌تواند باعث خطای bind شود؛ PID باید در لحظه شناسایی شود و نباید در دستورالعمل ثابت نوشته شود.
5. فرم Setup قبلاً فیلد Server را داشت، اما placeholder فقط «localhost یا نام سرور» بود و Named Instance/Port را توضیح نمی‌داد؛ این برای SQL Server نصب‌شده با Instance نام‌دار ابهام ایجاد می‌کرد.

## خلاصه تغییرات

- Launch profile به `launchUrl: /` تغییر کرد. در setup-only، خود Host مسیر ریشه را به Setup هدایت می‌کند؛ در حالت اتصال آماده، صفحه ورود باز می‌شود.
- placeholder و راهنمای فیلد SQL Server برای `SERVER\\INSTANCE` و `SERVER,PORT` شفاف شد.
- Runbook مستقل فارسی برای اجرای محلی، انتخاب Vendor.sln/Host، راه‌اندازی مرحله‌ای، 401، پورت 5001 و خطاهای SQL اضافه شد.
- هیچ Connection String، credential، schema، migration یا مجوزی تغییر نکرد.

## فایل‌های مهم تغییرکرده

- `Vendor/Vendor.Host/Properties/launchSettings.json` — مقصد شروع مرورگر.
- `Vendor/Vendor.Host/Development/StaticFiles/index.html` — راهنمای آدرس SQL Server.
- `Vendor/Vendor.Host/Development/StaticFiles/setup.css` — استایل راهنمای فیلد.
- `docs/VENDOR_DEVELOPMENT_SETUP_RUNBOOK.md` — راهنمای عملیاتی محلی.
- این handoff — ثبت یافته و محدودیت اعتبارسنجی.

## تصمیم‌های فنی

- `/` نقطه شروع یکتا است؛ منطق تشخیص Setup-only/full-host در خود برنامه باقی می‌ماند.
- Authorization صفحه Setup در حالت Host کامل تغییر نکرد.
- پورت 5001 به پورت تصادفی تبدیل نشد تا URL توسعه و TLS ثابت بماند؛ تداخل با راهنمای تشخیص و توقف کنترل‌شده پوشش داده شد.
- PR #810 (Issue #809) جداگانه raceهای state machine فرم را پوشش می‌دهد؛ تغییرات آن در این PR تکرار نشد.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بازبینی `launchSettings.json` و `Program.cs` | انجام شد | علت launchUrl و branching setup-only/full-host بررسی شد |
| بازبینی `VendorDevelopmentDatabaseSetup.cs` | انجام شد | Access policy و loopback/challenge/antiforgery بررسی شد |
| بازبینی HTML/CSS فرم | انجام شد | راهنمای Named Instance/Port افزوده شد |
| `dotnet build Vendor.sln` | اجرا نشد | این محیط به working tree/SDK ویندوز مالک دسترسی ندارد |
| `dotnet test Vendor.sln` | اجرا نشد | باید توسط Vendor CI اجرا شود |
| اجرای Visual Studio روی Windows | اجرا نشد | نیازمند میزبان مالک |
| SQL Server/Windows Credential Manager واقعی | اجرا نشد | هیچ دیتابیس یا credential واقعی تغییر داده نشد |

## امنیت، داده و سازگاری

- هیچ endpoint ناشناس یا authorization policy جدیدی اضافه نشد.
- هیچ Runtime permission یا SQL role تغییر نکرد.
- هیچ دیتابیس، Migration یا credential واقعی لمس نشد.
- راهنمای پورت به PID نمونه یا ثابت وابسته نیست و ابتدا شناسایی پردازش را الزامی می‌کند.

## ریسک‌ها و کارهای باقی‌مانده

- باید در Visual Studio بررسی شود که پس از آماده‌بودن اتصال، صفحه ورود باز شود و در حالت بدون اتصال، ریشه به Setup هدایت شود.
- CI باید JSON launch profile، Build و تست‌های Vendor را اجرا کند.
- خطای 401 صفحه Setup در حالت Host کامل عمدی است؛ اگر محصول در آینده نیاز به تغییر اتصال در Host کامل داشته باشد، باید مسیر مدیریت‌شده و مجاز جداگانه طراحی شود، نه اینکه صفحه فعلی anonymous شود.
- Issue #768 همچنان برای Staging، Windows Service، ACL، CNG و آزمون دو میزبان باز می‌ماند.

## شناسه‌های Git

- Branch: `fix/vendor-development-first-run-navigation`
- Commit: در PR ثبت می‌شود.
- Pull Request: در حال ایجاد.
