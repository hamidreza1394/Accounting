# طراحی فرم تنظیم اتصال دیتابیس Vendor در Development

- **Issue:** #797
- **تاریخ:** 2026-11-26
- **وضعیت:** طرح پیاده‌سازی‌شده در شاخه؛ تا CI موفق نشده Verified نیست.

## مسئله

Host مستقل Vendor پیش از راه‌اندازی، مقدار `ConnectionStrings:VendorDb` را اجباری می‌کند. اجرای Host در Visual Studio بدون تنظیم دستی Environment با خطا متوقف می‌شود، در نتیجه اپراتور راهی محلی برای واردکردن و کنترل اتصال ندارد.

## تصمیم طراحی

یک صفحهٔ محلی فارسی RTL برای Development اضافه شود. پروفایل Visual Studio، محیط Development و پرچم `FARANO_VENDOR_DevelopmentSetup__Enabled=true` را فعال می‌کند. اگر تنظیم اتصال موجود نیست، Host فقط صفحه setup را سرو می‌کند و Persistence، Identity، startup serviceها یا migrationها را ثبت/اجرا نمی‌کند. کاربر می‌تواند ابتدا اتصال را آزمایش کند؛ فقط نتیجهٔ موفق در Windows Credential Manager حساب فعلی ذخیره می‌شود. اجرای بعدی Vendor مقدار ذخیره‌شده را پیش از ثبت Persistence می‌خواند.

## گزینه‌های بررسی‌شده

- **Environment variable:** برای CI/استقرار مناسب است، اما تنظیم تکراری Visual Studio برای توسعه‌دهنده را حل نمی‌کند و ذخیره/مدیریت را به ابزار بیرونی واگذار می‌کند.
- **User Secrets:** ساده و cross-platform است، ولی اتصال و رمز را به‌صورت رمزنگاری‌شده در Secret Store ویندوز نگه نمی‌دارد و پس از انتقال کاربر/ماشین، رفتار ناخواسته دارد.
- **Windows Credential Manager:** با نیاز محلی Windows/Visual Studio هم‌راستا است، credential را per-user نگه می‌دارد و نیازی به فایل تنظیم رمزدار یا تغییر Customer ندارد؛ برای issue حاضر انتخاب شد.

## قرارداد و تجربه

- URL صفحه: `https://localhost:5001/development/database-setup`.
- فیلدها: SQL Server، دیتابیس Vendor، Windows Integrated یا SQL Login، و انتخاب صریح TrustServerCertificate برای گواهی توسعه‌ای.
- عملیات: دریافت antiforgery token، نمایش منبع تنظیم، آزمون اتصال، ذخیره اتصال موفق، حذف credential ذخیره‌شده.
- برای اتصال موفق، نام دیتابیس جاری بررسی می‌شود. هیچ DB ایجاد یا Migration اجرا نمی‌شود.
- ذخیره پس از تست دوباره سمت سرور نیز اتصال را باز می‌کند؛ با تغییر هر فیلد، لازم است کاربر دوباره آزمون کند.
- بعد از ذخیره/حذف، اجرای مجدد لازم است تا منبع پیکربندی تازه بارگذاری شود.

## مرز امنیت

- فقط Development و فقط ویندوز؛ فعال‌سازی از launch profile انجام می‌شود، نه از پیش‌فرض Production.
- فقط loopback، HTTPS، Origin دقیق هم‌میزبان، antiforgery و rate limit.
- SQL Login/password فقط در POST test/save ارسال می‌شوند؛ UI آن‌ها را در Web Storage یا cookie نگه نمی‌دارد؛ API، log و پیام خطا connection string/رمز را برنمی‌گردانند.
- Environment/Configuration صریح بر Credential Manager مقدم است؛ در این وضعیت ذخیره/حذف از صفحه غیرفعال است.
- کلید Credential Manager برای Vendor جدا است و فقط مقدار اتصال Vendor را نگه می‌دارد.
- هیچ وابستگی‌ای به دیتابیس Customer، نصب Windows Service، signing gate یا عملیات Migration اضافه نمی‌شود.

## مواردی که پس از پیاده‌سازی باید بررسی شوند

Vendor CI و .NET CI باید Restore/Build/Test و گاردهای publish را اجرا کنند. اجرا و اعتماد به Development HTTPS certificate و اتصال زنده به SQL Server در دستگاه مالک، بررسی دستی محیط توسعه است و در CI ادعا نمی‌شود.
