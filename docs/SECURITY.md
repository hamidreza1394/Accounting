# چک‌لیست امنیت

## یافته‌های مهم در نسخه ورودی

> **بازبینی 2026-10-21:** این بخش و «الزامات» وضعیت تاریخی نسخه ورودی را ثبت می‌کنند؛ وضعیت فعلی در بخش «وضعیت فعلی و شکاف‌ها (2026-10-21)» انتهای این سند است. هیچ ادعای امنیتی مطلق نمی‌شود.
- MasterPassword به شکل SHA-256 Base64 در appsettings ذخیره شده بود؛ مقدار قبلی با یک رمز ضعیف/قابل حدس مرتبط بود. مقدار از تنظیمات tracked حذف شده است.
- نام کاربری و برخی Claims در Login به‌صورت hardcode نوشته شده بودند؛ نام کاربری اکنون از تنظیمات می‌آید، اما سازوکار رمز عبور همچنان legacy است.
- یک حساب مدیر با ایمیل/شماره تلفن و رمز عبور آزمایشی در EF Core `HasData` تعریف شده بود؛ این Seed ثابت حذف شد تا حساب پیش‌فرض و اطلاعات شخصی نمونه ساخته نشوند.
- کلیدهای Activation در تنظیمات tracked بودند؛ مقادیر حذف شده‌اند. (به‌روزرسانی: Middleware Activation اکنون کد مرده است و به Pipeline متصل نیست — D-028.)
- مسیر Google OAuth در صفحه ورود هست ولی Handler ثبت نشده (AUD-2026-10-21-02)؛ Client Secret واقعی نباید در مخزن ذخیره شود.
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


## امنیت راه‌اندازی اولیه دیتابیس — Issue #22 (بازطراحی 2026-10-23)

> جزئیات و ماتریس رفتار: [`DATABASE_SETUP.md`](DATABASE_SETUP.md)؛ تصمیم D-036 (`NeedsApproval`). `Implemented` در شاخه `feature/database-setup-redesign` (ادغام‌نشده). بازبینی مستقل امنیتی (opus) در دو دور: بدون Critical/High/Medium پس از اصلاح؛ یافته‌های F1 و F2 (Medium) و N1..N3 (Low) اصلاح شدند. رفتار Windows (DPAPI/Service/IIS/UNC/`HardenWindowsAcl`) اجرا نشده.

