# تحویل وظیفه — بازطراحی صفحه ورود و احراز هویت فرانو

- **Pull Request:** https://github.com/hamidreza1394/Accounting/pull/26
- **Branch:** `arena/f7b0ee95-accounting`
- **Commit:** `ceac89f`
- **تاریخ:** 2026-10-17
- **وضعیت:** آماده بازبینی — CI سبز (build/test/migration checks)

## ۱. خلاصه تغییرات

بازطراحی کامل صفحه ورود با برند «فرانو» + اتصال دکمه «ورود به سامانه» به منطق واقعی احراز هویت (جدول `Users` + حساب زیرساخت Master)، اصلاح سیاست نام کاربری/کلمه عبور، قفل موقت تلاش‌های ناموفق، ثبت رویدادهای امنیتی، لوگوی رسمی و فونت ایران‌سنس.

## ۲. فایل‌های تغییرکرده

| فایل | علت |
|---|---|
| `Server/Pages/Account/Login.cshtml` | بازطراحی کامل: فرم سمت راست، پنل معرفی سمت چپ، سه متن الزامی، دسترس‌پذیری |
| `Server/Pages/Account/Login.cshtml.cs` | منطق واقعی ورود (AuthenticationService)، سناریوهای خطا، claims، پیام‌ها |
| `Server/Infrastructure/Security/AuthenticationService.cs` | **جدید:** اعتبارسنجی کاربران + Master، قفل موقت، SecurityAuditEvents، LoginLog |
| `Server/wwwroot/css/rtl/taraz-login.css` | بازنویسی طراحی روشن/حرفه‌ای + @font-face ایران‌سنس با مسیر صحیح فایل‌ها |
| `Server/wwwroot/js/taraz-login.js` | نمایش/مخفی‌سازی دسترس‌پذیر رمز، جلوگیری از ارسال هم‌زمان، فوکوس خطا |
| `Server/wwwroot/images/faranou-logo.jpg` | لوگوی رسمی ارائه‌شده (بدون تغییر محتوا) |
| `Server/wwwroot/images/faranou-mark.svg` | **جدید:** mark برداری مشتق از لوگوی رسمی |
| `Server/wwwroot/images/faranou-app-icon.png`, `faranou-favicon-*.png` | **جدید:** آیکون اپلیکیشن/Favicon از لوگوی رسمی |
| `ViewModels/Pages/Account/LoginViewModel.cs` | حذف محدودیت ساختاری رمز/نام کاربری از ورود؛ فقط Required + سقف طول |
| `ViewModels/Pages/Account/RegisterViewModel.cs`, `ViewModels/Pages/Admin/Users/CreateViewModel.cs`, `CommonViewModel.cs` | سیاست یکپارچه ایجاد: نام کاربری ۳–۲۰ (مجموعه Identity)، رمز ۸–۱۲۸ + رایج‌ها |
| `ViewModels/Validation/NotCommonPasswordAttribute.cs` | **جدید:** کنترل رمزهای رایج/افشاشده هنگام ایجاد/تغییر رمز |
| `Constants/RegularExpression.cs`, `MaxLength.cs`, `MinLength.cs` | سیاست جدید (حذف الگوی رمز، افزودن UserPassword=128/8) |
| `Resources/*` (DataDictionary, ButtonCaptions, Errors, Successes, Validations + Designer) | برند فرانو، «نام کاربری/کلمه عبور»، پیام‌های جدید (قفل، فیلدهای الزامی، ورود موفق) |
| `Server/Pages/Index.cshtml` + `.cs` | صفحه مقصد محافظت‌شده `[Authorize]` با پیام موفقیت و شرکت‌های فعال |
| `Server/Pages/Shared/PartialViews/_Seo.cshtml` | عنوان مرورگر با «فرانو» + متادیتای محصول |
| `Server/Pages/Shared/PartialViews/_FavoriteIcon.cshtml` | favicon از mark فرانو |
| `tests/Template.Tests/LoginPolicyTests.cs` | **جدید:** ۲۰ تست سیاست ورود/نام کاربری/رمز و کمک‌های احراز هویت |

## ۳. UI/UX، لوگو و فونت

