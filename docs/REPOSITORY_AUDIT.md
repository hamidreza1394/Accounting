# گزارش بررسی ایستای مخزن

- **تاریخ بررسی:** 2026-10-16
- **مبنای بررسی:** ZIP ارسالی `net9-baseline`، نه مخزن آنلاین GitHub.
- **روش:** بررسی ساختار، فایل‌های پروژه، تنظیمات JSON، نسخه‌های پکیج، مسیرهای دارایی فرانت‌اند، الگوهای پرریسک در C# و بازبینی هدفمند ورود، Cookie و Middleware خطا. اسکن کامل‌تر در `docs/CLASS_REVIEW_INVENTORY.csv` ثبت شده است.
- **محدودیت:** در محیط بررسی `dotnet` نصب نیست و DNS/دریافت شبکه نیز در دسترس نبود؛ بنابراین Restore، Build، Test، `libman restore` و تست مرورگر اجرا نشدند. این گزارش تأیید کامپایل یا تأیید امنیتی نهایی نیست.

## دامنه مخزن
- 12 فایل پروژه `.csproj` و یک Solution.
- 199 فایل C#، 76 فایل Razor `.cshtml` و یک فایل تست C# در بسته ورودی.
- 9 کتابخانه در `Server/libman.json` و دارایی‌های vendored در `Server/wwwroot/lib/`.
- 1 تست فعلی در `tests/Template.Tests`؛ این سطح پوشش برای ادعای آمادگی Production کافی نیست.

## اصلاحات اعمال‌شده در این دور
1. `AGENTS.md` به پروتکل شروع/پایان اجباری، قوانین معماری/امنیت و قالب گزارش Agent مجهز شد.
2. قالب‌های GitHub Issue برای وظیفه Agent و Bug Report اضافه شدند.
3. قالب تحویل هر وظیفه در `docs/handoffs/TEMPLATE.md` اضافه شد.
4. اسکریپت `scripts/check_static_assets.py` و گام CI برای جلوگیری از بارگذاری دارایی‌های CDN در صفحات/CSS/JS متعلق به برنامه اضافه شد. فایل‌های vendor داخل `wwwroot/lib` از این اسکن مستثنا هستند؛ باید هنگام ارتقای vendor جداگانه بررسی شوند.
5. Redirect پس از ورود اکنون فقط به URL محلی مجاز می‌رود و Open Redirect از `ReturnUrl` کنترل می‌شود.
6. تنظیمات Cookie احراز هویت موجود به HttpOnly، SameSite=Lax، عمر محدود و Secure در Production مجهز شد.
7. Middleware استثنای قدیمی دیگر Exception را بی‌صدا نمی‌بلعد؛ خطا را با ILogger ثبت و دوباره پرتاب می‌کند تا handler استاندارد پاسخ را مدیریت کند.
8. مستندات Coding Standards و Development با قواعد .NET/C#, EF Core, Razor, امنیت، تست، نسخه‌بندی و دارایی‌های محلی گسترش یافت.

## یافته‌های باقی‌مانده و اولویت‌ها

### P0 — پیش از انتشار عمومی/Production
- **احراز هویت legacy:** `Server/Pages/Account/Login.cshtml.cs` از نام کاربری مرکزی و SHA-256 خام استفاده می‌کند و Claim نقش `Admin` می‌سازد. این مدل برای Production مناسب نیست. پیش از استفاده واقعی، آن را با ASP.NET Core Identity یا `IPasswordHasher<TUser>` و نقش/مجوزهای پایدار جایگزین کن.
- **Secrets/config:** `MasterUsername`، `MasterPassword` و `ActivationKeys` در تنظیمات وجود دارند اما در نسخه ورودی خالی هستند. مقادیر واقعی را فقط در User Secrets/Environment/Secret Store قرار بده. اگر مقادیر قبلی قبلاً به Git push شده‌اند، آن‌ها را افشاشده فرض و Rotate کن.
- **عدم اجرای Build/Test:** این بسته در این محیط compile نشده؛ پیش از شروع قابلیت‌های جدید، روی ویندوز دارای SDK واقعی Restore/Build/Test را اجرا کن و خطاها را رفع کن.
- **پایان پشتیبانی .NET 9:** با وجود هدف فعلی net9.0، پایان پشتیبانی در نوامبر 2026 نزدیک است. تصمیم مهاجرت به .NET 10 LTS را پیش از توسعه محصول/استقرار بگیر.

