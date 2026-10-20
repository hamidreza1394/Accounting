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

## تنظیم حساب زیرساختی (Master)

`ApplicationSettings:MasterPassword` یک **اعتبارنامه هش‌شده** است، نه رمز خام؛ کد آن را با هش‌کننده پروژه بررسی می‌کند (به بخش «احراز هویت صفحه ورود — Issue #30» پایین‌تر مراجعه کنید).

روش توصیه‌شده تولید مقدار جدید:

```csharp
// یک‌بار در محیط توسعه/استقرار اجرا و خروجی را در User Secrets یا Secret Store قرار دهید
new Infrastructure.Security.PasswordHashService().Hash("<password>")
```

```bash
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ApplicationSettings:MasterUsername" "your-local-user" --project Server/Server.csproj
dotnet user-secrets set "ApplicationSettings:MasterPassword" "<credential produced by PasswordHashService.Hash>" --project Server/Server.csproj
```

- روش توصیه‌شده `Microsoft.AspNetCore.Identity.PasswordHasher<T>` است: PBKDF2، salt یکتا برای هر مقدار، قالب نسخه‌دار.
- پذیرش قالب قدیمی `Base64(SHA-256)` فقط برای **سازگاری و مهاجرت تدریجی** است؛ برای مقدار جدید از آن استفاده نکن.
- رمز خام را هرگز به‌عنوان `MasterPassword` در `appsettings.json`، User Secrets یا Environment قرار نده؛ مقدار تنظیمات به‌عنوان هش بررسی می‌شود و ورود با رمز خام ناموفق خواهد بود.
- اگر مقدار پیکربندی‌شده هنوز قالب قدیمی داشته باشد، در هر ورود موفق یک `LogWarning` بدون مقدار در Log ثبت می‌شود؛ آن را با مقدار جدید جایگزین کن.
- رمز خام، هش و مقدار تنظیمات هرگز در Log نوشته نمی‌شوند.

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
- برای نام کاربری ناموجود، همان هزینه محاسباتی بررسی رمز انجام می‌شود تا زمان پاسخ وجود حساب را افشا نکند. حساب‌هایی که هنوز هش قدیمی دارند نیز همین هزینه را می‌پردازند: پس از مقایسه ثابت‌زمانی SHA-256، یک بررسی ساختگی با هش‌کننده فعلی (PBKDF2) اجرا می‌شود که نتیجه‌اش نادیده گرفته می‌شود و برای رمز درست و نادرست یکسان اجرا می‌گردد. هدف، کاهش اختلاف آشکار هزینه دو مسیر است، نه برابری کامل زمان پاسخ؛ اختلاف‌های جزئی در حد نویز باقی می‌ماند و هیچ تضمین ریاضی برای یکسان‌بودن زمان دو مسیر وجود ندارد.
- حساب زیرساختی (`ApplicationSettings:MasterPassword`) هم قالب جدید و هم قالب قدیمی را می‌پذیرد؛ روش تولید مقدار جدید و قواعد آن در بخش «تنظیم حساب زیرساختی (Master)» در بالای همین سند آمده است. اگر مقدار قدیمی پیکربندی شده باشد، در هر ورود موفق یک هشدار (بدون مقدار) در Log ثبت می‌شود.
- ستون `Users.Password` به `varchar(255)` بزرگ شده است (Migration `WidenUserPasswordHash`)؛ Migration داده‌ای را تغییر نمی‌دهد.
- جریان ورود: Post/Redirect/Get، پیام عمومی برای اعتبارنامه اشتباه، پیام اختصاصی غیرفعال/قفل، حذف رمز از `ModelState` پیش از رندر پاسخ، و هدایت به مقصد موقت محافظت‌شده `/Account/LoginSuccess`.
- `ReturnUrl` فقط با `Url.IsLocalUrl` پذیرفته می‌شود؛ در غیر این صورت مقصد موقت.


## API و جداسازی شرکت‌ها — 2026-10-20
- API نسخه 1 فقط با Cookie احراز هویت (HttpOnly، SameSite=Lax، Secure در Production) کار می‌کند؛ توکنی در JavaScript یا `localStorage` نیست و CORS فعال نیست (D-012).
- درخواست تغییردهنده API (فعلاً `POST /api/v1/session/logout`) بدون هدر معتبر `X-XSRF-TOKEN` رد می‌شود (400). خروج صفحات Razor هم فقط با POST و Anti-forgery است؛ پیوند GET دیگر نشست را پایان نمی‌دهد.
- دسترسی به شرکت فقط از عضویت فعال در دیتابیس (کاربر فعال + عضویت فعال + شرکت فعال + نقش فعال همان شرکت) تعیین می‌شود و در هر درخواست دوباره بررسی می‌شود؛ Claim یا پارامتر کلاینت مبنای دسترسی نیست (D-013). شرکت غیرمجاز و ناموجود پاسخ یکسان 404 دارند.
- پاسخ‌های API `Cache-Control: no-store` دارند. خطای غیرمنتظره با جزئیات فقط در Log سرور ثبت و به کاربر پیام عمومی داده می‌شود.
- جلسه‌هایی که پیش از این تغییر صادر شده‌اند Claim شناسه کاربر ندارند؛ API برای آن‌ها 401 برمی‌گرداند و کاربر باید دوباره وارد شود. داشبورد Razor برای سازگاری، شناسه را از نام کاربری پیدا می‌کند.
- **باقی‌مانده:** Content-Security-Policy برای `/app`، اعتبارسنجی مجدد نشست (Security Stamp) هنگام تغییر رمز/غیرفعال‌سازی برای صفحات Razor، Rate Limiting برای API، و OpenAPI با کنترل دسترسی.


## بازبینی امنیتی پس از PR #38 — 2026-10-20
### اعتبارسنجی مجدد نشست (D-014)
- هر درخواست احرازشده حساب را بررسی می‌کند: حساب حذف‌شده یا غیرفعال، یا رمز/هش تغییرکرده، نشست را در همان درخواست پایان می‌دهد (صفحات به ورود هدایت می‌شوند، API پاسخ 401 می‌دهد). این شامل نشست‌های Razor و «مرا به خاطر بسپار» هم هست.
- نشست‌های قدیمی بدون Claim `CredentialStamp` یک‌بار باطل می‌شوند.

### CSP و هدرهای امنیتی (D-015)
| مسیر | سیاست |
|---|---|
| `/app/*` | اجباری: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` |
| `/api/*` | اجباری: `default-src 'none'; frame-ancestors 'none'` |
| صفحات Razor | اجباری: `object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'` + سیاست سخت‌گیرانه به‌صورت Report-Only |

همه پاسخ‌ها: `X-Content-Type-Options: nosniff`، `Referrer-Policy: strict-origin-when-cross-origin`، `Permissions-Policy` محدود، `Cross-Origin-Opener-Policy: same-origin`. کلاینت React در Chromium زیر سیاست اجباری بدون هیچ خطای CSP اجرا شد.

**موارد inline مانع اجبار CSP در Razor:** اسکریپت inline در `Pages/Shared/PartialViews/_DisplayToastMessages.cshtml` (همه صفحات دارای Toast)، `Pages/DatabaseSetup.cshtml`، `Pages/Admin/MenuItems/Create|Update.cshtml`، `Pages/Admin/PageManager/Delete.cshtml`؛ `<style>` inline در `DatabaseSetup`، `MenuItems/Create|Update` و `Account/LoginSuccess.cshtml`؛ Event handler inline (`onclick`/...) در `MenuItems/Create|Update` و `PageManager/Delete`؛ چند `style="..."`. TinyMCE در صفحات مدیریت هم پیش از اجبار باید بررسی شود.

### Rate Limiting
| سیاست | محدوده | پیش‌فرض (`RateLimiting` در appsettings) |
|---|---|---|
| `api` | همه `/api/v1`، به ازای کاربر (یا آدرس برای ناشناس) | 300 درخواست در 60 ثانیه |
| `api-sensitive` | `antiforgery/token` و `session/logout` | 30 در 60 ثانیه |
| `login` | فقط POST فرم ورود، به ازای آدرس کلاینت (GET محدود نمی‌شود) | 20 در 60 ثانیه |

پاسخ رد: 429 با `Retry-After` (برای API به‌صورت `application/problem+json`). این محدودیت مکمل قفل موقت ۵ تلاش ناموفق در ۱۵ دقیقه به ازای نام کاربری است. **محدودیت:** پشت Reverse Proxy تا پیکربندی ForwardedHeaders، همه کاربران پشت Proxy یک پارتیشن آدرس مشترک دارند.

### خطاهای API
خطای غیرمنتظره در API (حتی پیش از Endpoint Filter، مثلاً هنگام ساخت سرویس) فقط Log می‌شود و پاسخ عمومی `application/problem+json` بدون متن Exception، Connection String یا Stack Trace برمی‌گردد (تست Integration).

### هش‌های SHA-256 قدیمی — برنامه حذف
- هش جدید: `PasswordHasher<T>` نسخه V3 (PBKDF2-HMAC-SHA512، salt تصادفی ۱۲۸ بیتی برای هر رمز، 100,000 تکرار در .NET 10).
- هش قدیمی (`Base64(SHA-256)`، دقیقاً ۴۴ نویسه) فقط در ورود موفق پذیرفته و همان لحظه به V3 ارتقا داده می‌شود؛ ارتقا مهر نشست را عوض می‌کند و نشست‌های دیگر همان حساب را باطل می‌کند.
- شمارش حساب‌های باقی‌مانده: `SELECT COUNT(*) FROM Users WHERE LEN(Password) = 44;`
- **برنامه پیشنهادی:** (۱) از تاریخ استقرار این نسخه، ۹۰ روز فرصت ورود؛ (۲) سپس برای حساب‌های باقی‌مانده رمز تصادفی غیرقابل‌استفاده تنظیم و بازنشانی رمز توسط مدیر الزامی شود؛ (۳) پس از صفرشدن شمارش، پذیرش قالب قدیمی از `PasswordHashService.Verify` حذف شود. حساب زیرساختی (`MasterPassword`) باید همین حالا با مقدار تولیدشده توسط `PasswordHashService.Hash` جایگزین شود. این مراحل نیاز به تصمیم مسئول دارد و اجرا نشده است.