- چیدمان دقیقاً معکوس شد: **فرم ورود سمت راست**، **پنل معرفی سمت چپ** (با placement فیزیکی در RTL و LTR).
- سه متن الزامی با املا دقیق در پنل معرفی.
- لوگوی رسمی ارائه‌شده توسط کاربر (`faranou-logo.jpg`) بدون تغییر محتوا استفاده شد + mark برداری مشتق از آن برای نمایش کوچک/Favicon.
- فونت: IRANSansX (خانواده ایران‌سنسِ موجود در مخزن) با `@font-face` و وزن‌های واقعی (400/500/600/700) و اصلاح ناسازگاری حروف مسیر فایل‌ها.
- RTL واقعی، واکنش‌گرا (دسکتاپ/تبلت/موبایل)، label واقعی، focus مشخص، aria در پیام‌ها و دکمه رمز، عملکرد بدون JavaScript (اعتبارسنجی سرور).

## ۴. منطق واقعی ورود

`AuthenticationService.AuthenticateAsync`: قفل موقت → حساب Master (Bootstrap) → کاربر پایگاه داده (هش SHA-256 پروژه، مقایسه fixed-time، مقایسه dummy برای کاربر ناموجود) → بررسی `IsActive` → ثبت `LoginLog`/`LastLoginDateTime`/`SecurityAuditEvent`. پیام خطا همیشه عمومی: «نام کاربری یا کلمه عبور صحیح نیست.»؛ حساب غیرفعال فقط پس از رمز صحیح اعلام می‌شود.

## ۵. امنیت

- قفل موقت: ۵ تلاش ناموفق در ۱۵ دقیقه (برای هر نام کاربری، یکسان برای حساب ناموجود → بدون افشای وجود حساب).
- رویدادهای امنیتی (`LoginSucceeded`/`LoginFailed`/`LoginDeniedInactive`) بدون ثبت رمز/توکن.
- Anti-forgery حفظ شد؛ `ReturnUrl` فقط با `Url.IsLocalUrl`؛ کوکی HttpOnly + Secure (در Production) + SameSite=Lax + SlidingExpiration موجود؛ RememberMe با `IsPersistent`.
- MFA: در پروژه زیرساختی ندارد — نمایشی اضافه نشد (گزارش‌شده).

## ۶. Tenant

- claims `TenantId` فقط از عضویت‌های فعالِ اعتبارسنجی‌شده در دیتابیس (هرگز از ورودی کلاینت).
- نقش‌ها در مدل موجود per-company هستند (`CompanyRole` با کلید ترکیبی TenantId) — تغییر معماری داده نشد.
- صفحه Index شرکت‌های فعال کاربر را نمایش می‌دهد؛ انتخاب/سوییچ شرکت به‌عنوان جریان کامل UI هنوز ساخته نشده (گزارش‌شده).

## ۷. نتایج CI (واقعی)

Run [`39058125369`](https://github.com/hamidreza1394/Accounting/actions/runs/39058125369) — job `build-and-test` ✓ (1m25s):

| مرحله | نتیجه |
|---|---|
| Check local-only static assets | ✓ |
| Restore | ✓ |
| Build (Release) | ✓ |
| Test (`dotnet test`) | ✓ |
| Check the generated migrations | ✓ |
| Verify the migration and the model snapshot | ✓ |
| Check for pending model changes (`dotnet ef`) | ✓ |

اجرای محلی build/test ممکن نبود (باینری dotnet و دسترسی NuGet در محیط Agent موجود نیست)؛ بررسی‌های Python (`check_static_assets.py`، `generate_migrations.py --check`، `verify_migration_model.py`) محلی نیز سبز اجرا شدند.

## ۸. موارد باقی‌مانده / ریسک‌ها

1. **هش SHA-256 بدون salt** (سازوکار موجود پروژه و کاربران فعلی) — پیشنهاد: مهاجرت تدریجی به `PasswordHasher` با پشتیبانی دوگانه و rehash هنگام ورود + گسترش ستون (نیازمند migration).
2. **MFA** — نیازمند تصمیم محصول و زیرساخت (Email/SMS/TOTP).
3. **بازیابی رمز عبور** — بدون زیرساخت ایمیل/پیامک پیاده‌سازی نشد؛ لینک جعلی اضافه نشد.
4. **Lockout** روی حافظه/دیتابیس مشترک در چند instance ابری نیازمند بررسی توزیع‌شده است.
5. **نام کاربری لاتین** (تصمیم صریح، سازگار با ستون `varchar(20)` و مجموعه `IdentityOptions.User.AllowedUserNameCharacters`) — برای نام کاربری فارسی نیازمند تغییر ستون به `nvarchar` و migration است.
6. بازبینی بصری در مرورگر واقعی در این محیط ممکن نبود.
