# تحویل وظیفه — ISSUE-30-secure-login-authentication

- **Issue:** [#30](https://github.com/hamidreza1394/Accounting/issues/30)
- **Agent/مسئول:** WorkBuddy AI (Senior ASP.NET Core / Application Security)
- **تاریخ:** 2026-10-19
- **وضعیت:** انجام‌شده — منتظر بازبینی؛ Merge نشده

## هدف و معیارهای پذیرش
- [x] فیلد خالی: پیام فارسی از اعتبارسنجی مدل، بدون نشست، بدون بازگرداندن رمز.
- [x] اعتبارنامه اشتباه: پیام عمومی «نام کاربری یا کلمه عبور صحیح نیست.»، بدون کوکی، ثبت رویداد امنیتی.
- [x] حساب غیرفعال: پیام اختصاصی فقط پس از رمز صحیح (بدون افشای وجود حساب).
- [x] قفل موقت: حفظ سیاست فعلی (۵ تلاش ناموفق در ۱۵ دقیقه) و پیام فارسی.
- [x] ورود موفق: کوکی واقعی با تنظیمات امن، حفظ Remember Me، ثبت `LastLoginDateTime`/`LoginLog`/رویداد، هدایت به مقصد موقت محافظت‌شده.
- [x] مقصد موقت فقط پیام موفقیت را نشان می‌دهد و برای کاربر ناشناس قابل مشاهده نیست.
- [x] `ReturnUrl` فقط محلی (`Url.IsLocalUrl`) و بدون Open Redirect.
- [x] مهاجرت رمز عبور به `PasswordHasher<T>` با پذیرش هش قدیمی و ارتقای آن پس از ورود موفق.

## خلاصه تغییرات

### ۱) جریان ورود (PageModel)
- `LoginModel` اکنون از `Infrastructure.BasePageModel` ارث می‌برد و `AuthenticationService` را از **DI** می‌گیرد؛ `new` حذف شد.
- وابستگی `DbContext` از PageModel حذف شد، بنابراین `DisposeDatabaseContextAsync()` که یک سرویس Scoped تزریق‌شده را Dispose می‌کرد نیز حذف شد (نقض مالکیت چرخه عمر).
- الگوی Post/Redirect/Get: پس از `SignInAsync` پیام موفقیت در TempData ثبت و سپس Redirect انجام می‌شود.
- `ClearPassword()` هم `ViewModel.Password` و هم کلید `ViewModel.Password` را از `ModelState` حذف می‌کند (پیش‌تر فقط property پاک می‌شد).
- `GetLocalReturnUrl()` فقط URL محلی برمی‌گرداند؛ در غیر این صورت مقصد موقت.
- استثناها با `ILogger` ثبت و به کاربر پیام عمومی نمایش داده می‌شود (بدون رمز/هش/توکن در Log).

### ۲) مقصد موقت
- صفحه جدید `Server/Pages/Account/LoginSuccess.cshtml(.cs)` با `[Authorize]`.
- فقط پیام «ورود به حساب با موفقیت انجام شد. بخش‌های نرم‌افزار در مراحل بعدی تکمیل خواهند شد.» را نمایش می‌دهد (کلید جدید `Successes.LoginSucceededPendingFeatures` در `fa-IR` و neutral).
- پیام از TempData خوانده می‌شود تا پس از Redirect باقی بماند؛ در بارگذاری مجدد همان متن از منابع نمایش داده می‌شود.
- هیچ فرم، دکمه ارسال یا ماژول حسابداری در صفحه نیست.

### ۳) رمز عبور
- `Server/Infrastructure/Security/PasswordHashService.cs` اضافه شد: `PasswordHasher<Domain.User>` (PBKDF2، salt یکتا، قالب نسخه‌دار).
- `Verify` هر دو قالب را می‌شناسد: قالب جدید و هش قدیمی Base64-SHA256 (۴۴ کاراکتر، ۳۲ بایت). هش قدیمی `SuccessRehashNeeded` برمی‌گرداند.
- `AuthenticationService` پس از ورود موفق هش قدیمی را در همان رکورد ارتقا می‌دهد؛ رمزهای موجود بازنویسی یا باطل نمی‌شوند.
- هش نامعتبر/بریده در ستون دیگر `FormatException` تولید نمی‌کند و به `Failed` تبدیل می‌شود.
- `SimulateVerification` برای نام کاربری ناموجود همان هزینه محاسباتی را انجام می‌دهد (عدم افشای وجود حساب از طریق زمان پاسخ).
- رمز حساب زیرساختی (Master) هم قالب جدید و هم قالب قدیمی را می‌پذیرد؛ در صورت استفاده از قالب قدیمی فقط یک هشدار بدون مقدار در Log ثبت می‌شود.
- مسیر ایجاد کاربر ادمین (`Admin/Users/Create`) اکنون با `PasswordHashService` هش می‌سازد.

### ۴) طرح داده
- `Constants.FixedLength.PasswordHash = 255` اضافه شد و `Domain.User.Password` حداکثر طول ۲۵۵ گرفت (حداقل ۴۴ برای سازگاری با هش قدیمی حفظ شد).
- Migration `20261019071146_WidenUserPasswordHash`: `Users.Password` از `varchar(44)` به `varchar(255)`. بدون تغییر داده؛ `Down` خطر بریدن هش جدید را مستند می‌کند.
- Migration با `dotnet ef migrations add` واقعی (SDK 10.0.401) ساخته شد، نه با `scripts/generate_migrations.py`. در نتیجه `DatabaseContextModelSnapshot.cs` به قالب رسمی EF Core بازنویسی شد؛ این فایل تولیدی است و بازنویسی آن عمدی و لازم است. بررسی `dotnet ef migrations has-pending-model-changes` → «No changes have been made to the model since the last migration.»

### ۵) پیکربندی
- `Program.cs`: ثبت `PasswordHashService` (Singleton، بدون state) و `AuthenticationService` (Scoped، وابسته به `DbContext`).
- Cookie: نام صریح، `HttpOnly`، `SameSite=Lax`، `Secure=Always` در غیر Development، `SlidingExpiration`، `ExpireTimeSpan=30m` (بدون تضعیف مقادیر قبلی) + `LoginPath`/`AccessDeniedPath` صریح.
- ترتیب `UseAuthentication` → `UseAuthorization` بدون تغییر و با تست محافظت شد.

## فایل‌های مهم تغییرکرده
- `Server/Pages/Account/Login.cshtml.cs` — DI، PRG، پاک‌سازی رمز، مقصد موقت.
- `Server/Pages/Account/LoginSuccess.cshtml` + `.cshtml.cs` — مقصد موقت محافظت‌شده.
- `Server/Infrastructure/Security/PasswordHashService.cs` — هش/بررسی دوقالبی.
- `Server/Infrastructure/Security/AuthenticationService.cs` — تزریق سرویس هش، ارتقای هش، حذف SHA-256 خام.
- `Server/Program.cs` — ثبت DI و تنظیمات Cookie.
- `Server/Pages/Admin/Users/Create.cshtml.cs` — تولید هش استاندارد برای کاربر جدید.
- `Domain/User.cs`, `Constants/FixedLength.cs` — طول ستون رمز.
- `Infrastructure/Persistence/Migrations/20261019071146_WidenUserPasswordHash.cs` + Snapshot.
- `Resources/Messages/Successes.resx`, `Successes.fa-IR.resx`, `Successes.Designer.cs` — پیام موفقیت موقت.
- `tests/Template.Tests/LoginAuthenticationTests.cs` — ۳۷ آزمون جدید (۳ کلاس).
- `Directory.Packages.props`, `tests/Template.Tests/Template.Tests.csproj` — `Microsoft.EntityFrameworkCore.InMemory` فقط برای آزمون.
- مستندات: `docs/SECURITY.md`, `docs/TESTING.md`, `docs/PROJECT_STATUS.md`, `docs/database-migrations.md`, `docs/WORK_LOG.md`, همین فایل.

## تصمیم‌های فنی
- **DI به‌جای `new`:** سرویس احراز هویت به `DbContext` وابسته است؛ ساخت دستی آن چرخه عمر Scoped را نقض می‌کرد و امکان Dispose نادرست را ایجاد می‌کرد.
- **مقصد موقت جداگانه به‌جای `/Index`:** `/Index` کارت حساب و فهرست شرکت‌ها را نشان می‌دهد؛ معیار پذیرش «فقط پیام موفقیت» بود. `/Index` دست‌نخورده باقی ماند.
- **`ReturnUrl` محلی حفظ شد:** اگر مقدار محلی معتبر باشد به آن هدایت می‌شود (رفتار قبلی حفظ)، در غیر این صورت مقصد موقت. اعتبارسنجی تضعیف نشد.
- **مهاجرت تدریجی هش:** تغییر یک‌باره قالب بدون دسترسی به رمز خام ممکن نیست؛ ارتقای «در لحظه ورود موفق» امن‌ترین گزینه بدون قفل‌شدن کاربران است. حساب‌هایی که هرگز وارد نشوند هش قدیمی خود را نگه می‌دارند (ریسک مستند).
- **`PasswordHasher<T>` بدون ASP.NET Core Identity کامل:** کمترین وابستگی جدید (بدون بسته NuGet جدید؛ در shared framework موجود است) و بدون تغییر معماری.
- **InMemory provider فقط در تست:** برای آزمون جریان ورود بدون SQL Server؛ در کد اجرایی استفاده نمی‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | موفق | ۹ پروژه Restore شد؛ فقط هشدار NU1902/NU1903 برای `System.Security.Cryptography.Pkcs 9.0.11` (موجود در `main`، موضوع PR #29) |
| `dotnet build Template.sln --configuration Release` | موفق | `0 Error(s)`؛ ۱۲ هشدار (هیچ‌کدام در فایل‌های این تغییر نیست) |
| `dotnet test Template.sln --no-build --configuration Release` | موفق | `total: 90`, `failed: 0`, `succeeded: 90` (۵۳ آزمون قبلی + ۳۷ آزمون جدید) |
| `python scripts/check_static_assets.py` | موفق | `PASS: no remote CDN asset references found` |
| `git diff --check` | پاک | بدون فضای انتهایی/تعارض |
| Migration روی SQL Server واقعی | **اجرا نشده** | SQL Server در این محیط موجود نیست؛ فقط scaffold و بازبینی شد |

## امنیت، داده و سازگاری
- هیچ رمز، هش، توکن یا Connection String در Log ثبت نمی‌شود؛ پیام کاربر عمومی است.
- Anti-forgery، اعتبارسنجی سرور، نمایش/مخفی‌سازی رمز، Loading دکمه و دسترس‌پذیری صفحه ورود دست‌نخورده و با آزمون محافظت شد.
- کوکی: `HttpOnly`، `SameSite=Lax`، `Secure` در Production، انقضای ۳۰ دقیقه با Sliding — هیچ تنظیمی برای آسان‌شدن تست تضعیف نشد.
- شناسه شرکت‌ها فقط از عضویت‌های معتبر دیتابیس در Claims قرار می‌گیرد (بدون اعتماد به ورودی مرورگر).
- Migration فقط ستون را بزرگ می‌کند؛ داده‌ای حذف/بازنویسی نمی‌شود. `Down` برای هش‌های جدید داده از دست می‌دهد و در فایل Migration هشدار داده شده است.
- سازگاری: حساب‌های دارای هش قدیمی بدون مداخله وارد می‌شوند و هششان ارتقا می‌یابد.

## ریسک‌ها و کارهای باقی‌مانده
1. **مهاجرت هش کامل نشده:** حساب‌هایی که هرگز وارد نشوند هش SHA-256 خود را نگه می‌دارند. گام بعدی: اجبار به تغییر رمز یا هشدار مدیریتی برای حساب‌های بدون ورود پس از تاریخ مشخص.
2. **آزمون HTTP واقعی انجام نشد:** تست‌ها در سطح PageModel + سرویس اجرا شده‌اند (بدون `WebApplicationFactory` و بدون SQL Server). صدور واقعی کوکی روی مرورگر و رفتار `Secure` روی HTTPS باید در محیط استقرار تأیید شود.
3. **Migration روی SQL Server اجرا نشده:** پیش از استقرار باید `dotnet ef database update` (یا `--migrate`) روی دیتابیس آزمون اجرا و ستون بررسی شود.
4. **ASP.NET Core Identity کامل** (قفل حساب، تأیید ایمیل، 2FA) هنوز پیاده نشده؛ این تغییر فقط هش و جریان ورود را استاندارد می‌کند.
5. **حساب زیرساختی با قالب قدیمی** فقط هشدار Log می‌دهد؛ اپراتور باید مقدار را با هش جدید جایگزین کند (روش در `docs/SECURITY.md`).
6. آزمون «عدم بازگرداندن رمز در HTML» در سطح `ModelState` و متن کد انجام شده، نه با رندر واقعی Razor در مرورگر.

## شناسه‌های Git
- Branch: `fix/secure-login-authentication`
- Commit: در PR ثبت می‌شود
- Pull Request: در PR ثبت می‌شود
