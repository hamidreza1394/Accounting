# راهنمای اجرای محلی و راه‌اندازی اولیه Vendor

- **Issue مرتبط:** [#768](https://github.com/hamidreza1394/Accounting/issues/768)
- **دامنه:** اجرای Development روی Windows و SQL Server؛ این سند Runbook محلی است و ادعای آمادگی Production ندارد.

## ۱) انتخاب Solution و پروژه

1. از ریشه مخزن، فایل `Vendor.sln` را در Visual Studio 2026 (نسخه 18.0 یا بالاتر) باز کنید؛ `Template.sln` مربوط به برنامه اصلی Customer است.
2. پروژه `Vendor/Vendor.Host/Vendor.Host.csproj` را به‌عنوان Startup Project انتخاب کنید.
3. در نوار اجرا، پروفایل `Vendor.Host` را انتخاب کنید.
4. Build را روی همین Solution مستقل انجام دهید.

## ۲) رفتار شروع مرورگر

پروفایل `Vendor.Host` مرورگر را به مسیر ریشه `/` می‌فرستد:

- اگر اتصال Vendor DB هنوز پیکربندی/ذخیره نشده باشد، Host وارد حالت setup-only می‌شود و خود برنامه مسیر `/` را به `/development/database-setup` هدایت می‌کند.
- اگر اتصال Vendor DB از Configuration یا Windows Credential Manager بارگذاری شود، Host کامل بالا می‌آید و مسیر `/` صفحه ورود Vendor را نمایش می‌دهد.
- صفحه `/development/database-setup` در حالت Host کامل زیر سیاست احراز هویت قرار دارد. بنابراین پاسخ `401` در این مسیر، وقتی دیتابیس از قبل تنظیم شده و هنوز وارد حساب مدیر نشده‌اید، رفتار fail-closed مورد انتظار است؛ برای ورود به پنل از `https://localhost:5001/` استفاده کنید.
- برای اجرای مجدد Setup در حالت Host کامل، ابتدا باید وضعیت اتصال را از منبع پیکربندی‌شده مدیریت کنید؛ این صفحه مسیر عمومی و ناشناس برای تغییر اتصال فعال نیست.

## ۳) فرم اتصال SQL Server

| فیلد | راهنما |
|---|---|
| آدرس SQL Server | برای نمونه محلی از `localhost`؛ برای Named Instance از `SERVER\INSTANCE` مانند `localhost\SQLEXPRESS`؛ برای پورت مشخص از `SERVER,PORT` استفاده کنید. |
| نام دیتابیس Vendor | نام دیتابیس مستقل Vendor، معمولاً `VendorDb`. دیتابیس Customer را انتخاب نکنید. |
| روش ورود Windows | از هویت Windows پردازشی که Host را اجرا می‌کند استفاده می‌کند؛ همان هویت باید روی SQL Server مجوز لازم داشته باشد. |
| روش ورود SQL | فقط وقتی انتخاب کنید که SQL Server authentication فعال و Login مجاز دارید. رمز را در چت، Git یا log قرار ندهید. |
| اعتماد به گواهی سرور | فقط برای SQL محلی/توسعه و زمانی که گواهی قابل اعتبارسنجی نیست، آگاهانه فعال شود. در استقرار واقعی از گواهی معتبر استفاده کنید. |
| نام/رمز Runtime | اختیاری است؛ در صورت خالی‌بودن، جریان Provisioning هویت کم‌اختیار را ایجاد می‌کند. حساب Runtime را با حساب مدیریتی SQL یا `sa` یکی نکنید. |

نکته مهم دربارهٔ «ذخیره به‌عنوان اتصال Runtime»: این دکمه اطلاعات روش ورود اصلی فرم (Windows یا SQL) را در Windows Credential Manager ذخیره می‌کند؛ بنابراین فقط وقتی از آن استفاده کنید که همان هویت از قبل برای Runtime کم‌اختیار آماده شده باشد. از حساب sa، sysadmin یا db_owner استفاده نکنید. فیلدهای جداگانهٔ نام/گذرواژه Runtime فقط در عملیات «راه‌اندازی کامل» برای ایجاد/معرفی هویت Runtime مصرف می‌شوند؛ این دو مسیر را با هم اشتباه نگیرید.

## ۴) ترتیب امن راه‌اندازی

1. کد Setup challenge را فقط از کنسول همان پردازش Host بردارید؛ با Restart عوض می‌شود.
2. آدرس سرور و نام دیتابیس Vendor را وارد کنید و روش احراز هویت را متناسب با SQL Server انتخاب کنید.
3. ابتدا «آزمون اتصال» را اجرا کنید.
4. از «بازرسی سرور و دیتابیس» برای مشاهده وضعیت موجود، مالکیت دیتابیس و Migrationها استفاده کنید.
5. فقط پس از بررسی مقصد، «راه‌اندازی کامل» را اجرا کنید. Provisioning می‌تواند دیتابیس را ایجاد کند، Migrationهای Vendor را اعمال کند و Login/User کم‌اختیار Runtime را آماده کند.
6. اگر عملیات در شبکه قطع شد یا نتیجه نامشخص بود، Provisioning را کورکورانه تکرار نکنید؛ ابتدا بازرسی را اجرا کنید.
7. اگر گذرواژه Runtime تولید شد، فقط همان بار نمایش داده می‌شود؛ آن را در محل امن سیستم خود نگهداری کنید و در چت یا log نفرستید.
8. پس از موفقیت، Host را با Stop در Visual Studio متوقف و دوباره اجرا کنید؛ سپس صفحه ورود را در `https://localhost:5001/` باز کنید.

## ۵) خطاهای رایج

### پورت 5001 اشغال است

خطای `Failed to bind to address https://127.0.0.1:5001: address already in use` یعنی پردازش دیگری روی پورت گوش می‌دهد. ابتدا همه اجرای قبلی Vendor را با Stop در Visual Studio یا بستن کنسول همان پردازش متوقف کنید.

برای شناسایی پردازش در PowerShell:

```powershell
Get-NetTCPConnection -LocalPort 5001 -State Listen |
    Select-Object LocalAddress, LocalPort, OwningProcess,
    @{Name="ProcessName";Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}}
```

فقط پس از اطمینان از اینکه PID متعلق به نمونه قدیمی `Faranou.Vendor.Host` است، همان پردازش را متوقف کنید. PID نمونه‌ای و ثابت نیست؛ PID را از خروجی فعلی بخوانید و مقدار قدیمی را دوباره استفاده نکنید.

### صفحه Setup پاسخ 401 می‌دهد

ابتدا `https://localhost:5001/` را باز کنید. اگر صفحه ورود Vendor نمایش داده می‌شود، اتصال از قبل بارگذاری شده و Host کامل است؛ 401 در مسیر Setup مانع ورود عادی نیست. اگر صفحه اصلی هم 401 می‌دهد، خروجی کنسول Host و درخواست Network را بررسی کنید؛ هیچ endpoint یا سیاست احراز هویت را برای رفع خطا ناشناس نکنید.

### اتصال SQL برقرار نمی‌شود

- نام سرور و Named Instance/Port را دوباره بررسی کنید.
- مطمئن شوید SQL Server service و در صورت نیاز SQL Server Browser فعال‌اند.
- روش Windows/SQL را با روش فعال در SQL Server تطبیق دهید.
- مطمئن شوید Login انتخاب‌شده به دیتابیس Vendor دسترسی دارد.
- TrustServerCertificate را فقط برای توسعه محلی و با آگاهی از اثر TLS استفاده کنید.
- متن خطا را بدون Connection String، username/password یا token برای بررسی نگه دارید.

## ۶) مرزهای امنیتی که نباید دور زده شوند

- Vendor DB از Customer DB مستقل است؛ هیچ‌گاه Customer DB را به‌عنوان Vendor DB انتخاب نکنید.
- Runtime برنامه نباید با `sa` یا `db_owner` اجرا شود؛ دسترسی Runtime باید محدود به دیتابیس Vendor و نیازهای خواندن/نوشتن باشد.
- Setup فقط Development ویندوز و Loopback است و challenge کنسول، Origin، Antiforgery و Rate Limit باید حفظ شوند.
- هیچ secret یا Connection String واقعی در Git، issue، PR، screenshot یا log منتشر نشود.
- این Runbook جایگزین نصب سرویس Windows، Key Ceremony، Staging یا آزمون دو میزبان نیست.
