# چک‌لیست امنیت

## یافته‌های مهم در نسخه ورودی
- MasterPassword به شکل SHA-256 Base64 در appsettings ذخیره شده بود؛ مقدار قبلی با یک رمز ضعیف/قابل حدس مرتبط بود. مقدار از تنظیمات tracked حذف شده است.
- نام کاربری و برخی Claims در Login به‌صورت hardcode نوشته شده بودند؛ نام کاربری اکنون از تنظیمات می‌آید، اما سازوکار رمز عبور همچنان legacy است.
- یک حساب مدیر با ایمیل/شماره تلفن و رمز عبور آزمایشی در EF Core `HasData` تعریف شده بود؛ این Seed ثابت حذف شد تا حساب پیش‌فرض و اطلاعات شخصی نمونه ساخته نشوند.
- کلیدهای Activation در تنظیمات tracked بودند؛ مقادیر حذف شده‌اند و باید از Secret/Environment وارد شوند.
- تنظیمات Google OAuth در کد کامنت شده بود؛ Client Secret واقعی نباید در مخزن ذخیره شود.
- خطای عمومی سفارشی موجود ممکن است Exception را ببلعد؛ logging و correlation ID باید در فاز امنیت/observability اصلاح شوند.

## اصلاحات امنیتی این بازبینی
- مقصد `ReturnUrl` پس از ورود با `Url.IsLocalUrl` بررسی می‌شود؛ URL خارجی به صفحه اصلی هدایت می‌شود تا Open Redirect جلوگیری شود.
- Cookie احراز هویت HttpOnly و SameSite=Lax است؛ در Production فقط روی HTTPS ارسال می‌شود و عمر 30 دقیقه‌ای با Sliding Expiration دارد. این تغییرها جایگزین سیستم هویت استاندارد و تست امنیتی نیستند.

## الزامات
- پیش از Production، احراز هویت سفارشی با ASP.NET Core Identity یا `IPasswordHasher<TUser>` جایگزین شود؛ SHA-256 خام برای رمز عبور مناسب نیست.
- اصل حداقل دسترسی (Least Privilege)، دفاع در عمق (Defense in Depth) و deny-by-default رعایت شود.
- برای صفحات تغییر‌دهنده داده، Anti-forgery فعال و بررسی شود.
- Cookies: `HttpOnly`, `Secure`, `SameSite` و زمان انقضای مناسب.
- کنترل مجوز سمت سرور انجام شود؛ پنهان‌کردن دکمه UI کنترل امنیت نیست.
- اعتبارسنجی ورودی سمت سرور، encoding خروجی، Query پارامتری و جلوگیری از XSS/SQL Injection.
- محدودسازی تلاش ورود، خطای عمومی login و عدم افشای وجود حساب.
- اسرار فقط در User Secrets (توسعه) یا Secret Store/Environment (استقرار) باشند؛ اگر مقدار واقعی قبلاً commit شده، آن را افشاشده فرض و Rotate کن.
- Dependabot/NuGet audit و اسکن Secretها در CI فعال شوند.
- خطاهای تولیدی نباید Stack Trace یا داده حساس به کاربر نشان دهند.

## تنظیم محلی موقت برای Login قدیمی
```bash
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ApplicationSettings:MasterUsername" "your-local-user" --project Server/Server.csproj
dotnet user-secrets set "ApplicationSettings:MasterPassword" "<Base64-SHA256-hash>" --project Server/Server.csproj
```
این فقط برای سازگاری موقت با Login فعلی است و راهکار توصیه‌شده برای Production نیست.

## OWASP Top 10 و DevSecOps
هر PR باید اثر تغییر بر Access Control، Cryptographic Failures، Injection، Insecure Design، Security Misconfiguration، Vulnerable Components، Authentication Failures، Integrity Failures، Logging/Monitoring و SSRF را بررسی کند. امنیت بخشی از چرخه توسعه و CI است، نه بررسی انتهایی.


## امنیت راه‌اندازی اولیه دیتابیس — Issue #22