### P1 — پیش از شروع قابلیت‌های جدی
- **Frontend versions:** `Server/libman.json` به Bootstrap 5.2.2 و Bootstrap Icons 1.9.1 اشاره می‌کند؛ این با هدف Bootstrap 5.3+ منطبق نیست. در این محیط دسترسی شبکه برای دریافت باینری‌ها وجود نداشت؛ بنابراین عمداً فقط شماره نسخه را تغییر ندادم تا فایل‌های محلی و manifest ناسازگار نشوند. در وظیفه جداگانه، Bootstrap را به نسخه پایدار 5.3.x (در زمان بررسی سایت رسمی 5.3.8) و Icons را به نسخه مناسب ارتقا بده، فایل‌های محلی را واقعاً به‌روزرسانی و UI/RTL/آیکون‌ها را تست کن.
- **منطق در PageModelها:** CRUDهای متعدد حاوی `catch (Exception)` و دسترسی مستقیم به Persistence هستند. هر PageModel را در Issueهای کوچک به Application منتقل کن؛ بازنویسی دسته‌جمعی بدون تست پرریسک است.
- **مدیریت Exception:** اکنون مسیر استاندارد `UseExceptionHandler` در Program موجود است. Middleware قدیمی در صورت استفاده ثبت و rethrow می‌کند، اما بهتر است در آینده استفاده مستقیم آن حذف و handler استاندارد تنها نقطه مدیریت پاسخ خطا باشد.
- **Coverage:** فقط تست پایه Hashing وجود دارد؛ برای ورود، مجوزها، CRUD، Validation، Cookie، Redirect و مسیرهای خطا تست اضافه کن.
- **Migration/Database:** پیش از تغییر Schema، وضعیت Migrationها، مدل‌های EF، cascade delete، unique index و روش deployment/rollback را بررسی و مستند کن.
- **نمونه‌های قالب:** صفحات About/Contact/Index هنوز لینک‌ها و اطلاعات صاحب قالب را دارند؛ پیش از انتشار محصول با اطلاعات پروژه جایگزین شوند.
- **OAuth:** کد Google Authentication در Program کامنت شده، در حالی که PackageReference موجود است؛ یا در Issue جداگانه کامل و امن پیاده‌سازی شود یا وابستگی/کد بلااستفاده حذف شود.

### P2 — بهبود تدریجی
- هماهنگ‌سازی نام‌گذاری و formatter کد قدیمی در PRهای مرتبط، نه بازقالب‌بندی بزرگ.
- افزودن تحلیل وابستگی و اسکن آسیب‌پذیری NuGet در CI پس از تثبیت سیاست fail/warn.
- افزودن تست Integration با SQL Server یا محیط آزمون معتبر؛ EF InMemory جایگزین کامل SQL Server نیست.
- بررسی مجوز فایل‌های vendor، حذف وابستگی‌های غیرمصرف‌شده و تعیین فرآیند نگهداری نسخه‌ها.

## یافته‌های اسکن ایستا
- جست‌وجوی اولیه برای `FromSqlRaw`، `ExecuteSqlRaw` و `SqlCommand(` در فایل‌های C# مورد بررسی، موردی پیدا نکرد.
- جست‌وجوی اولیه برای `.Result`، `.Wait()` و `GetAwaiter().GetResult()` در فایل‌های C# مورد بررسی، موردی پیدا نکرد.
- چند `catch (Exception)` در PageModelهای CRUD و Registration وجود دارد؛ بررسی محتوای هر catch و اطمینان از ثبت خطا هنوز باید به‌صورت وظیفه‌ای انجام شود.
- TODOها در Login، Register و Page routing باقی هستند و باید هنگام پیاده‌سازی همان قابلیت تکمیل شوند.
- URLهای بیرونی در صفحات Contact/Index عمدتاً لینک محتوایی هستند؛ آن‌ها را با CDN دارایی اشتباه نگیرید. اسکن جدید فقط hostهای CDN را در attributeهای منبع و CSS بررسی می‌کند.

## فرمان‌های لازم روی دستگاه توسعه
```powershell
dotnet --info
dotnet restore Template.sln
dotnet build Template.sln --no-restore --configuration Release
dotnet test Template.sln --no-build --configuration Release
python scripts/check_static_assets.py
dotnet format Template.sln --verify-no-changes
```

## منابع نسخه‌ای بررسی‌شده
- Bootstrap 5.3 download docs: https://getbootstrap.com/docs/5.3/getting-started/download/
- Bootstrap Icons usage/version: https://icons.getbootstrap.com/usage/
- EF Core 9.0.21 package listing: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/9.0.21

**تفسیر نتیجه:** این بسته اکنون مستندات و کنترل‌های بیشتری دارد و چند ایراد قابل اثبات اصلاح شده، اما تا رفع موارد P0 و اجرای واقعی Build/Test نباید به‌عنوان نسخه Production-ready تلقی شود.