- صفحه تنظیمات فقط وقتی در دسترس است که Connection String از Configuration یا فایل محلی قابل استفاده نباشد. حالت‌ها: Configured / Setup / Blocked؛ فایل موجودِ ناخوانا (`Corrupt`، `Undecryptable`، `LegacyUnreadable`) هرگز صفحه ساده first-run نمی‌دهد و جایگزینی آن کد راه‌اندازی **و** تیک تأیید می‌خواهد و فایل قبلی ابتدا در `backups/` کپی می‌شود. در `AccessDenied` و `UnsupportedVersion` (Blocked) فرم ارائه نمی‌شود و POST جعلی چیزی را تغییر نمی‌دهد.
- دسترسی اولیه با کد تصادفی یک‌بارمصرف (`setup-token.txt` در پوشه تنظیمات، خارج از مخزن) کنترل می‌شود؛ مقایسه با `CryptographicOperations.FixedTimeEquals`؛ در Log نمی‌آید و پس از ذخیره موفق حذف می‌شود. اپراتور آن را فقط روی میزبان برنامه بخواند.
- Connection String کامل با ASP.NET Core Data Protection محافظت می‌شود (`ApplicationName=Farano`، Purpose `Farano.DatabaseConnectionSettings.v2`، حلقه کلید تازه). در Windows کلیدها با DPAPI (پیش‌فرض: وابسته به حساب کاربر جاری) محافظت می‌شوند؛ در Unix مجوز پوشه `700` و فایل `600`. در Windows `HardenWindowsAcl` وراثت را قطع و FullControl را فقط به کاربر جاری، SYSTEM و Administrators می‌دهد (هرگز استثنا پرتاب نمی‌کند؛ شکست = هشدار `WindowsAclNotApplied`)؛ **این کد روی Windows اجرا نشده (فقط کامپایل شده)**. UNC بدون هشدار در کد پذیرفته می‌شود و ACL آن با اپراتور است. حلقه کلید در Linux در حالت rest رمز نیست و با مجوز `700` محافظت می‌شود.
- محل ذخیره: `Farano:Settings:Directory` یا `LocalApplicationData/Farano/Settings`؛ مسیر سفارشی باید absolute و بیرون پوشه برنامه باشد و هرگز به پوشه برنامه fallback نمی‌شود. ProgramData و DPAPI سطح ماشین رد شدند (D-036).
- نام دیتابیس با فهرست مجاز (حرف/رقم/`_`/`-`/نقطه و فاصله میانی؛ حداکثر ۱۲۳ نویسه؛ ZWNJ و سایر نویسه‌های `Cf` رد) و نرمال‌سازی NFKC برای نام سیستمی بررسی می‌شود و مرجع نهایی `DB_ID` سمت سرور است (۱ تا ۴ ← `SystemDatabaseNotAllowed`): نام هم‌ریخت مثل «ｍａｓｔｅｒ» پیش‌تر می‌توانست دور بزند (یافته F1، با آزمایش تأیید شد، اصلاح شد). فهرست مجاز چون نام فایل‌های `.mdf/_log.ldf` از نام ساخته می‌شود لازم است.
- راه‌اندازی یک‌بارمصرف است: پس از مصرف کد یا تغییر وضعیت فایل‌ها `AlreadyConfigured`؛ `Save` هم زیر قفل وضعیت را دوباره می‌سنجد؛ کد راه‌اندازی بازتاب داده نمی‌شود؛ تنظیم‌شدن مسیر پوشه با مقایسه روی مسیر واقعی (لینک‌ها) و ممنوعیت پوشه برنامه، ریشه محتوا و `WebRoot`.
- **ملاحظه SSRF:** دارنده کد راه‌اندازی می‌تواند اتصال خروجی به سرور/پورت دلخواه را از میزبان آزمایش کند. محدودیت نرخ برای حدس کد وجود ندارد (کد ۱۹۲ بیتی). صفحه در حالت Setup/Blocked مسیرهای پوشه سرور را به هر کسی که به صفحه می‌رسد نشان می‌دهد (پیگیری، `NeedsApproval`). صفحه را فقط روی شبکه مورد اعتماد بگذارید.
- فرمان `--settings` را با همان حساب سرویس اجرا کنید؛ `status` ممکن است با چرخش کلید در `keys/` بنویسد.
- فایل قدیمی `TarazAva` فقط‌خواندنی است و هرگز حذف یا تغییر نمی‌شود؛ دیتابیس هرگز `DROP` نمی‌شود؛ فایل‌های `.mdf/.ldf` را برنامه نمی‌سازد/جابه‌جا نمی‌کند.
- از مسیر تنظیمات و `keys/` نسخه پشتیبان امن بگیرید؛ بدون کلید ciphertext قابل بازیابی نیست. دسترسی پوشه فقط برای حساب سرویس/کاربر مجاز باشد.
- در شبکه‌های غیرقابل‌اعتماد، صفحه راه‌اندازی را فقط پشت HTTPS منتشر کنید. کد یک‌بارمصرف جایگزین TLS یا کنترل دسترسی میزبان نیست.
- `TrustServerCertificate`: اتصال همیشه `Encrypt=Mandatory` است؛ این گزینه فقط **اعتبارسنجی گواهی سرور را خاموش می‌کند** و در برابر MITM آسیب‌پذیر است؛ فقط برای سرور محلی/آزمایشی، در تولید گواهی معتبر.
- خطاها فقط با کد خطا، مرحله و شناسه پیگیری (۱۲ hex) نمایش داده و Log می‌شوند؛ Connection String، نام کاربری و رمز در پیام و Log نیستند (`DatabaseSetupRequest.ToString()` و `DatabaseSettingsLoadResult.ToString()` مقدار حساس چاپ نمی‌کنند). فرمان `--settings status` هرگز Connection String چاپ نمی‌کند.
- **ریسک باقی‌مانده:** برنامه تنظیم سراسری `AddDataProtection` ندارد؛ Cookie ورود و Anti-forgery از حلقه کلید پیش‌فرض فریمورک استفاده می‌کنند (پیگیری جداگانه؛ `docs/DEPLOYMENT.md` بخش ۵). نام Farano/Faranou در این جریان `NeedsApproval` است (`DATABASE_SETUP.md` بخش ۱).

## احراز هویت صفحه ورود — Issue #30