- صفحه تنظیمات فقط زمانی در دسترس است که Connection String از Configuration یا فایل محلی محافظت‌شده قابل بازیابی نباشد.
- دسترسی اولیه با کد تصادفی یک‌بارمصرف کنترل می‌شود. کد در فایل `setup-token.txt` خارج از مخزن قرار می‌گیرد، در log نوشته نمی‌شود و پس از ذخیره موفق حذف می‌شود. اپراتور باید آن را فقط روی میزبان برنامه بخواند.
- Connection String کامل، از جمله رمز SQL، با ASP.NET Core Data Protection محافظت و خارج از repository ذخیره می‌شود. در ویندوز کلیدها با DPAPI (وابسته به هویت کاربر سرویس) محافظت می‌شوند؛ در سیستم‌های Unix مجوز دسترسی فایل/پوشه به کاربر اجراکننده محدود می‌شود.
- از مسیر محلی تنظیمات و پوشه کلیدها نسخه پشتیبان امن بگیرید؛ بدون کلید Data Protection تنظیمات رمزنگاری‌شده قابل بازیابی نیستند. دسترسی به پوشه LocalApplicationData باید فقط برای حساب سرویس/کاربر مجاز باشد.
- در شبکه‌های غیرقابل‌اعتماد، صفحه راه‌اندازی را فقط پشت HTTPS منتشر کنید. کد یک‌بارمصرف جایگزین TLS، کنترل دسترسی میزبان یا سیاست امنیتی شبکه نیست.
- گزینه TrustServerCertificate فقط برای سرور مورد اعتماد/گواهی خودامضا مناسب است؛ در Production گواهی معتبر ترجیح دارد.
- خطاهای SQL فقط با شماره خطا به کاربر نمایش داده می‌شوند؛ Connection String و رمز عبور نباید در log ثبت شوند.

## احراز هویت صفحه ورود — Issue #30

- رمز عبور کاربران اکنون با `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (PBKDF2، salt یکتا برای هر رمز، قالب نسخه‌دار) هش می‌شود. هش قدیمی `Base64(SHA-256)` دیگر تولید نمی‌شود.
- سازگاری: هش قدیمی هنوز پذیرفته می‌شود و پس از نخستین ورود موفق در همان رکورد با هش استاندارد جایگزین می‌شود (`SuccessRehashNeeded`). حساب‌هایی که هرگز وارد نشوند هش قدیمی خود را نگه می‌دارند؛ پیش از Production باید یا وارد شوند یا رمزشان بازنشانی شود.
- مقدار هش نامعتبر/بریده در ستون، ورود را با شکست مواجه می‌کند (نه `FormatException`).
- برای نام کاربری ناموجود، همان هزینه محاسباتی بررسی رمز انجام می‌شود تا زمان پاسخ وجود حساب را افشا نکند.
- حساب زیرساختی (`ApplicationSettings:MasterPassword`) هم قالب جدید و هم قالب قدیمی را می‌پذیرد. تولید مقدار جدید:
  ```csharp
  // یک‌بار در محیط توسعه/استقرار اجرا و مقدار را در User Secrets/Secret Store قرار دهید
  new Infrastructure.Security.PasswordHashService().Hash("<password>")
  ```
  اگر مقدار قدیمی پیکربندی شده باشد، در هر ورود موفق یک هشدار (بدون مقدار) در Log ثبت می‌شود.
- ستون `Users.Password` به `varchar(255)` بزرگ شده است (Migration `WidenUserPasswordHash`)؛ Migration داده‌ای را تغییر نمی‌دهد.
- جریان ورود: Post/Redirect/Get، پیام عمومی برای اعتبارنامه اشتباه، پیام اختصاصی غیرفعال/قفل، حذف رمز از `ModelState` پیش از رندر پاسخ، و هدایت به مقصد موقت محافظت‌شده `/Account/LoginSuccess`.
- `ReturnUrl` فقط با `Url.IsLocalUrl` پذیرفته می‌شود؛ در غیر این صورت مقصد موقت.