- رمز عبور کاربران اکنون با `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (PBKDF2، salt یکتا برای هر رمز، قالب نسخه‌دار) هش می‌شود. هش قدیمی `Base64(SHA-256)` دیگر تولید نمی‌شود.
- سازگاری: هش قدیمی هنوز پذیرفته می‌شود و پس از نخستین ورود موفق در همان رکورد با هش استاندارد جایگزین می‌شود (`SuccessRehashNeeded`). حساب‌هایی که هرگز وارد نشوند هش قدیمی خود را نگه می‌دارند؛ پیش از Production باید یا وارد شوند یا رمزشان بازنشانی شود.
- مقدار هش نامعتبر/بریده در ستون، ورود را با شکست مواجه می‌کند (نه `FormatException`).
- برای نام کاربری ناموجود، همان هزینه محاسباتی بررسی رمز انجام می‌شود تا زمان پاسخ وجود حساب را افشا نکند. حساب‌هایی که هنوز هش قدیمی دارند نیز همین هزینه را می‌پردازند: پس از مقایسه ثابت‌زمانی SHA-256، یک بررسی ساختگی با هش‌کننده فعلی (PBKDF2) اجرا می‌شود که نتیجه‌اش نادیده گرفته می‌شود و برای رمز درست و نادرست یکسان اجرا می‌گردد. هدف، کاهش اختلاف آشکار هزینه دو مسیر است، نه برابری کامل زمان پاسخ؛ اختلاف‌های جزئی در حد نویز باقی می‌ماند و هیچ تضمین ریاضی برای یکسان‌بودن زمان دو مسیر وجود ندارد.
- حساب زیرساختی (`ApplicationSettings:MasterPassword`) هم قالب جدید و هم قالب قدیمی را می‌پذیرد؛ روش تولید مقدار جدید و قواعد آن در بخش «تنظیم حساب زیرساختی (Master)» در بالای همین سند آمده است. اگر مقدار قدیمی پیکربندی شده باشد، در هر ورود موفق یک هشدار (بدون مقدار) در Log ثبت می‌شود.
- ستون `Users.Password` به `varchar(255)` بزرگ شده است (Migration `WidenUserPasswordHash`)؛ Migration داده‌ای را تغییر نمی‌دهد.
- جریان ورود: Post/Redirect/Get، پیام عمومی برای اعتبارنامه اشتباه، پیام اختصاصی غیرفعال/قفل، حذف رمز از `ModelState` پیش از رندر پاسخ، و هدایت به `ReturnUrl` محلی یا مقصد ثابت و محلی بر اساس نوع حساب (`/app/` یا `/`)؛ `ReturnUrl` به صفحات `/Account/Login*` نادیده گرفته می‌شود و `/Account/LoginSuccess` فقط با `[Authorize]` به همان مقصد Redirect می‌کند. مقصد هیچ دسترسی داده‌ای نمی‌دهد: شرکت و دوره را سرور هنگام `POST /api/v1/context` دوباره اعتبارسنجی می‌کند.
- `ReturnUrl` فقط با `Url.IsLocalUrl` پذیرفته می‌شود؛ در غیر این صورت مقصد موقت.


## API و جداسازی شرکت‌ها — 2026-10-20
- API نسخه 1 فقط با Cookie احراز هویت (HttpOnly، SameSite=Lax، Secure در Production) کار می‌کند؛ توکنی در JavaScript یا `localStorage` نیست و CORS فعال نیست (D-012).
- درخواست تغییردهنده API (فعلاً `POST /api/v1/session/logout`) بدون هدر معتبر `X-XSRF-TOKEN` رد می‌شود (400). خروج صفحات Razor هم فقط با POST و Anti-forgery است؛ پیوند GET دیگر نشست را پایان نمی‌دهد.
- دسترسی به شرکت فقط از عضویت فعال در دیتابیس (کاربر فعال + عضویت فعال + شرکت فعال + نقش فعال همان شرکت) تعیین می‌شود و در هر درخواست دوباره بررسی می‌شود؛ Claim یا پارامتر کلاینت مبنای دسترسی نیست (D-013). شرکت غیرمجاز و ناموجود پاسخ یکسان 404 دارند.
- پاسخ‌های API `Cache-Control: no-store` دارند. خطای غیرمنتظره با جزئیات فقط در Log سرور ثبت و به کاربر پیام عمومی داده می‌شود.
- جلسه‌هایی که پیش از این تغییر صادر شده‌اند Claim شناسه کاربر ندارند؛ API برای آن‌ها 401 برمی‌گرداند و کاربر باید دوباره وارد شود. داشبورد Razor برای سازگاری، شناسه را از نام کاربری پیدا می‌کند.
- **باقی‌مانده (تاریخی؛ ۲۰۲۶-۱۰-۲۰):** این فهرست منسوخ است — CSP، اعتبارسنجی مجدد نشست و Rate Limiting در بازبینی‌های بعدی همین سند انجام شده‌اند (D-014، D-015)؛ فقط OpenAPI باقی است.


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


## پیگیری آمادگی Production — 2026-10-20
- **Proxy و میزبان (D-017):** اعتماد فقط به Proxyهای فهرست‌شده؛ پیش‌فرض هیچ. `X-Forwarded-Host` پذیرفته نمی‌شود. برای `AllowedHosts=*` هشدار شروع برنامه ثبت می‌شود. جزئیات: `docs/DEPLOYMENT.md`.
- **CSP (D-018):** inlineهای این صفحات منتقل شدند: `_DisplayToastMessages`، `DatabaseSetup`، `Admin/MenuItems/Create|Update`، `Admin/PageManager/Delete`، `Account/LoginSuccess`، `Account/Login`، `_DisplayPictures` و پاورقی‌ها. CSP سخت‌گیرانه Razor اکنون اجباری است، به‌جز `/Admin/PageManager` (TinyMCE) که Report-Only مانده. تست `ContentSecurityPolicyTests` بازگشت inline و `javascript:` را منع می‌کند و E2E هیچ نقض اجباری در صفحات ورود، خانه، داشبورد، خروج، مدیریت و راه‌اندازی اولیه نیافت.
- **گزارش CSP:** `POST /csp-report` پیش‌فرض خاموش است (`CspReportingEnabled`). فقط Media Typeهای گزارش و حداکثر ۸KB پذیرفته می‌شود، Rate Limit `CspReport` دارد (۲۰ در دقیقه به ازای آدرس) و Log پاک‌سازی‌شده بدون ذخیره ثبت می‌شود.
- **Master (D-019):** مراحل در `docs/DEPLOYMENT.md` بخش ۶ آمده است. هیچ رمز یا هش واقعی در Git یا تست ثابت وجود ندارد؛ تست‌ها رمز را در زمان اجرا تولید می‌کنند.
- **سازگاری کاربران:** منطق ورود کاربران عادی تغییر نکرده است. کاربران فعلی (با هش جدید یا قدیمی) بدون تغییر رمز وارد می‌شوند. فقط ارتقای Master یا خاموش‌کردن قالب قدیمی Master نیاز به ورود مجدد Master دارد.
- **رفع اشکال:** نام فایل‌های فونت در `fontiran.css` با حروف کوچک بود (`IRANSansX-thin`/`light`) و روی میزبان حساس به حروف (Linux) 404 می‌داد. اصلاح شد و یک تست وجود همه URLهای محلی CSS را با حروف دقیق بررسی می‌کند.


## وضعیت فعلی و شکاف‌ها (2026-10-21)
مبنا: `main`@`1d39dbf`؛ شاهد اجرا: CI `39585708662` (این بازبینی مستنداتی خودش تست اجرا نکرده است).

| موضوع | وضعیت |
|---|---|
| Cookie امن، Anti-forgery، اعتبارسنجی مجدد نشست، CSP، Rate Limiting | `Implemented` و در تست‌های CI پوشش دارد (D-012، D-014، D-015) |
| جداسازی شرکت با `CompanyAccessService` | `Implemented` (D-013)؛ بدون Global Query Filter (عمدی) |
| مجوز عملیاتی نقش‌ها | `NotImplemented` — در `Server/` مصرف نمی‌شود (D-025) |
| لایسنس/فعال‌سازی | `NotImplemented`؛ Activation قدیمی کد مرده و قابل جعل بود (D-028) |
| `UserCompanyMemberships.UserId` بدون FK | شکاف؛ ریسک عضویت یتیم (D-029) |
| هش SHA-256 قدیمی | هنوز پذیرفته می‌شود؛ برنامه حذف نیازمند تصمیم (بالاتر) |
| اسکن اسرار در CI | در `dotnet.yml` اسکن اختصاصی اسرار گزارش نشده است؛ بررسی لازم |
| Branch protection مخزن | **نامشخص** (از خارج مخزن قابل تأیید نیست) |

### مرز هویت‌ها (D-029)
نقش `Admin` صفحات قدیمی، حساب Master زیرساختی، نقش/مجوز شرکتی و مدیر فروشنده چهار دامنه جدا هستند. مدیر فروشنده هرگز نباید ردیف `Users` نصب مشتری یا نقش عادی باشد. حساب پشتیبان دائمی یا دسترسی پنهان در نصب اختصاصی ممنوع است.

### الزامات امنیتی لایسنس (طرح؛ `NotImplemented`)
- کلید خصوصی صدور هرگز در Git، بسته مشتری، دیتابیس نصب یا Log نباشد؛ فقط در Secret Store/ماژول سخت‌افزاری فروشنده (D-024).
- بررسی لایسنس سمت سرور؛ پنهان‌کردن منو در React کنترل امنیتی نیست.
- امضا روی بایت‌های دقیق Payload با `kid` و `sequence` یکنواخت؛ `requestId` یک‌بارمصرف.
- هیچ داده مالی یا رمز در فایل درخواست/پاسخ لایسنس نباشد.
- کپی کامل VM در نصب آفلاین قابل جلوگیری نیست؛ ادعای مصونیت نکنید. جزئیات: `docs/LICENSING_THREAT_MODEL.md`.
- «اعلام مسئولانه آسیب‌پذیری»: در این مأموریت مورد فوری جدید که نیازمند اصلاح کد باشد ثبت نشد؛ موارد باز در `REPOSITORY_AUDIT.md` است.

## زمینه کاری: شرکت و دوره مالی (D-031) — 2026-10-21
- انتخاب فقط سمت سرور اعتبار دارد: شرکت از طریق `CompanyAccessService` (عضویت فعال در دیتابیس) و دوره با شرط `TenantId` شرکت تأییدشده جست‌وجو می‌شود؛ شناسه‌ها و Claimهای ارسالی کلاینت مرجع اعتماد نیستند (D-013).
- شرکت غیرمجاز، دوره ناموجود و دوره شرکت دیگر پاسخ 404 یکسان می‌گیرند (بدون نشت وجود داده)؛ دوره غیرقابل‌انتخاب 409؛ `POST`/`DELETE` نیازمند Anti-forgery (400 در غیر این صورت).
- Cookie `.Faranou.WorkContext`: Data Protection با Purpose جدا و محدودیت زمانی، HttpOnly، SameSite=Lax، Secure خارج از Development، بدون Expires؛ وابسته به کاربر و `SessionId` هر ورود، حذف در ورود/خروج؛ دست‌کاری/انقضا/کاربر یا ورود دیگر = انتخاب‌نشده. هیچ مقدار Cookie در Log نمی‌آید.
- لغو عضویت یا غیرفعال‌شدن شرکت در درخواست بعدی اثر می‌کند چون هر خواندن دوباره با دیتابیس سنجیده می‌شود. دوره `Closed` فقط‌خواندنی (`accessMode=readOnly`) است و `Archived` قابل ورود نیست (D-032)؛ **هر Endpoint نوشتن آینده باید `WorkContext.AllowsWrites` را سمت سرور بررسی کند** (فعلاً Endpoint نوشتن حسابداری وجود ندارد).
- تغییر زمینه حین نشست فقط با `POST /api/v1/context` (Anti-forgery، بررسی عضویت و تعلق دوره) انجام می‌شود، سطح دسترسی را بالا نمی‌برد و در `SecurityAuditEvents` با `WorkContextSelected`/`WorkContextChanged`/`WorkContextSelectionDenied` ثبت می‌شود (فقط نام رویداد و شناسه کاربر؛ بدون Cookie/توکن).
- **محدودیت‌ها (ادعای امنیت کامل نمی‌شود):** Cookie دزدیده‌شده تا پایان همان نشست قابل استفاده است (هم‌تراز Cookie ورود)؛ نشست‌های قدیمی بدون `SessionId` برای انتخاب باید دوباره وارد شوند؛ تست‌ها با Provider حافظه‌ای محلی اجرا شده‌اند و نتیجه روی SQL Server در CI ثبت نشده است.

## گروه‌های شرکتی و گزارش گروه (D-034) — 2026-10-22
- عضویت شرکت در گروه، نقش هلدینگ یا مالکیت **هیچ دسترسی داده‌ای** نمی‌دهد. هر عملیات گروه با `CompanyAccessService.HasPermissionAsync` (کاربر فعال + عضویت فعال + شرکت فعال + نقش فعال + تخصیص کد مجوز) سنجیده می‌شود؛ جدول کدها و محل سنجش: `docs/CORPORATE_GROUPS.md` بخش ۵.
- گزارش گروه مجوز را در شرکت مالک **و همه** شرکت‌های محدوده می‌خواهد؛ کمبود حتی در یک شرکت کل درخواست را رد می‌کند و نام آن شرکت افشا نمی‌شود.
- گروه ناموجود، غیرفعال یا بدون مجوز مشاهده نتیجه یکسان `NotFound` دارد. شناسه‌های ارسالی گروه/شرکت/دوره/پروفایل فقط «درخواست»اند؛ تعلق دوره به شرکت و پروفایل به گروه در سرویس و با FK ترکیبی دیتابیس کنترل می‌شود.
- ممیزی در `SecurityAuditEvents` (رویدادهای `CorporateGroup*`، `OwnershipRelationship*`، `ConsolidationRun*`، `CorporateGroupAccessDenied`)؛ بدون عدد مالی، Cookie یا توکن.
- نقش `Admin` قدیمی و حساب Master دسترسی ندارند (D-029). Global Query Filter لایه امنیتی کافی شمرده نمی‌شود و اضافه نشد (D-013).
- **محدودیت‌ها:** API، خروجی Excel/PDF و Job پس‌زمینه گروه هنوز وجود ندارند؛ هر کدام در آینده باید همین سرویس را با شناسه کاربر اجراکننده فراخوانی کنند و تست منفی داشته باشند. تخصیص مجوز به نقش‌ها هنوز UI ندارد.

### ریسک‌های باقی‌مانده گروه شرکتی (بازبینی امنیتی مستقل 2026-10-22)
- **بدون یافته Critical/High/Medium.** یافته‌های Low اصلاح شدند: کد گروه یکتا در هر شرکت مالک (جلوگیری از کاوش وجود گروه شرکت دیگر)، نگاشت Deadlock/یکتایی به `Conflict` و پاک‌سازی ChangeTracker، ایندکس `ControlBasis`، ردیف ممیزی برای شرکت عضو/فرعی، پاسخ یکسان `NotFound` در افزودن و پایان عضویت، فیلتر گروه‌ها در SQL.
- **پذیرفته‌شده/باز:** (۱) `ControlCycle` ممکن است وجود زنجیره کنترلی از طریق شرکت‌های نامرئی را نشان دهد (D-034)؛ (۲) خطاهای جمع درصد و کنترل یگانه از روابط مالکان دیگرِ همان شرکت فرعی می‌آیند (کاربر در فرعی مجوز دارد)؛ (۳) ممیزی ردهای دسترسی محدودیت نرخ ندارد — Endpointهای آینده باید پشت Rate Limiter موجود باشند؛ (۴) Down کاتالوگ‌های تخصیص‌یافته را نگه می‌دارد، پس Up بعدی همان دسترسی‌ها را برمی‌گرداند (عمدی)؛ (۵) سرویس `userId` را از فراخوان می‌گیرد: Endpoint آینده فقط از `CompanyAccessService.GetUserId(User)` با `[Authorize]` و Anti-forgery استفاده کند و نام گروه/پروفایل را در خروجی Encode کند.

## کوکی ترجیح نام کاربری — بازبینی PR #67 (2026-10-23)
- `.Faranou.RememberedUsername` فقط پس از ورود موفق و تیک صریح کاربر نوشته می‌شود و فقط نام کاربری دارد؛ HttpOnly، SameSite=Lax، Path=/، ۳۶۵ روز، `Secure` بیرون از Development (هم‌سو با کوکی احراز هویت). رمز هرگز در کوکی/Storage برنامه نمی‌رود؛ ذخیره رمز با Password Manager مرورگر و تأیید کاربر است.
- مقدار هنگام خواندن اعتبارسنجی می‌شود (طول ≤ `MaxLength.Username`، بدون کاراکتر کنترلی) و کدگذاری‌شده نمایش داده می‌شود. با ورود بدون تیک (حتی ناموفق) حذف می‌شود؛ خروج از حساب آن را عمداً نگه می‌دارد. ریسک: دیده‌شدن نام کاربری روی رایانه مشترک.

## مرز مشتری، شرکت پیش‌فرض و Endpointهای اپراتور (D-037..D-040) — 2026-10-23
وضعیت: `Implemented`؛ `Verified` ادعا نمی‌شود؛ CI اجرا نشده (سقف صورتحساب GitHub). بازبینی معماری و امنیتی مستقل روی Diff این کار انجام شد؛ نتیجه آن در handoff `docs/handoffs/ISSUE-66-customer-boundary-default-company-audit.md` و D-038 ثبت شده است.

- **اصل:** `Companies.CustomerBoundaryId` مات و بدون PII است، دسترسی نمی‌دهد و فقط محدود می‌کند؛ تعیین دسترسی همچنان فقط با `CompanyAccessService` (D-013). مرز `null` یعنی Fail-closed. گروه/عضویت/مالکیت فقط داخل یک مرز Provision‌شده و بررسی مرز **داخل** تراکنش‌های Serializable تکرار می‌شود؛ روابط قدیمیِ مرز دیگر در اعتبارسنجی نوشتن نه اثر می‌گذارند نه چیزی فاش می‌کنند. API/UI گروه وجود ندارد.
- **`POST /api/v1/operator/customer-boundaries/assign`:** فقط حساب زیرساختی (غیر آن 404)؛ Anti-forgery؛ Rate Limit `api-sensitive`؛ ۱ تا ۵۰۰ شناسه متمایز؛ 404 برای شناسه ناشناخته؛ 409 برای مرز متفاوت موجود یا عبور رابطه/گروه موجود از مرز؛ تراکنش Serializable؛ Deadlock/Snapshot Conflict (SQL 1205/3960) → 409.
- **`GET /api/v1/operator/customer-boundaries/diagnostics`:** فقط‌خواندنی، فقط حساب زیرساختی (غیر آن 404)؛ شمارش‌ها و حداکثر ۲۰۰ شناسه شرکت حل‌نشده؛ بدون نام.
- **ممیزی:** Provision فقط جزئیات کران‌دار ثبت می‌کند (تعداد + SHA-256 شناسه‌های مرتب‌شده + نام کاربری اپراتور). یافته F1 بازبینی امنیتی: ثبت فهرست شناسه‌ها ستون `DetailsJson` (۴۰۰۰ نویسه) را با بیش از حدود ۱۰۰ شناسه سرریز می‌کرد؛ اصلاح شد. ردها (409 و Anti-forgery) با رویداد `CustomerBoundaryProvisionRejected` و نام اپراتور ثبت می‌شوند.
- **برچسب‌های رابطه:** `unknown` برای مرز `null` یا هر واقعیت مرتبط Provision‌نشده/متقاطع، یک بیت غیرقابل‌تمایز است تا وجود شرکت/مشتری دیگر فاش نشود. `independent` یعنی «رابطه ثبت‌شده‌ای نیست»، نه استقلال حقوقی (D-040).
- **شرکت پیش‌فرض:** `POST /api/v1/context/default-company` با Anti-forgery؛ 404 یکسان برای شرکت ناموجود/غیرمجاز؛ `GET /api/v1/context` پیش‌فرض را فقط در صورت دسترس‌پذیر بودن برمی‌گرداند.

**ریسک‌های باقی‌مانده (پذیرفته/باز؛ `NeedsApproval` برای ردیف‌های دارای برچسب):**
1. اعمال مرز فقط در سطح برنامه است؛ SQL مستقیم یا مسیر نوشتن آینده آن را دور می‌زند. تریگر دیتابیس که به‌روزرسانی «غیرتهی ← متفاوت/تهی» را رد کند پیشنهاد دنبال‌کار است (`NeedsApproval`).
2. اپراتور حساب Master محلی است (راز مشترک، بدون MFA؛ فقط Rate Limit و ممیزی). انتقال به سامانه کنترلی فروشنده (D-021، `NotImplemented`).
3. Provision به GUID اپراتور اعتماد می‌کند؛ اشتباه تایپی می‌تواند شرکت‌های دو مشتری را در یک مرز بگذارد (F5). کاهش پیشنهادی: پارامتر «تعداد اعضای مورد انتظار»/`expectNewBoundary` (پیاده‌سازی نشده).
4. رویه اصلاح/ادغام/تفکیک/فروش شرکت (مرز غیرتهی از راه API تغییر نمی‌کند) نیازمند رویه ممیزی‌شده جدا با تراکنش Serializable و بررسی مجدد روابط است: `NeedsApproval`، `NotImplemented`.
5. برچسب‌ها برای هر عضو شرکت بدون `corporate-groups.view` دیده می‌شوند (فقط روابط داخل یک مرز)؛ تصمیم محصول `NeedsApproval`. اصطلاحات برچسب‌ها نیز `NeedsApproval`.
6. Endpoint تشخیصی همه ردیف‌ها را بارگذاری می‌کند (فقط اپراتور، Rate Limit) و عضویت‌های پایان‌یافته/گروه‌های غیرفعال را محافظه‌کارانه می‌شمارد.
7. کپی کامل VM آفلاین کنترل‌پذیر نیست (D-027) و مرز کنترل لایسنس نیست. درون‌ریزی یک شرکت به دیتابیس ابری مشترک باید با `null` وارد شود.
8. اجرا نشده: Firefox/Safari، axe، HTTPS پشت Proxy، CI.

## Vendor control-plane authentication (Issue #103)
- Uses ASP.NET Core Identity 10 built-in TOTP and WebAuthn/passkey APIs; no custom cryptographic/WebAuthn protocol code.
- Password-only sign-in is rejected for MFA-unenrolled accounts. Password+TOTP uses the short-lived Identity two-factor cookie, which is not sufficient for protected endpoints.
- Passkey assertion is validated before the final application cookie is issued. The administrator must remain enabled and the updated credential counter is persisted.
- The fallback authorization handler re-reads the administrator from the Vendor DB and requires an enabled account plus an amr claim matching an enrolled factor.
- Every auth POST validates antiforgery; credential attempts are rate-limited by remote IP. Auth responses and WebAuthn/TOTP setup payloads are not cacheable.
- Production requires a fully qualified persistent Data Protection key directory, an explicit non-wildcard AllowedHosts list, and an explicit passkey relying-party domain.
- Branch-only until CI/review/committed migration complete. Bootstrap, granular RBAC, maker-checker, independent audit checkpoints, and UI remain out of scope.
## سامانهٔ مرکزی Vendor — زنجیرهٔ ممیزی (Issue #105)
- Writer زنجیرهٔ ممیزی در شاخهٔ Issue #105 با شناسه‌های قابل‌اعتبارسنجی، بدون متن آزاد/PII، hash نسخه‌دار و قفل تراکنشی SQL Server پیاده شده است؛ تا ادغام/CI `NotVerified` است.
- Hash-chain در همان پایگاه داده، تغییر یک رخداد را تشخیص می‌دهد اما بازنویسی کامل توسط DBA را متوقف نمی‌کند. برای Production، حساب SQL برنامه باید کمترین دسترسی لازم و بدون UPDATE/DELETE روی جدول ممیزی داشته باشد؛ مهاجرت/حساب اجرایی نیازمند تفکیک مجوز است.
- پیش از ادغام عملیات حساس: Verify chain باید fail-closed عمل کند؛ شکست اعتبارسنجی نباید به صدور لایسنس، تغییر مدیر یا عملیات حساس اجازه دهد.
- Checkpoint/export به مقصد مستقل، پایش شکست/انحراف، retention و runbook بازیابی هنوز پیاده نشده‌اند؛ هیچ ادعای ممیزی تغییرناپذیر/Production-ready مطرح نمی‌شود.


## زیرساخت RBAC در Vendor — Issue #150 (هنوز تأیید Production نشده)
- در Host فروشنده، کاتالوگ نسخه‌دار مجوزها تعریف شده است. شناسه مجوز با نام نقش یا نمایش/مخفی‌بودن اجزای UI یکی نیست.
- سیاست‌های نام‌دار مجوز، گارد موجود حساب فعال و MFA را همراه با مجوز مشخص اعمال می‌کنند.
- مجوزها از Claimهای نقش ASP.NET Core Identity با نوع `vendor:permission:v1` خوانده می‌شوند؛ شناسه‌های ناشناخته باید رد شوند.
- این تغییر فقط زیرساخت اولیه مجوزدهی است. API/UI مدیریت نقش و مجوز، کنترل منبع‌محور مشتری/نصب/قرارداد/لایسنس، گردش‌کار Maker-Checker و checkpoint مستقل ممیزی هنوز تکمیل نشده‌اند.
- تا موفقیت Vendor CI، تست‌های Integration روی SQL Server، گارد Snapshot مدل EF، بررسی بسته انتشار و CI کامل مخزن، این پیاده‌سازی تأییدشده برای Production محسوب نمی‌شود.


## پیگیری امنیت Vendor — گزینه‌های Passkey (Issue #152، 2026-10-26)
- گزینه‌های احراز هویت Passkey نباید بر اساس وجود/وضعیت حساب، فهرست credential متفاوتی افشا کنند.
- پیاده‌سازی در شاخه اصلاحی برای تمام درخواست‌ها از MakePasskeyRequestOptionsAsync(user: null) استفاده می‌کند؛ این روش برای discoverable credentials/ورود بدون نام کاربری مستند شده است.
- وضعیت: Implemented / Not Verified تا زمانی که آزمون HTTP رگرسیون و CI کامل سبز شوند. Anti-forgery، rate limiting و Cache-Control: no-store باید حفظ شوند.
- مرجع: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys?view=aspnetcore-10.0
